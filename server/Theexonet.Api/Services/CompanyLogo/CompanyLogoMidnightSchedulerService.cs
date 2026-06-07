using Theexonet.Core.Services;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.CompanyLogo;

/// <summary>
/// At each UTC midnight, queues AI logo generation for active mines missing a logo.
/// </summary>
public sealed class CompanyLogoMidnightSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<CompanyLogoMidnightSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = UtcGameClock.NextDayBoundaryUtc - DateTime.UtcNow + TimeSpan.FromSeconds(5);
            if (delay < TimeSpan.FromSeconds(1))
            {
                delay = TimeSpan.FromSeconds(1);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            logger.LogInformation("UTC midnight reached. Queuing missing company logos for AI generation.");
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<CompanyLogoQueueService>();
                var count = await queue.EnqueueMissingLogosAtMidnightAsync(stoppingToken);
                logger.LogInformation("Midnight company logo queue added {Count} mines.", count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Midnight company logo enqueue failed.");
            }
        }
    }
}
