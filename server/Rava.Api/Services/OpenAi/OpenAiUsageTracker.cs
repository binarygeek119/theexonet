using System.Text.Json;
using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Api.Services.OpenAi;

public sealed class OpenAiUsageTracker(IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private UsageState _state = new();

    public string FilePath =>
        RavaDataPaths.ResolveFile(environment.ContentRootPath, "openai-usage.json");

    public void Load()
    {
        lock (_gate)
        {
            _state = ReadFromDisk();
            ResetTodayIfNeededLocked();
        }
    }

    public void RecordRequest(string? category)
    {
        var bucket = NormalizeCategory(category);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        lock (_gate)
        {
            ResetTodayIfNeededLocked(today);
            _state.TotalRequests++;
            _state.RequestsToday++;
            _state.TodayUtc = today;
            _state.LastRequestUtc = now;
            if (!_state.ByCategory.TryGetValue(bucket, out var count))
            {
                count = 0;
            }

            _state.ByCategory[bucket] = count + 1;
            WriteToDiskLocked();
        }
    }

    public OpenAiUsageSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            ResetTodayIfNeededLocked();
            return new OpenAiUsageSnapshot(
                _state.TotalRequests,
                _state.RequestsToday,
                new Dictionary<string, long>(_state.ByCategory),
                _state.LastRequestUtc);
        }
    }

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return OpenAiUsageCategories.Other;
        }

        return category.Trim().ToLowerInvariant();
    }

    private void ResetTodayIfNeededLocked(DateOnly? utcToday = null)
    {
        var today = utcToday ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (_state.TodayUtc == today)
        {
            return;
        }

        _state.TodayUtc = today;
        _state.RequestsToday = 0;
    }

    private UsageState ReadFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new UsageState();
            }

            var json = File.ReadAllText(FilePath);
            var record = JsonSerializer.Deserialize<UsageRecord>(json, JsonOptions);
            if (record is null)
            {
                return new UsageState();
            }

            return new UsageState
            {
                TotalRequests = Math.Max(0, record.TotalRequests),
                RequestsToday = Math.Max(0, record.RequestsToday),
                TodayUtc = DateOnly.TryParse(record.TodayUtc, out var day)
                    ? day
                    : DateOnly.FromDateTime(DateTime.UtcNow),
                LastRequestUtc = record.LastRequestUtc,
                ByCategory = record.ByCategory ?? new Dictionary<string, long>(),
            };
        }
        catch
        {
            return new UsageState();
        }
    }

    private void WriteToDiskLocked()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var record = new UsageRecord
        {
            TotalRequests = _state.TotalRequests,
            RequestsToday = _state.RequestsToday,
            TodayUtc = _state.TodayUtc.ToString("yyyy-MM-dd"),
            LastRequestUtc = _state.LastRequestUtc,
            ByCategory = new Dictionary<string, long>(_state.ByCategory),
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine);
    }

    private sealed class UsageState
    {
        public long TotalRequests { get; set; }
        public long RequestsToday { get; set; }
        public DateOnly TodayUtc { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateTime? LastRequestUtc { get; set; }
        public Dictionary<string, long> ByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class UsageRecord
    {
        public long TotalRequests { get; set; }
        public long RequestsToday { get; set; }
        public string TodayUtc { get; set; } = "";
        public DateTime? LastRequestUtc { get; set; }
        public Dictionary<string, long>? ByCategory { get; set; }
    }
}

public sealed record OpenAiUsageSnapshot(
    long TotalRequests,
    long RequestsToday,
    IReadOnlyDictionary<string, long> RequestsByCategory,
    DateTime? LastRequestUtc);
