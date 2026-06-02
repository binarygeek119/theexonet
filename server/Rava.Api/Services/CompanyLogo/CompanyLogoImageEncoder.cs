using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Rava.Api.Services.CompanyLogo;

internal static class CompanyLogoImageEncoder
{
    private const int ChromaKeyThreshold = 48;
    private const byte OpaqueAlphaThreshold = 24;
    private const int TrimPaddingPx = 10;

    public static async Task<byte[]> PrepareTransparentPngAsync(byte[] sourceBytes, CancellationToken ct)
    {
        await using var input = new MemoryStream(sourceBytes);
        using var image = await Image.LoadAsync<Rgba32>(input, ct);

        if (!HasMeaningfulAlpha(image))
        {
            ApplyChromaKey(image);
        }

        TrimTransparentBounds(image, TrimPaddingPx);

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
        var background = SampleCornerBackground(image);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    if (IsBackgroundPixel(pixel, background))
                    {
                        pixel.A = 0;
                    }
                }
            }
        });
    }

    private static (byte R, byte G, byte B) SampleCornerBackground(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        if (width == 0 || height == 0)
        {
            return (255, 255, 255);
        }

        var samples = new[]
        {
            image[0, 0],
            image[width - 1, 0],
            image[0, height - 1],
            image[width - 1, height - 1],
        };

        var r = (byte)samples.Average(p => p.R);
        var g = (byte)samples.Average(p => p.G);
        var b = (byte)samples.Average(p => p.B);
        return (r, g, b);
    }

    private static bool IsBackgroundPixel(Rgba32 pixel, (byte R, byte G, byte B) background) =>
        IsNear(pixel.R, pixel.G, pixel.B, background.R, background.G, background.B)
        || IsNear(pixel.R, pixel.G, pixel.B, 0, 255, 0)
        || IsNear(pixel.R, pixel.G, pixel.B, 255, 255, 255)
        || IsNear(pixel.R, pixel.G, pixel.B, 245, 245, 245)
        || IsNear(pixel.R, pixel.G, pixel.B, 230, 230, 230);

    private static void TrimTransparentBounds(Image<Rgba32> image, int padding)
    {
        if (!TryGetOpaqueBounds(image, out var left, out var top, out var right, out var bottom))
        {
            return;
        }

        left = Math.Max(0, left - padding);
        top = Math.Max(0, top - padding);
        right = Math.Min(image.Width - 1, right + padding);
        bottom = Math.Min(image.Height - 1, bottom + padding);

        var width = right - left + 1;
        var height = bottom - top + 1;
        if (width <= 0 || height <= 0 || (width == image.Width && height == image.Height))
        {
            return;
        }

        image.Mutate(ctx => ctx.Crop(new Rectangle(left, top, width, height)));
    }

    private static bool TryGetOpaqueBounds(
        Image<Rgba32> image,
        out int left,
        out int top,
        out int right,
        out int bottom)
    {
        var boundsLeft = image.Width;
        var boundsTop = image.Height;
        var boundsRight = -1;
        var boundsBottom = -1;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A <= OpaqueAlphaThreshold)
                    {
                        continue;
                    }

                    if (x < boundsLeft)
                    {
                        boundsLeft = x;
                    }

                    if (y < boundsTop)
                    {
                        boundsTop = y;
                    }

                    if (x > boundsRight)
                    {
                        boundsRight = x;
                    }

                    if (y > boundsBottom)
                    {
                        boundsBottom = y;
                    }
                }
            }
        });

        left = boundsLeft;
        top = boundsTop;
        right = boundsRight;
        bottom = boundsBottom;
        return right >= left && bottom >= top;
    }

    private static bool IsNear(byte r, byte g, byte b, byte tr, byte tg, byte tb)
    {
        return Math.Abs(r - tr) <= ChromaKeyThreshold
            && Math.Abs(g - tg) <= ChromaKeyThreshold
            && Math.Abs(b - tb) <= ChromaKeyThreshold;
    }
}
