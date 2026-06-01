namespace Rava.Infrastructure.Services;

public class ProfileAvatarStorageOptions
{
    public const string RelativeFolder = "profile";

    public const string PublicUrlPath = "images/profile";

    public string ImagesRootPath { get; set; } = string.Empty;
}
