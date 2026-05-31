using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;

namespace Rava.Infrastructure.Services;

public class HateSpeechTermsProvider(
    IWebHostEnvironment environment,
    IOptionsMonitor<HateSpeechOptions> options)
{
    private string[] _cachedTerms = [];
    private int _hateSpeechCount;
    private int _badLanguageCount;
    private int _politicalCount;
    private int _sexualCount;
    private DateTime _cachedHateSpeechWriteUtc = DateTime.MinValue;
    private DateTime _cachedBadLanguageWriteUtc = DateTime.MinValue;
    private DateTime _cachedPoliticalWriteUtc = DateTime.MinValue;
    private DateTime _cachedSexualWriteUtc = DateTime.MinValue;

    public IReadOnlyList<string> GetTerms()
    {
        ReloadIfNeeded();
        if (_cachedTerms.Length > 0)
        {
            return _cachedTerms;
        }

        return options.CurrentValue.Terms ?? [];
    }

    public (int HateSpeechCount, int BadLanguageCount, int PoliticalCount, int SexualCount) GetTermCounts()
    {
        ReloadIfNeeded();
        return (_hateSpeechCount, _badLanguageCount, _politicalCount, _sexualCount);
    }

    private void ReloadIfNeeded()
    {
        var settings = options.CurrentValue;
        var hateSpeechPath = ResolvePath(settings.TermsFile);
        var badLanguagePath = ResolvePath(settings.BadLanguageFile);
        var politicalPath = ResolvePath(settings.PoliticalTermsFile);
        var sexualPath = ResolvePath(settings.SexualTermsFile);

        var hateSpeechWriteUtc = GetWriteUtc(hateSpeechPath);
        var badLanguageWriteUtc = GetWriteUtc(badLanguagePath);
        var politicalWriteUtc = GetWriteUtc(politicalPath);
        var sexualWriteUtc = GetWriteUtc(sexualPath);

        if (hateSpeechWriteUtc == _cachedHateSpeechWriteUtc
            && badLanguageWriteUtc == _cachedBadLanguageWriteUtc
            && politicalWriteUtc == _cachedPoliticalWriteUtc
            && sexualWriteUtc == _cachedSexualWriteUtc)
        {
            return;
        }

        var hateSpeechTerms = LoadTerms(hateSpeechPath);
        var badLanguageTerms = LoadTerms(badLanguagePath);
        var politicalTerms = LoadTerms(politicalPath);
        var sexualTerms = LoadTerms(sexualPath);

        _hateSpeechCount = hateSpeechTerms.Length;
        _badLanguageCount = badLanguageTerms.Length;
        _politicalCount = politicalTerms.Length;
        _sexualCount = sexualTerms.Length;
        _cachedTerms = hateSpeechTerms
            .Concat(badLanguageTerms)
            .Concat(politicalTerms)
            .Concat(sexualTerms)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _cachedHateSpeechWriteUtc = hateSpeechWriteUtc;
        _cachedBadLanguageWriteUtc = badLanguageWriteUtc;
        _cachedPoliticalWriteUtc = politicalWriteUtc;
        _cachedSexualWriteUtc = sexualWriteUtc;
    }

    private static DateTime GetWriteUtc(string path) =>
        File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

    private static string[] LoadTerms(string path) =>
        File.Exists(path) ? HateSpeechTermsCsvLoader.LoadFromFile(path) : [];

    private string ResolvePath(string fileName) =>
        Path.Combine(environment.ContentRootPath, fileName);
}
