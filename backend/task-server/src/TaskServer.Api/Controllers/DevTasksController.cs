using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Extensions;
using TaskServer.Core.Interfaces;

namespace TaskServer.Api.Controllers;

/// <summary>
/// Development-only controller for testing task operations without authentication.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dev/tasks")]
[AllowAnonymous]
public class DevTasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ITaskGroupRepository _groupRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<DevTasksController> _logger;
    private readonly IWebHostEnvironment _environment;

    // Fixed test user ID for development
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DevTasksController(
        ITaskService taskService,
        ITaskGroupRepository groupRepository,
        ITaskRepository taskRepository,
        ILogger<DevTasksController> logger,
        IWebHostEnvironment environment)
    {
        _taskService = taskService;
        _groupRepository = groupRepository;
        _taskRepository = taskRepository;
        _logger = logger;
        _environment = environment;
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

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetTasks(CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var tasks = await _taskService.GetUserTasksAsync(TestUserId, ct);
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
        if (!_environment.IsDevelopment())
            return NotFound();

        var task = await _taskService.GetTaskAsync(id, TestUserId, ct);

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
        if (!_environment.IsDevelopment())
            return NotFound();

        try
        {
            var task = await _taskService.CreateTaskAsync(TestUserId, request, ct);
            _logger.LogInformation("Created task {TaskId} of type {TaskType} in group {GroupId}", task.Id, task.Type, task.GroupId);
            var response = await ToResponseWithGroupAsync(task, ct);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST", ex.Message));
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponse>> CancelTask(Guid id, CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        try
        {
            var task = await _taskService.CancelTaskAsync(id, TestUserId, ct);
            return Ok(await ToResponseWithGroupAsync(task, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", ex.Message));
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
        if (!_environment.IsDevelopment())
            return NotFound();

        var deleted = await _taskService.DeleteTaskAsync(id, TestUserId, ct);

        if (!deleted)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
        }

        return NoContent();
    }

    // Hierarchical task endpoints

    [HttpPost("hierarchy")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> CreateTaskHierarchy([FromBody] CreateTaskHierarchyRequest request, CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        try
        {
            var task = await _taskService.CreateTaskHierarchyAsync(TestUserId, request, ct);
            _logger.LogInformation("Created hierarchical task {TaskId} of type {TaskType}", task.Id, task.Type);
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
        if (!_environment.IsDevelopment())
            return NotFound();

        var children = await _taskService.GetChildTasksAsync(id, TestUserId, ct);

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
        if (!_environment.IsDevelopment())
            return NotFound();

        try
        {
            var task = await _taskService.CancelTaskSubtreeAsync(id, TestUserId, ct);
            return Ok(await ToResponseWithGroupAsync(task, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", ex.Message));
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
        if (!_environment.IsDevelopment())
            return NotFound();

        var deleted = await _taskService.DeleteTaskSubtreeAsync(id, TestUserId, ct);

        if (!deleted)
        {
            return NotFound(new ErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
        }

        return NoContent();
    }
}
