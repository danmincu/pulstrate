using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Executors;

/// <summary>
/// A simple hierarchical task executor that demonstrates parent-child task relationships.
/// The parent task waits for all children to complete. Progress is aggregated from children.
/// </summary>
public class SimpleHierarchicalExecutor : TaskExecutorBase
{
    private readonly ILogger<SimpleHierarchicalExecutor> _logger;

    public SimpleHierarchicalExecutor(ILogger<SimpleHierarchicalExecutor> logger)
    {
        _logger = logger;
    }

    public override string TaskType => "simple-hierarchical";

    public override async Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        // This executor is for parent tasks - it doesn't do work itself
        // The TaskProcessorService handles orchestrating children
        // This method is only called if the task has no children (leaf node)

        var payload = ParsePayload(task.Payload);
        var durationMs = payload.DurationMs;
        var steps = payload.Steps;

        _logger.LogInformation("SimpleHierarchical task {TaskId} starting with {Steps} steps over {Duration}ms",
            task.Id, steps, durationMs);

        var stepDuration = durationMs / steps;

        for (int i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(stepDuration, cancellationToken);

            var percentage = (double)i / steps * 100;
            progress.Report(new TaskProgressUpdate(
                percentage,
                $"Step {i} of {steps}",
                JsonSerializer.Serialize(new { step = i, totalSteps = steps })
            ));

            _logger.LogDebug("SimpleHierarchical task {TaskId} completed step {Step}/{TotalSteps}",
                task.Id, i, steps);
        }

        _logger.LogInformation("SimpleHierarchical task {TaskId} completed all {Steps} steps", task.Id, steps);
    }

    public override void OnAllSubtasksSuccess(TaskItem parentTask, IReadOnlyList<TaskItem> completedChildren)
    {
        _logger.LogInformation(
            "SimpleHierarchical parent task {TaskId} - all {ChildCount} children completed successfully",
            parentTask.Id, completedChildren.Count);
    }

    private static SimpleHierarchicalPayload ParsePayload(string payload)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<SimpleHierarchicalPayload>(payload, options)
                   ?? new SimpleHierarchicalPayload();
        }
        catch
        {
            return new SimpleHierarchicalPayload();
        }
    }

    private class SimpleHierarchicalPayload
    {
        public int DurationMs { get; set; } = 5000;
        public int Steps { get; set; } = 5;
    }
}
