namespace TaskServer.Core.Entities;

public class TaskGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaxParallelism { get; set; } = 1;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
