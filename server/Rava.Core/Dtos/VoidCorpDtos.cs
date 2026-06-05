namespace Rava.Core.Dtos;

public record VoidCorpProductDto(
    string Slug,
    string DisplayName,
    string Category,
    string Tagline,
    string Summary,
    string Description,
    decimal BasePrice,
    string Color,
    string? UiSymbol,
    string? ImageUrl,
    string Source);

public record VoidCorpCatalogDto(
    DateTime UpdatedAtUtc,
    IReadOnlyList<VoidCorpProductDto> Products);
