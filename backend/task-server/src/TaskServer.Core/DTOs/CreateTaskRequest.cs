namespace TaskServer.Core.DTOs;

public record CreateTaskRequest(
    Guid? Id,
    int Priority,
    string Type,
    string Payload,
    Guid? GroupId,
    // Hierarchical task support
    Guid? ParentTaskId = null,
    double Weight = 1.0,
    bool SubtaskParallelism = true
);
