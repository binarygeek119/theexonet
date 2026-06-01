using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Rava.Api.Services.CompanyLogo;

internal static class CompanyLogoImageEncoder
{
    private const int ChromaKeyThreshold = 42;

    public static async Task<byte[]> PrepareTransparentPngAsync(byte[] sourceBytes, CancellationToken ct)
    {
        await using var input = new MemoryStream(sourceBytes);
        using var image = await Image.LoadAsync<Rgba32>(input, ct);

        if (!HasMeaningfulAlpha(image))
        {
            ApplyChromaKey(image);
        }

        await using var output = new MemoryStream();
        await image.SaveAsPngAsync(output, ct);
        return output.ToArray();
    }

    private static bool HasMeaningfulAlpha(Image<Rgba32> image)
    {
        var transparentPixels = 0;
        var total = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    total++;
                    if (row[x].A < 250)
                    {
                        transparentPixels++;
                    }
                }
            }
        });

        return total > 0 && transparentPixels > total / 200;
    }

    private static void ApplyChromaKey(Image<Rgba32> image)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    if (IsNear(pixel.R, pixel.G, pixel.B, 0, 255, 0)
                        || IsNear(pixel.R, pixel.G, pixel.B, 255, 255, 255)
                        || IsNear(pixel.R, pixel.G, pixel.B, 245, 245, 245))
                    {
                        pixel.A = 0;
                    }
                }
            }
        });
    }

    private static bool IsNear(byte r, byte g, byte b, byte tr, byte tg, byte tb)
    {
        return Math.Abs(r - tr) <= ChromaKeyThreshold
            && Math.Abs(g - tg) <= ChromaKeyThreshold
            && Math.Abs(b - tb) <= ChromaKeyThreshold;
    }
}
