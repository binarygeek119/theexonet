namespace Rava.Core.Interfaces;

public interface ICompanyLogoGenerator
{
    Task<(byte[]? PngBytes, string? Error)> GenerateAsync(
        string companyName,
        string username,
        string mood,
        string aboutMe,
        string interests,
        string music,
        CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}
