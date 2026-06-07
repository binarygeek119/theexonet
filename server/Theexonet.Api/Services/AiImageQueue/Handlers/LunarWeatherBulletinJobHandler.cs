using System.Text.Json;
using Theexonet.Api.Services.LunarWeather;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class LunarWeatherBulletinJobHandler(LunarWeatherService lunarWeatherService) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.LunarWeatherBulletin;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null
            ? "Lunar Weather bulletin"
            : $"Lunar Weather bulletin ({payload.BulletinDate})";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null || !DateOnly.TryParse(payload.BulletinDate, out var bulletinDate))
        {
            return (false, "Invalid Lunar Weather bulletin job payload.");
        }

        return await lunarWeatherService.ProcessBulletinJobAsync(
            bulletinDate,
            payload.ForceRegenerate,
            ct);
    }

    private static LunarWeatherBulletinJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<LunarWeatherBulletinJobPayload>(payloadJson, SerializerOptions);
}
