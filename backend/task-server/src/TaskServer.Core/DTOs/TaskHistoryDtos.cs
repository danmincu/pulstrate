namespace TaskServer.Core.DTOs;

/// <summary>
/// A single progress history entry for a task.
/// </summary>
public record ProgressHistoryEntryDto(
    Guid TaskId,
    string TaskType,
    DateTime Timestamp,
    double Percentage,
    string? Details,
    string? Payload
);

/// <summary>
/// A single state change history entry for a task.
/// </summary>
public record StateChangeHistoryEntryDto(
    Guid TaskId,
    string TaskType,
    string TaskIdShort,
    DateTime Timestamp,
    string NewState,
    string? Details
);

/// <summary>
/// Combined history for a task (or task tree).
/// </summary>
public record TaskHistoryDto(
    IReadOnlyList<ProgressHistoryEntryDto> ProgressHistory,
    IReadOnlyList<StateChangeHistoryEntryDto> StateChangeHistory
);
