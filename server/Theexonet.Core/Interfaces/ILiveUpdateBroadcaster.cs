using Theexonet.Core.Dtos;

namespace Theexonet.Core.Interfaces;

public interface ILiveUpdateBroadcaster
{
    IAsyncEnumerable<LiveUpdateEventDto> SubscribeAsync(Guid playerId, CancellationToken cancellationToken);

    void PublishToPlayer(Guid playerId, LiveUpdateEventDto evt);

    void PublishGlobal(LiveUpdateEventDto evt);
}
