using System.Text.Json;
using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Api.Services.OffworldNews;

public sealed class OffworldNewsAdminSettingsStore(IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private int _reporterPoolSize;

    public string FilePath =>
        RavaDataPaths.ResolveFile(environment.ContentRootPath, "offworld-news-admin-settings.json");

    public int ReporterPoolSize
    {
        get
        {
            lock (_gate)
            {
                return _reporterPoolSize;
            }
        }
    }

    public void Load()
    {
        lock (_gate)
        {
            _reporterPoolSize = ReadPoolSizeFromDisk();
            OffworldNewsReporterCatalog.ConfigureStoryPoolSize(_reporterPoolSize);
        }
    }

    public (int PoolSize, string? Error) SaveReporterPoolSize(int reporterPoolSize)
    {
        if (reporterPoolSize < 0)
        {
            return (0, "Reporter pool size cannot be negative.");
        }

        var total = OffworldNewsReporterCatalog.All.Count;
        if (reporterPoolSize > total && total > 0)
        {
            return (0, $"Reporter pool size cannot exceed the roster count ({total}).");
        }

        lock (_gate)
        {
            _reporterPoolSize = reporterPoolSize;
            OffworldNewsReporterCatalog.ConfigureStoryPoolSize(_reporterPoolSize);
            WriteToDisk();
        }

        return (_reporterPoolSize, null);
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

    private int ReadPoolSizeFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return 0;
            }

            var json = File.ReadAllText(FilePath);
            var record = JsonSerializer.Deserialize<OffworldNewsAdminSettingsRecord>(json, JsonOptions);
            return Math.Max(0, record?.ReporterPoolSize ?? 0);
        }
        catch
        {
            return 0;
        }
    }

    private void WriteToDisk()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var record = new OffworldNewsAdminSettingsRecord(_reporterPoolSize);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine);
    }

    private sealed record OffworldNewsAdminSettingsRecord(int ReporterPoolSize);
}
