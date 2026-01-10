namespace TaskServer.Core.Interfaces;

/// <summary>
/// Service for cancelling running tasks.
/// </summary>
public interface ITaskCancellationService
{
    /// <summary>
    /// Attempts to cancel a running task by signaling its CancellationToken.
    /// </summary>
    /// <param name="taskId">The ID of the task to cancel.</param>
    /// <returns>True if the task was found and cancellation was signaled; false otherwise.</returns>
    bool TryCancelRunningTask(Guid taskId);
}
