using System.Collections.Concurrent;
using TaskServer.Core.Constants;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Storage;

public class InMemoryTaskGroupRepository : ITaskGroupRepository
{
    private readonly ConcurrentDictionary<Guid, TaskGroup> _groups = new();

    public Task<TaskGroup?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _groups.TryGetValue(id, out var group);
        return Task.FromResult(group);
    }

    public Task<TaskGroup?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var group = _groups.Values.FirstOrDefault(g =>
            g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(group);
    }

    public Task<IReadOnlyList<TaskGroup>> GetAllAsync(CancellationToken ct = default)
    {
        var groups = _groups.Values
            .OrderBy(g => g.Name)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaskGroup>>(groups);
    }

    public Task<TaskGroup> AddAsync(TaskGroup group, CancellationToken ct = default)
    {
        if (!_groups.TryAdd(group.Id, group))
        {
            throw new InvalidOperationException($"Group with ID {group.Id} already exists.");
        }
        return Task.FromResult(group);
    }

    public Task<TaskGroup> UpdateAsync(TaskGroup group, CancellationToken ct = default)
    {
        _groups[group.Id] = group;
        return Task.FromResult(group);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (id == TaskGroupConstants.DefaultGroupId)
        {
            throw new InvalidOperationException("Cannot delete the default group.");
        }
        return Task.FromResult(_groups.TryRemove(id, out _));
    }
}
