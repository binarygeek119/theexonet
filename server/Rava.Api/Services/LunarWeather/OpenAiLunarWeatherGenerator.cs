using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rava.Api.Services.OpenAi;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Services.LunarWeather;

public sealed class OpenAiLunarWeatherGenerator(
    OpenAiConnectionResolver openAi,
    HttpClient httpClient,
    ILogger logger)
{
    private const int BatchSize = 15;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<LunarWeatherReadingDto>> GenerateReadingsAsync(
        DateOnly bulletinDate,
        IReadOnlyList<LunarWeatherRelayProfile> operationalRelays,
        CancellationToken ct)
    {
        if (operationalRelays.Count == 0)
        {
            return [];
        }

        var observedBase = bulletinDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(5);
        var readings = new List<LunarWeatherReadingDto>();
        var batchIndex = 0;

        foreach (var batch in operationalRelays.Chunk(BatchSize))
        {
            var batchReadings = await GenerateBatchAsync(bulletinDate, batch, observedBase, batchIndex, ct);
            readings.AddRange(batchReadings);
            batchIndex++;
        }

        return readings;
    }

    private async Task<IReadOnlyList<LunarWeatherReadingDto>> GenerateBatchAsync(
        DateOnly bulletinDate,
        IReadOnlyList<LunarWeatherRelayProfile> batch,
        DateTime observedBase,
        int batchIndex,
        CancellationToken ct)
    {
        var relayBlock = string.Join(
            "\n",
            batch.Select(relay =>
                $"- {relay.Id} | {relay.Name} | region={relay.Region} | sector={relay.Sector} | body={relay.BodyType}"));

        var prompt = $$"""
            You are the Lunar Weather Service (LWS) editor for the theexonet sci-fi universe (asteroid mining, belt freight, hard vacuum).
            Generate space-environment forecasts for exactly {{batch.Count}} weather relays listed below.
            Rules:
            - NEVER use rain, snow, humidity, barometric pressure in hPa, wind mph, or Earth weather clichés.
            - Use vacuum/space metrics: micrometeor flux, solar wind, radiation index, plasma sheath, regolith static, magnetopause, coma plumes, flare corridors, dust electrostatics, cryo vents, orbital debris flux.
            - alertLevel must be one of: nominal, caution, advisory, warning, severe
            - conditions: 2-4 short space-weather phrases per relay
            - summary: one sentence operational bulletin for miners and convoy captains
            - pressureNote describes vacuum/exosphere context, not weather on a planet with air unless the body type has a thin exosphere
            - Do not mention AI, ChatGPT, or language models
            - Edition date: {{bulletinDate:yyyy-MM-dd}}

            Relays:
            {{relayBlock}}

            Return JSON only:
            {
              "readings": [
                {
                  "relayId": "LW-001",
                  "summary": "string",
                  "alertLevel": "nominal",
                  "conditions": ["string", "string"],
                  "particleFlux": "string",
                  "radiationIndex": "string",
                  "visibility": "string",
                  "pressureNote": "string"
                }
              ]
            }
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(openAi.BaseUrl, "/chat/completions"));
        OpenAiUsageLoggingHandler.SetCategory(request, OpenAiUsageCategories.LunarWeather);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = openAi.TextModel,
            temperature = 0.85,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "You write believable hard-science space weather bulletins. Output valid JSON only.",
                },
                new { role = "user", content = prompt },
            },
        });

        using var response = await httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OpenAI lunar weather generation failed ({Status}) batch {Batch}: {Body}",
                (int)response.StatusCode,
                batchIndex,
                payload);
            return TemplateFallback(batch, bulletinDate, observedBase, batchIndex);
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(payload, SerializerOptions);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return TemplateFallback(batch, bulletinDate, observedBase, batchIndex);
        }

        var parsed = JsonSerializer.Deserialize<GeneratedReadingsPayload>(content, SerializerOptions);
        if (parsed?.Readings is null || parsed.Readings.Count == 0)
        {
            return TemplateFallback(batch, bulletinDate, observedBase, batchIndex);
        }

        var byId = batch.ToDictionary(relay => relay.Id, StringComparer.OrdinalIgnoreCase);
        var results = new List<LunarWeatherReadingDto>();
        var offset = batchIndex * BatchSize;

        for (var index = 0; index < parsed.Readings.Count; index++)
        {
            var item = parsed.Readings[index];
            if (string.IsNullOrWhiteSpace(item.RelayId) || !byId.TryGetValue(item.RelayId, out var relay))
            {
                continue;
            }

            var conditions = (item.Conditions ?? [])
                .Where(condition => !string.IsNullOrWhiteSpace(condition))
                .Take(6)
                .ToList();
            if (conditions.Count == 0)
            {
                conditions.Add("Telemetry within nominal vacuum envelope");
            }

            results.Add(new LunarWeatherReadingDto(
                relay.Id,
                relay.Slug,
                relay.Name,
                relay.Region,
                relay.Sector,
                item.Summary ?? $"{relay.Name}: Space weather bulletin received.",
                NormalizeAlert(item.AlertLevel),
                conditions,
                item.ParticleFlux,
                item.RadiationIndex,
                item.Visibility,
                item.PressureNote,
                observedBase.AddMinutes((offset + index) * 2.3)));
        }

        if (results.Count < batch.Count)
        {
            var missing = batch.Where(relay => results.All(r => !string.Equals(r.RelayId, relay.Id, StringComparison.Ordinal)));
            results.AddRange(TemplateFallback(missing.ToList(), bulletinDate, observedBase, batchIndex));
        }

        return results.OrderBy(reading => reading.RelayId, StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<LunarWeatherReadingDto> TemplateFallback(
        IReadOnlyList<LunarWeatherRelayProfile> relays,
        DateOnly bulletinDate,
        DateTime observedBase,
        int batchIndex)
    {
        var template = LunarWeatherTemplateGenerator.Generate(
            bulletinDate,
            new LunarWeatherOptions(),
            relays,
            [],
            relays.Count);
        var offset = batchIndex * BatchSize;
        return template.Readings
            .Select((reading, index) => reading with
            {
                ObservedAt = observedBase.AddMinutes((offset + index) * 2.3),
            })
            .ToList();
    }

    private static string NormalizeAlert(string? alertLevel)
    {
        var normalized = (alertLevel ?? "nominal").Trim().ToLowerInvariant();
        return normalized is "nominal" or "caution" or "advisory" or "warning" or "severe"
            ? normalized
            : "caution";
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return $"{trimmed}{path}";
    }

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice>? Choices { get; init; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; init; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; init; }
    }

    private sealed class GeneratedReadingsPayload
    {
        public List<GeneratedReading>? Readings { get; init; }
    }

    private sealed class GeneratedReading
    {
        public string? RelayId { get; init; }
        public string? Summary { get; init; }
        public string? AlertLevel { get; init; }
        public List<string>? Conditions { get; init; }
        public string? ParticleFlux { get; init; }
        public string? RadiationIndex { get; init; }
        public string? Visibility { get; init; }
        public string? PressureNote { get; init; }
    }
}
