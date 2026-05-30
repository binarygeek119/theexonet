namespace Rava.Core.Configuration;

public class AdminOptions
{
    public const string SectionName = "Admin";

    public string[] Usernames { get; set; } = [];

    public bool IsAdminUsername(string? username)
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
