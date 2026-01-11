using Microsoft.AspNetCore.SignalR;
using TaskServer.Core.Entities;
using TaskServer.Core.Extensions;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Services;

public class NotificationService<THub> : INotificationService where THub : Hub<ITaskHubClient>
{
    private readonly IHubContext<THub, ITaskHubClient> _hubContext;
    private readonly ITaskGroupRepository _groupRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskHistoryService _historyService;

    public NotificationService(
        IHubContext<THub, ITaskHubClient> hubContext,
        ITaskGroupRepository groupRepository,
        ITaskRepository taskRepository,
        ITaskHistoryService historyService)
    {
        _hubContext = hubContext;
        _groupRepository = groupRepository;
        _taskRepository = taskRepository;
        _historyService = historyService;
    }

    public async Task NotifyTaskCreatedAsync(TaskItem task)
    {
        var groupName = await GetGroupNameAsync(task.GroupId);
        var response = task.ToResponse(groupName);
        await _hubContext.Clients.Group($"user:{task.OwnerId}").OnTaskCreated(response);
        await _hubContext.Clients.Group($"task:{task.Id}").OnTaskCreated(response);
    }

    public async Task NotifyTaskUpdatedAsync(TaskItem task)
    {
        var groupName = await GetGroupNameAsync(task.GroupId);
        var response = task.ToResponse(groupName);
        await _hubContext.Clients.Group($"user:{task.OwnerId}").OnTaskUpdated(response);
        await _hubContext.Clients.Group($"task:{task.Id}").OnTaskUpdated(response);
    }

    private async Task<string> GetGroupNameAsync(Guid groupId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        return group?.Name ?? "default";
    }

    public async Task NotifyTaskDeletedAsync(Guid taskId, Guid ownerId)
    {
        await _hubContext.Clients.Group($"user:{ownerId}").OnTaskDeleted(taskId);
        await _hubContext.Clients.Group($"task:{taskId}").OnTaskDeleted(taskId);
    }

    public async Task NotifyStateChangedAsync(Guid taskId, Guid ownerId, TaskState state, string? details)
    {
        await _hubContext.Clients.Group($"user:{ownerId}").OnStateChanged(taskId, state.ToString(), details);
        await _hubContext.Clients.Group($"task:{taskId}").OnStateChanged(taskId, state.ToString(), details);

        // Record history if tracking is enabled
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task?.TrackHistory == true)
        {
            await _historyService.RecordStateChangeAsync(
                task.RootTaskId, taskId, task.Type, state, details);
        }
    }

    public async Task NotifyProgressAsync(Guid taskId, Guid ownerId, double percentage, string? details, string? payload)
    {
        await _hubContext.Clients.Group($"user:{ownerId}").OnProgress(taskId, percentage, details, payload);
        await _hubContext.Clients.Group($"task:{taskId}").OnProgress(taskId, percentage, details, payload);

        // Record history if tracking is enabled
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task?.TrackHistory == true)
        {
            await _historyService.RecordProgressAsync(
                task.RootTaskId, taskId, task.Type, percentage, details, payload);
        }
    }
}
