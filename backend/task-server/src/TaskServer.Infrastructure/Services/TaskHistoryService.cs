using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;
using TaskServer.Infrastructure.Storage;

namespace TaskServer.Infrastructure.Services;

/// <summary>
/// Service for recording and retrieving task history.
/// Delegates storage to InMemoryTaskHistoryRepository.
/// </summary>
public class TaskHistoryService : ITaskHistoryService
{
    private readonly InMemoryTaskHistoryRepository _repository;

    public TaskHistoryService(InMemoryTaskHistoryRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task RecordProgressAsync(
        Guid rootTaskId,
        Guid taskId,
        string taskType,
        double percentage,
        string? details,
        string? payload)
    {
        var entry = new ProgressHistoryEntryDto(
            TaskId: taskId,
            TaskType: taskType,
            Timestamp: DateTime.UtcNow,
            Percentage: percentage,
            Details: details,
            Payload: payload
        );

        _repository.AddProgressEntry(rootTaskId, entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordStateChangeAsync(
        Guid rootTaskId,
        Guid taskId,
        string taskType,
        TaskState newState,
        string? details)
    {
        var entry = new StateChangeHistoryEntryDto(
            TaskId: taskId,
            TaskType: taskType,
            TaskIdShort: taskId.ToString()[..8],
            Timestamp: DateTime.UtcNow,
            NewState: newState.ToString(),
            Details: details
        );

        _repository.AddStateChangeEntry(rootTaskId, entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TaskHistoryDto> GetHistoryAsync(
        Guid rootTaskId,
        int progressLimit = 100,
        int stateLimit = 50)
    {
        var progressHistory = _repository.GetProgressHistory(rootTaskId, progressLimit);
        var stateHistory = _repository.GetStateChangeHistory(rootTaskId, stateLimit);

        return Task.FromResult(new TaskHistoryDto(progressHistory, stateHistory));
    }

    /// <inheritdoc />
    public Task ClearHistoryAsync(Guid rootTaskId)
    {
        _repository.ClearHistory(rootTaskId);
        return Task.CompletedTask;
    }
}
