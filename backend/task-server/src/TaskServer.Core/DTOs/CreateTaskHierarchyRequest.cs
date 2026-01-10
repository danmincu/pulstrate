namespace TaskServer.Core.DTOs;

/// <summary>
/// Request to create a task hierarchy (parent with children) in a single atomic operation.
/// Children can be nested (have their own children) to create deep hierarchies.
/// </summary>
public record CreateTaskHierarchyRequest(
    CreateTaskRequest ParentTask,
    IReadOnlyList<CreateTaskHierarchyRequest> ChildTasks
);
