using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Theexonet.Api.Services.OffworldNews;

internal static class OffworldNewsImageEncoder
{
    public const int JpegQuality = 85;

    public static async Task SaveAsJpegAsync(byte[] sourceBytes, string filePath, CancellationToken ct)
    {
        await using var input = new MemoryStream(sourceBytes);
        using var image = await Image.LoadAsync(input, ct);
        ApplySpaceBlueTint(image);
        var encoder = new JpegEncoder { Quality = JpegQuality };
        await image.SaveAsJpegAsync(filePath, encoder, ct);
    }

    /// <summary>
    /// Applies a consistent ONN space-theme blue/cyan grade to generated illustrations.
    /// </summary>
    internal static void ApplySpaceBlueTint(Image image)
    {
        image.Mutate(ctx => ctx.ProcessPixelRowsAsVector4((row, _) =>
        {
            for (var i = 0; i < row.Length; i++)
            {
                ref var pixel = ref row[i];
                var red = pixel.X;
                var green = pixel.Y;
                var blue = pixel.Z;

                pixel.X = red * 0.82f + blue * 0.05f;
                pixel.Y = green * 0.90f + blue * 0.04f;
                pixel.Z = MathF.Min(1f, blue * 1.14f + 0.04f);
            }
        }));
    }
}
