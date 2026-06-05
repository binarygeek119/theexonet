namespace Rava.Core.Dtos;

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
    IReadOnlyList<ForeverfallInmateDto> MaleWing,
    IReadOnlyList<ForeverfallInmateDto> FemaleWing);

public record ForeverfallArchiveEntryDto(
    DateOnly IntakeDate,
    int IntakeCount,
    int MaleCount,
    int FemaleCount,
    string? SampleName);

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
    int FemaleCount);
