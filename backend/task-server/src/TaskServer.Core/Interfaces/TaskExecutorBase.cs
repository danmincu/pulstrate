using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

/// <summary>
/// Base class for task executors with virtual methods for subtask lifecycle events.
/// Executors can extend this class to implement custom logic when subtasks report progress,
/// change state, or all complete successfully.
/// </summary>
public abstract class TaskExecutorBase : ITaskExecutor
{
    public abstract string TaskType { get; }

    public abstract Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called when a child task reports progress. Override to handle subtask progress.
    /// Default implementation does nothing.
    /// </summary>
    /// <param name="parentTask">The parent task</param>
    /// <param name="childTask">The child task that reported progress</param>
    /// <param name="progress">The progress update from the child</param>
    public virtual void OnSubtaskProgress(TaskItem parentTask, TaskItem childTask, TaskProgressUpdate progress)
    {
        // Default: no-op
    }

    /// <summary>
    /// Called when a child task changes state. Override to handle subtask state changes.
    /// Default implementation does nothing.
    /// </summary>
    /// <param name="parentTask">The parent task</param>
    /// <param name="childTask">The child task that changed state</param>
    /// <param name="stateChange">The state change details</param>
    public virtual void OnSubtaskStateChange(TaskItem parentTask, TaskItem childTask, TaskStateChange stateChange)
    {
        // Default: no-op
    }

    /// <summary>
    /// Called when a child task changes state. Override to dynamically add new subtasks.
    /// Return new subtasks to add to the current parent task. These will be enqueued and
    /// executed according to the parent's parallelism setting.
    /// Progress will be recalculated to include the new children.
    /// </summary>
    /// <param name="parentTask">The parent task (only subtasks of this parent can be added)</param>
    /// <param name="childTask">The child task that changed state</param>
    /// <param name="stateChange">The state change details</param>
    /// <returns>List of new subtasks to add, or null/empty for no additions</returns>
    public virtual Task<IReadOnlyList<CreateTaskRequest>?> OnSubtaskStateChangeAsync(
        TaskItem parentTask,
        TaskItem childTask,
        TaskStateChange stateChange)
    {
        // Default: no dynamic subtasks
        return Task.FromResult<IReadOnlyList<CreateTaskRequest>?>(null);
    }

    /// <summary>
    /// Called when all subtasks complete successfully. Override to perform finalization logic.
    /// Default implementation does nothing.
    /// </summary>
    /// <param name="parentTask">The parent task</param>
    /// <param name="completedChildren">All completed child tasks</param>
    public virtual void OnAllSubtasksSuccess(TaskItem parentTask, IReadOnlyList<TaskItem> completedChildren)
    {
        // Default: no-op
    }
}
