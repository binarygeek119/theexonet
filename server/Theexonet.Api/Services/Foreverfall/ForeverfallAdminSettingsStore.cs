using System.Text.Json;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.Foreverfall;

public sealed class ForeverfallAdminSettingsStore(IOptions<ForeverfallOptions> defaults, IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private ForeverfallAdminSettingsRecord _current = FromDefaults(defaults.Value);

    public string FilePath =>
        TheexonetDataPaths.ResolveFile(environment.ContentRootPath, "foreverfall-admin-settings.json");

    public void Load()
    {
        lock (_gate)
        {
            _current = ReadFromDisk() ?? FromDefaults(defaults.Value);
        }
    }

    public AdminForeverfallSettingsDto GetSettings(int portraitPoolCount)
    {
        lock (_gate)
        {
            return ToDto(_current, portraitPoolCount);
        }
    }

    public ForeverfallOptions ToGenerationOptions()
    {
        lock (_gate)
        {
            var config = defaults.Value;
            return new ForeverfallOptions
            {
                Enabled = _current.Enabled && config.Enabled,
                CacheDirectory = config.CacheDirectory,
                MaxInmateImages = _current.MaxInmateImages,
                RetentionDays = _current.RetentionDays,
                TargetDailyIntake = _current.TargetDailyIntake,
                IntakeVariance = _current.IntakeVariance,
                MinDailyIntake = _current.MinDailyIntake,
                MaxDailyIntake = _current.MaxDailyIntake,
            };
        }
    }

    public (AdminForeverfallSettingsDto? Settings, string? Error) Save(
        AdminUpdateForeverfallSettingsRequest request,
        int portraitPoolCount)
    {
        var (values, error) = ForeverfallAdminSettingsValidator.Validate(request);
        if (error is not null || values is null)
        {
            return (null, error);
        }

        lock (_gate)
        {
            _current = new ForeverfallAdminSettingsRecord(
                values.Enabled,
                values.MaxInmateImages,
                values.RetentionDays,
                values.TargetDailyIntake,
                values.IntakeVariance,
                values.MinDailyIntake,
                values.MaxDailyIntake);
            WriteToDisk();
            return (ToDto(_current, portraitPoolCount), null);
        }
    }

    private static ForeverfallAdminSettingsRecord FromDefaults(ForeverfallOptions config) =>
        new(
            config.Enabled,
            Math.Clamp(config.MaxInmateImages, 1, 5000),
            Math.Clamp(config.RetentionDays, 1, 365),
            Math.Clamp(config.TargetDailyIntake, 1, 5000),
            Math.Max(0, config.IntakeVariance),
            Math.Clamp(config.MinDailyIntake, 1, 5000),
            Math.Clamp(config.MaxDailyIntake, 1, 5000));

    private AdminForeverfallSettingsDto ToDto(ForeverfallAdminSettingsRecord record, int portraitPoolCount) =>
        new(
            record.Enabled,
            record.MaxInmateImages,
            portraitPoolCount,
            record.RetentionDays,
            record.TargetDailyIntake,
            record.IntakeVariance,
            record.MinDailyIntake,
            record.MaxDailyIntake);

    private ForeverfallAdminSettingsRecord? ReadFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ForeverfallAdminSettingsRecord>(File.ReadAllText(FilePath), JsonOptions);
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

    private sealed record ForeverfallAdminSettingsRecord(
        bool Enabled,
        int MaxInmateImages,
        int RetentionDays,
        int TargetDailyIntake,
        int IntakeVariance,
        int MinDailyIntake,
        int MaxDailyIntake);
}
