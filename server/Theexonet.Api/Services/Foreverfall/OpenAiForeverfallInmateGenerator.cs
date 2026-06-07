using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Theexonet.Api.Services.OpenAi;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.Foreverfall;

public sealed class OpenAiForeverfallInmateGenerator(
    OpenAiConnectionResolver openAi,
    HttpClient httpClient,
    ILogger logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<GeneratedForeverfallInmate>> GenerateInmatesAsync(
        DateOnly intakeDate,
        int count,
        int maleCount,
        int femaleCount,
        CancellationToken ct)
    {
        if (count <= 0)
        {
            return [];
        }

        if (!openAi.IsApiKeyConfigured)
        {
            return ForeverfallInmateTemplateGenerator.Generate(intakeDate, count, maleCount, femaleCount);
        }

        var prompt = ForeverfallInmatePrompts.BuildBatchPrompt(intakeDate, count, maleCount, femaleCount);

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(openAi.BaseUrl, "/chat/completions"));
        OpenAiUsageLoggingHandler.SetCategory(request, OpenAiUsageCategories.ForeverfallInmate);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = openAi.TextModel,
            temperature = 0.9,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = ForeverfallInmatePrompts.SystemPrompt },
                new { role = "user", content = prompt },
            },
        });

        using var response = await httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OpenAI Foreverfall inmate generation failed ({Status}): {Body}",
                (int)response.StatusCode,
                payload);
            return ForeverfallInmateTemplateGenerator.Generate(intakeDate, count, maleCount, femaleCount);
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(payload, SerializerOptions);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return ForeverfallInmateTemplateGenerator.Generate(intakeDate, count, maleCount, femaleCount);
        }

        var parsed = JsonSerializer.Deserialize<GeneratedInmatesPayload>(content, SerializerOptions);
        if (parsed?.Inmates is null || parsed.Inmates.Count == 0)
        {
            return ForeverfallInmateTemplateGenerator.Generate(intakeDate, count, maleCount, femaleCount);
        }

        var results = parsed.Inmates
            .Where(inmate => !string.IsNullOrWhiteSpace(inmate.DisplayName))
            .Select(inmate => new GeneratedForeverfallInmate(
                inmate.DisplayName!.Trim(),
                string.IsNullOrWhiteSpace(inmate.Species) ? "Human" : inmate.Species.Trim(),
                NormalizeGender(inmate.Gender),
                string.IsNullOrWhiteSpace(inmate.Crime) ? "Unspecified galactic felony" : inmate.Crime.Trim(),
                string.IsNullOrWhiteSpace(inmate.Sentence)
                    ? "Galactic lifetime at Foreverfall Penitentiary."
                    : inmate.Sentence.Trim(),
                string.IsNullOrWhiteSpace(inmate.IntakeReason)
                    ? $"Processed on intake date {intakeDate:yyyy-MM-dd}."
                    : inmate.IntakeReason.Trim(),
                string.IsNullOrWhiteSpace(inmate.Bio)
                    ? "No supplemental dossier on file."
                    : inmate.Bio.Trim()))
            .Take(count)
            .ToList();

        if (results.Count < count)
        {
            var fallback = ForeverfallInmateTemplateGenerator.Generate(
                intakeDate,
                count - results.Count,
                Math.Max(0, maleCount - results.Count(g => g.Gender == "male")),
                Math.Max(0, femaleCount - results.Count(g => g.Gender == "female")));
            results.AddRange(fallback);
        }

        return BalanceGender(results, maleCount, femaleCount);
    }

    private static IReadOnlyList<GeneratedForeverfallInmate> BalanceGender(
        List<GeneratedForeverfallInmate> inmates,
        int maleCount,
        int femaleCount)
    {
        var males = inmates.Where(i => i.Gender == "male").Take(maleCount).ToList();
        var females = inmates.Where(i => i.Gender == "female").Take(femaleCount).ToList();
        var combined = males.Concat(females).ToList();

        while (combined.Count < maleCount + femaleCount && combined.Count < inmates.Count)
        {
            var next = inmates[combined.Count];
            combined.Add(next with { Gender = males.Count < maleCount ? "male" : "female" });
            if (combined[^1].Gender == "male")
            {
                males.Add(combined[^1]);
            }
            else
            {
                females.Add(combined[^1]);
            }
        }

        return combined;
    }

    private static string NormalizeGender(string? gender)
    {
        var normalized = (gender ?? "male").Trim().ToLowerInvariant();
        return normalized is "female" or "f" ? "female" : "male";
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

    private sealed class GeneratedInmatesPayload
    {
        public List<GeneratedInmate>? Inmates { get; init; }
    }

    private sealed class GeneratedInmate
    {
        public string? DisplayName { get; init; }
        public string? Species { get; init; }
        public string? Gender { get; init; }
        public string? Crime { get; init; }
        public string? Sentence { get; init; }
        public string? IntakeReason { get; init; }
        public string? Bio { get; init; }
    }
}
