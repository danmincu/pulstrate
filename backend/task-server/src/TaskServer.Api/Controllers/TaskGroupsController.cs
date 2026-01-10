using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/groups")]
[Authorize]
public class TaskGroupsController : ControllerBase
{
    private readonly ITaskGroupService _groupService;
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskQueue _taskQueue;
    private readonly ILogger<TaskGroupsController> _logger;

    public TaskGroupsController(
        ITaskGroupService groupService,
        ITaskRepository taskRepository,
        ITaskQueue taskQueue,
        ILogger<TaskGroupsController> logger)
    {
        _groupService = groupService;
        _taskRepository = taskRepository;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskGroupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TaskGroupResponse>>> GetGroups(CancellationToken ct)
    {
        var groups = await _groupService.GetAllGroupsAsync(ct);
        var tasks = await _taskRepository.GetAllAsync(ct);
        var queuedCounts = await _taskQueue.GetQueuedCountByGroupAsync(ct);

        var responses = groups.Select(group =>
        {
            var groupTasks = tasks.Where(t => t.GroupId == group.Id);
            var activeCount = groupTasks.Count(t => t.State == TaskState.Executing);
            queuedCounts.TryGetValue(group.Id, out var queuedCount);

            return new TaskGroupResponse(
                group.Id,
                group.Name,
                group.MaxParallelism,
                group.Description,
                group.CreatedAt,
                group.UpdatedAt,
                activeCount,
                queuedCount
            );
        });

        return Ok(responses);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskGroupResponse>> GetGroup(Guid id, CancellationToken ct)
    {
        var group = await _groupService.GetGroupAsync(id, ct);
        if (group == null)
        {
            return NotFound(new ErrorResponse("GROUP_NOT_FOUND", $"Group with ID {id} not found"));
        }

        var tasks = await _taskRepository.GetAllAsync(ct);
        var groupTasks = tasks.Where(t => t.GroupId == group.Id);
        var activeCount = groupTasks.Count(t => t.State == TaskState.Executing);
        var queuedCounts = await _taskQueue.GetQueuedCountByGroupAsync(ct);
        queuedCounts.TryGetValue(group.Id, out var queuedCount);

        var response = new TaskGroupResponse(
            group.Id,
            group.Name,
            group.MaxParallelism,
            group.Description,
            group.CreatedAt,
            group.UpdatedAt,
            activeCount,
            queuedCount
        );

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskGroupResponse>> CreateGroup([FromBody] CreateTaskGroupRequest request, CancellationToken ct)
    {
        try
        {
            var group = await _groupService.CreateGroupAsync(request, ct);
            var response = new TaskGroupResponse(
                group.Id,
                group.Name,
                group.MaxParallelism,
                group.Description,
                group.CreatedAt,
                group.UpdatedAt,
                0,
                0
            );
            return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST", ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TaskGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskGroupResponse>> UpdateGroup(Guid id, [FromBody] UpdateTaskGroupRequest request, CancellationToken ct)
    {
        try
        {
            var group = await _groupService.UpdateGroupAsync(id, request, ct);

            var tasks = await _taskRepository.GetAllAsync(ct);
            var groupTasks = tasks.Where(t => t.GroupId == group.Id);
            var activeCount = groupTasks.Count(t => t.State == TaskState.Executing);
            var queuedCounts = await _taskQueue.GetQueuedCountByGroupAsync(ct);
            queuedCounts.TryGetValue(group.Id, out var queuedCount);

            var response = new TaskGroupResponse(
                group.Id,
                group.Name,
                group.MaxParallelism,
                group.Description,
                group.CreatedAt,
                group.UpdatedAt,
                activeCount,
                queuedCount
            );

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse("GROUP_NOT_FOUND", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST", ex.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        try
        {
            var deleted = await _groupService.DeleteGroupAsync(id, ct);
            if (!deleted)
            {
                return NotFound(new ErrorResponse("GROUP_NOT_FOUND", $"Group with ID {id} not found"));
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST", ex.Message));
        }
    }
}
