using Microsoft.Extensions.Logging;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Executors;

/// <summary>
/// Executor for hierarchical parent tasks.
/// Parent tasks don't execute work themselves - they orchestrate their children.
/// The TaskProcessorService handles the actual orchestration.
/// This executor provides lifecycle hooks for monitoring subtask completion.
///
/// To create a custom parent executor that can dynamically add subtasks:
/// 1. Inherit from TaskExecutorBase
/// 2. Override OnSubtaskStateChangeAsync to return new CreateTaskRequest objects
/// 3. The returned subtasks will be added to this parent and executed
///
/// Example use cases:
/// - Retry failed subtasks automatically
/// - Add follow-up tasks based on subtask results
/// - Implement saga patterns with compensating actions
/// </summary>
public class HierarchicalParentExecutor : TaskExecutorBase
{
    private readonly ILogger<HierarchicalParentExecutor> _logger;

    public HierarchicalParentExecutor(ILogger<HierarchicalParentExecutor> logger)
    {
        _logger = logger;
    }

    public override string TaskType => "hierarchical-parent";

    public override Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        // This method should never be called for hierarchical-parent tasks
        // because they always have children and are handled by ExecuteParentTaskAsync.
        // If we somehow reach here, it means the parent has no children which is an error.
        throw new InvalidOperationException(
            $"HierarchicalParentExecutor.ExecuteAsync was called for task {task.Id}. " +
            "Hierarchical parent tasks must have children and are orchestrated by TaskProcessorService.");
    }

    public override void OnSubtaskProgress(TaskItem parent, TaskItem child, TaskProgressUpdate progress)
    {
        _logger.LogDebug(
            "Hierarchical parent {ParentId}: Child {ChildId} ({ChildType}) progress {Progress}%",
            parent.Id, child.Id, child.Type, progress.Percentage);
    }

    public override void OnSubtaskStateChange(TaskItem parent, TaskItem child, TaskStateChange change)
    {
        _logger.LogInformation(
            "Hierarchical parent {ParentId}: Child {ChildId} ({ChildType}) state changed to {NewState}",
            parent.Id, child.Id, child.Type, change.NewState);
    }

    /// <summary>
    /// Called when a child task reaches a terminal state.
    /// Override this method to dynamically add new subtasks based on child completion/failure.
    ///
    /// The default hierarchical-parent does not add dynamic subtasks.
    /// Create a custom executor to implement dynamic subtask logic.
    ///
    /// Example: To retry failed tasks, return a new CreateTaskRequest with the same type/payload.
    /// Example: To add follow-up tasks, return new CreateTaskRequests based on the completed child's result.
    /// </summary>
    public override Task<IReadOnlyList<CreateTaskRequest>?> OnSubtaskStateChangeAsync(
        TaskItem parentTask,
        TaskItem childTask,
        TaskStateChange stateChange)
    {
        // Default hierarchical-parent does not add dynamic subtasks
        // Subclasses can override this to add custom logic
        return Task.FromResult<IReadOnlyList<CreateTaskRequest>?>(null);
    }

    public override void OnAllSubtasksSuccess(TaskItem parentTask, IReadOnlyList<TaskItem> completedChildren)
    {
        _logger.LogInformation(
            "Hierarchical parent {ParentId} completed successfully with {ChildCount} children",
            parentTask.Id, completedChildren.Count);

        // Log child types for visibility
        var childTypes = completedChildren
            .GroupBy(c => c.Type)
            .Select(g => $"{g.Key}:{g.Count()}")
            .ToList();

        _logger.LogInformation(
            "Hierarchical parent {ParentId} child breakdown: {ChildTypes}",
            parentTask.Id, string.Join(", ", childTypes));
    }
}
