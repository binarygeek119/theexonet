namespace Rava.Infrastructure.Services;

public class ProfileBackgroundStorageOptions
{
    /// <summary>Under WebRootPath (e.g. /var/www/publish/html/images/profile-backgrounds).</summary>
    public const string RelativeFolder = "images/profile-backgrounds";

    public string WebRootPath { get; set; } = string.Empty;
}
