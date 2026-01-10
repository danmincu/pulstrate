using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Services;

/// <summary>
/// Service for aggregating child task progress to parent tasks using weighted averages.
/// </summary>
public class ProgressAggregationService : IProgressAggregationService
{
    private readonly ITaskRepository _repository;
    private readonly INotificationService _notifications;

    public ProgressAggregationService(ITaskRepository repository, INotificationService notifications)
    {
        _repository = repository;
        _notifications = notifications;
    }

    public async Task OnChildProgressReportedAsync(Guid childTaskId, double childProgress, CancellationToken ct = default)
    {
        var child = await _repository.GetByIdAsync(childTaskId, ct);
        if (child?.ParentTaskId == null)
        {
            return; // No parent to update
        }

        await RecalculateParentProgressAsync(child.ParentTaskId.Value, ct);
    }

    public async Task OnChildStateChangedAsync(Guid childTaskId, TaskState newState, CancellationToken ct = default)
    {
        var child = await _repository.GetByIdAsync(childTaskId, ct);
        if (child?.ParentTaskId == null)
        {
            return; // No parent to update
        }

        // Recalculate progress (completed children contribute 100%)
        await RecalculateParentProgressAsync(child.ParentTaskId.Value, ct);
    }

    private async Task RecalculateParentProgressAsync(Guid parentId, CancellationToken ct)
    {
        var parent = await _repository.GetByIdAsync(parentId, ct);
        if (parent == null)
        {
            return;
        }

        var children = await _repository.GetChildrenAsync(parentId, ct);
        if (children.Count == 0)
        {
            return; // No children to aggregate
        }

        // Calculate weighted average progress
        double totalWeight = children.Sum(c => c.Weight);
        double weightedProgress = 0;

        foreach (var child in children)
        {
            double childContribution = child.State switch
            {
                TaskState.Completed => 100,
                TaskState.Cancelled => child.Progress, // Keep progress at cancellation point
                TaskState.Errored => child.Progress,   // Keep progress at error point
                TaskState.Terminated => child.Progress, // Keep progress at termination point
                _ => child.Progress
            };
            weightedProgress += (child.Weight / totalWeight) * childContribution;
        }

        parent.Progress = weightedProgress;
        parent.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(parent, ct);
        await _notifications.NotifyProgressAsync(
            parent.Id,
            parent.OwnerId,
            parent.Progress,
            $"Aggregated from {children.Count} children",
            null);

        // Recursively update ancestors (progress only bubbles up immediate level per design)
        if (parent.ParentTaskId.HasValue)
        {
            await RecalculateParentProgressAsync(parent.ParentTaskId.Value, ct);
        }
    }
}
