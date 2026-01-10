using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default);
    Task<TaskItem> AddAsync(TaskItem task, CancellationToken ct = default);
    Task<TaskItem> UpdateAsync(TaskItem task, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetQueuedTasksAsync(CancellationToken ct = default);

    // Hierarchical task support
    Task<IReadOnlyList<TaskItem>> GetChildrenAsync(Guid parentTaskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetDescendantsAsync(Guid parentTaskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> AddBatchAsync(IEnumerable<TaskItem> tasks, CancellationToken ct = default);
    Task<int> GetChildCountAsync(Guid parentTaskId, CancellationToken ct = default);
    Task<bool> DeleteSubtreeAsync(Guid rootTaskId, CancellationToken ct = default);
}
