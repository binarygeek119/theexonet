namespace Rava.Infrastructure.Services;

public class ProfileAvatarStorageOptions
{
    public const string RelativeFolder = "images/profile";

    public string WebRootPath { get; set; } = string.Empty;
}
