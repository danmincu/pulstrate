using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

/// <summary>
/// Service for aggregating child task progress to parent tasks.
/// </summary>
public interface IProgressAggregationService
{
    /// <summary>
    /// Called when a child task reports progress. Recalculates and updates parent progress.
    /// </summary>
    Task OnChildProgressReportedAsync(Guid childTaskId, double childProgress, CancellationToken ct = default);

    /// <summary>
    /// Called when a child task changes state. May trigger parent state changes and progress recalculation.
    /// </summary>
    Task OnChildStateChangedAsync(Guid childTaskId, TaskState newState, CancellationToken ct = default);
}
