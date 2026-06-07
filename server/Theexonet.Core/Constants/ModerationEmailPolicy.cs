namespace Theexonet.Core.Constants;

public static class ModerationEmailPolicy
{
    public static bool ShouldSkipNotification(string? reason) =>
        !string.IsNullOrWhiteSpace(reason)
        && reason.TrimStart().StartsWith("[TEST]", StringComparison.OrdinalIgnoreCase);
}
