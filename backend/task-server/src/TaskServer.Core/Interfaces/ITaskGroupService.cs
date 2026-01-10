using TaskServer.Core.DTOs;
using TaskServer.Core.Entities;

namespace TaskServer.Core.Interfaces;

public interface ITaskGroupService
{
    Task<TaskGroup> CreateGroupAsync(CreateTaskGroupRequest request, CancellationToken ct = default);
    Task<TaskGroup?> GetGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<TaskGroup?> GetGroupByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<TaskGroup>> GetAllGroupsAsync(CancellationToken ct = default);
    Task<TaskGroup> UpdateGroupAsync(Guid groupId, UpdateTaskGroupRequest request, CancellationToken ct = default);
    Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct = default);
    Task EnsureDefaultGroupsExistAsync(CancellationToken ct = default);
}
