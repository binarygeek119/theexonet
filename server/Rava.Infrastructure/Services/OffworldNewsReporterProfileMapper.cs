using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Infrastructure.Services;

public static class OffworldNewsReporterProfileMapper
{
    public static PlayerProfileResponse ToPlayerProfile(
        OffworldNewsReporterProfile reporter,
        string friendshipStatus,
        Guid? friendshipId)
    {
        var interests = reporter.Specialties.Count > 0
            ? string.Join(", ", reporter.Specialties)
            : reporter.Beat;

        return new PlayerProfileResponse(
            Guid.Empty,
            OffworldNewsReporterSocial.UsernameFor(reporter),
            OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug),
            OffworldNewsReporterPaths.AvatarUrl(reporter.Slug),
            OffworldNewsReporterPaths.BackgroundUrl(reporter.Slug),
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

    public static PublicProfileSummaryDto ToPublicSummary(OffworldNewsReporterProfile reporter, int rank = 0) =>
        new(
            OffworldNewsReporterSocial.UsernameFor(reporter),
            OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug),
            $"{reporter.Title} · {reporter.Bureau}",
            reporter.Personality,
            OffworldNewsReporterPaths.AvatarUrl(reporter.Slug),
            1,
            0,
            0,
            0,
            rank,
            IsReporter: true,
            ReporterSlug: reporter.Slug);

    public static PublicProfileDetailDto ToPublicDetail(OffworldNewsReporterProfile reporter)
    {
        var interests = reporter.Specialties.Count > 0
            ? string.Join(", ", reporter.Specialties)
            : reporter.Beat;

        return new PublicProfileDetailDto(
            reporter.DisplayName,
            OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug),
            $"{reporter.Title} · {reporter.Bureau}",
            OffworldNewsReporterPaths.AvatarUrl(reporter.Slug),
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
}
