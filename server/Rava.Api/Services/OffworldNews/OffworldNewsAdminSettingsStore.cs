using System.Text.Json;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Services.OffworldNews;

public sealed class OffworldNewsAdminSettingsStore(
    IOptions<OffworldNewsOptions> defaults,
    IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private OffworldNewsAdminSettingsRecord _current = FromDefaults(defaults.Value);

    public string FilePath =>
        RavaDataPaths.ResolveFile(environment.ContentRootPath, "offworld-news-admin-settings.json");

    public int ReporterPoolSize
    {
        get
        {
            lock (_gate)
            {
                return _current.ReporterPoolSize;
            }
        }
    }

    public void Load()
    {
        lock (_gate)
        {
            _current = ReadFromDisk() ?? FromDefaults(defaults.Value);
            OffworldNewsReporterCatalog.ConfigureStoryPoolSize(_current.ReporterPoolSize);
        }
    }

    public AdminOffworldNewsSettingsDto GetSettings()
    {
        lock (_gate)
        {
            return ToDto(_current);
        }
    }

    public OffworldNewsOptions ToGenerationOptions()
    {
        lock (_gate)
        {
            var config = defaults.Value;
            return new OffworldNewsOptions
            {
                Enabled = config.Enabled,
                CacheDirectory = config.CacheDirectory,
                ReportersFile = config.ReportersFile,
                StoriesPerDay = _current.StoriesPerDay,
                StoriesPerDayVariance = _current.StoriesPerDayVariance,
                MinStoriesPerDay = _current.MinStoriesPerDay,
                MaxStoriesPerDay = _current.MaxStoriesPerDay,
                MaxImagesPerDay = config.MaxImagesPerDay,
            };
        }
    }

    public (AdminOffworldNewsSettingsDto? Settings, string? Error) Save(AdminUpdateOffworldNewsSettingsRequest request)
    {
        var total = OffworldNewsReporterCatalog.All.Count;
        if (request.ReporterPoolSize > total && total > 0)
        {
            return (null, $"Reporter pool size cannot exceed the roster count ({total}).");
        }

        var (values, error) = OffworldNewsAdminSettingsValidator.Validate(request);
        if (error is not null || values is null)
        {
            return (null, error);
        }

        lock (_gate)
        {
            _current = new OffworldNewsAdminSettingsRecord(
                values.ReporterPoolSize,
                values.StoriesPerDay,
                values.StoriesPerDayVariance,
                values.MinStoriesPerDay,
                values.MaxStoriesPerDay);
            OffworldNewsReporterCatalog.ConfigureStoryPoolSize(_current.ReporterPoolSize);
            WriteToDisk();
            return (ToDto(_current), null);
        }
    }

    public int ActivePoolCount()
    {
        var total = OffworldNewsReporterCatalog.All.Count;
        var size = ReporterPoolSize;
        if (size <= 0 || size >= total)
        {
            return total;
        }

        return size;
    }

    private static OffworldNewsAdminSettingsRecord FromDefaults(OffworldNewsOptions config) =>
        new(
            0,
            Math.Max(1, config.StoriesPerDay),
            Math.Max(0, config.StoriesPerDayVariance),
            Math.Max(1, config.MinStoriesPerDay),
            Math.Max(1, config.MaxStoriesPerDay));

    private AdminOffworldNewsSettingsDto ToDto(OffworldNewsAdminSettingsRecord record)
    {
        var total = OffworldNewsReporterCatalog.All.Count;
        return new AdminOffworldNewsSettingsDto(
            record.ReporterPoolSize,
            total,
            ActivePoolCount(),
            record.StoriesPerDay,
            record.StoriesPerDayVariance,
            record.MinStoriesPerDay,
            record.MaxStoriesPerDay);
    }

    private OffworldNewsAdminSettingsRecord? ReadFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var json = File.ReadAllText(FilePath);
            var legacy = JsonSerializer.Deserialize<OffworldNewsAdminSettingsRecord>(json, JsonOptions);
            if (legacy is null)
            {
                return null;
            }

            if (legacy.StoriesPerDay <= 0)
            {
                var fromDefaults = FromDefaults(defaults.Value);
                return legacy with
                {
                    StoriesPerDay = fromDefaults.StoriesPerDay,
                    StoriesPerDayVariance = fromDefaults.StoriesPerDayVariance,
                    MinStoriesPerDay = fromDefaults.MinStoriesPerDay,
                    MaxStoriesPerDay = fromDefaults.MaxStoriesPerDay,
                };
            }

            return legacy;
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

    private sealed record OffworldNewsAdminSettingsRecord(
        int ReporterPoolSize,
        int StoriesPerDay = 5,
        int StoriesPerDayVariance = 3,
        int MinStoriesPerDay = 1,
        int MaxStoriesPerDay = 10);
}
