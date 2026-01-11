using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

/// <summary>
/// Service for recording and retrieving task history (progress updates and state changes).
/// History is keyed by root task ID for hierarchical task rollup.
/// </summary>
public interface ITaskHistoryService
{
    /// <summary>
    /// Records a progress update to history.
    /// </summary>
    /// <param name="rootTaskId">The root task ID (for hierarchical rollup)</param>
    /// <param name="taskId">The task that reported the progress</param>
    /// <param name="taskType">The type of the task</param>
    /// <param name="percentage">Progress percentage (0-100)</param>
    /// <param name="details">Progress details text</param>
    /// <param name="payload">Progress payload JSON</param>
    Task RecordProgressAsync(
        Guid rootTaskId,
        Guid taskId,
        string taskType,
        double percentage,
        string? details,
        string? payload);

    /// <summary>
    /// Records a state change to history.
    /// </summary>
    /// <param name="rootTaskId">The root task ID (for hierarchical rollup)</param>
    /// <param name="taskId">The task that changed state</param>
    /// <param name="taskType">The type of the task</param>
    /// <param name="newState">The new state</param>
    /// <param name="details">State change details</param>
    Task RecordStateChangeAsync(
        Guid rootTaskId,
        Guid taskId,
        string taskType,
        TaskState newState,
        string? details);

    /// <summary>
    /// Gets the history for a root task (includes all child task history).
    /// </summary>
    /// <param name="rootTaskId">The root task ID</param>
    /// <param name="progressLimit">Maximum progress entries to return</param>
    /// <param name="stateLimit">Maximum state change entries to return</param>
    /// <returns>Combined history for the task tree</returns>
    Task<TaskHistoryDto> GetHistoryAsync(
        Guid rootTaskId,
        int progressLimit = 100,
        int stateLimit = 50);

    /// <summary>
    /// Clears all history for a root task.
    /// </summary>
    /// <param name="rootTaskId">The root task ID</param>
    Task ClearHistoryAsync(Guid rootTaskId);
}
