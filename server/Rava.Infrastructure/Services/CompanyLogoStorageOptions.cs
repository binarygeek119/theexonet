namespace Rava.Infrastructure.Services;

public class CompanyLogoStorageOptions
{
    public const string RelativeFolder = "company-logos";

    public const string PublicUrlPath = "images/company-logos";

    public string ImagesRootPath { get; set; } = string.Empty;
}
