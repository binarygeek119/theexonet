using Microsoft.Extensions.Options;
using Rava.Core.Configuration;

namespace Rava.Infrastructure.Services;

public class StaffModerationPolicy(
    IOptionsMonitor<AdminOptions> adminOptions,
    IOptionsMonitor<ModeratorOptions> moderatorOptions)
{
    public string? ValidateModerationAction(string targetUsername, string actorUsername)
    {
        if (adminOptions.CurrentValue.IsAdminUsername(targetUsername))
        {
            return "Admin accounts cannot be flagged, banned, or unbanned.";
        }

        if (moderatorOptions.CurrentValue.IsModeratorUsername(targetUsername)
            && !adminOptions.CurrentValue.IsAdminUsername(actorUsername))
        {
            return "Only admins can flag, ban, or unban moderator accounts.";
        }

        return null;
    }

    public bool IsProtectedAdmin(string? username) =>
        adminOptions.CurrentValue.IsAdminUsername(username);

    public bool IsModeratorAccount(string? username) =>
        moderatorOptions.CurrentValue.IsModeratorUsername(username);
}
