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
        var received = await CollectFirstEventAsync(
            broadcaster,
            playerId,
            () => broadcaster.PublishToPlayer(
                playerId,
                new LiveUpdateEventDto(LiveUpdateTypes.Refresh, LiveUpdateScopes.Messages)));

        Assert.Equal(LiveUpdateTypes.Refresh, received.Type);
        Assert.Equal(LiveUpdateScopes.Messages, received.Scope);
    }

    [Fact]
    public async Task PublishGlobal_ReachesSubscriber()
    {
        using var broadcaster = new LiveUpdateBroadcaster();
        var playerId = Guid.NewGuid();
        var received = await CollectFirstEventAsync(
            broadcaster,
            playerId,
            () => broadcaster.PublishGlobal(
                new LiveUpdateEventDto(LiveUpdateTypes.Refresh, LiveUpdateScopes.Market)));

        Assert.Equal(LiveUpdateScopes.Market, received.Scope);
    }

    private static async Task<LiveUpdateEventDto> CollectFirstEventAsync(
        LiveUpdateBroadcaster broadcaster,
        Guid playerId,
        Action publish)
    {
        var received = new List<LiveUpdateEventDto>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in broadcaster.SubscribeAsync(playerId, cts.Token))
                {
                    received.Add(evt);
                    return;
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }, cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (received.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
            publish();
            await Task.Delay(15, cts.Token);
        }

        await subscribeTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Single(received);
        return received[0];
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
