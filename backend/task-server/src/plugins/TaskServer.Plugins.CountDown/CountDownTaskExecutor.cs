using System.Text.Json;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Plugins.CountDown;

public class CountDownPayload
{
    public int DurationInSeconds { get; set; } = 60;
}

public class CountDownTaskExecutor : ITaskExecutor
{
    public string TaskType => "countdown";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<CountDownPayload>(task.Payload, JsonOptions)
                      ?? new CountDownPayload();

        var totalSeconds = payload.DurationInSeconds;

        for (int remaining = totalSeconds; remaining >= 0; remaining--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var percentage = ((double)(totalSeconds - remaining) / totalSeconds) * 100;
            var timeSpan = TimeSpan.FromSeconds(remaining);
            var details = remaining == 0
                ? "Countdown complete!"
                : $"Time remaining: {timeSpan:mm\\:ss}";

            // Include remaining seconds as JSON payload
            var payloadJson = JsonSerializer.Serialize(new { remainingSeconds = remaining });
            progress.Report(new TaskProgressUpdate(percentage, details, payloadJson));

            if (remaining > 0)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}
