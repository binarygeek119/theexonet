namespace Rava.Core.Services;

public sealed record VoidCorpCatalogDocument(
    DateTime UpdatedAtUtc,
    IReadOnlyList<VoidCorpCatalogEntryDocument> Products);

public sealed record VoidCorpCatalogEntryDocument(
    string Slug,
    string DisplayName,
    string Category,
    string Tagline,
    string Summary,
    string Description,
    decimal BasePrice,
    string Color,
    string? UiSymbol,
    string? ImageFileName,
    string Source);

public sealed record VoidCorpCatalogSyncResult(
    int Added,
    int Updated,
    int Unchanged,
    int MissingImages);
