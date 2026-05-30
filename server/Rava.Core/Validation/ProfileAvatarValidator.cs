namespace Rava.Core.Validation;

public static class ProfileAvatarValidator
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
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

    private static bool HasSupportedImageHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 &&
            header[0] == 0xFF &&
            header[1] == 0xD8 &&
            header[2] == 0xFF)
        {
            return true;
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 &&
            header[1] == (byte)'P' &&
            header[2] == (byte)'N' &&
            header[3] == (byte)'G' &&
            header[4] == 0x0D &&
            header[5] == 0x0A &&
            header[6] == 0x1A &&
            header[7] == 0x0A)
        {
            return true;
        }

        if (header.Length >= 6 &&
            header[0] == (byte)'G' &&
            header[1] == (byte)'I' &&
            header[2] == (byte)'F' &&
            header[3] == (byte)'8')
        {
            return true;
        }

        if (header.Length >= 12 &&
            header[0] == (byte)'R' &&
            header[1] == (byte)'I' &&
            header[2] == (byte)'F' &&
            header[3] == (byte)'F' &&
            header[8] == (byte)'W' &&
            header[9] == (byte)'E' &&
            header[10] == (byte)'B' &&
            header[11] == (byte)'P')
        {
            return true;
        }

        return false;
    }
}
