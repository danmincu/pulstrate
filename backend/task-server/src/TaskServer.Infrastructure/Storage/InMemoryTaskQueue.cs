using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Storage;

public class InMemoryTaskQueue : ITaskQueue
{
    private readonly ILogger<InMemoryTaskQueue>? _logger;

    // Per-group queues
    private readonly ConcurrentDictionary<Guid, GroupQueue> _groupQueues = new();
    private readonly ConcurrentDictionary<Guid, bool> _cancelledTasks = new();
    private readonly SemaphoreSlim _anyTaskSignal = new(0);
    private readonly object _globalLock = new();

    private class GroupQueue
    {
        public PriorityQueue<(Guid TaskId, Guid GroupId), (int Priority, long Sequence)> Queue { get; } = new(
            Comparer<(int Priority, long Sequence)>.Create((a, b) =>
            {
                var priorityCompare = b.Priority.CompareTo(a.Priority);
                if (priorityCompare != 0) return priorityCompare;
                return a.Sequence.CompareTo(b.Sequence);
            }));
        public object Lock { get; } = new();
        public long SequenceCounter;
    }

    public InMemoryTaskQueue(ILogger<InMemoryTaskQueue>? logger = null)
    {
        _logger = logger;
    }

    public Task EnqueueAsync(Guid taskId, Guid groupId, int priority, CancellationToken ct = default)
    {
        long sequence;

        // Acquire global lock to ensure dequeue iteration sees new groups atomically
        lock (_globalLock)
        {
            var groupQueue = _groupQueues.GetOrAdd(groupId, _ => new GroupQueue());

            lock (groupQueue.Lock)
            {
                sequence = Interlocked.Increment(ref groupQueue.SequenceCounter);
                groupQueue.Queue.Enqueue((taskId, groupId), (priority, sequence));
            }
        }

        _logger?.LogInformation("Task {TaskId} enqueued to group {GroupId} with priority {Priority}, sequence #{Sequence}",
            taskId, groupId, priority, sequence);

        _anyTaskSignal.Release();
        return Task.CompletedTask;
    }

    public async Task<(Guid TaskId, Guid GroupId)?> DequeueAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            await _anyTaskSignal.WaitAsync(ct);

            // Find the highest priority task across all groups
            (Guid TaskId, Guid GroupId)? bestTask = null;
            (int Priority, long Sequence) bestKey = (int.MinValue, long.MaxValue);
            GroupQueue? bestGroupQueue = null;

            lock (_globalLock)
            {
                foreach (var kvp in _groupQueues)
                {
                    var groupQueue = kvp.Value;
                    lock (groupQueue.Lock)
                    {
                        if (groupQueue.Queue.TryPeek(out var task, out var key))
                        {
                            // Compare: higher priority wins, then lower sequence (earlier)
                            var isBetter = bestTask == null ||
                                           key.Priority > bestKey.Priority ||
                                           (key.Priority == bestKey.Priority && key.Sequence < bestKey.Sequence);

                            if (isBetter)
                            {
                                bestTask = task;
                                bestKey = key;
                                bestGroupQueue = groupQueue;
                            }
                        }
                    }
                }

                // Dequeue from the best group
                if (bestTask != null && bestGroupQueue != null)
                {
                    lock (bestGroupQueue.Lock)
                    {
                        bestGroupQueue.Queue.TryDequeue(out _, out _);
                    }
                }
            }

            if (bestTask == null)
            {
                continue;
            }

            // Check if cancelled
            if (_cancelledTasks.TryRemove(bestTask.Value.TaskId, out _))
            {
                _logger?.LogInformation("Task {TaskId} skipped (cancelled), was sequence #{Sequence}",
                    bestTask.Value.TaskId, bestKey.Sequence);
                continue;
            }

            _logger?.LogInformation("Task {TaskId} dequeued from group {GroupId}, priority {Priority}, sequence #{Sequence}",
                bestTask.Value.TaskId, bestTask.Value.GroupId, bestKey.Priority, bestKey.Sequence);

            return bestTask;
        }

        return null;
    }

    public Task<bool> TryCancelAsync(Guid taskId, CancellationToken ct = default)
    {
        _cancelledTasks[taskId] = true;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetQueuedCountByGroupAsync(CancellationToken ct = default)
    {
        var counts = new Dictionary<Guid, int>();

        foreach (var kvp in _groupQueues)
        {
            lock (kvp.Value.Lock)
            {
                counts[kvp.Key] = kvp.Value.Queue.Count;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }
}
