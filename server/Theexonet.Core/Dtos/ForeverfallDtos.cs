namespace Theexonet.Core.Dtos;

public record ForeverfallInmateDto(
    string Id,
    DateOnly IntakeDate,
    string DisplayName,
    string Species,
    string Gender,
    string Sentence,
    string Crime,
    string IntakeReason,
    string Bio,
    string ImageId,
    string ImageUrl);

public record ForeverfallRosterDto(
    DateOnly IntakeDate,
    DateTime GeneratedAt,
    string Source,
    int IntakeCount,
    int MaleCount,
    int FemaleCount,
    string IntakeOfficer,
    IReadOnlyList<ForeverfallInmateDto> MaleWing,
    IReadOnlyList<ForeverfallInmateDto> FemaleWing);

public record ForeverfallArchiveEntryDto(
    DateOnly IntakeDate,
    int IntakeCount,
    int MaleCount,
    int FemaleCount,
    string? IntakeOfficer);

public record ForeverfallArchivesDto(
    IReadOnlyList<ForeverfallArchiveEntryDto> Rosters);

public record ForeverfallSearchResultDto(
    IReadOnlyList<ForeverfallInmateDto> Inmates,
    int TotalMatches);

public record AdminForeverfallSettingsDto(
    bool Enabled,
    int MaxInmateImages,
    int PortraitPoolCount,
    int RetentionDays,
    int TargetDailyIntake,
    int IntakeVariance,
    int MinDailyIntake,
    int MaxDailyIntake);

public record AdminUpdateForeverfallSettingsRequest(
    bool Enabled,
    int MaxInmateImages,
    int RetentionDays,
    int TargetDailyIntake,
    int IntakeVariance,
    int MinDailyIntake,
    int MaxDailyIntake);

public record AdminForeverfallStatusDto(
    DateOnly Today,
    int TodayIntakeCount,
    int PortraitPoolCount,
    int MaxInmateImages,
    DateOnly? OldestRosterDate,
    int RosterCount);

public record AdminForeverfallRegenerateResponse(
    string Message,
    DateOnly IntakeDate,
    string Source,
    int IntakeCount,
    int MaleCount,
    int FemaleCount,
    int PortraitsQueued = 0);

public record ForeverfallPortraitJobItem(
    string ImageId,
    string DisplayName,
    string Species,
    string Gender);

public record AdminForeverfallPortraitJobDto(
    string Status,
    string? Message,
    int PortraitAttempts,
    int PortraitsSaved,
    string? ImageGenerationError,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    int QueuedJobs = 0);
