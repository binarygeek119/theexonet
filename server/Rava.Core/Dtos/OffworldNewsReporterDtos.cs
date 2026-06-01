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

public record AdminOffworldNewsReportersPageDto(
    IReadOnlyList<AdminOffworldNewsReporterRowDto> Reporters,
    AdminOffworldNewsSettingsDto Settings,
    string ReportersFilePath);

public record AdminOffworldNewsReporterRowDto(
    string Slug,
    string DisplayName,
    string Title,
    string Beat,
    string Bureau,
    string Personality,
    string WritingVoice,
    string DirectoryBio,
    string OnnBio,
    string StoryKicker,
    IReadOnlyList<string> Specialties,
    bool InStoryPool);

public record AdminOffworldNewsSettingsDto(
    int ReporterPoolSize,
    int TotalReporters,
    int ActivePoolCount);

public record AdminUpdateOffworldNewsReporterRequest(
    string? NewSlug,
    string DisplayName,
    string Title,
    string Beat,
    string Bureau,
    string Personality,
    string WritingVoice,
    string DirectoryBio,
    string OnnBio,
    string StoryKicker,
    string Specialties);

public record AdminUpdateOffworldNewsSettingsRequest(int ReporterPoolSize);

public record AdminOffworldNewsReporterPortraitJobDto(
    string Status,
    string? Message,
    int ReporterCount,
    int ImageAttempts,
    int ImagesSaved,
    string? ImageGenerationError,
    DateTime? StartedUtc,
    DateTime? CompletedUtc);
