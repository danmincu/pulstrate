using System.Collections.Concurrent;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Storage;

public class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _tasks.TryGetValue(id, out var task);
        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<TaskItem>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        var tasks = _tasks.Values
            .Where(t => t.OwnerId == ownerId)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
    }

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default)
    {
        var tasks = _tasks.Values
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
    }

    public Task<TaskItem> AddAsync(TaskItem task, CancellationToken ct = default)
    {
        if (!_tasks.TryAdd(task.Id, task))
        {
            throw new InvalidOperationException($"Task with ID {task.Id} already exists.");
        }
        return Task.FromResult(task);
    }

    public Task<TaskItem> UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        _tasks[task.Id] = task;
        return Task.FromResult(task);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_tasks.TryRemove(id, out _));
    }

    public Task<IReadOnlyList<TaskItem>> GetQueuedTasksAsync(CancellationToken ct = default)
    {
        var tasks = _tasks.Values
            .Where(t => t.State == TaskState.Queued)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
    }

    // Hierarchical task support

    public Task<IReadOnlyList<TaskItem>> GetChildrenAsync(Guid parentTaskId, CancellationToken ct = default)
    {
        var children = _tasks.Values
            .Where(t => t.ParentTaskId == parentTaskId)
            .OrderBy(t => t.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaskItem>>(children);
    }

    public Task<IReadOnlyList<TaskItem>> GetDescendantsAsync(Guid parentTaskId, CancellationToken ct = default)
    {
        var descendants = new List<TaskItem>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentTaskId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var children = _tasks.Values.Where(t => t.ParentTaskId == currentId).ToList();
            descendants.AddRange(children);
            foreach (var child in children)
            {
                queue.Enqueue(child.Id);
            }
        }

        return Task.FromResult<IReadOnlyList<TaskItem>>(descendants);
    }

    public Task<IReadOnlyList<TaskItem>> AddBatchAsync(IEnumerable<TaskItem> tasks, CancellationToken ct = default)
    {
        var addedTasks = new List<TaskItem>();
        foreach (var task in tasks)
        {
            if (!_tasks.TryAdd(task.Id, task))
            {
                throw new InvalidOperationException($"Task with ID {task.Id} already exists.");
            }
            addedTasks.Add(task);
        }
        return Task.FromResult<IReadOnlyList<TaskItem>>(addedTasks);
    }

    public Task<int> GetChildCountAsync(Guid parentTaskId, CancellationToken ct = default)
    {
        var count = _tasks.Values.Count(t => t.ParentTaskId == parentTaskId);
        return Task.FromResult(count);
    }

    public Task<bool> DeleteSubtreeAsync(Guid rootTaskId, CancellationToken ct = default)
    {
        // Get all descendants first (BFS)
        var descendants = GetDescendantsAsync(rootTaskId, ct).Result;

        // Delete descendants from leaves to root (reverse order)
        foreach (var descendant in descendants.Reverse())
        {
            _tasks.TryRemove(descendant.Id, out _);
        }

        // Delete root
        return Task.FromResult(_tasks.TryRemove(rootTaskId, out _));
    }
}
