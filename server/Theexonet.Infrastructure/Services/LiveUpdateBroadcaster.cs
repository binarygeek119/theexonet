using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Infrastructure.Services;

public sealed class LiveUpdateBroadcaster : ILiveUpdateBroadcaster, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Channel<LiveUpdateEventDto>> _playerChannels = new();
    private readonly Channel<LiveUpdateEventDto> _globalChannel = Channel.CreateUnbounded<LiveUpdateEventDto>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public void PublishToPlayer(Guid playerId, LiveUpdateEventDto evt)
    {
        if (_playerChannels.TryGetValue(playerId, out var channel))
        {
            channel.Writer.TryWrite(evt);
        }
    }

    public void PublishGlobal(LiveUpdateEventDto evt) =>
        _globalChannel.Writer.TryWrite(evt);

    public async IAsyncEnumerable<LiveUpdateEventDto> SubscribeAsync(
        Guid playerId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var playerChannel = _playerChannels.GetOrAdd(
            playerId,
            _ => Channel.CreateUnbounded<LiveUpdateEventDto>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                }));

        await using var registration = cancellationToken.Register(() => playerChannel.Writer.TryComplete());

        try
        {
            await foreach (var evt in MergeAsync(playerChannel.Reader, _globalChannel.Reader, cancellationToken))
            {
                yield return evt;
            }
        }
        finally
        {
            _playerChannels.TryRemove(playerId, out _);
            playerChannel.Writer.TryComplete();
        }
    }

    private static async IAsyncEnumerable<LiveUpdateEventDto> MergeAsync(
        ChannelReader<LiveUpdateEventDto> playerReader,
        ChannelReader<LiveUpdateEventDto> globalReader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pendingPlayer = playerReader.ReadAsync(cancellationToken).AsTask();
        var pendingGlobal = globalReader.ReadAsync(cancellationToken).AsTask();

        while (!cancellationToken.IsCancellationRequested)
        {
            var completed = await Task.WhenAny(pendingPlayer, pendingGlobal);
            if (completed == pendingPlayer)
            {
                yield return await pendingPlayer;
                pendingPlayer = playerReader.ReadAsync(cancellationToken).AsTask();
            }
            else
            {
                yield return await pendingGlobal;
                pendingGlobal = globalReader.ReadAsync(cancellationToken).AsTask();
            }
        }
    }

    public void Dispose()
    {
        _globalChannel.Writer.TryComplete();
        foreach (var (_, channel) in _playerChannels)
        {
            channel.Writer.TryComplete();
        }

        _playerChannels.Clear();
    }
}
