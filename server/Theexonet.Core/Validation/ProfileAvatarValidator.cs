namespace Theexonet.Core.Validation;

public static class ProfileAvatarValidator
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    public static string? Validate(string? contentType, long length, ReadOnlySpan<byte> header)
    {
        if (length <= 0)
        {
            return "Choose an image file to upload.";
        }

        if (length > MaxBytes)
        {
            return "Profile photo must be 2 MB or smaller.";
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            return "Profile photo must be a JPEG, PNG, WebP, or GIF image.";
        }

        if (!HasSupportedImageHeader(header))
        {
            return "That file does not look like a valid image.";
        }

        return null;
    }

    public static string ExtensionForContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin"
        };

    private static bool HasSupportedImageHeader(ReadOnlySpan<byte> header) =>
        ProfileImageHeaderValidator.HasSupportedImageHeader(header);
}
