using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;

namespace Theexonet.Api.Services;

public class AdminRequirement : IAuthorizationRequirement;

public class AdminAuthorizationHandler(IOptions<AdminOptions> options) : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        var username = context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;

        if (string.IsNullOrWhiteSpace(username))
        {
            return Task.CompletedTask;
        }

        if (options.Value.IsAdminUsername(username))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
