using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Rava.Api.Services.OffworldNews;

internal static class OffworldNewsImageEncoder
{
    public const int JpegQuality = 85;

    public static async Task SaveAsJpegAsync(byte[] sourceBytes, string filePath, CancellationToken ct)
    {
        await using var input = new MemoryStream(sourceBytes);
        using var image = await Image.LoadAsync(input, ct);
        var encoder = new JpegEncoder { Quality = JpegQuality };
        await image.SaveAsJpegAsync(filePath, encoder, ct);
    }
}
