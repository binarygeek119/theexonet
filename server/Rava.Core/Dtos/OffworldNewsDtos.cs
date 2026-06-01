namespace Rava.Core.Dtos;

public record OffworldNewsStoryDto(
    string Id,
    string Headline,
    string Dek,
    string Body,
    string Category,
    string Location,
    string Author,
    DateTime PublishedAt,
    string? CompanyName,
    string? ImageUrl);

public record OffworldNewsEditionDto(
    DateOnly EditionDate,
    DateTime GeneratedAt,
    string Source,
    IReadOnlyList<OffworldNewsStoryDto> Stories);

public record OffworldNewsArchiveEntryDto(
    DateOnly EditionDate,
    int StoryCount,
    string? Headline);

public record OffworldNewsArchivesDto(
    IReadOnlyList<OffworldNewsArchiveEntryDto> Editions);
