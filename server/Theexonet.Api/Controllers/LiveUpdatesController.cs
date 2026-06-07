using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Api.Services;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/live")]
public class LiveUpdatesController(
    ILiveUpdateBroadcaster broadcaster,
    ClientBuildInfo clientBuildInfo) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    [Authorize]
    [HttpGet("events")]
    public async Task Events(CancellationToken cancellationToken)
    {
        var playerId = User.GetPlayerId();

        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.ContentType = "text/event-stream";

        await WriteEventAsync(
            new LiveUpdateEventDto(LiveUpdateTypes.Hello, HtmlBuild: clientBuildInfo.HtmlBuild),
            cancellationToken);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = RunHeartbeatAsync(heartbeatCts.Token);

        try
        {
            await foreach (var evt in broadcaster.SubscribeAsync(playerId, cancellationToken))
            {
                await WriteEventAsync(evt, cancellationToken);
            }
        }
        finally
        {
            await heartbeatCts.CancelAsync();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when the SSE connection closes.
            }
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await WriteEventAsync(
                new LiveUpdateEventDto(LiveUpdateTypes.Ping, HtmlBuild: clientBuildInfo.HtmlBuild),
                cancellationToken);
        }
    }

    private async Task WriteEventAsync(LiveUpdateEventDto evt, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(evt, JsonOptions);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
