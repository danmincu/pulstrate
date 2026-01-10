using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

public interface ITaskService
{
    Task<TaskItem> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskItem?> GetTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetUserTasksAsync(Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> UpdateTaskAsync(Guid taskId, Guid ownerId, UpdateTaskRequest request, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> CancelTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);

    // Hierarchical task support
    Task<TaskItem> CreateTaskHierarchyAsync(Guid ownerId, CreateTaskHierarchyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetChildTasksAsync(Guid parentTaskId, Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> CancelTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<bool> DeleteTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
}
