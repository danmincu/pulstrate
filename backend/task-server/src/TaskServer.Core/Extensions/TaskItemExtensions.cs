using TaskServer.Core.Constants;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;

namespace TaskServer.Core.Extensions;

public static class TaskItemExtensions
{
    public static TaskResponse ToResponse(
        this TaskItem task,
        string? groupName = null,
        int childCount = 0,
        IReadOnlyList<ProgressHistoryEntryDto>? progressHistory = null,
        IReadOnlyList<StateChangeHistoryEntryDto>? stateChangeHistory = null)
    {
        return new TaskResponse(
            task.Id,
            task.OwnerId,
            task.GroupId,
            groupName ?? TaskGroupConstants.DefaultGroupName,
            task.Priority,
            task.Type,
            task.Payload,
            task.State.ToString(),
            task.Progress,
            task.ProgressDetails,
            task.ProgressPayload,
            task.StateDetails,
            task.Output,
            task.CreatedAt,
            task.UpdatedAt,
            task.StartedAt,
            task.CompletedAt,
            task.ParentTaskId,
            task.Weight,
            task.SubtaskParallelism,
            childCount,
            task.TrackHistory,
            progressHistory,
            stateChangeHistory
        );
    }
}
