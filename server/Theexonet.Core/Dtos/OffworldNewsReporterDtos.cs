namespace Theexonet.Core.Dtos;

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
    string ReportedLocationsNote,
    IReadOnlyList<string> NotableLocations,
    IReadOnlyList<string> NotableStories,
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
    string Gender,
    string NotableLocations,
    string NotableStories,
    string Hair,
    string Eyes,
    string Race,
    string Build,
    string FacialHair,
    string Makeup,
    string DistinctiveFeatures,
    string Species,
    bool InStoryPool,
    string AvatarUrl,
    string BackgroundUrl);

public record AdminOffworldNewsSettingsDto(
    int ReporterPoolSize,
    int TotalReporters,
    int ActivePoolCount,
    int StoriesPerDay,
    int StoriesPerDayVariance,
    int MinStoriesPerDay,
    int MaxStoriesPerDay);

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
    string Specialties,
    string Gender,
    string NotableLocations,
    string NotableStories,
    string Hair,
    string Eyes,
    string Race,
    string Build,
    string FacialHair,
    string Makeup,
    string DistinctiveFeatures,
    string Species);

public record AdminCreateOffworldNewsReporterRequest(
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
    string Specialties,
    string Gender,
    string NotableLocations,
    string NotableStories,
    string Hair,
    string Eyes,
    string Race,
    string Build,
    string FacialHair,
    string Makeup,
    string DistinctiveFeatures,
    string Species);

public record AdminUpdateOffworldNewsSettingsRequest(
    int ReporterPoolSize,
    int StoriesPerDay,
    int StoriesPerDayVariance,
    int MinStoriesPerDay,
    int MaxStoriesPerDay);

public record AdminOffworldNewsReporterPortraitJobDto(
    string Status,
    string? Message,
    int ReporterCount,
    int ImageAttempts,
    int ImagesSaved,
    string? ImageGenerationError,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    int QueuedJobs = 0);
