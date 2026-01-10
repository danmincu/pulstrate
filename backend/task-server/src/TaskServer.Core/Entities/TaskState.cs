namespace TaskServer.Core.Entities;

public enum TaskState
{
    Queued,
    Executing,
    Cancelled,
    Errored,
    Completed,
    Terminated
}
