namespace TaskServer.Core.Interfaces;

public interface ITaskQueue
{
    Task EnqueueAsync(Guid taskId, Guid groupId, int priority, CancellationToken ct = default);
    Task<(Guid TaskId, Guid GroupId)?> DequeueAsync(CancellationToken ct = default);
    Task<bool> TryCancelAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetQueuedCountByGroupAsync(CancellationToken ct = default);
}
