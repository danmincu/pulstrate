namespace TaskServer.Core.DTOs;

public record TaskResponse(
    Guid Id,
    Guid OwnerId,
    Guid GroupId,
    string GroupName,
    int Priority,
    string Type,
    string Payload,
    string State,
    double Progress,
    string? ProgressDetails,
    string? ProgressPayload,
    string? StateDetails,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    // Hierarchical task support
    Guid? ParentTaskId,
    double Weight,
    bool SubtaskParallelism,
    int ChildCount
);
