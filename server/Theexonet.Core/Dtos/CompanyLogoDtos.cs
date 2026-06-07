namespace Theexonet.Core.Dtos;

public record CompanyLogoGenerationStatusDto(
    string Status,
    string Message,
    DateTime? RequestedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record CompanyLogoGenerationActionResponse(
    string Status,
    string Message,
    CompanyLogoGenerationStatusDto? Generation = null);
