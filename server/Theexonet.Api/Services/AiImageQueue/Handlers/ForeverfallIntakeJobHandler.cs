using System.Text.Json;
using Theexonet.Api.Services.Foreverfall;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class ForeverfallIntakeJobHandler(ForeverfallPenitentiaryService penitentiaryService) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.ForeverfallIntake;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null
            ? "Foreverfall intake"
            : $"Foreverfall intake ({payload.IntakeDate})";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null || !DateOnly.TryParse(payload.IntakeDate, out var intakeDate))
        {
            return (false, "Invalid Foreverfall intake job payload.");
        }

        return await penitentiaryService.ProcessIntakeJobAsync(intakeDate, payload.ForceRegenerate, ct);
    }

    private static ForeverfallIntakeJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<ForeverfallIntakeJobPayload>(payloadJson, SerializerOptions);
}
