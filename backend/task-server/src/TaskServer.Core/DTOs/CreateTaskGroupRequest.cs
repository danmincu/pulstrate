namespace TaskServer.Core.DTOs;

public record CreateTaskGroupRequest(
    Guid? Id,
    string Name,
    int MaxParallelism,
    string? Description
);
