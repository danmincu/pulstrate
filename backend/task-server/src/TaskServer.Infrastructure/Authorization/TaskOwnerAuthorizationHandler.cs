using Microsoft.AspNetCore.Authorization;
using TaskServer.Core.Entities;

namespace TaskServer.Infrastructure.Authorization;

public class TaskOwnerAuthorizationHandler : AuthorizationHandler<TaskOwnerRequirement, TaskItem>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TaskOwnerRequirement requirement,
        TaskItem resource)
    {
        var userIdClaim = context.User.FindFirst("user_id")?.Value
                       ?? context.User.FindFirst("sub")?.Value;

        if (userIdClaim != null && resource.OwnerId.ToString() == userIdClaim)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
