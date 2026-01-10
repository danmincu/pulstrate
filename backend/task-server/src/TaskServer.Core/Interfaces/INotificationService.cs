using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

public interface INotificationService
{
    Task NotifyTaskCreatedAsync(TaskItem task);
    Task NotifyTaskUpdatedAsync(TaskItem task);
    Task NotifyTaskDeletedAsync(Guid taskId, Guid ownerId);
    Task NotifyStateChangedAsync(Guid taskId, Guid ownerId, TaskState state, string? details);
    Task NotifyProgressAsync(Guid taskId, Guid ownerId, double percentage, string? details, string? payload);
}
