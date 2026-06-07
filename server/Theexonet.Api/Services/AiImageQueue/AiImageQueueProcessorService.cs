using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.AiImageQueue;

/// <summary>
/// Processes the master AI image queue sequentially.
/// </summary>
public sealed class AiImageQueueProcessorService(
    IServiceScopeFactory scopeFactory,
    IOptions<AiImageQueueOptions> options,
    ILogger<AiImageQueueProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("AI generation queue processor disabled.");
            return;
        }

        var idleDelay = TimeSpan.FromSeconds(5);
        var gap = TimeSpan.FromSeconds(Math.Max(1, options.Value.SecondsBetweenJobs));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();
                var processed = await queue.ProcessNextAsync(stoppingToken);
                if (processed)
                {
                    await Task.Delay(gap, stoppingToken);
                }
                else
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI generation queue processor error.");
                await Task.Delay(idleDelay, stoppingToken);
            }
        }
    }
}
