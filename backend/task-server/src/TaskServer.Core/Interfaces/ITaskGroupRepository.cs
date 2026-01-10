using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

public interface ITaskGroupRepository
{
    Task<TaskGroup?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskGroup?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<TaskGroup>> GetAllAsync(CancellationToken ct = default);
    Task<TaskGroup> AddAsync(TaskGroup group, CancellationToken ct = default);
    Task<TaskGroup> UpdateAsync(TaskGroup group, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
