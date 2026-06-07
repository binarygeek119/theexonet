using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;

namespace Theexonet.Api.Services;

public class ModeratorRequirement : IAuthorizationRequirement;

public class ModeratorAuthorizationHandler(
    IOptions<ModeratorOptions> moderatorOptions,
    IOptions<AdminOptions> adminOptions) : AuthorizationHandler<ModeratorRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModeratorRequirement requirement)
    {
        var username = context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;

        if (string.IsNullOrWhiteSpace(username))
        {
            return Task.CompletedTask;
        }

        if (IsStaffUsername(username, moderatorOptions.Value.Usernames)
            || IsStaffUsername(username, adminOptions.Value.Usernames))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool IsStaffUsername(string username, string[] allowed) =>
        (allowed ?? []).Any(name => string.Equals(name, username, StringComparison.OrdinalIgnoreCase));
}
