using TaskServer.Core.DTOs;

namespace TaskServer.Core.Interfaces;

public interface ITaskHubClient
{
    Task OnTaskCreated(TaskResponse task);
    Task OnTaskUpdated(TaskResponse task);
    Task OnTaskDeleted(Guid taskId);
    Task OnStateChanged(Guid taskId, string newState, string? details);
    Task OnProgress(Guid taskId, double percentage, string? details, string? payload);
}
