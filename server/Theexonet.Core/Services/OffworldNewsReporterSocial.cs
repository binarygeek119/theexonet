using System.Security.Cryptography;
using System.Text;
using Theexonet.Core.Validation;

namespace Theexonet.Core.Services;

/// <summary>Miner-profile identity for ONN correspondents (not database players).</summary>
public static class OffworldNewsReporterSocial
{
    public static string ProfileNumberFor(string slug)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"onn-reporter-profile:{slug}"));
        var digits = (BitConverter.ToUInt32(hash, 0) % 9000) + 1000;
        var suffix = string.Concat(slug.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        if (suffix.Length > 4)
        {
            suffix = suffix[..4];
        }

        suffix = suffix.PadRight(4, 'X');
        return $"!ONN-{digits}-{suffix}";
    }

    public static string UsernameFor(OffworldNewsReporterProfile reporter) => reporter.Slug;

    public static OffworldNewsReporterProfile? TryGetByProfileNumber(string? profileNumberInput)
    {
        var normalized = ProfileNumberNormalizer.Normalize(profileNumberInput);
        if (normalized is null)
        {
            return null;
        }

        return OffworldNewsReporterCatalog.All
            .FirstOrDefault(reporter =>
                string.Equals(ProfileNumberFor(reporter.Slug), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static OffworldNewsReporterProfile? TryGetByUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var key = username.Trim().ToLowerInvariant();
        return OffworldNewsReporterCatalog.All.FirstOrDefault(reporter =>
            string.Equals(reporter.Slug, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(OffworldNewsReporterCatalog.HandleFromSlug(reporter.Slug), key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reporter.DisplayName, username.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<OffworldNewsReporterProfile> Search(string query, int limit)
    {
        limit = Math.Clamp(limit, 1, 50);
        if (string.IsNullOrWhiteSpace(query))
        {
            return OffworldNewsReporterCatalog.All.Take(limit).ToList();
        }

        return OffworldNewsReporterCatalog.Search(query, limit);
    }
}
