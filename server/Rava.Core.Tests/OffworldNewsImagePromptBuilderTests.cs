using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsImagePromptBuilderTests
{
    [Fact]
    public void Build_UsesAiImagePromptWhenProvided()
    {
        var story = SampleStory(
            headline: "Cargo backlog snarls Phobos Anchorage",
            dek: "Inspectors reject mismatched manifests.",
            body: "Long body that should not appear when AI prompt is set.");

        var prompt = OffworldNewsImagePromptBuilder.Build(
            story,
            aiImagePrompt: "Dock workers in vac suits argue beside stacked ore canisters under amber floodlights.");

        Assert.Contains("Dock workers in vac suits", prompt);
        Assert.DoesNotContain("Long body that should not appear", prompt);
        Assert.Contains("cool blue and cyan tint", prompt);
    }

    [Fact]
    public void Build_FallsBackToStoryContentWhenAiPromptMissing()
    {
        var story = SampleStory(
            headline: "Voidium spreads widen on Ceres Relay",
            dek: "Traders hold ore until NPC buyers post clearer Rax prices.",
            body: "Miners in orange suits monitor conveyor belts feeding a voidium hopper.\n\nAnalysts warned payroll pressure could force emergency buy backs.");

        var prompt = OffworldNewsImagePromptBuilder.Build(story);

        Assert.Contains("Voidium spreads widen on Ceres Relay", prompt);
        Assert.Contains("Traders hold ore until NPC buyers", prompt);
        Assert.Contains("Miners in orange suits monitor conveyor belts", prompt);
        Assert.Contains("Ceres Relay", prompt);
        Assert.Contains("Markets coverage", prompt);
        Assert.Contains("Ferroxite Mining Co", prompt);
    }

    [Fact]
    public void ExtractBodyExcerpt_TruncatesLongBody()
    {
        var longBody = string.Join(' ', Enumerable.Repeat("belt", 200));

        var excerpt = OffworldNewsImagePromptBuilder.ExtractBodyExcerpt(longBody);

        Assert.True(excerpt.Length <= 421);
        Assert.EndsWith("…", excerpt);
    }

    private static OffworldNewsStoryDto SampleStory(string headline, string dek, string body) =>
        new(
            "sample-story",
            headline,
            dek,
            body,
            "Markets",
            "Ceres Relay",
            "ONN Wire Desk",
            "mira-solano",
            DateTime.UtcNow,
            "Ferroxite Mining Co",
            null);
}
