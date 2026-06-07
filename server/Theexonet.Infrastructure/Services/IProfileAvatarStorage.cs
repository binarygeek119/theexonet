namespace Theexonet.Infrastructure.Services;

public interface IProfileAvatarStorage
{
    Task<string> SaveAsync(Guid playerId, Stream content, string contentType, CancellationToken cancellationToken);

    Task DeleteForPlayerAsync(Guid playerId, CancellationToken cancellationToken);
}
