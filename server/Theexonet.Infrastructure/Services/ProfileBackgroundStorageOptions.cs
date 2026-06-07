namespace Theexonet.Infrastructure.Services;

public class ProfileBackgroundStorageOptions
{
    /// <summary>Under ImagesRootPath (e.g. /var/www/data/images/profile-backgrounds).</summary>
    public const string RelativeFolder = "profile-backgrounds";

    public const string PublicUrlPath = "images/profile-backgrounds";

    public string ImagesRootPath { get; set; } = string.Empty;
}
