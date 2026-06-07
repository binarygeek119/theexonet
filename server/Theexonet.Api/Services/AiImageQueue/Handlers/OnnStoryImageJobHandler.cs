using System.Text.Json;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class OnnStoryImageJobHandler(OffworldNewsService offworldNewsService) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.OnnStoryImage;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null
            ? "ONN story image"
            : $"ONN story image {payload.StoryId} ({payload.EditionDate})";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null
            || !DateOnly.TryParse(payload.EditionDate, out var editionDate)
            || string.IsNullOrWhiteSpace(payload.StoryId))
        {
            return (false, "Invalid ONN story image job payload.");
        }

        return await offworldNewsService.ApplyQueuedStoryImageAsync(
            editionDate,
            payload.StoryId,
            payload.StoryIndex,
            payload.ImagePrompt,
            ct);
    }

    private static OnnStoryImageJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<OnnStoryImageJobPayload>(payloadJson, SerializerOptions);
}
