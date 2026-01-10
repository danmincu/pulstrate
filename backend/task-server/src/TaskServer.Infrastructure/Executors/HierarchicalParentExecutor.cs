using Microsoft.Extensions.Logging;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Executors;

/// <summary>
/// Executor for hierarchical parent tasks.
/// Parent tasks don't execute work themselves - they orchestrate their children.
/// The TaskProcessorService handles the actual orchestration.
/// This executor provides lifecycle hooks for monitoring subtask completion.
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
