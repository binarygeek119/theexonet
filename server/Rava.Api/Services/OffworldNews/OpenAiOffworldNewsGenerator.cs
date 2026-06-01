using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Services.OffworldNews;

public sealed class OpenAiOffworldNewsGenerator(
    OffworldNewsOptions options,
    HttpClient httpClient,
    ILogger logger)
{
    public const string HttpClientName = "OffworldNewsOpenAi";

    public async Task<OffworldNewsEditionDto> GenerateAsync(
        DateOnly editionDate,
        string cacheRoot,
        OffworldNewsCompanyContext? companyContext,
        CancellationToken ct)
    {
        var storyCount = Math.Clamp(options.StoriesPerDay, 1, 10);
        var stories = await GenerateStoriesAsync(editionDate, storyCount, companyContext, ct);
        var imageCap = options.MaxImagesPerDay <= 0 ? storyCount : Math.Clamp(options.MaxImagesPerDay, 1, storyCount);

        for (var index = 0; index < stories.Count && index < imageCap; index++)
        {
            var story = stories[index];
            try
            {
                var imagePath = await GenerateAndStoreImageAsync(story, editionDate, cacheRoot, ct);
                if (imagePath is not null)
                {
                    stories[index] = story with { ImageUrl = imagePath };
                    continue;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Offworld News image generation failed for story {StoryId} on {Date}",
                    story.Id,
                    editionDate);
            }

            stories[index] = story with
            {
                ImageUrl = story.ImageUrl ?? OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
            };
        }

        for (var index = imageCap; index < stories.Count; index++)
        {
            var story = stories[index];
            stories[index] = story with
            {
                ImageUrl = story.ImageUrl ?? OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
            };
        }

        return new OffworldNewsEditionDto(
            editionDate,
            DateTime.UtcNow,
            "openai",
            stories);
    }

    private async Task<List<OffworldNewsStoryDto>> GenerateStoriesAsync(
        DateOnly editionDate,
        int storyCount,
        OffworldNewsCompanyContext? companyContext,
        CancellationToken ct)
    {
        var publishedBase = editionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(6);
        var rising = companyContext?.RisingCompanies ?? [];
        var struggling = companyContext?.StrugglingCompanies ?? [];
        var risingList = rising.Count > 0 ? string.Join(", ", rising) : "none available";
        var strugglingList = struggling.Count > 0 ? string.Join(", ", struggling) : "none available";

        var prompt = $$"""
            You write satirical but believable sci-fi news for "Offworld News Network" (ONN), covering the RAVA game universe:
            - RAVA = Reactive Asteroid Venturing Agency; players run asteroid mines
            - Currency is Rax (not dollars/credits in headlines)
            - Ores: Ferroxite, Voidium, Stellarite, Salvage Scrap
            - Supplies: Drill Bits, Fuel Cells, Life Support, Comm Modules (linked to live US stock symbols in-game)
            - Features: Trade Market auctions, company name trading, Exonet browser, Miner Profiles leaderboard, emergency buy back at 50% refinery value, UTC game days, shipping routes to NPC refineries
            - Tone: mix of Bloomberg wire + frontier tabloid; no real-world politics; no hate; family-friendly

            Story topics MUST vary across the edition and include several of:
            - Belt stock/supply markets and ore prices
            - Shipping routes, refinery queues, cargo manifests
            - Player-style mining companies doing well (use rising list) or in trouble (use struggling list)
            - Fake corporate names when not using a player company
            - New planets, survey charters, new mine openings
            - Interplanetary politics (Orbital Commons, charter votes, registry rules)

            Real player mining companies doing well lately: {{risingList}}
            Real player mining companies under pressure lately: {{strugglingList}}
            You may headline these exact company names when appropriate. Also invent believable fake company names.

            Generate exactly {{storyCount}} unique news stories for edition date {{editionDate:yyyy-MM-dd}}.
            Each body must be at least 2 full paragraphs (can be 3-4) separated by \\n\\n with concrete game details.
            Return JSON only with this shape:
            {
              "stories": [
                {
                  "id": "kebab-case-slug",
                  "headline": "string",
                  "dek": "short subheadline",
                  "body": "paragraphs separated by \\n\\n",
                  "category": "Markets|Mining|Corporate|Shipping|Politics|Exonet",
                  "location": "fictional belt or outer-planet location",
                  "author": "reporter name",
                  "companyName": "featured mining company or syndicate in the story"
                }
              ]
            }
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(options.BaseUrl, "/chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = options.TextModel,
            temperature = 0.9,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "You are a sci-fi newsroom editor. Output valid JSON only." },
                new { role = "user", content = prompt },
            },
        });

        using var response = await httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OpenAI story generation failed ({Status}): {Body}",
                (int)response.StatusCode,
                payload);
            return OffworldNewsTemplateGenerator.Generate(editionDate, storyCount, companyContext).Stories.ToList();
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(payload, SerializerOptions);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return OffworldNewsTemplateGenerator.Generate(editionDate, storyCount, companyContext).Stories.ToList();
        }

        var parsed = JsonSerializer.Deserialize<GeneratedStoriesPayload>(content, SerializerOptions);
        if (parsed?.Stories is null || parsed.Stories.Count == 0)
        {
            return OffworldNewsTemplateGenerator.Generate(editionDate, storyCount, companyContext).Stories.ToList();
        }

        var stories = new List<OffworldNewsStoryDto>();
        for (var index = 0; index < Math.Min(storyCount, parsed.Stories.Count); index++)
        {
            var item = parsed.Stories[index];
            var category = item.Category ?? "Markets";
            stories.Add(new OffworldNewsStoryDto(
                string.IsNullOrWhiteSpace(item.Id) ? $"story-{index + 1}" : item.Id,
                item.Headline ?? $"Story {index + 1}",
                item.Dek ?? string.Empty,
                item.Body ?? string.Empty,
                category,
                item.Location ?? "Ceres Relay",
                item.Author ?? "ONN Wire Desk",
                publishedBase.AddHours(index * 2.5),
                item.CompanyName,
                OffworldNewsTemplateGenerator.PlaceholderImageForCategory(category)));
        }

        while (stories.Count < storyCount)
        {
            stories.AddRange(
                OffworldNewsTemplateGenerator.Generate(editionDate, storyCount - stories.Count, companyContext).Stories);
        }

        return stories.Take(storyCount).ToList();
    }

    private async Task<string?> GenerateAndStoreImageAsync(
        OffworldNewsStoryDto story,
        DateOnly editionDate,
        string cacheRoot,
        CancellationToken ct)
    {
        var companyHint = string.IsNullOrWhiteSpace(story.CompanyName) ? string.Empty : $" Company: {story.CompanyName}.";
        var imagePrompt =
            $"Editorial sci-fi news photo illustration for asteroid mining economy. No text, no logos, no words. Scene inspired by: {story.Headline}. {story.Dek}.{companyHint} Outer space industrial, cinematic, cool blue color grading and cyan atmospheric tint.";

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(options.BaseUrl, "/images/generations"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = options.ImageModel,
            prompt = imagePrompt,
            size = "1024x1024",
            response_format = "url",
        });

        using var response = await httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OpenAI image generation failed ({Status}): {Body}",
                (int)response.StatusCode,
                payload);
            return null;
        }

        var parsed = JsonSerializer.Deserialize<ImageGenerationResponse>(payload, SerializerOptions);
        var remoteUrl = parsed?.Data?.FirstOrDefault()?.Url;
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        var imageDir = Path.Combine(cacheRoot, "images", editionDate.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(imageDir);
        var fileName = $"{SanitizeFileName(story.Id)}.jpg";
        var filePath = Path.Combine(imageDir, fileName);

        var imageBytes = await httpClient.GetByteArrayAsync(remoteUrl, ct);
        await OffworldNewsImageEncoder.SaveAsJpegAsync(imageBytes, filePath, ct);

        return $"/exonet/offworld-news/images/{editionDate:yyyy-MM-dd}/{fileName}";
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        baseUrl = baseUrl.TrimEnd('/');
        return $"{baseUrl}{path}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '-' : ch);
        }

        var sanitized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "story" : sanitized;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }
    }

    private sealed class GeneratedStoriesPayload
    {
        public List<GeneratedStory>? Stories { get; set; }
    }

    private sealed class GeneratedStory
    {
        public string? Id { get; set; }
        public string? Headline { get; set; }
        public string? Dek { get; set; }
        public string? Body { get; set; }
        public string? Category { get; set; }
        public string? Location { get; set; }
        public string? Author { get; set; }
        public string? CompanyName { get; set; }
    }

    private sealed class ImageGenerationResponse
    {
        public List<ImageData>? Data { get; set; }
    }

    private sealed class ImageData
    {
        public string? Url { get; set; }
    }
}
