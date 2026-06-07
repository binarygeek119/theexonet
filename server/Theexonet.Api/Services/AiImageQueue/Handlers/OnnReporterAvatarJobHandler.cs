using System.Text.Json;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class OnnReporterAvatarJobHandler(
    OffworldNewsReporterPortraitGenerator generator) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.OnnReporterAvatar;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null ? "ONN reporter avatar" : $"ONN avatar for {payload.Slug}";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Slug))
        {
            return (false, "Invalid ONN reporter avatar job payload.");
        }

        return await generator.GenerateAvatarAsync(payload.Slug.Trim(), ct);
    }

    private static OnnReporterPortraitJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<OnnReporterPortraitJobPayload>(payloadJson, SerializerOptions);
}
