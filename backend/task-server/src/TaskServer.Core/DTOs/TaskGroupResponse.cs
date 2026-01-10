namespace TaskServer.Core.DTOs;

public record TaskGroupResponse(
    Guid Id,
    string Name,
    int MaxParallelism,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ActiveTaskCount,
    int QueuedTaskCount
);
