namespace Rava.Core.Dtos;

public record LunarWeatherRelayDto(
    string Id,
    string Slug,
    string Name,
    string Region,
    string Sector,
    string BodyType);

public record LunarWeatherReadingDto(
    string RelayId,
    string RelaySlug,
    string RelayName,
    string Region,
    string Sector,
    string Summary,
    string AlertLevel,
    IReadOnlyList<string> Conditions,
    string? ParticleFlux,
    string? RadiationIndex,
    string? Visibility,
    string? PressureNote,
    DateTime ObservedAt);

public record LunarWeatherOutageDto(
    string RelayId,
    string RelaySlug,
    string RelayName,
    string Region,
    string Issue,
    string? Detail);

public record LunarWeatherBulletinDto(
    DateOnly BulletinDate,
    DateTime GeneratedAt,
    string Source,
    int RelayPoolSize,
    int TargetOperationalCount,
    int OperationalCount,
    int OutageCount,
    IReadOnlyList<LunarWeatherReadingDto> Readings,
    IReadOnlyList<LunarWeatherOutageDto> Outages);

public record LunarWeatherArchiveEntryDto(
    DateOnly BulletinDate,
    int OperationalCount,
    int OutageCount,
    string? SampleHeadline);

public record LunarWeatherArchivesDto(
    IReadOnlyList<LunarWeatherArchiveEntryDto> Bulletins);

public record AdminLunarWeatherSettingsDto(
    int RelayPoolSize,
    int TotalRelaysInCatalog,
    int TargetOperationalCount,
    int OperationalVariance,
    int MinOperationalCount,
    int MaxOperationalCount);

public record AdminUpdateLunarWeatherSettingsRequest(
    int RelayPoolSize,
    int TargetOperationalCount,
    int OperationalVariance,
    int MinOperationalCount,
    int MaxOperationalCount);

public record AdminLunarWeatherRegenerateResponse(
    string Message,
    DateOnly BulletinDate,
    string Source,
    int OperationalCount,
    int OutageCount);
