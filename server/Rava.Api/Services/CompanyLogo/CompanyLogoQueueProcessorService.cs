using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Infrastructure.Services;

namespace Rava.Api.Services.CompanyLogo;

/// <summary>
/// Processes the company logo generation queue sequentially.
/// </summary>
public sealed class CompanyLogoQueueProcessorService(
    IServiceScopeFactory scopeFactory,
    IOptions<CompanyLogoOptions> options,
    ILogger<CompanyLogoQueueProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(20);
        var gap = TimeSpan.FromSeconds(Math.Max(3, options.Value.SecondsBetweenGenerations));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<CompanyLogoQueueService>();
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
                logger.LogWarning(ex, "Company logo queue processor error.");
                await Task.Delay(idleDelay, stoppingToken);
            }
        }
    }
}
