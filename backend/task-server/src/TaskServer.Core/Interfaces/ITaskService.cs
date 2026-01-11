using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

public interface ITaskService
{
    Task<TaskItem> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, string? authToken = null, CancellationToken ct = default);
    Task<TaskItem?> GetTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetUserTasksAsync(Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> UpdateTaskAsync(Guid taskId, Guid ownerId, UpdateTaskRequest request, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> CancelTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);

    // Hierarchical task support
    Task<TaskItem> CreateTaskHierarchyAsync(Guid ownerId, CreateTaskHierarchyRequest request, string? authToken = null, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetChildTasksAsync(Guid parentTaskId, Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> CancelTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<bool> DeleteTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);

    // Dynamic subtask addition during execution
    /// <summary>
    /// Adds a subtask to an executing parent task. The parent must be in Executing state.
    /// The child inherits the parent's auth token and defaults to the parent's group.
    /// </summary>
    /// <param name="parentTaskId">The ID of the executing parent task</param>
    /// <param name="childRequest">The child task to create</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created child task</returns>
    /// <exception cref="KeyNotFoundException">Parent task not found</exception>
    /// <exception cref="InvalidOperationException">Parent is not in Executing state</exception>
    Task<TaskItem> AddSubtaskAsync(Guid parentTaskId, CreateTaskRequest childRequest, CancellationToken ct = default);

    /// <summary>
    /// Adds multiple subtasks to an executing parent task.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> AddSubtasksAsync(Guid parentTaskId, IReadOnlyList<CreateTaskRequest> childRequests, CancellationToken ct = default);
}
