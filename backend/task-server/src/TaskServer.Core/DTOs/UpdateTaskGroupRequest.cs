namespace TaskServer.Core.DTOs;

public record UpdateTaskGroupRequest(
    string? Name,
    int? MaxParallelism,
    string? Description
);
