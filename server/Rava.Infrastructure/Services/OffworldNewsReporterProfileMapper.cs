using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Infrastructure.Services;

public static class OffworldNewsReporterProfileMapper
{
    public static PlayerProfileResponse ToPlayerProfile(
        OffworldNewsReporterProfile reporter,
        string friendshipStatus,
        Guid? friendshipId,
        RavaHostingPaths? hostingPaths = null) =>
        ToPlayerProfile(reporter, friendshipStatus, friendshipId, hostingPaths?.ReporterAssetRoots() ?? []);

    public static PlayerProfileResponse ToPlayerProfile(
        OffworldNewsReporterProfile reporter,
        string friendshipStatus,
        Guid? friendshipId,
        params string[] reporterAssetRoots)
    {
        var interests = reporter.Specialties.Count > 0
            ? string.Join(", ", reporter.Specialties)
            : reporter.Beat;

        return new PlayerProfileResponse(
            Guid.Empty,
            reporter.DisplayName,
            OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug),
            ResolveAvatarUrl(reporter.Slug, reporterAssetRoots),
            ResolveBackgroundUrl(reporter.Slug, reporterAssetRoots),
            string.Empty,
            reporter.Personality,
            reporter.DirectoryBio,
            "ONN relay desk",
            interests,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            DateTime.UtcNow.AddYears(-2),
            1,
            0,
            $"{reporter.Bureau} · {reporter.Beat}",
            0,
            0,
            false,
            friendshipStatus,
            friendshipId?.ToString() ?? string.Empty,
            ActiveFlag: null,
            Friends: null,
            MineId: null,
            CompanyNameListed: false,
            CompanyNameListingId: null,
            CompanyNameListingPrice: null,
            ReclaimableCompanyNames: null,
            CompanyNameReclaimFee: 0,
            IsReporter: true,
            ReporterSlug: reporter.Slug,
            OnnProfilePath: OffworldNewsReporterCatalog.OnnProfilePath(reporter.Slug));
    }

    public static PublicProfileSummaryDto ToPublicSummary(
        OffworldNewsReporterProfile reporter,
        int rank = 0,
        RavaHostingPaths? hostingPaths = null) =>
        ToPublicSummary(reporter, rank, hostingPaths?.ReporterAssetRoots() ?? []);

    public static PublicProfileSummaryDto ToPublicSummary(
        OffworldNewsReporterProfile reporter,
        int rank,
        params string[] reporterAssetRoots) =>
        new(
            OffworldNewsReporterSocial.UsernameFor(reporter),
            OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug),
            $"{reporter.Title} · {reporter.Bureau}",
            string.Empty,
            reporter.Personality,
            ResolveAvatarUrl(reporter.Slug, reporterAssetRoots),
            1,
            0,
            0,
            0,
            rank,
            IsReporter: true,
            ReporterSlug: reporter.Slug);

    public static PublicProfileDetailDto ToPublicDetail(
        OffworldNewsReporterProfile reporter,
        RavaHostingPaths? hostingPaths = null) =>
        ToPublicDetail(reporter, hostingPaths?.ReporterAssetRoots() ?? []);

    public static PublicProfileDetailDto ToPublicDetail(
        OffworldNewsReporterProfile reporter,
        params string[] reporterAssetRoots)
    {
        var interests = reporter.Specialties.Count > 0
            ? string.Join(", ", reporter.Specialties)
            : reporter.Beat;

        return new PublicProfileDetailDto(
            reporter.DisplayName,
            OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug),
            $"{reporter.Title} · {reporter.Bureau}",
            string.Empty,
            ResolveAvatarUrl(reporter.Slug, reporterAssetRoots),
            reporter.Personality,
            reporter.DirectoryBio,
            interests,
            "ONN relay desk",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            DateTime.UtcNow.AddYears(-2),
            1,
            0,
            0,
            0,
            IsReporter: true,
            ReporterSlug: reporter.Slug,
            OnnProfilePath: OffworldNewsReporterCatalog.OnnProfilePath(reporter.Slug));
    }

    private static string ResolveAvatarUrl(string slug, string[] reporterAssetRoots) =>
        reporterAssetRoots.Length > 0
            ? OffworldNewsReporterPaths.ResolveAvatarUrl(slug, reporterAssetRoots)
            : OffworldNewsReporterPaths.AvatarUrl(slug);

    private static string ResolveBackgroundUrl(string slug, string[] reporterAssetRoots) =>
        reporterAssetRoots.Length > 0
            ? OffworldNewsReporterPaths.ResolveBackgroundUrl(slug, reporterAssetRoots)
            : OffworldNewsReporterPaths.BackgroundUrl(slug);
}
