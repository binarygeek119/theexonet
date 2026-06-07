using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Core.Tests;

public class AiImageQueueServiceTests
{
    private sealed class StubHandler : IAiImageJobHandler
    {
        public StubHandler(string kind) => Kind = kind;

        public string Kind { get; }

        public string Describe(string payloadJson) => $"stub:{Kind}";

        public Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct) =>
            Task.FromResult<(bool Ok, string? Error)>((true, null));
    }

    private static AiImageQueueService CreateService(AppDbContext db, params string[] kinds)
    {
        var handlers = kinds.Select(kind => new StubHandler(kind)).Cast<IAiImageJobHandler>();
        var options = Options.Create(new AiImageQueueOptions { Enabled = true, SecondsBetweenJobs = 1 });
        return new AiImageQueueService(db, handlers, options, NullLogger<AiImageQueueService>.Instance);
    }

    private static AppDbContext CreateDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnqueueUniqueAsync_skips_duplicate_kind_and_source()
    {
        await using var db = CreateDb(nameof(EnqueueUniqueAsync_skips_duplicate_kind_and_source));
        var service = CreateService(db, AiImageJobKinds.OnnEditionStories);
        var payload = new OnnEditionStoriesJobPayload("2026-06-07");
        const string source = "onn:edition:2026-06-07";

        var first = await service.EnqueueUniqueAsync(
            AiImageJobKinds.OnnEditionStories,
            payload,
            source,
            CancellationToken.None);
        var second = await service.EnqueueUniqueAsync(
            AiImageJobKinds.OnnEditionStories,
            payload,
            source,
            CancellationToken.None);

        Assert.Equal(1, first.EnqueuedCount);
        Assert.Equal(0, second.EnqueuedCount);
        Assert.Equal(1, await db.AiImageQueue.CountAsync());
    }

    [Fact]
    public async Task WaitForCompletionAsync_returns_when_job_completes()
    {
        await using var db = CreateDb(nameof(WaitForCompletionAsync_returns_when_job_completes));
        var service = CreateService(db, AiImageJobKinds.LunarWeatherBulletin);
        const string source = "lunar-weather:bulletin:2026-06-07";
        await service.EnqueueAsync(
            AiImageJobKinds.LunarWeatherBulletin,
            new LunarWeatherBulletinJobPayload("2026-06-07"),
            source,
            CancellationToken.None);

        var job = await db.AiImageQueue.SingleAsync();
        job.Status = AiImageQueueStatuses.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var wait = await service.WaitForCompletionAsync(
            AiImageJobKinds.LunarWeatherBulletin,
            source,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        Assert.True(wait.Completed);
        Assert.False(wait.Failed);
        Assert.Equal(job.Id, wait.JobId);
    }

    [Fact]
    public async Task WaitForCompletionAsync_returns_failed_when_job_fails()
    {
        await using var db = CreateDb(nameof(WaitForCompletionAsync_returns_failed_when_job_fails));
        var service = CreateService(db, AiImageJobKinds.ForeverfallIntake);
        const string source = "foreverfall:intake:2026-06-07";
        await service.EnqueueAsync(
            AiImageJobKinds.ForeverfallIntake,
            new ForeverfallIntakeJobPayload("2026-06-07"),
            source,
            CancellationToken.None);

        var job = await db.AiImageQueue.SingleAsync();
        job.Status = AiImageQueueStatuses.Failed;
        job.Error = "test failure";
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var wait = await service.WaitForCompletionAsync(
            AiImageJobKinds.ForeverfallIntake,
            source,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        Assert.False(wait.Completed);
        Assert.True(wait.Failed);
        Assert.Equal("test failure", wait.Error);
    }
}
