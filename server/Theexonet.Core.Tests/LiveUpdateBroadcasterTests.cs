using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Core.Tests;

public class LiveUpdateBroadcasterTests
{
    [Fact]
    public async Task PublishToPlayer_ReachesSubscriber()
    {
        using var broadcaster = new LiveUpdateBroadcaster();
        var playerId = Guid.NewGuid();
        var received = new List<LiveUpdateEventDto>();

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var evt in broadcaster.SubscribeAsync(playerId, CancellationToken.None))
            {
                received.Add(evt);
                if (received.Count >= 1)
                {
                    break;
                }
            }
        });

        await Task.Delay(50);
        broadcaster.PublishToPlayer(
            playerId,
            new LiveUpdateEventDto(LiveUpdateTypes.Refresh, LiveUpdateScopes.Messages));

        await subscribeTask;

        Assert.Single(received);
        Assert.Equal(LiveUpdateTypes.Refresh, received[0].Type);
        Assert.Equal(LiveUpdateScopes.Messages, received[0].Scope);
    }

    [Fact]
    public async Task PublishGlobal_ReachesSubscriber()
    {
        using var broadcaster = new LiveUpdateBroadcaster();
        var playerId = Guid.NewGuid();
        var received = new List<LiveUpdateEventDto>();

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var evt in broadcaster.SubscribeAsync(playerId, CancellationToken.None))
            {
                received.Add(evt);
                if (received.Count >= 1)
                {
                    break;
                }
            }
        });

        await Task.Delay(50);
        broadcaster.PublishGlobal(
            new LiveUpdateEventDto(LiveUpdateTypes.Refresh, LiveUpdateScopes.Market));

        await subscribeTask;

        Assert.Single(received);
        Assert.Equal(LiveUpdateScopes.Market, received[0].Scope);
    }

    [Fact]
    public void PublishToPlayer_DoesNotThrowWhenNobodyListening()
    {
        using var broadcaster = new LiveUpdateBroadcaster();
        var exception = Record.Exception(() =>
            broadcaster.PublishToPlayer(
                Guid.NewGuid(),
                new LiveUpdateEventDto(LiveUpdateTypes.Refresh, LiveUpdateScopes.Mine)));

        Assert.Null(exception);
    }
}
