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
        var drafts = await GenerateStoriesAsync(editionDate, storyCount, companyContext, ct);
        drafts = await ApplyImagesToDraftsAsync(drafts, editionDate, cacheRoot, ct);

        var stories = drafts.Select(draft => draft.Story).ToList();
        return new OffworldNewsEditionDto(
            editionDate,
            DateTime.UtcNow,
            "openai",
            stories);
    }

    public async Task<OffworldNewsEditionDto> RegenerateImagesAsync(
        OffworldNewsEditionDto edition,
        string cacheRoot,
        CancellationToken ct)
    {
        var drafts = edition.Stories
            .Select(story => new StoryDraft(
                story with
                {
                    ImageUrl = OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                    ImageAspect = null,
                },
                null))
            .ToList();

        drafts = await ApplyImagesToDraftsAsync(drafts, edition.EditionDate, cacheRoot, ct);

        return edition with
        {
            GeneratedAt = DateTime.UtcNow,
            Stories = drafts.Select(draft => draft.Story).ToList(),
        };
    }

    private async Task<List<StoryDraft>> ApplyImagesToDraftsAsync(
        List<StoryDraft> drafts,
        DateOnly editionDate,
        string cacheRoot,
        CancellationToken ct)
    {
        var imageCap = options.MaxImagesPerDay <= 0
            ? drafts.Count
            : Math.Clamp(options.MaxImagesPerDay, 1, drafts.Count);

        for (var index = 0; index < drafts.Count && index < imageCap; index++)
        {
            var draft = drafts[index];
            var story = draft.Story;
            try
            {
                var imagePath = await GenerateAndStoreImageAsync(
                    story,
                    draft.ImagePrompt,
                    editionDate,
                    index,
                    cacheRoot,
                    ct);
                if (imagePath is not null)
                {
                    drafts[index] = draft with
                    {
                        Story = story with { ImageUrl = imagePath.Path, ImageAspect = imagePath.AspectKey },
                    };
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

            drafts[index] = draft with
            {
                Story = story with
                {
                    ImageUrl = story.ImageUrl ?? OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                },
            };
        }

        for (var index = imageCap; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var story = draft.Story;
            drafts[index] = draft with
            {
                Story = story with
                {
                    ImageUrl = story.ImageUrl ?? OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                },
            };
        }

        return drafts;
    }

    private sealed record StoryDraft(OffworldNewsStoryDto Story, string? ImagePrompt);

    private async Task<List<StoryDraft>> GenerateStoriesAsync(
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
            For each story, imagePrompt must describe ONE specific visual scene from that story (people, equipment, location, action).
            imagePrompt must match the headline/body — never a generic asteroid wallpaper. No text or logos in the scene.
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
                  "companyName": "featured mining company or syndicate in the story",
                  "imagePrompt": "1-2 sentences describing the exact illustration scene for this story only"
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
            return ToStoryDrafts(OffworldNewsTemplateGenerator.Generate(editionDate, storyCount, companyContext).Stories);
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(payload, SerializerOptions);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return ToStoryDrafts(OffworldNewsTemplateGenerator.Generate(editionDate, storyCount, companyContext).Stories);
        }

        var parsed = JsonSerializer.Deserialize<GeneratedStoriesPayload>(content, SerializerOptions);
        if (parsed?.Stories is null || parsed.Stories.Count == 0)
        {
            return ToStoryDrafts(OffworldNewsTemplateGenerator.Generate(editionDate, storyCount, companyContext).Stories);
        }

        var drafts = new List<StoryDraft>();
        for (var index = 0; index < Math.Min(storyCount, parsed.Stories.Count); index++)
        {
            var item = parsed.Stories[index];
            var category = item.Category ?? "Markets";
            var story = new OffworldNewsStoryDto(
                string.IsNullOrWhiteSpace(item.Id) ? $"story-{index + 1}" : item.Id,
                item.Headline ?? $"Story {index + 1}",
                item.Dek ?? string.Empty,
                item.Body ?? string.Empty,
                category,
                item.Location ?? "Ceres Relay",
                item.Author ?? "ONN Wire Desk",
                publishedBase.AddHours(index * 2.5),
                item.CompanyName,
                OffworldNewsTemplateGenerator.PlaceholderImageForCategory(category));
            drafts.Add(new StoryDraft(story, item.ImagePrompt));
        }

        while (drafts.Count < storyCount)
        {
            drafts.AddRange(
                ToStoryDrafts(
                    OffworldNewsTemplateGenerator.Generate(editionDate, storyCount - drafts.Count, companyContext).Stories));
        }

        return drafts.Take(storyCount).ToList();
    }

    private static List<StoryDraft> ToStoryDrafts(IEnumerable<OffworldNewsStoryDto> stories) =>
        stories.Select(story => new StoryDraft(story, null)).ToList();

    private sealed record GeneratedImageResult(string Path, string AspectKey);

    private async Task<GeneratedImageResult?> GenerateAndStoreImageAsync(
        OffworldNewsStoryDto story,
        string? aiImagePrompt,
        DateOnly editionDate,
        int storyIndex,
        string cacheRoot,
        CancellationToken ct)
    {
        var aspect = OffworldNewsImageAspectCatalog.Pick(editionDate, story.Id, storyIndex);
        var imagePrompt = OffworldNewsImagePromptBuilder.Build(story, aiImagePrompt, aspect.CompositionHint);

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(options.BaseUrl, "/images/generations"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = options.ImageModel,
            prompt = imagePrompt,
            size = aspect.ApiSize,
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

        return new GeneratedImageResult(
            $"/exonet/offworld-news/images/{editionDate:yyyy-MM-dd}/{fileName}",
            aspect.Key);
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
        public string? ImagePrompt { get; set; }
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
