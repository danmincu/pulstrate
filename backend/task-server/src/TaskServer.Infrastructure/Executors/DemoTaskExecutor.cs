using System.Text.Json;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Executors;

public class DemoTaskPayload
{
    public int DurationSeconds { get; set; } = 10;
    public int Steps { get; set; } = 10;
}

public class DemoTaskExecutor : ITaskExecutor
{
    public string TaskType => "demo";

    public async Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DemoTaskPayload>(task.Payload) ?? new DemoTaskPayload();
        var delayPerStep = (payload.DurationSeconds * 1000) / payload.Steps;

        for (int i = 1; i <= payload.Steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(delayPerStep, cancellationToken);

            var percentage = (double)i / payload.Steps * 100;
            progress.Report(new TaskProgressUpdate(percentage, $"Completed step {i} of {payload.Steps}"));
        }
    }
}
