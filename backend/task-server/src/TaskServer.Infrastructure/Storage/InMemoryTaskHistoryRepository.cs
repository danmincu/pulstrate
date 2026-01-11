using System.Collections.Concurrent;
using TaskServer.Core.DTOs;

namespace TaskServer.Infrastructure.Storage;

/// <summary>
/// Internal storage container for a single root task's history.
/// </summary>
internal class HistoryStore
{
    public List<ProgressHistoryEntryDto> ProgressHistory { get; } = new();
    public List<StateChangeHistoryEntryDto> StateChangeHistory { get; } = new();
    public readonly object Lock = new();
}

/// <summary>
/// In-memory storage for task history, keyed by root task ID.
/// Thread-safe with bounded size (circular buffer behavior).
/// </summary>
public class InMemoryTaskHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, HistoryStore> _stores = new();

    /// <summary>
    /// Maximum number of progress history entries per root task.
    /// </summary>
    public const int MaxProgressEntries = 200;

    /// <summary>
    /// Maximum number of state change history entries per root task.
    /// </summary>
    public const int MaxStateEntries = 100;

    /// <summary>
    /// Adds a progress history entry for a root task.
    /// </summary>
    public void AddProgressEntry(Guid rootTaskId, ProgressHistoryEntryDto entry)
    {
        var store = _stores.GetOrAdd(rootTaskId, _ => new HistoryStore());

        lock (store.Lock)
        {
            store.ProgressHistory.Add(entry);

            // Trim oldest entries if over limit (circular buffer)
            while (store.ProgressHistory.Count > MaxProgressEntries)
            {
                store.ProgressHistory.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Adds a state change history entry for a root task.
    /// </summary>
    public void AddStateChangeEntry(Guid rootTaskId, StateChangeHistoryEntryDto entry)
    {
        var store = _stores.GetOrAdd(rootTaskId, _ => new HistoryStore());

        lock (store.Lock)
        {
            store.StateChangeHistory.Add(entry);

            // Trim oldest entries if over limit (circular buffer)
            while (store.StateChangeHistory.Count > MaxStateEntries)
            {
                store.StateChangeHistory.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Gets the progress history for a root task.
    /// </summary>
    /// <param name="rootTaskId">The root task ID</param>
    /// <param name="limit">Maximum entries to return (from most recent)</param>
    /// <returns>List of progress history entries, newest last</returns>
    public IReadOnlyList<ProgressHistoryEntryDto> GetProgressHistory(Guid rootTaskId, int limit = MaxProgressEntries)
    {
        if (!_stores.TryGetValue(rootTaskId, out var store))
        {
            return Array.Empty<ProgressHistoryEntryDto>();
        }

        lock (store.Lock)
        {
            var count = store.ProgressHistory.Count;
            if (count <= limit)
            {
                return store.ProgressHistory.ToList();
            }

            // Return the last 'limit' entries
            return store.ProgressHistory.Skip(count - limit).ToList();
        }
    }

    /// <summary>
    /// Gets the state change history for a root task.
    /// </summary>
    /// <param name="rootTaskId">The root task ID</param>
    /// <param name="limit">Maximum entries to return (from most recent)</param>
    /// <returns>List of state change history entries, newest last</returns>
    public IReadOnlyList<StateChangeHistoryEntryDto> GetStateChangeHistory(Guid rootTaskId, int limit = MaxStateEntries)
    {
        if (!_stores.TryGetValue(rootTaskId, out var store))
        {
            return Array.Empty<StateChangeHistoryEntryDto>();
        }

        lock (store.Lock)
        {
            var count = store.StateChangeHistory.Count;
            if (count <= limit)
            {
                return store.StateChangeHistory.ToList();
            }

            // Return the last 'limit' entries
            return store.StateChangeHistory.Skip(count - limit).ToList();
        }
    }

    /// <summary>
    /// Clears all history for a root task.
    /// </summary>
    public void ClearHistory(Guid rootTaskId)
    {
        _stores.TryRemove(rootTaskId, out _);
    }

    /// <summary>
    /// Checks if history exists for a root task.
    /// </summary>
    public bool HasHistory(Guid rootTaskId)
    {
        return _stores.ContainsKey(rootTaskId);
    }
}
