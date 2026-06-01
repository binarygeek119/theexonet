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
        CancellationToken ct)
    {
        var storyCount = Math.Clamp(options.StoriesPerDay, 1, 10);
        var stories = await GenerateStoriesAsync(editionDate, storyCount, ct);
        var imageCount = Math.Clamp(options.MaxImagesPerDay, 0, storyCount);
        var imageIndexes = SelectImageIndexes(editionDate, storyCount, imageCount);

        for (var index = 0; index < stories.Count; index++)
        {
            if (!imageIndexes.Contains(index))
            {
                continue;
            }

            var story = stories[index];
            try
            {
                var imagePath = await GenerateAndStoreImageAsync(story, editionDate, cacheRoot, ct);
                if (imagePath is not null)
                {
                    stories[index] = story with { ImageUrl = imagePath };
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
        CancellationToken ct)
    {
        var publishedBase = editionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(6);
        var prompt = $$"""
            You write satirical but believable sci-fi news for "Offworld News Network" (ONN), covering the RAVA universe:
            - RAVA = Reactive Asteroid Venturing Agency; players run asteroid mines
            - Currency is Rax (not dollars/credits in headlines)
            - Ores: Ferroxite, Voidium, Stellarite, Salvage Scrap
            - Supplies: Drill Bits, Fuel Cells, Life Support, Comm Modules
            - Features: Trade Market auctions, company name trading, Exonet browser, emergency buy back at 50% refinery value, UTC game days
            - Tone: mix of Bloomberg wire + frontier tabloid; no real-world politics; no hate; family-friendly

            Generate exactly {{storyCount}} unique news stories for edition date {{editionDate:yyyy-MM-dd}}.
            Return JSON only with this shape:
            {
              "stories": [
                {
                  "id": "kebab-case-slug",
                  "headline": "string",
                  "dek": "short subheadline",
                  "body": "2-3 paragraphs separated by \\n\\n",
                  "category": "Markets|Mining|Corporate|Shipping|Exonet|Culture",
                  "location": "fictional belt location",
                  "author": "reporter name"
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
            return OffworldNewsTemplateGenerator.Generate(editionDate, storyCount).Stories.ToList();
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(payload, SerializerOptions);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return OffworldNewsTemplateGenerator.Generate(editionDate, storyCount).Stories.ToList();
        }

        var parsed = JsonSerializer.Deserialize<GeneratedStoriesPayload>(content, SerializerOptions);
        if (parsed?.Stories is null || parsed.Stories.Count == 0)
        {
            return OffworldNewsTemplateGenerator.Generate(editionDate, storyCount).Stories.ToList();
        }

        var stories = new List<OffworldNewsStoryDto>();
        for (var index = 0; index < Math.Min(storyCount, parsed.Stories.Count); index++)
        {
            var item = parsed.Stories[index];
            stories.Add(new OffworldNewsStoryDto(
                string.IsNullOrWhiteSpace(item.Id) ? $"story-{index + 1}" : item.Id,
                item.Headline ?? $"Story {index + 1}",
                item.Dek ?? string.Empty,
                item.Body ?? string.Empty,
                item.Category ?? "Markets",
                item.Location ?? "Ceres Relay",
                item.Author ?? "ONN Wire Desk",
                publishedBase.AddHours(index * 2.5),
                null));
        }

        while (stories.Count < storyCount)
        {
            stories.AddRange(
                OffworldNewsTemplateGenerator.Generate(editionDate, storyCount - stories.Count).Stories);
        }

        return stories.Take(storyCount).ToList();
    }

    private async Task<string?> GenerateAndStoreImageAsync(
        OffworldNewsStoryDto story,
        DateOnly editionDate,
        string cacheRoot,
        CancellationToken ct)
    {
        var imagePrompt =
            $"Editorial sci-fi news illustration for asteroid mining economy. No text or logos. Scene inspired by: {story.Headline}. {story.Dek}. Cinematic, muted colors, outer space industrial.";

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
        var fileName = $"{SanitizeFileName(story.Id)}.png";
        var filePath = Path.Combine(imageDir, fileName);

        var imageBytes = await httpClient.GetByteArrayAsync(remoteUrl, ct);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);

        return $"/exonet/offworld-news/images/{editionDate:yyyy-MM-dd}/{fileName}";
    }

    private static HashSet<int> SelectImageIndexes(DateOnly editionDate, int storyCount, int imageCount)
    {
        if (imageCount <= 0 || storyCount <= 0)
        {
            return [];
        }

        var random = new Random(HashCode.Combine(editionDate, 991));
        var indexes = Enumerable.Range(0, storyCount).OrderBy(_ => random.Next()).Take(imageCount);
        return indexes.ToHashSet();
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
