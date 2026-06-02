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
            MigrateLegacyCountersLocked();
            ResetTodayIfNeededLocked();
        }
    }

    public void RecordOutcome(string? category, bool success)
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

            if (success)
            {
                _state.SuccessfulRequests++;
                _state.SuccessfulRequestsToday++;
            }
            else
            {
                _state.FailedRequests++;
                _state.FailedRequestsToday++;
            }

            Increment(_state.ByCategory, bucket);
            if (success)
            {
                Increment(_state.SuccessfulByCategory, bucket);
            }
            else
            {
                Increment(_state.FailedByCategory, bucket);
            }

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
                _state.SuccessfulRequests,
                _state.FailedRequests,
                _state.RequestsToday,
                _state.SuccessfulRequestsToday,
                _state.FailedRequestsToday,
                new Dictionary<string, long>(_state.ByCategory),
                new Dictionary<string, long>(_state.SuccessfulByCategory),
                new Dictionary<string, long>(_state.FailedByCategory),
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

    private static void Increment(Dictionary<string, long> bucket, string key)
    {
        bucket.TryGetValue(key, out var count);
        bucket[key] = count + 1;
    }

    private void MigrateLegacyCountersLocked()
    {
        if (_state.SuccessfulRequests > 0 || _state.FailedRequests > 0)
        {
            return;
        }

        if (_state.TotalRequests <= 0)
        {
            return;
        }

        _state.SuccessfulRequests = _state.TotalRequests;
        _state.SuccessfulRequestsToday = _state.RequestsToday;
        foreach (var (key, count) in _state.ByCategory)
        {
            _state.SuccessfulByCategory[key] = count;
        }
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
        _state.SuccessfulRequestsToday = 0;
        _state.FailedRequestsToday = 0;
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
                SuccessfulRequests = Math.Max(0, record.SuccessfulRequests),
                FailedRequests = Math.Max(0, record.FailedRequests),
                RequestsToday = Math.Max(0, record.RequestsToday),
                SuccessfulRequestsToday = Math.Max(0, record.SuccessfulRequestsToday),
                FailedRequestsToday = Math.Max(0, record.FailedRequestsToday),
                TodayUtc = DateOnly.TryParse(record.TodayUtc, out var day)
                    ? day
                    : DateOnly.FromDateTime(DateTime.UtcNow),
                LastRequestUtc = record.LastRequestUtc,
                ByCategory = record.ByCategory ?? new Dictionary<string, long>(),
                SuccessfulByCategory = record.SuccessfulByCategory ?? new Dictionary<string, long>(),
                FailedByCategory = record.FailedByCategory ?? new Dictionary<string, long>(),
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
            SuccessfulRequests = _state.SuccessfulRequests,
            FailedRequests = _state.FailedRequests,
            RequestsToday = _state.RequestsToday,
            SuccessfulRequestsToday = _state.SuccessfulRequestsToday,
            FailedRequestsToday = _state.FailedRequestsToday,
            TodayUtc = _state.TodayUtc.ToString("yyyy-MM-dd"),
            LastRequestUtc = _state.LastRequestUtc,
            ByCategory = new Dictionary<string, long>(_state.ByCategory),
            SuccessfulByCategory = new Dictionary<string, long>(_state.SuccessfulByCategory),
            FailedByCategory = new Dictionary<string, long>(_state.FailedByCategory),
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine);
    }

    private sealed class UsageState
    {
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public long RequestsToday { get; set; }
        public long SuccessfulRequestsToday { get; set; }
        public long FailedRequestsToday { get; set; }
        public DateOnly TodayUtc { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateTime? LastRequestUtc { get; set; }
        public Dictionary<string, long> ByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> SuccessfulByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> FailedByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class UsageRecord
    {
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public long RequestsToday { get; set; }
        public long SuccessfulRequestsToday { get; set; }
        public long FailedRequestsToday { get; set; }
        public string TodayUtc { get; set; } = "";
        public DateTime? LastRequestUtc { get; set; }
        public Dictionary<string, long>? ByCategory { get; set; }
        public Dictionary<string, long>? SuccessfulByCategory { get; set; }
        public Dictionary<string, long>? FailedByCategory { get; set; }
    }
}

public sealed record OpenAiUsageSnapshot(
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    long RequestsToday,
    long SuccessfulRequestsToday,
    long FailedRequestsToday,
    IReadOnlyDictionary<string, long> RequestsByCategory,
    IReadOnlyDictionary<string, long> SuccessfulRequestsByCategory,
    IReadOnlyDictionary<string, long> FailedRequestsByCategory,
    DateTime? LastRequestUtc);
