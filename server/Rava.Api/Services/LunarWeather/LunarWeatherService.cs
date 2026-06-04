using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Rava.Api.Services.OffworldNews;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Services.LunarWeather;

public sealed class LunarWeatherService(
    IOptions<LunarWeatherOptions> lunarOptions,
    LunarWeatherAdminSettingsStore adminSettings,
    OpenAiConnectionResolver openAi,
    RavaHostingPaths hostingPaths,
    IHttpClientFactory httpClientFactory,
    ILogger<LunarWeatherService> logger)
{
    private static readonly ConcurrentDictionary<DateOnly, SemaphoreSlim> GenerationLocks = new();
    private static readonly ConcurrentDictionary<DateOnly, byte> BackgroundUpgradeQueued = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly LunarWeatherOptions _options = lunarOptions.Value;
    private readonly string _cacheRoot = hostingPaths.LunarWeatherCacheRoot;

    private LunarWeatherOptions GenerationOptions => adminSettings.ToGenerationOptions();

    public async Task<LunarWeatherBulletinDto> GetBulletinAsync(DateOnly? bulletinDate = null, CancellationToken ct = default)
    {
        var date = bulletinDate ?? UtcGameClock.Today;

        if (!_options.Enabled)
        {
            return BuildFreshBulletin(date);
        }

        var cached = TryLoadBulletin(date);
        if (cached is not null)
        {
            return cached;
        }

        await EnsureBulletinAsync(date, forceRegenerate: false, ct);

        return TryLoadBulletin(date) ?? BuildFreshBulletin(date);
    }

    public LunarWeatherArchivesDto ListArchives()
    {
        if (!_options.Enabled || LunarWeatherStoragePaths.CountEditionFiles(_cacheRoot) == 0)
        {
            return new LunarWeatherArchivesDto([]);
        }

        var entries = new List<LunarWeatherArchiveEntryDto>();
        foreach (var path in LunarWeatherStoragePaths.EnumerateEditionFiles(_cacheRoot))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!DateOnly.TryParse(fileName, out var date))
            {
                continue;
            }

            var sample = (string?)null;
            var operational = 0;
            var outage = 0;
            try
            {
                var bulletin = JsonSerializer.Deserialize<LunarWeatherBulletinDto>(File.ReadAllText(path), JsonOptions);
                if (bulletin is not null)
                {
                    operational = bulletin.OperationalCount;
                    outage = bulletin.OutageCount;
                    sample = bulletin.Readings.FirstOrDefault()?.Summary;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read Lunar Weather archive index for {Date}", date);
            }

            entries.Add(new LunarWeatherArchiveEntryDto(date, operational, outage, sample));
        }

        entries.Sort((left, right) => right.BulletinDate.CompareTo(left.BulletinDate));
        return new LunarWeatherArchivesDto(entries);
    }

    public async Task EnsureBulletinAsync(
        DateOnly date,
        bool forceRegenerate,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        LunarWeatherStoragePaths.EnsureEditionDirectory(_cacheRoot);

        if (!forceRegenerate && TryLoadBulletin(date) is not null)
        {
            var existing = TryLoadBulletin(date)!;
            if (!string.Equals(existing.Source, "openai", StringComparison.OrdinalIgnoreCase)
                && openAi.IsApiKeyConfigured)
            {
                QueueBackgroundAiUpgrade(date);
            }

            return;
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!forceRegenerate && TryLoadBulletin(date) is not null)
            {
                return;
            }

            if (forceRegenerate)
            {
                BackgroundUpgradeQueued.TryRemove(date, out _);
            }

            var template = BuildFreshBulletin(date);
            TrySaveBulletin(template);
            logger.LogInformation(
                "Lunar Weather template bulletin ready for {Date} (force={Force})",
                date,
                forceRegenerate);

            if (openAi.IsApiKeyConfigured)
            {
                QueueBackgroundAiUpgrade(date);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private void QueueBackgroundAiUpgrade(DateOnly date)
    {
        if (!openAi.IsApiKeyConfigured)
        {
            return;
        }

        if (!BackgroundUpgradeQueued.TryAdd(date, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync();
                try
                {
                    var cached = TryLoadBulletin(date);
                    if (string.Equals(cached?.Source, "openai", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    logger.LogInformation("Generating Lunar Weather AI bulletin for {Date} in background", date);
                    await GenerateAndStoreBulletinAsync(date, CancellationToken.None);
                }
                finally
                {
                    gate.Release();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background Lunar Weather AI upgrade failed for {Date}", date);
            }
            finally
            {
                BackgroundUpgradeQueued.TryRemove(date, out _);
            }
        });
    }

    private async Task<LunarWeatherBulletinDto> GenerateAndStoreBulletinAsync(DateOnly date, CancellationToken ct)
    {
        var (operational, outage, targetCount) = SelectRelaySets(date);
        LunarWeatherBulletinDto bulletin;

        if (!openAi.IsApiKeyConfigured)
        {
            logger.LogInformation("Lunar Weather API key not configured; using template for {Date}", date);
            bulletin = LunarWeatherTemplateGenerator.Generate(
                date,
                GenerationOptions,
                operational,
                outage,
                targetCount);
        }
        else
        {
            var generator = new OpenAiLunarWeatherGenerator(
                openAi,
                httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
                logger);
            var readings = await generator.GenerateReadingsAsync(date, operational, ct);
            var generation = GenerationOptions;
            bulletin = new LunarWeatherBulletinDto(
                date,
                DateTime.UtcNow,
                "openai",
                generation.RelayPoolSize,
                targetCount,
                readings.Count,
                outage.Count,
                readings,
                LunarWeatherTemplateGenerator.BuildOutages(date, outage));
        }

        TrySaveBulletin(bulletin);
        return bulletin;
    }

    private LunarWeatherBulletinDto BuildFreshBulletin(DateOnly date)
    {
        var (operational, outage, targetCount) = SelectRelaySets(date);
        return LunarWeatherTemplateGenerator.Generate(date, GenerationOptions, operational, outage, targetCount);
    }

    private (IReadOnlyList<LunarWeatherRelayProfile> Operational, IReadOnlyList<LunarWeatherRelayProfile> Outage, int TargetCount)
        SelectRelaySets(DateOnly date)
    {
        var generation = GenerationOptions;
        var pool = LunarWeatherRelayCatalog.All.Take(generation.RelayPoolSize).ToList();
        var targetCount = LunarWeatherRelaySelector.ResolveOperationalCount(date, generation);
        var operational = LunarWeatherRelaySelector.SelectOperationalRelays(date, pool, targetCount);
        var outage = LunarWeatherRelaySelector.SelectOutageRelays(pool, operational);
        return (operational, outage, targetCount);
    }

    private LunarWeatherBulletinDto? TryLoadBulletin(DateOnly date)
    {
        var path = LunarWeatherStoragePaths.ResolveEditionFilePath(_cacheRoot, date);
        if (path is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LunarWeatherBulletinDto>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Lunar Weather bulletin cache for {Date}", date);
            return null;
        }
    }

    private void TrySaveBulletin(LunarWeatherBulletinDto bulletin)
    {
        try
        {
            LunarWeatherStoragePaths.EnsureEditionDirectory(_cacheRoot);
            var path = LunarWeatherStoragePaths.EditionFilePath(_cacheRoot, bulletin.BulletinDate);
            File.WriteAllText(path, JsonSerializer.Serialize(bulletin, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to write Lunar Weather bulletin cache for {Date} under {Root}",
                bulletin.BulletinDate,
                _cacheRoot);
        }
    }
}
