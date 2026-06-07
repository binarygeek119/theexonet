using System.Text.Json;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.LunarWeather;

public sealed class LunarWeatherAdminSettingsStore(IOptions<LunarWeatherOptions> defaults, IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private LunarWeatherAdminSettingsRecord _current = FromDefaults(defaults.Value);

    public string FilePath =>
        TheexonetDataPaths.ResolveFile(environment.ContentRootPath, "lunar-weather-admin-settings.json");

    public void Load()
    {
        lock (_gate)
        {
            _current = ReadFromDisk() ?? FromDefaults(defaults.Value);
        }
    }

    public AdminLunarWeatherSettingsDto GetSettings()
    {
        lock (_gate)
        {
            return ToDto(_current);
        }
    }

    public LunarWeatherOptions ToGenerationOptions()
    {
        lock (_gate)
        {
            var config = defaults.Value;
            return new LunarWeatherOptions
            {
                Enabled = config.Enabled,
                CacheDirectory = config.CacheDirectory,
                RelaysFile = config.RelaysFile,
                RelayPoolSize = _current.RelayPoolSize,
                TargetOperationalCount = _current.TargetOperationalCount,
                OperationalVariance = _current.OperationalVariance,
                MinOperationalCount = _current.MinOperationalCount,
                MaxOperationalCount = _current.MaxOperationalCount,
            };
        }
    }

    public (AdminLunarWeatherSettingsDto? Settings, string? Error) Save(AdminUpdateLunarWeatherSettingsRequest request)
    {
        var catalogTotal = LunarWeatherRelayCatalog.All.Count;
        var (values, error) = LunarWeatherAdminSettingsValidator.Validate(request, catalogTotal);
        if (error is not null || values is null)
        {
            return (null, error);
        }

        lock (_gate)
        {
            _current = new LunarWeatherAdminSettingsRecord(
                values.RelayPoolSize,
                values.TargetOperationalCount,
                values.OperationalVariance,
                values.MinOperationalCount,
                values.MaxOperationalCount);
            WriteToDisk();
            return (ToDto(_current), null);
        }
    }

    private static LunarWeatherAdminSettingsRecord FromDefaults(LunarWeatherOptions config) =>
        new(
            Math.Clamp(config.RelayPoolSize, 1, 100),
            Math.Clamp(config.TargetOperationalCount, 1, 100),
            Math.Max(0, config.OperationalVariance),
            Math.Clamp(config.MinOperationalCount, 1, 100),
            Math.Clamp(config.MaxOperationalCount, 1, 100));

    private AdminLunarWeatherSettingsDto ToDto(LunarWeatherAdminSettingsRecord record)
    {
        var catalogTotal = LunarWeatherRelayCatalog.All.Count;
        return new AdminLunarWeatherSettingsDto(
            record.RelayPoolSize,
            catalogTotal,
            record.TargetOperationalCount,
            record.OperationalVariance,
            record.MinOperationalCount,
            record.MaxOperationalCount);
    }

    private LunarWeatherAdminSettingsRecord? ReadFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LunarWeatherAdminSettingsRecord>(File.ReadAllText(FilePath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void WriteToDisk()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(_current, JsonOptions) + Environment.NewLine);
    }

    private sealed record LunarWeatherAdminSettingsRecord(
        int RelayPoolSize,
        int TargetOperationalCount,
        int OperationalVariance,
        int MinOperationalCount,
        int MaxOperationalCount);
}
