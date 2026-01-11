using Microsoft.Extensions.Logging;
using TaskServer.Core.Constants;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;
    private readonly ITaskQueue _queue;
    private readonly INotificationService _notifications;
    private readonly ITaskCancellationService? _cancellationService;
    private readonly IProgressAggregationService? _progressAggregation;
    private readonly ILogger<TaskService>? _logger;

    public TaskService(
        ITaskRepository repository,
        ITaskQueue queue,
        INotificationService notifications,
        ITaskCancellationService? cancellationService = null,
        IProgressAggregationService? progressAggregation = null,
        ILogger<TaskService>? logger = null)
    {
        _repository = repository;
        _queue = queue;
        _notifications = notifications;
        _cancellationService = cancellationService;
        _progressAggregation = progressAggregation;
        _logger = logger;
    }

    public async Task<TaskItem> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, string? authToken = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var groupId = request.GroupId ?? TaskGroupConstants.DefaultGroupId;
        var taskId = request.Id ?? Guid.NewGuid();

        // Determine RootTaskId and TrackHistory based on parent
        Guid rootTaskId = taskId;
        bool trackHistory = request.TrackHistory;

        if (request.ParentTaskId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(request.ParentTaskId.Value, ct);
            if (parent == null)
            {
                throw new InvalidOperationException($"Parent task with ID {request.ParentTaskId.Value} not found.");
            }
            if (parent.OwnerId != ownerId)
            {
                throw new UnauthorizedAccessException("Parent task belongs to a different user.");
            }
            // Inherit RootTaskId and TrackHistory from parent
            rootTaskId = parent.RootTaskId;
            trackHistory = parent.TrackHistory;
        }

        var task = new TaskItem
        {
            Id = taskId,
            OwnerId = ownerId,
            GroupId = groupId,
            Priority = request.Priority,
            Type = request.Type,
            Payload = request.Payload,
            State = TaskState.Queued,
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now,
            ParentTaskId = request.ParentTaskId,
            Weight = request.Weight,
            SubtaskParallelism = request.SubtaskParallelism,
            AuthToken = authToken,
            RootTaskId = rootTaskId,
            TrackHistory = trackHistory
        };

        await _repository.AddAsync(task, ct);
        await _queue.EnqueueAsync(task.Id, task.GroupId, task.Priority, ct);
        await _notifications.NotifyTaskCreatedAsync(task);

        // Give the task processor a moment to pick up and potentially fail fast tasks
        // (e.g., tasks with no executor), then return the current state
        await Task.Delay(50, ct);
        var currentTask = await _repository.GetByIdAsync(task.Id, ct);
        return currentTask ?? task;
    }

    public async Task<TaskItem?> GetTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task == null || task.OwnerId != ownerId)
        {
            return null;
        }
        return task;
    }

    public async Task<IReadOnlyList<TaskItem>> GetUserTasksAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await _repository.GetByOwnerAsync(ownerId, ct);
    }

    public async Task<TaskItem> UpdateTaskAsync(Guid taskId, Guid ownerId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Task with ID {taskId} not found.");
        }
        if (task.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("You do not have permission to update this task.");
        }
        if (task.State != TaskState.Queued)
        {
            throw new InvalidOperationException("Only queued tasks can be updated.");
        }

        if (request.Priority.HasValue)
        {
            task.Priority = request.Priority.Value;
        }
        if (request.Payload != null)
        {
            task.Payload = request.Payload;
        }
        task.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(task, ct);
        await _notifications.NotifyTaskUpdatedAsync(task);

        return task;
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task == null)
        {
            return false;
        }
        if (task.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this task.");
        }

        if (task.State == TaskState.Queued)
        {
            await _queue.TryCancelAsync(taskId, ct);
        }

        var deleted = await _repository.DeleteAsync(taskId, ct);
        if (deleted)
        {
            await _notifications.NotifyTaskDeletedAsync(taskId, ownerId);
        }

        return deleted;
    }

    public async Task<TaskItem> CancelTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Task with ID {taskId} not found.");
        }
        if (task.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("You do not have permission to cancel this task.");
        }
        if (task.State != TaskState.Queued && task.State != TaskState.Executing)
        {
            throw new InvalidOperationException($"Cannot cancel task in state {task.State}.");
        }

        if (task.State == TaskState.Queued)
        {
            await _queue.TryCancelAsync(taskId, ct);
        }
        else if (task.State == TaskState.Executing)
        {
            // Signal the running task to cancel via its CancellationToken
            _cancellationService?.TryCancelRunningTask(taskId);
        }

        task.State = TaskState.Cancelled;
        task.StateDetails = "Cancelled by user request";
        task.UpdatedAt = DateTime.UtcNow;
        task.CompletedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(task, ct);
        await _notifications.NotifyStateChangedAsync(taskId, ownerId, TaskState.Cancelled, task.StateDetails);

        return task;
    }

    // Hierarchical task support

    public async Task<TaskItem> CreateTaskHierarchyAsync(Guid ownerId, CreateTaskHierarchyRequest request, string? authToken = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var allTasks = new List<TaskItem>();

        // Recursively build all tasks (root sets its own Id as RootTaskId)
        var parentTask = BuildTaskHierarchy(ownerId, request, null, null, null, now, authToken, allTasks);

        // Add all tasks atomically
        await _repository.AddBatchAsync(allTasks, ct);

        // Only enqueue the root parent task - children will be managed by the processor
        await _queue.EnqueueAsync(parentTask.Id, parentTask.GroupId, parentTask.Priority, ct);

        // Notify for all created tasks
        foreach (var task in allTasks)
        {
            await _notifications.NotifyTaskCreatedAsync(task);
        }

        return parentTask;
    }

    private TaskItem BuildTaskHierarchy(Guid ownerId, CreateTaskHierarchyRequest request, Guid? parentId, Guid? rootTaskId, bool? trackHistory, DateTime now, string? authToken, List<TaskItem> allTasks)
    {
        var taskRequest = request.ParentTask;
        var taskId = taskRequest.Id ?? Guid.NewGuid();
        var groupId = taskRequest.GroupId ?? TaskGroupConstants.DefaultGroupId;

        // For root tasks, use own ID as RootTaskId and request's TrackHistory
        // For children, inherit RootTaskId and TrackHistory from root
        var effectiveRootTaskId = rootTaskId ?? taskId;
        var effectiveTrackHistory = trackHistory ?? taskRequest.TrackHistory;

        var task = new TaskItem
        {
            Id = taskId,
            OwnerId = ownerId,
            GroupId = groupId,
            Priority = taskRequest.Priority,
            Type = taskRequest.Type,
            Payload = taskRequest.Payload,
            State = TaskState.Queued,
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now,
            ParentTaskId = parentId,
            Weight = taskRequest.Weight,
            SubtaskParallelism = taskRequest.SubtaskParallelism,
            AuthToken = authToken,
            RootTaskId = effectiveRootTaskId,
            TrackHistory = effectiveTrackHistory
        };

        allTasks.Add(task);

        // Recursively build children (pass root's RootTaskId and TrackHistory)
        foreach (var childRequest in request.ChildTasks)
        {
            BuildTaskHierarchy(ownerId, childRequest, taskId, effectiveRootTaskId, effectiveTrackHistory, now, authToken, allTasks);
        }

        return task;
    }

    public async Task<IReadOnlyList<TaskItem>> GetChildTasksAsync(Guid parentTaskId, Guid ownerId, CancellationToken ct = default)
    {
        var parent = await _repository.GetByIdAsync(parentTaskId, ct);
        if (parent == null || parent.OwnerId != ownerId)
        {
            return Array.Empty<TaskItem>();
        }
        return await _repository.GetChildrenAsync(parentTaskId, ct);
    }

    public async Task<TaskItem> CancelTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException($"Task with ID {taskId} not found.");
        }
        if (task.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("You do not have permission to cancel this task.");
        }

        // Get all descendants and cancel them first (leaves to root)
        var descendants = await _repository.GetDescendantsAsync(taskId, ct);
        foreach (var descendant in descendants.Reverse())
        {
            await CancelSingleTaskInternalAsync(descendant, "Cancelled (cascade from parent)", ct);
        }

        // Cancel the root task
        await CancelSingleTaskInternalAsync(task, "Cancelled by user request (with subtree)", ct);

        return task;
    }

    private async Task CancelSingleTaskInternalAsync(TaskItem task, string reason, CancellationToken ct)
    {
        if (task.State != TaskState.Queued && task.State != TaskState.Executing)
        {
            return; // Already in terminal state
        }

        if (task.State == TaskState.Queued)
        {
            await _queue.TryCancelAsync(task.Id, ct);
        }
        else if (task.State == TaskState.Executing)
        {
            _cancellationService?.TryCancelRunningTask(task.Id);
        }

        task.State = TaskState.Cancelled;
        task.StateDetails = reason;
        task.UpdatedAt = DateTime.UtcNow;
        task.CompletedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(task, ct);
        await _notifications.NotifyStateChangedAsync(task.Id, task.OwnerId, TaskState.Cancelled, task.StateDetails);
    }

    public async Task<bool> DeleteTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task == null)
        {
            return false;
        }
        if (task.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this task.");
        }

        // Cancel all running/queued tasks in subtree first
        await CancelTaskSubtreeAsync(taskId, ownerId, ct);

        // Get all descendants for notification
        var descendants = await _repository.GetDescendantsAsync(taskId, ct);

        // Delete entire subtree
        var deleted = await _repository.DeleteSubtreeAsync(taskId, ct);

        if (deleted)
        {
            // Notify deletion for all tasks
            foreach (var descendant in descendants)
            {
                await _notifications.NotifyTaskDeletedAsync(descendant.Id, ownerId);
            }
            await _notifications.NotifyTaskDeletedAsync(taskId, ownerId);
        }

        return deleted;
    }

    // Dynamic subtask addition during execution

    public async Task<TaskItem> AddSubtaskAsync(Guid parentTaskId, CreateTaskRequest childRequest, CancellationToken ct = default)
    {
        var parent = await _repository.GetByIdAsync(parentTaskId, ct)
            ?? throw new KeyNotFoundException($"Parent task {parentTaskId} not found");

        if (parent.State != TaskState.Executing)
        {
            throw new InvalidOperationException(
                $"Cannot add subtask to parent in state {parent.State}. Parent must be in Executing state.");
        }

        var now = DateTime.UtcNow;
        var child = new TaskItem
        {
            Id = childRequest.Id ?? Guid.NewGuid(),
            OwnerId = parent.OwnerId,
            GroupId = childRequest.GroupId ?? parent.GroupId,
            Priority = childRequest.Priority,
            Type = childRequest.Type,
            Payload = childRequest.Payload,
            State = TaskState.Queued,
            Progress = 0,
            ParentTaskId = parentTaskId,
            Weight = childRequest.Weight,
            SubtaskParallelism = childRequest.SubtaskParallelism,
            AuthToken = parent.AuthToken,  // Inherit auth token from parent
            RootTaskId = parent.RootTaskId,  // Inherit root task ID from parent
            TrackHistory = parent.TrackHistory,  // Inherit history tracking from parent
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(child, ct);
        await _queue.EnqueueAsync(child.Id, child.GroupId, child.Priority, ct);
        await _notifications.NotifyTaskCreatedAsync(child);

        // Trigger progress recalculation (new child will be included with 0% progress)
        if (_progressAggregation != null)
        {
            await _progressAggregation.OnChildStateChangedAsync(child.Id, TaskState.Queued, ct);
        }

        _logger?.LogInformation(
            "Dynamically added subtask {ChildId} ({ChildType}) to executing parent {ParentId}",
            child.Id, child.Type, parentTaskId);

        return child;
    }

    public async Task<IReadOnlyList<TaskItem>> AddSubtasksAsync(Guid parentTaskId, IReadOnlyList<CreateTaskRequest> childRequests, CancellationToken ct = default)
    {
        var children = new List<TaskItem>();
        foreach (var request in childRequests)
        {
            var child = await AddSubtaskAsync(parentTaskId, request, ct);
            children.Add(child);
        }
        return children;
    }

    // Task output and payload modification for sequential workflows

    public async Task SetTaskOutputAsync(Guid taskId, string output, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        task.Output = output;
        task.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(task, ct);

        _logger?.LogDebug("Set output for task {TaskId}: {OutputLength} chars", taskId, output?.Length ?? 0);
    }

    public async Task UpdateQueuedTaskPayloadAsync(Guid taskId, string newPayload, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (task.State != TaskState.Queued)
        {
            throw new InvalidOperationException(
                $"Cannot update payload for task in state {task.State}. Task must be in Queued state.");
        }

        var oldPayload = task.Payload;
        task.Payload = newPayload;
        task.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(task, ct);

        _logger?.LogInformation(
            "Updated payload for queued task {TaskId} ({TaskType}): {OldLength} -> {NewLength} chars",
            taskId, task.Type, oldPayload?.Length ?? 0, newPayload?.Length ?? 0);
    }
}
