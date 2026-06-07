using System.Text.Json;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class OnnEditionStoriesJobHandler(OffworldNewsService offworldNewsService) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.OnnEditionStories;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null
            ? "ONN edition stories"
            : $"ONN edition stories ({payload.EditionDate})";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null || !DateOnly.TryParse(payload.EditionDate, out var editionDate))
        {
            return (false, "Invalid ONN edition stories job payload.");
        }

        return await offworldNewsService.ProcessEditionStoriesJobAsync(
            editionDate,
            payload.ForceRegenerate,
            ct);
    }

    private static OnnEditionStoriesJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<OnnEditionStoriesJobPayload>(payloadJson, SerializerOptions);
}
