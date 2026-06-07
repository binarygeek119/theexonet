namespace Theexonet.Core.Dtos;

/// <summary>Player mine names surfaced in daily Offworld News editions.</summary>
public record OffworldNewsCompanyContext(
    IReadOnlyList<string> RisingCompanies,
    IReadOnlyList<string> StrugglingCompanies);
