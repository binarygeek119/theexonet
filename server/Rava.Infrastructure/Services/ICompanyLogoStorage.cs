namespace Rava.Infrastructure.Services;

public interface ICompanyLogoStorage
{
    Task<string> SaveAsync(Guid mineId, Stream content, CancellationToken cancellationToken);

    Task DeleteForMineAsync(Guid mineId, CancellationToken cancellationToken);
}
