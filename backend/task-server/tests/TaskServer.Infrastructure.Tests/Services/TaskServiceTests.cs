using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;
using TaskServer.Infrastructure.Services;
using TaskServer.Infrastructure.Storage;

namespace TaskServer.Infrastructure.Tests.Services;

public class TaskServiceTests
{
    private readonly ITaskRepository _repository;
    private readonly ITaskQueue _queue;
    private readonly INotificationService _notifications;
    private readonly ITaskService _taskService;

    public TaskServiceTests()
    {
        _repository = new InMemoryTaskRepository();
        _queue = new InMemoryTaskQueue();
        _notifications = new MockNotificationService();
        _taskService = new TaskService(_repository, _queue, _notifications);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldCreateTask_WithQueuedState()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var request = new CreateTaskRequest(null, 10, "demo", "{\"test\": true}", null);

        // Act
        var task = await _taskService.CreateTaskAsync(ownerId, request);

        // Assert
        Assert.NotNull(task);
        Assert.Equal(ownerId, task.OwnerId);
        Assert.Equal(10, task.Priority);
        Assert.Equal("demo", task.Type);
        Assert.Equal(TaskState.Queued, task.State);
        Assert.Equal(0, task.Progress);
    }

    [Fact]
    public async Task CreateTaskAsync_WithClientProvidedId_ShouldUseProvidedId()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var request = new CreateTaskRequest(taskId, 5, "demo", "{}", null);

        // Act
        var task = await _taskService.CreateTaskAsync(ownerId, request);

        // Assert
        Assert.Equal(taskId, task.Id);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldReturnTask_WhenOwnerMatches()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var request = new CreateTaskRequest(null, 5, "demo", "{}", null);
        var createdTask = await _taskService.CreateTaskAsync(ownerId, request);

        // Act
        var retrievedTask = await _taskService.GetTaskAsync(createdTask.Id, ownerId);

        // Assert
        Assert.NotNull(retrievedTask);
        Assert.Equal(createdTask.Id, retrievedTask.Id);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldReturnNull_WhenOwnerDoesNotMatch()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var differentOwnerId = Guid.NewGuid();
        var request = new CreateTaskRequest(null, 5, "demo", "{}", null);
        var createdTask = await _taskService.CreateTaskAsync(ownerId, request);

        // Act
        var retrievedTask = await _taskService.GetTaskAsync(createdTask.Id, differentOwnerId);

        // Assert
        Assert.Null(retrievedTask);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldUpdateTask_WhenStateIsQueued()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var request = new CreateTaskRequest(null, 5, "demo", "{\"original\": true}", null);
        var createdTask = await _taskService.CreateTaskAsync(ownerId, request);
        var updateRequest = new UpdateTaskRequest(10, "{\"updated\": true}");

        // Act
        var updatedTask = await _taskService.UpdateTaskAsync(createdTask.Id, ownerId, updateRequest);

        // Assert
        Assert.Equal(10, updatedTask.Priority);
        Assert.Equal("{\"updated\": true}", updatedTask.Payload);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldReturnTrue_WhenTaskExists()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var request = new CreateTaskRequest(null, 5, "demo", "{}", null);
        var createdTask = await _taskService.CreateTaskAsync(ownerId, request);

        // Act
        var result = await _taskService.DeleteTaskAsync(createdTask.Id, ownerId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetUserTasksAsync_ShouldReturnOnlyUserTasks()
    {
        // Arrange
        var ownerId1 = Guid.NewGuid();
        var ownerId2 = Guid.NewGuid();
        await _taskService.CreateTaskAsync(ownerId1, new CreateTaskRequest(null, 5, "demo", "{}", null));
        await _taskService.CreateTaskAsync(ownerId1, new CreateTaskRequest(null, 5, "demo", "{}", null));
        await _taskService.CreateTaskAsync(ownerId2, new CreateTaskRequest(null, 5, "demo", "{}", null));

        // Act
        var tasks = await _taskService.GetUserTasksAsync(ownerId1);

        // Assert
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, t => Assert.Equal(ownerId1, t.OwnerId));
    }

    private class MockNotificationService : INotificationService
    {
        public Task NotifyTaskCreatedAsync(TaskItem task) => Task.CompletedTask;
        public Task NotifyTaskUpdatedAsync(TaskItem task) => Task.CompletedTask;
        public Task NotifyTaskDeletedAsync(Guid taskId, Guid ownerId) => Task.CompletedTask;
        public Task NotifyStateChangedAsync(Guid taskId, Guid ownerId, TaskState state, string? details) => Task.CompletedTask;
        public Task NotifyProgressAsync(Guid taskId, Guid ownerId, double percentage, string? details, string? payload) => Task.CompletedTask;
    }
}
