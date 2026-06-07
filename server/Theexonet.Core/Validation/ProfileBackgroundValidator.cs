namespace Theexonet.Core.Validation;

public static class ProfileBackgroundValidator
{
    public const long MaxBytes = 4 * 1024 * 1024;

    public static string? Validate(string? contentType, long length, ReadOnlySpan<byte> header)
    {
        if (length <= 0)
        {
            return "Choose an image file to upload.";
        }

        if (length > MaxBytes)
        {
            return "Profile banner must be 4 MB or smaller.";
        }

        if (string.IsNullOrWhiteSpace(contentType) ||
            !ProfileAvatarValidator.AllowedContentTypes.Contains(contentType))
        {
            return "Profile banner must be a JPEG, PNG, WebP, or GIF image.";
        }

        if (!ProfileImageHeaderValidator.HasSupportedImageHeader(header))
        {
            return "That file does not look like a valid image.";
        }

        return null;
    }
}
