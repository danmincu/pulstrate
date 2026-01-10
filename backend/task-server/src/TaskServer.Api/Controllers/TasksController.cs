using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Extensions;
using TaskServer.Core.Interfaces;

namespace TaskServer.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ITaskGroupRepository _groupRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskService taskService,
        ITaskGroupRepository groupRepository,
        ITaskRepository taskRepository,
        ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _groupRepository = groupRepository;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    private async Task<string> GetGroupNameAsync(Guid groupId, CancellationToken ct)
    {
        var group = await _groupRepository.GetByIdAsync(groupId, ct);
        return group?.Name ?? "default";
    }

    private async Task<TaskResponse> ToResponseWithGroupAsync(TaskItem task, CancellationToken ct)
    {
        var groupName = await GetGroupNameAsync(task.GroupId, ct);
        var childCount = await _taskRepository.GetChildCountAsync(task.Id, ct);
        return task.ToResponse(groupName, childCount);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? throw new UnauthorizedAccessException("User ID not found in token");

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return Guid.Parse(userIdClaim.PadLeft(32, '0').Substring(0, 32).Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-"));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetTasks(CancellationToken ct)
    {
        var userId = GetUserId();
        var tasks = await _taskService.GetUserTasksAsync(userId, ct);

        var responses = new List<TaskResponse>();
        foreach (var task in tasks)
        {
            responses.Add(await ToResponseWithGroupAsync(task, ct));
        }
        return Ok(responses);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetTask(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var task = await _taskService.GetTaskAsync(id, userId, ct);

        if (task == null)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
        }

        return Ok(await ToResponseWithGroupAsync(task, ct));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> CreateTask([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var task = await _taskService.CreateTaskAsync(userId, request, ct);
            var response = await ToResponseWithGroupAsync(task, ct);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST", ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponse>> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var task = await _taskService.UpdateTaskAsync(id, userId, request, ct);
            return Ok(await ToResponseWithGroupAsync(task, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse("INVALID_STATE", ex.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var deleted = await _taskService.DeleteTaskAsync(id, userId, ct);
            if (!deleted)
            {
                return NotFound(new ErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponse>> CancelTask(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var task = await _taskService.CancelTaskAsync(id, userId, ct);
            return Ok(await ToResponseWithGroupAsync(task, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse("INVALID_STATE", ex.Message));
        }
    }

    // Hierarchical task endpoints

    [HttpPost("hierarchy")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> CreateTaskHierarchy([FromBody] CreateTaskHierarchyRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var task = await _taskService.CreateTaskHierarchyAsync(userId, request, ct);
            var response = await ToResponseWithGroupAsync(task, ct);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST", ex.Message));
        }
    }

    [HttpGet("{id:guid}/children")]
    [ProducesResponseType(typeof(IEnumerable<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetChildTasks(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var children = await _taskService.GetChildTasksAsync(id, userId, ct);

        var responses = new List<TaskResponse>();
        foreach (var task in children)
        {
            responses.Add(await ToResponseWithGroupAsync(task, ct));
        }
        return Ok(responses);
    }

    [HttpPost("{id:guid}/cancel-subtree")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponse>> CancelTaskSubtree(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var task = await _taskService.CancelTaskSubtreeAsync(id, userId, ct);
            return Ok(await ToResponseWithGroupAsync(task, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse("INVALID_STATE", ex.Message));
        }
    }

    [HttpDelete("{id:guid}/subtree")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTaskSubtree(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var deleted = await _taskService.DeleteTaskSubtreeAsync(id, userId, ct);
            if (!deleted)
            {
                return NotFound(new ErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
