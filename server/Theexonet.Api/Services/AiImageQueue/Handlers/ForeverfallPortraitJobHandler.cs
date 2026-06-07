using System.Text.Json;
using Theexonet.Api.Services.Foreverfall;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class ForeverfallPortraitJobHandler(
    ForeverfallPenitentiaryService penitentiaryService) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.ForeverfallPortrait;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null
            ? "Foreverfall portrait"
            : $"Foreverfall portrait {payload.ImageId} for {payload.DisplayName}";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null || string.IsNullOrWhiteSpace(payload.ImageId))
        {
            return (false, "Invalid Foreverfall portrait job payload.");
        }

        return await penitentiaryService.GenerateAndSavePortraitAsync(
            new ForeverfallPortraitJobItem(
                payload.ImageId,
                payload.DisplayName,
                payload.Species,
                payload.Gender),
            ct);
    }

    private static ForeverfallPortraitJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<ForeverfallPortraitJobPayload>(payloadJson, SerializerOptions);
}
