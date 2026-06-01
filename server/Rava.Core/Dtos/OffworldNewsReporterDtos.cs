namespace Rava.Core.Dtos;

public record OffworldNewsReporterDto(
    string Slug,
    string DisplayName,
    string Handle,
    string Title,
    string Beat,
    string Bureau,
    string Personality,
    string DirectoryBio,
    string OnnBio,
    string DirectoryTeaser,
    IReadOnlyList<string> Specialties,
    string AvatarUrl,
    string BackgroundUrl,
    string DirectoryProfilePath,
    string OnnProfilePath,
    string Network = "Offworld News Network");

public record OffworldNewsReporterStoryRefDto(
    DateOnly EditionDate,
    string StoryId,
    string Headline,
    string Category,
    DateTime PublishedAt,
    bool IsArchive);

public record OffworldNewsReportersDto(
    IReadOnlyList<OffworldNewsReporterDto> Reporters);

public record OffworldNewsReporterDetailDto(
    OffworldNewsReporterDto Reporter,
    IReadOnlyList<OffworldNewsReporterStoryRefDto> RecentStories);

public record AdminOffworldNewsReporterPortraitsResponse(
    string Message,
    int ReporterCount,
    int ImageAttempts,
    int ImagesSaved,
    string? ImageGenerationError = null);
