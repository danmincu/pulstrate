using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

public interface ITaskExecutor
{
    string TaskType { get; }

    Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken
    );
}

public record TaskProgressUpdate(
    double Percentage,
    string? Details = null,
    string? PayloadJson = null
);

public record TaskStateChange(
    Guid TaskId,
    TaskState NewState,
    string? Details = null
);
