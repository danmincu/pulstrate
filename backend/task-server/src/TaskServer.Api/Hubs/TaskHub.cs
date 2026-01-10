using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskServer.Core.DTOs;
using TaskServer.Core.Extensions;
using TaskServer.Core.Interfaces;

namespace TaskServer.Api.Hubs;

// [Authorize] - Commented out for development. TODO: Re-enable for production
public class TaskHub : Hub<ITaskHubClient>
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TaskHub> _logger;
    private readonly IWebHostEnvironment _environment;

    // Fixed test user ID for development (same as DevTasksController)
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public TaskHub(ITaskService taskService, ILogger<TaskHub> logger, IWebHostEnvironment environment)
    {
        _taskService = taskService;
        _logger = logger;
        _environment = environment;
    }

    private Guid GetUserId()
    {
        // In development mode, return test user ID if not authenticated
        if (_environment.IsDevelopment())
        {
            var userIdClaim = Context.User?.FindFirst("user_id")?.Value
                           ?? Context.User?.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return TestUserId;
            }

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
        }
        else
        {
            var userIdClaim = Context.User?.FindFirst("user_id")?.Value
                           ?? Context.User?.FindFirst("sub")?.Value
                           ?? throw new HubException("User ID not found in token");

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
        }

        // Fallback: normalize the claim to a GUID
        var claim = Context.User?.FindFirst("user_id")?.Value
                 ?? Context.User?.FindFirst("sub")?.Value
                 ?? string.Empty;

        if (string.IsNullOrEmpty(claim))
        {
            return _environment.IsDevelopment() ? TestUserId : throw new HubException("User ID not found in token");
        }

        var normalized = new string(claim.Where(c => char.IsLetterOrDigit(c)).ToArray());
        if (normalized.Length >= 32)
        {
            normalized = normalized.Substring(0, 32);
        }
        else
        {
            normalized = normalized.PadLeft(32, '0');
        }

        return Guid.Parse($"{normalized.Substring(0, 8)}-{normalized.Substring(8, 4)}-{normalized.Substring(12, 4)}-{normalized.Substring(16, 4)}-{normalized.Substring(20, 12)}");
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetUserId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation("User {UserId} connected to TaskHub", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OnConnectedAsync");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetUserId();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation("User {UserId} disconnected from TaskHub", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OnDisconnectedAsync");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<TaskResponse> CreateTask(CreateTaskRequest request)
    {
        var userId = GetUserId();
        var task = await _taskService.CreateTaskAsync(userId, request);
        return task.ToResponse();
    }

    public async Task<TaskResponse?> GetTask(Guid id)
    {
        var userId = GetUserId();
        var task = await _taskService.GetTaskAsync(id, userId);
        return task?.ToResponse();
    }

    public async Task<TaskResponse[]> GetTasks()
    {
        var userId = GetUserId();
        var tasks = await _taskService.GetUserTasksAsync(userId);
        return tasks.Select(t => t.ToResponse()).ToArray();
    }

    public async Task<TaskResponse> UpdateTask(Guid id, UpdateTaskRequest request)
    {
        var userId = GetUserId();
        var task = await _taskService.UpdateTaskAsync(id, userId, request);
        return task.ToResponse();
    }

    public async Task DeleteTask(Guid id)
    {
        var userId = GetUserId();
        await _taskService.DeleteTaskAsync(id, userId);
    }

    public async Task<TaskResponse> CancelTask(Guid id)
    {
        var userId = GetUserId();
        var task = await _taskService.CancelTaskAsync(id, userId);
        return task.ToResponse();
    }

    public async Task SubscribeToTask(Guid taskId)
    {
        var userId = GetUserId();
        var task = await _taskService.GetTaskAsync(taskId, userId);
        if (task != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"task:{taskId}");
            _logger.LogInformation("User {UserId} subscribed to task {TaskId}", userId, taskId);
        }
    }

    public async Task UnsubscribeFromTask(Guid taskId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"task:{taskId}");
        _logger.LogInformation("Connection {ConnectionId} unsubscribed from task {TaskId}", Context.ConnectionId, taskId);
    }

    public async Task SubscribeToAllUserTasks()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        _logger.LogInformation("User {UserId} subscribed to all their tasks", userId);
    }

    public async Task UnsubscribeFromAllUserTasks()
    {
        var userId = GetUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        _logger.LogInformation("User {UserId} unsubscribed from all their tasks", userId);
    }
}
