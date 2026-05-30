namespace Rava.Core.Configuration;

public class ModeratorOptions
{
    public const string SectionName = "Moderator";

    public string[] Usernames { get; set; } = [];

    public bool IsModeratorUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var normalized = username.Trim();
        return (Usernames ?? []).Any(name =>
            !string.IsNullOrWhiteSpace(name)
            && string.Equals(name.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }
}
