using System.Text.Json;
using System.Text.Json.Serialization;
using Rava.Core.Dtos;

namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed class LunarWeatherEditionScanner : IExonetAiAssetScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string AreaName => "LunarWeather";

    public ExonetAiAssetScanAreaResult Scan(ExonetAiAssetScanContext context)
    {
        if (!context.LunarWeatherEnabled)
        {
            return ExonetAiAssetScanAreaResult.SkippedArea(AreaName);
        }

        var cacheRoot = context.HostingPaths.LunarWeatherCacheRoot;
        var invalid = 0;

        foreach (var editionPath in LunarWeatherStoragePaths.EnumerateEditionFiles(cacheRoot))
        {
            var fileName = Path.GetFileNameWithoutExtension(editionPath);
            if (!DateOnly.TryParse(fileName, out _))
            {
                invalid++;
                continue;
            }

            try
            {
                var bulletin = JsonSerializer.Deserialize<LunarWeatherBulletinDto>(
                    File.ReadAllText(editionPath),
                    JsonOptions);
                if (bulletin is null)
                {
                    invalid++;
                }
            }
            catch (JsonException)
            {
                invalid++;
            }
        }

        return new ExonetAiAssetScanAreaResult(AreaName, Invalid: invalid);
    }
}
