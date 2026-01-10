using TaskServer.Core.Constants;
using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Services;

public class TaskGroupService : ITaskGroupService
{
    private readonly ITaskGroupRepository _repository;
    private readonly ITaskRepository _taskRepository;

    public TaskGroupService(ITaskGroupRepository repository, ITaskRepository taskRepository)
    {
        _repository = repository;
        _taskRepository = taskRepository;
    }

    public async Task<TaskGroup> CreateGroupAsync(CreateTaskGroupRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByNameAsync(request.Name, ct);
        if (existing != null)
        {
            throw new InvalidOperationException($"Group with name '{request.Name}' already exists.");
        }

        var now = DateTime.UtcNow;
        var group = new TaskGroup
        {
            Id = request.Id ?? Guid.NewGuid(),
            Name = request.Name,
            MaxParallelism = request.MaxParallelism,
            Description = request.Description,
            CreatedAt = now,
            UpdatedAt = now
        };

        return await _repository.AddAsync(group, ct);
    }

    public async Task<TaskGroup?> GetGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        return await _repository.GetByIdAsync(groupId, ct);
    }

    public async Task<TaskGroup?> GetGroupByNameAsync(string name, CancellationToken ct = default)
    {
        return await _repository.GetByNameAsync(name, ct);
    }

    public async Task<IReadOnlyList<TaskGroup>> GetAllGroupsAsync(CancellationToken ct = default)
    {
        return await _repository.GetAllAsync(ct);
    }

    public async Task<TaskGroup> UpdateGroupAsync(Guid groupId, UpdateTaskGroupRequest request, CancellationToken ct = default)
    {
        var group = await _repository.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            throw new KeyNotFoundException($"Group with ID {groupId} not found.");
        }

        if (request.Name != null)
        {
            var existing = await _repository.GetByNameAsync(request.Name, ct);
            if (existing != null && existing.Id != groupId)
            {
                throw new InvalidOperationException($"Group with name '{request.Name}' already exists.");
            }
            group.Name = request.Name;
        }

        if (request.MaxParallelism.HasValue)
        {
            group.MaxParallelism = request.MaxParallelism.Value;
        }

        if (request.Description != null)
        {
            group.Description = request.Description;
        }

        group.UpdatedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(group, ct);
    }

    public async Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        if (groupId == TaskGroupConstants.DefaultGroupId)
        {
            throw new InvalidOperationException("Cannot delete the default group.");
        }

        // Check if group has any queued or executing tasks
        var allTasks = await _taskRepository.GetAllAsync(ct);
        var groupTasks = allTasks.Where(t => t.GroupId == groupId &&
            (t.State == TaskState.Queued || t.State == TaskState.Executing)).ToList();

        if (groupTasks.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete group with {groupTasks.Count} active task(s). Cancel or wait for tasks to complete first.");
        }

        return await _repository.DeleteAsync(groupId, ct);
    }

    public async Task EnsureDefaultGroupsExistAsync(CancellationToken ct = default)
    {
        // Create default group if it doesn't exist
        var defaultGroup = await _repository.GetByIdAsync(TaskGroupConstants.DefaultGroupId, ct);
        if (defaultGroup == null)
        {
            var now = DateTime.UtcNow;
            await _repository.AddAsync(new TaskGroup
            {
                Id = TaskGroupConstants.DefaultGroupId,
                Name = TaskGroupConstants.DefaultGroupName,
                MaxParallelism = TaskGroupConstants.DefaultGroupMaxParallelism,
                Description = "Default task group for unassigned tasks",
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }

        // Create CPU processing group if it doesn't exist
        var cpuGroup = await _repository.GetByNameAsync(TaskGroupConstants.CpuProcessingGroupName, ct);
        if (cpuGroup == null)
        {
            var now = DateTime.UtcNow;
            await _repository.AddAsync(new TaskGroup
            {
                Id = Guid.NewGuid(),
                Name = TaskGroupConstants.CpuProcessingGroupName,
                MaxParallelism = Environment.ProcessorCount,
                Description = "CPU-bound task processing group (parallelism = CPU cores)",
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }

        // Create exclusive processing group if it doesn't exist
        var exclusiveGroup = await _repository.GetByNameAsync(TaskGroupConstants.ExclusiveProcessingGroupName, ct);
        if (exclusiveGroup == null)
        {
            var now = DateTime.UtcNow;
            await _repository.AddAsync(new TaskGroup
            {
                Id = Guid.NewGuid(),
                Name = TaskGroupConstants.ExclusiveProcessingGroupName,
                MaxParallelism = TaskGroupConstants.ExclusiveGroupMaxParallelism,
                Description = "Single-task execution group (e.g., for exclusive resources like radio transmission)",
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }
    }
}
