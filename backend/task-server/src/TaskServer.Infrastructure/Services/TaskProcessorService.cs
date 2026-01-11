using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskServer.Core.Constants;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Services;

public class TaskServerOptions
{
    public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount;
    public int DefaultTaskTimeoutMinutes { get; set; } = 60;
    public int TaskQueuePollingIntervalMs { get; set; } = 100;
}

public class TaskProcessorService : BackgroundService, ITaskCancellationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITaskQueue _queue;
    private readonly ITaskGroupRepository _groupRepository;
    private readonly ILogger<TaskProcessorService> _logger;
    private readonly TaskServerOptions _options;
    private readonly Dictionary<string, ITaskExecutor> _executors;
    private readonly CancellationTokenSource _taskCts = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _runningTasks = new();
    private readonly object _runningTasksLock = new();

    // Per-group semaphores for parallelism control
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _groupSemaphores = new();
    private readonly ConcurrentDictionary<Guid, int> _groupMaxParallelism = new();

    public TaskProcessorService(
        IServiceProvider serviceProvider,
        ITaskQueue queue,
        ITaskGroupRepository groupRepository,
        IEnumerable<ITaskExecutor> executors,
        IOptions<TaskServerOptions> options,
        ILogger<TaskProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _groupRepository = groupRepository;
        _logger = logger;
        _options = options.Value;
        _executors = executors.ToDictionary(e => e.TaskType, e => e);
    }

    private async Task<SemaphoreSlim> GetOrCreateGroupSemaphoreAsync(Guid groupId, CancellationToken ct)
    {
        if (_groupSemaphores.TryGetValue(groupId, out var existingSemaphore))
        {
            return existingSemaphore;
        }

        // Get max parallelism from group
        var group = await _groupRepository.GetByIdAsync(groupId, ct);
        var maxParallelism = group?.MaxParallelism ?? TaskGroupConstants.DefaultGroupMaxParallelism;

        // Store the max parallelism for this group
        _groupMaxParallelism[groupId] = maxParallelism;

        // Create and store semaphore
        var semaphore = new SemaphoreSlim(maxParallelism);
        var added = _groupSemaphores.GetOrAdd(groupId, semaphore);

        // If another thread added a semaphore first, dispose ours
        if (added != semaphore)
        {
            semaphore.Dispose();
        }

        _logger.LogInformation("Created semaphore for group {GroupId} with max parallelism {MaxParallelism}",
            groupId, maxParallelism);

        return added;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task processor started with group-based parallelism");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _taskCts.Token);

        while (!linkedCts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await _queue.DequeueAsync(linkedCts.Token);
                if (result == null)
                {
                    continue;
                }

                var (taskId, groupId) = result.Value;

                // Fire and forget - ProcessTaskAsync handles semaphore waiting internally
                // This allows the main loop to continue dequeueing tasks from other groups
                _ = ProcessTaskAsync(taskId, groupId, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in task processor main loop");
            }
        }

        _logger.LogInformation("Task processor stopping...");
    }

    private async Task ProcessTaskAsync(Guid taskId, Guid groupId, CancellationToken stoppingToken)
    {
        // Get or create semaphore for this group
        var groupSemaphore = await GetOrCreateGroupSemaphoreAsync(groupId, stoppingToken);

        // Wait for a slot in this group - this happens per-task, not blocking the main dequeue loop
        await groupSemaphore.WaitAsync(stoppingToken);

        var semaphoreReleased = false; // Track if we released early for parent tasks

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var progressAggregation = scope.ServiceProvider.GetRequiredService<IProgressAggregationService>();
            var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();

            var task = await repository.GetByIdAsync(taskId, stoppingToken);
            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found in repository", taskId);
                return;
            }

            if (task.State == TaskState.Cancelled)
            {
                _logger.LogInformation("Task {TaskId} was cancelled before execution", taskId);
                return;
            }

            // Check if this task has children - if so, execute as a parent task
            var children = await repository.GetChildrenAsync(taskId, stoppingToken);
            if (children.Count > 0)
            {
                // IMPORTANT: Release the semaphore BEFORE executing parent task.
                // Parent tasks are orchestrators - they don't do real work, they just wait for children.
                // If we hold the semaphore while waiting, and children are in the same group,
                // we get a deadlock (parent waits for children, children wait for parent's slot).
                groupSemaphore.Release();
                semaphoreReleased = true;

                await ExecuteParentTaskAsync(task, children, repository, notifications, progressAggregation, taskService, groupId, stoppingToken);
                return;
            }

            // Regular task execution (no children)
            if (!_executors.TryGetValue(task.Type, out var executor))
            {
                _logger.LogError("No executor found for task type: {TaskType}", task.Type);
                task.State = TaskState.Errored;
                task.StateDetails = $"No executor found for task type: {task.Type}";
                task.UpdatedAt = DateTime.UtcNow;
                task.CompletedAt = DateTime.UtcNow;
                await repository.UpdateAsync(task, stoppingToken);
                await notifications.NotifyStateChangedAsync(taskId, task.OwnerId, TaskState.Errored, task.StateDetails);
                // Notify parent of state change
                await progressAggregation.OnChildStateChangedAsync(taskId, TaskState.Errored, stoppingToken);
                return;
            }

            using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            taskCts.CancelAfter(TimeSpan.FromMinutes(_options.DefaultTaskTimeoutMinutes));

            lock (_runningTasksLock)
            {
                _runningTasks[taskId] = taskCts;
            }

            try
            {
                task.State = TaskState.Executing;
                task.StartedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
                await repository.UpdateAsync(task, stoppingToken);
                await notifications.NotifyStateChangedAsync(taskId, task.OwnerId, TaskState.Executing, null);

                _logger.LogInformation("Task {TaskId} started execution in group {GroupId}", taskId, groupId);

                var progress = new Progress<TaskProgressUpdate>(async update =>
                {
                    task.Progress = update.Percentage;
                    task.ProgressDetails = update.Details;
                    task.ProgressPayload = update.PayloadJson;
                    task.UpdatedAt = DateTime.UtcNow;
                    await repository.UpdateAsync(task, CancellationToken.None);
                    await notifications.NotifyProgressAsync(taskId, task.OwnerId, update.Percentage, update.Details, update.PayloadJson);
                    // Aggregate progress to parent if exists
                    await progressAggregation.OnChildProgressReportedAsync(taskId, update.Percentage, CancellationToken.None);

                    // Call OnSubtaskProgress on the parent executor if this task has a parent
                    if (task.ParentTaskId.HasValue)
                    {
                        await CallParentOnSubtaskProgressAsync(task, update, repository);
                    }
                });

                await executor.ExecuteAsync(task, progress, taskCts.Token);

                task.State = TaskState.Completed;
                task.Progress = 100;
                task.CompletedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
                await repository.UpdateAsync(task, CancellationToken.None);
                await notifications.NotifyStateChangedAsync(taskId, task.OwnerId, TaskState.Completed, null);
                // Notify parent of state change
                await progressAggregation.OnChildStateChangedAsync(taskId, TaskState.Completed, CancellationToken.None);

                _logger.LogInformation("Task {TaskId} completed successfully in group {GroupId}", taskId, groupId);
            }
            catch (OperationCanceledException) when (task.State == TaskState.Cancelled)
            {
                _logger.LogInformation("Task {TaskId} was cancelled during execution", taskId);
                await progressAggregation.OnChildStateChangedAsync(taskId, TaskState.Cancelled, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                task.State = TaskState.Terminated;
                task.StateDetails = "Task timed out or was terminated";
                task.CompletedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
                await repository.UpdateAsync(task, CancellationToken.None);
                await notifications.NotifyStateChangedAsync(taskId, task.OwnerId, TaskState.Terminated, task.StateDetails);
                await progressAggregation.OnChildStateChangedAsync(taskId, TaskState.Terminated, CancellationToken.None);
                _logger.LogWarning("Task {TaskId} was terminated in group {GroupId}", taskId, groupId);
            }
            catch (Exception ex)
            {
                task.State = TaskState.Errored;
                task.StateDetails = ex.Message;
                task.CompletedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
                await repository.UpdateAsync(task, CancellationToken.None);
                await notifications.NotifyStateChangedAsync(taskId, task.OwnerId, TaskState.Errored, task.StateDetails);
                await progressAggregation.OnChildStateChangedAsync(taskId, TaskState.Errored, CancellationToken.None);
                _logger.LogError(ex, "Task {TaskId} failed with error in group {GroupId}", taskId, groupId);
            }
            finally
            {
                lock (_runningTasksLock)
                {
                    _runningTasks.Remove(taskId);
                }
            }
        }
        finally
        {
            // Release the semaphore unless we already released it (for parent tasks)
            if (!semaphoreReleased)
            {
                groupSemaphore.Release();
            }
        }
    }

    private async Task ExecuteParentTaskAsync(
        TaskItem parent,
        IReadOnlyList<TaskItem> children,
        ITaskRepository repository,
        INotificationService notifications,
        IProgressAggregationService progressAggregation,
        ITaskService taskService,
        Guid groupId,
        CancellationToken stoppingToken)
    {
        // Set parent to executing
        parent.State = TaskState.Executing;
        parent.StartedAt = DateTime.UtcNow;
        parent.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(parent, stoppingToken);
        await notifications.NotifyStateChangedAsync(parent.Id, parent.OwnerId, TaskState.Executing, null);

        _logger.LogInformation("Parent task {TaskId} started with {ChildCount} children in group {GroupId}",
            parent.Id, children.Count, groupId);

        // Get executor for parent (if any) to call virtual methods
        _executors.TryGetValue(parent.Type, out var parentExecutor);
        var baseExecutor = parentExecutor as TaskExecutorBase;

        using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        taskCts.CancelAfter(TimeSpan.FromMinutes(_options.DefaultTaskTimeoutMinutes));

        lock (_runningTasksLock)
        {
            _runningTasks[parent.Id] = taskCts;
        }

        try
        {
            // Track children that have already had their hooks called (for sequential mode)
            var processedChildIds = new HashSet<Guid>();

            if (parent.SubtaskParallelism)
            {
                // Execute children in parallel - enqueue all at once
                _logger.LogInformation("Enqueueing {ChildCount} children in parallel for parent {TaskId}", children.Count, parent.Id);
                foreach (var child in children)
                {
                    await _queue.EnqueueAsync(child.Id, child.GroupId, child.Priority, stoppingToken);
                }
            }
            else
            {
                // Execute children sequentially - call hooks BETWEEN children to enable data passing
                _logger.LogInformation("Executing {ChildCount} children sequentially for parent {TaskId}", children.Count, parent.Id);

                // Use list for index access (to potentially modify next child's payload)
                var childList = children.ToList();

                for (int i = 0; i < childList.Count; i++)
                {
                    taskCts.Token.ThrowIfCancellationRequested();

                    var child = childList[i];
                    await _queue.EnqueueAsync(child.Id, child.GroupId, child.Priority, stoppingToken);

                    // Wait for child to complete
                    await WaitForTaskCompletionAsync(child.Id, repository, taskCts.Token);

                    // Refresh child from repository to get final state and output
                    var completedChild = await repository.GetByIdAsync(child.Id, taskCts.Token);

                    // Call hook IMMEDIATELY after child completes, BEFORE enqueueing next
                    // This allows the hook to:
                    // 1. Read the completed child's Output
                    // 2. Update the next queued sibling's Payload via UpdateQueuedTaskPayloadAsync
                    // 3. Dynamically add new subtasks
                    if (completedChild != null && IsTerminalState(completedChild.State))
                    {
                        await HandleChildStateChangeAsync(parent, completedChild, baseExecutor, taskService, taskCts.Token);
                        processedChildIds.Add(completedChild.Id);
                    }
                }
            }

            // Wait for all children (including dynamically added ones) to complete
            // Skip children we've already processed hooks for (sequential mode pre-created children)
            await WaitForAllChildrenWithHooksAsync(parent, repository, taskService, baseExecutor, processedChildIds, taskCts.Token);

            // Check if all children succeeded (fetch fresh - may include dynamically added children)
            var finalChildren = await repository.GetChildrenAsync(parent.Id, stoppingToken);
            var allSucceeded = finalChildren.All(c => c.State == TaskState.Completed);

            if (allSucceeded)
            {
                // Call virtual method on executor if available
                baseExecutor?.OnAllSubtasksSuccess(parent, finalChildren);

                parent.State = TaskState.Completed;
                parent.Progress = 100;
                parent.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Parent task {TaskId} completed successfully - all {ChildCount} children succeeded",
                    parent.Id, finalChildren.Count);
            }
            else
            {
                var failedChildren = finalChildren.Where(c => c.State != TaskState.Completed).ToList();
                parent.State = TaskState.Errored;
                parent.StateDetails = $"{failedChildren.Count} child task(s) did not complete successfully";
                parent.CompletedAt = DateTime.UtcNow;
                _logger.LogWarning("Parent task {TaskId} failed - {FailedCount} children did not complete",
                    parent.Id, failedChildren.Count);
            }

            parent.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(parent, CancellationToken.None);
            await notifications.NotifyStateChangedAsync(parent.Id, parent.OwnerId, parent.State, parent.StateDetails);
            // Notify aggregation for parent (in case parent has a parent)
            await progressAggregation.OnChildStateChangedAsync(parent.Id, parent.State, CancellationToken.None);
        }
        catch (OperationCanceledException) when (parent.State == TaskState.Cancelled)
        {
            _logger.LogInformation("Parent task {TaskId} was cancelled", parent.Id);
            await progressAggregation.OnChildStateChangedAsync(parent.Id, TaskState.Cancelled, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            parent.State = TaskState.Terminated;
            parent.StateDetails = "Parent task timed out or was terminated";
            parent.CompletedAt = DateTime.UtcNow;
            parent.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(parent, CancellationToken.None);
            await notifications.NotifyStateChangedAsync(parent.Id, parent.OwnerId, TaskState.Terminated, parent.StateDetails);
            await progressAggregation.OnChildStateChangedAsync(parent.Id, TaskState.Terminated, CancellationToken.None);
            _logger.LogWarning("Parent task {TaskId} was terminated", parent.Id);
        }
        catch (Exception ex)
        {
            parent.State = TaskState.Errored;
            parent.StateDetails = ex.Message;
            parent.CompletedAt = DateTime.UtcNow;
            parent.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(parent, CancellationToken.None);
            await notifications.NotifyStateChangedAsync(parent.Id, parent.OwnerId, TaskState.Errored, parent.StateDetails);
            await progressAggregation.OnChildStateChangedAsync(parent.Id, TaskState.Errored, CancellationToken.None);
            _logger.LogError(ex, "Parent task {TaskId} failed with error", parent.Id);
        }
        finally
        {
            lock (_runningTasksLock)
            {
                _runningTasks.Remove(parent.Id);
            }
        }
    }

    private async Task WaitForTaskCompletionAsync(Guid taskId, ITaskRepository repository, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var task = await repository.GetByIdAsync(taskId, ct);
            if (task == null || IsTerminalState(task.State))
            {
                return;
            }
            await Task.Delay(_options.TaskQueuePollingIntervalMs, ct);
        }
    }

    private async Task WaitForAllChildrenCompletionAsync(Guid parentId, ITaskRepository repository, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var children = await repository.GetChildrenAsync(parentId, ct);
            if (children.All(c => IsTerminalState(c.State)))
            {
                return;
            }
            await Task.Delay(_options.TaskQueuePollingIntervalMs, ct);
        }
    }

    /// <summary>
    /// Waits for all children to complete while calling lifecycle hooks on state changes.
    /// When a child reaches a terminal state, calls OnSubtaskStateChangeAsync on the parent executor.
    /// If the hook returns new subtasks, they are added to the parent.
    /// </summary>
    /// <param name="parent">The parent task</param>
    /// <param name="repository">Task repository</param>
    /// <param name="taskService">Task service for adding dynamic subtasks</param>
    /// <param name="baseExecutor">The parent's executor (may be null)</param>
    /// <param name="alreadyProcessedChildIds">
    /// Child IDs that have already had their hooks called (e.g., sequential mode pre-created children).
    /// These will be skipped to avoid double-processing.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    private async Task WaitForAllChildrenWithHooksAsync(
        TaskItem parent,
        ITaskRepository repository,
        ITaskService taskService,
        TaskExecutorBase? baseExecutor,
        HashSet<Guid> alreadyProcessedChildIds,
        CancellationToken ct)
    {
        // Track the last known state of each child to detect changes
        var lastKnownStates = new Dictionary<Guid, TaskState>();

        while (!ct.IsCancellationRequested)
        {
            // Fetch fresh children (may include dynamically added ones)
            var children = await repository.GetChildrenAsync(parent.Id, ct);

            // Check for state changes
            foreach (var child in children)
            {
                // Skip children that have already had their hooks called (sequential mode)
                if (alreadyProcessedChildIds.Contains(child.Id))
                {
                    // Still track state for completion check, but don't call hooks again
                    lastKnownStates[child.Id] = child.State;
                    continue;
                }

                var hasKnownState = lastKnownStates.TryGetValue(child.Id, out var lastState);

                if (!hasKnownState)
                {
                    // New child (either initial or dynamically added)
                    lastKnownStates[child.Id] = child.State;

                    // If it's already in a terminal state (unlikely for new children), call hook
                    if (IsTerminalState(child.State))
                    {
                        await HandleChildStateChangeAsync(parent, child, baseExecutor, taskService, ct);
                    }
                }
                else if (child.State != lastState)
                {
                    // State changed
                    lastKnownStates[child.Id] = child.State;

                    // If reached terminal state, call the hook
                    if (IsTerminalState(child.State))
                    {
                        await HandleChildStateChangeAsync(parent, child, baseExecutor, taskService, ct);
                    }
                }
            }

            // Check if all children are in terminal state
            if (children.Count > 0 && children.All(c => IsTerminalState(c.State)))
            {
                return;
            }

            await Task.Delay(_options.TaskQueuePollingIntervalMs, ct);
        }
    }

    /// <summary>
    /// Handles a child reaching a terminal state by calling executor hooks.
    /// If OnSubtaskStateChangeAsync returns new subtasks, they are added to the parent.
    /// </summary>
    private async Task HandleChildStateChangeAsync(
        TaskItem parent,
        TaskItem child,
        TaskExecutorBase? baseExecutor,
        ITaskService taskService,
        CancellationToken ct)
    {
        var stateChange = new TaskStateChange(child.Id, child.State, child.StateDetails);

        // Call the synchronous hook (for backwards compatibility and logging)
        baseExecutor?.OnSubtaskStateChange(parent, child, stateChange);

        // Call the async hook that can return new subtasks
        if (baseExecutor != null)
        {
            try
            {
                var newSubtasks = await baseExecutor.OnSubtaskStateChangeAsync(parent, child, stateChange);

                if (newSubtasks != null && newSubtasks.Count > 0)
                {
                    _logger.LogInformation(
                        "Parent {ParentId}: Adding {Count} dynamic subtasks after child {ChildId} ({ChildType}) -> {State}",
                        parent.Id, newSubtasks.Count, child.Id, child.Type, child.State);

                    // Add the new subtasks - they will be enqueued and included in waiting
                    await taskService.AddSubtasksAsync(parent.Id, newSubtasks, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calling OnSubtaskStateChangeAsync for parent {ParentId}, child {ChildId}",
                    parent.Id, child.Id);
            }
        }
    }

    private static bool IsTerminalState(TaskState state)
    {
        return state == TaskState.Completed ||
               state == TaskState.Cancelled ||
               state == TaskState.Errored ||
               state == TaskState.Terminated;
    }

    /// <summary>
    /// Calls OnSubtaskProgress on the parent executor when a child reports progress.
    /// </summary>
    private async Task CallParentOnSubtaskProgressAsync(TaskItem child, TaskProgressUpdate update, ITaskRepository repository)
    {
        if (!child.ParentTaskId.HasValue)
            return;

        try
        {
            var parent = await repository.GetByIdAsync(child.ParentTaskId.Value, CancellationToken.None);
            if (parent == null)
                return;

            if (_executors.TryGetValue(parent.Type, out var parentExecutor) && parentExecutor is TaskExecutorBase baseExecutor)
            {
                baseExecutor.OnSubtaskProgress(parent, child, update);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calling OnSubtaskProgress for child {ChildId}", child.Id);
        }
    }

    public bool TryCancelRunningTask(Guid taskId)
    {
        lock (_runningTasksLock)
        {
            if (_runningTasks.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
                return true;
            }
        }
        return false;
    }

    public override void Dispose()
    {
        _taskCts.Cancel();
        _taskCts.Dispose();

        foreach (var semaphore in _groupSemaphores.Values)
        {
            semaphore.Dispose();
        }
        _groupSemaphores.Clear();

        base.Dispose();
    }
}
