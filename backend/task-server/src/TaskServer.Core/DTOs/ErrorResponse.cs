namespace TaskServer.Core.DTOs;

public record ErrorResponse(
    string Code,
    string Message,
    string? Details = null,
    string? TraceId = null
);
