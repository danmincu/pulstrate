using System.Text.Json;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Plugins.RollDice;

public class RollDicePayload
{
    public int DesiredDice1 { get; set; } = 6;
    public int DesiredDice2 { get; set; } = 6;
}

public class RollDiceProgressPayload
{
    public int Roll { get; set; }
    public int Dice1 { get; set; }
    public int Dice2 { get; set; }
    public string Target { get; set; } = "";
    public bool IsMatch { get; set; }
}

public class RollDiceTaskExecutor : ITaskExecutor
{
    public string TaskType => "rolldice";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Random RandomGenerator = new();

    public async Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<RollDicePayload>(task.Payload, JsonOptions)
                      ?? new RollDicePayload();

        // Validate dice values (1-6)
        var desired1 = Math.Clamp(payload.DesiredDice1, 1, 6);
        var desired2 = Math.Clamp(payload.DesiredDice2, 1, 6);
        var target = $"{desired1}-{desired2}";

        const int maxRolls = 100;

        for (int roll = 1; roll <= maxRolls; roll++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Roll two dice
            var dice1 = RandomGenerator.Next(1, 7);
            var dice2 = RandomGenerator.Next(1, 7);

            // Check if we have a match (order matters or not? Let's say order doesn't matter)
            var isMatch = (dice1 == desired1 && dice2 == desired2) ||
                          (dice1 == desired2 && dice2 == desired1);

            // Calculate progress percentage
            var percentage = (double)roll;

            // Create progress payload
            var progressPayload = new RollDiceProgressPayload
            {
                Roll = roll,
                Dice1 = dice1,
                Dice2 = dice2,
                Target = target,
                IsMatch = isMatch
            };

            var payloadJson = JsonSerializer.Serialize(progressPayload);

            if (isMatch)
            {
                // Success! Report final progress and return
                var successDetails = $"Rolled {dice1}-{dice2}! Matched {target} on roll #{roll}";
                progress.Report(new TaskProgressUpdate(100, successDetails, payloadJson));
                return;
            }

            // Report progress
            var details = $"Roll #{roll}: {dice1}-{dice2} (looking for {target})";
            progress.Report(new TaskProgressUpdate(percentage, details, payloadJson));

            // Wait 1 second before next roll
            await Task.Delay(1000, cancellationToken);
        }

        // Reached max rolls without match - throw to trigger Errored state
        throw new InvalidOperationException(
            $"Failed to roll {target} after {maxRolls} attempts. Better luck next time!");
    }
}
