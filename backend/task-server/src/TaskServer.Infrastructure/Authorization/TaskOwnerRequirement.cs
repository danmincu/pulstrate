using Microsoft.AspNetCore.Authorization;

namespace TaskServer.Infrastructure.Authorization;

public class TaskOwnerRequirement : IAuthorizationRequirement
{
}
