using System.Buffers.Binary;

namespace Theexonet.Core.Validation;

public static class CompanyLogoValidator
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public static string? Validate(string? contentType, ReadOnlySpan<byte> png)
    {
        if (png.Length <= 0)
        {
            return "Choose a PNG file to upload.";
        }

        if (png.Length > MaxBytes)
        {
            return "Company logo must be 2 MB or smaller.";
        }

        if (!string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return "Company logo must be a transparent PNG file.";
        }

        if (!ProfileImageHeaderValidator.HasSupportedImageHeader(png) ||
            !IsPngSignature(png))
        {
            return "Company logo must be a valid PNG file.";
        }

        if (!PngSupportsTransparency(png))
        {
            return "Company logo must be a transparent PNG (RGBA, grayscale with alpha, or palette with transparency).";
        }

        return null;
    }

    private static bool IsPngSignature(ReadOnlySpan<byte> png) =>
        png.Length >= 8 &&
        png[0] == 0x89 &&
        png[1] == (byte)'P' &&
        png[2] == (byte)'N' &&
        png[3] == (byte)'G' &&
        png[4] == 0x0D &&
        png[5] == 0x0A &&
        png[6] == 0x1A &&
        png[7] == 0x0A;

    private static bool PngSupportsTransparency(ReadOnlySpan<byte> png)
    {
        if (png.Length < 26)
        {
            return false;
        }

        var colorType = png[25];
        return colorType switch
        {
            4 or 6 => true,
            3 => ContainsChunk(png, "tRNS"u8),
            _ => false,
        };
    }

    private static bool ContainsChunk(ReadOnlySpan<byte> png, ReadOnlySpan<byte> chunkType)
    {
        var pos = 8;
        while (pos + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.Slice(pos, 4));
            if (length < 0 || pos + 12 + length > png.Length)
            {
                break;
            }

            if (png.Slice(pos + 4, 4).SequenceEqual(chunkType))
            {
                return true;
            }

            if (png.Slice(pos + 4, 4).SequenceEqual("IEND"u8))
            {
                break;
            }

            pos += 12 + length;
        }

        return false;
    }
}
