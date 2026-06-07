using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Infrastructure.Services;

public static class LiveUpdatePublisher
{
    public static void NotifyPlayerRefresh(ILiveUpdateBroadcaster broadcaster, Guid playerId, string scope) =>
        broadcaster.PublishToPlayer(
            playerId,
            new LiveUpdateEventDto(LiveUpdateTypes.Refresh, scope));

    public static void NotifyGlobalRefresh(ILiveUpdateBroadcaster broadcaster, string scope) =>
        broadcaster.PublishGlobal(new LiveUpdateEventDto(LiveUpdateTypes.Refresh, scope));

    public static void NotifySessionEnd(ILiveUpdateBroadcaster broadcaster, Guid playerId) =>
        broadcaster.PublishToPlayer(playerId, new LiveUpdateEventDto(LiveUpdateTypes.SessionEnd));
}
