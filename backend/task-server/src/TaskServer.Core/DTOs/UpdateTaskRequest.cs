namespace TaskServer.Core.DTOs;

public record UpdateTaskRequest(
    int? Priority,
    string? Payload
);
