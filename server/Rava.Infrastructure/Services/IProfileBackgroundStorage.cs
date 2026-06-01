namespace Rava.Infrastructure.Services;

public interface IProfileBackgroundStorage
{
    Task<string> SaveAsync(Guid playerId, Stream content, string contentType, CancellationToken cancellationToken);

    Task DeleteForPlayerAsync(Guid playerId, CancellationToken cancellationToken);
}
