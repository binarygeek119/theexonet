namespace Theexonet.Core.Dtos;

public record AdminAiImageQueueStatusDto(
    string Status,
    string? CurrentJobDescription,
    string? CurrentJobKind,
    int QueuedCount,
    int CompletedToday,
    int FailedToday,
    IReadOnlyDictionary<string, int> QueuedByKind);

public record AiImageQueueEnqueueResult(
    int EnqueuedCount,
    string? Message);

public record OnnStoryImageJobPayload(
    string EditionDate,
    string StoryId,
    int StoryIndex,
    string? ImagePrompt = null);

public record OnnReporterPortraitJobPayload(string Slug);

public record ForeverfallPortraitJobPayload(
    string ImageId,
    string DisplayName,
    string Species,
    string Gender);

public record VoidCorpProductJobPayload(string Slug);

public record TestingDummyAssetJobPayload(int ProfileIndex);

public record CompanyLogoJobPayload(
    Guid QueueEntityId,
    Guid MineId);

public record OnnEditionStoriesJobPayload(
    string EditionDate,
    bool ForceRegenerate = false);

public record ForeverfallIntakeJobPayload(
    string IntakeDate,
    bool ForceRegenerate = false);

public record LunarWeatherBulletinJobPayload(
    string BulletinDate,
    bool ForceRegenerate = false);

public record AiGenerationQueueWaitResult(
    bool Completed,
    bool Failed,
    string? Error,
    Guid? JobId);
