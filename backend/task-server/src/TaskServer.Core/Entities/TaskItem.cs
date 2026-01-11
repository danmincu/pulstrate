using TaskServer.Core.Constants;

namespace TaskServer.Core.Entities;

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid GroupId { get; set; } = TaskGroupConstants.DefaultGroupId;
    public int Priority { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public TaskState State { get; set; }
    public double Progress { get; set; }
    public string? ProgressDetails { get; set; }
    public string? ProgressPayload { get; set; }
    public string? StateDetails { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Hierarchical task support
    public Guid? ParentTaskId { get; set; }
    public double Weight { get; set; } = 1.0;
    public bool SubtaskParallelism { get; set; } = true;

    // Authentication token for downstream service calls
    // Captured at task creation time, used by executors for authenticated HTTP calls
    public string? AuthToken { get; set; }
}
