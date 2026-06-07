using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class OffworldNewsReporterPortraitPromptsTests
{
    public OffworldNewsReporterPortraitPromptsTests()
    {
        ReporterCatalogTestSupport.ConfigureFromTestOutput();
    }

    [Fact]
    public void BuildAvatarPrompt_includes_personality_and_beat()
    {
        var reporter = OffworldNewsReporterCatalog.All[0];
        var prompt = OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(reporter);

        Assert.Contains(reporter.Personality, prompt);
        Assert.Contains(reporter.Beat, prompt);
        Assert.Contains("no text", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAvatarPrompt_includes_portrait_gender_for_humans()
    {
        var female = OffworldNewsReporterCatalog.All.First(r => r.Gender == OffworldNewsReporterPortraitGender.Female && !r.Appearance.IsAlien);
        var male = OffworldNewsReporterCatalog.All.First(r => r.Gender == OffworldNewsReporterPortraitGender.Male && !r.Appearance.IsAlien);

        Assert.Contains("clearly female adult human woman journalist", OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(female));
        Assert.Contains("clearly male adult human man journalist", OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(male));
    }

    [Fact]
    public void BuildAvatarPrompt_includes_appearance_when_configured()
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug("mira-solano");
        Assert.NotNull(reporter);
        Assert.False(reporter!.Appearance.IsEmpty);

        var prompt = OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(reporter);

        Assert.Contains("Match this appearance faithfully", prompt);
        Assert.Contains(reporter.Appearance.Hair, prompt);
        Assert.Contains(reporter.Appearance.Eyes, prompt);
        Assert.Contains("human", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAvatarPrompt_describes_alien_species_and_anatomy()
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug("theo-brassard");
        Assert.NotNull(reporter);

        var prompt = OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(reporter!);

        Assert.Contains("Europan alien journalist", prompt);
        Assert.Contains("authentically non-human", prompt);
        Assert.Contains(reporter!.Appearance.Race, prompt);
        Assert.Contains("cool blue and cyan color grade", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildBackgroundPrompt_includes_bureau_and_location()
    {
        var reporter = OffworldNewsReporterCatalog.All[1];
        var prompt = OffworldNewsReporterPortraitPrompts.BuildBackgroundPrompt(reporter);

        Assert.Contains(reporter.Bureau, prompt);
        Assert.Contains("news location", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no people", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildBackgroundPrompt_includes_career_story_memorabilia()
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug("marcus-whitaker");
        Assert.NotNull(reporter);
        Assert.NotEmpty(reporter!.NotableStories);

        var prompt = OffworldNewsReporterPortraitPrompts.BuildBackgroundPrompt(reporter);

        Assert.Contains("Titan Freight Hub", prompt);
        Assert.Contains(reporter.NotableStories[0], prompt);
        Assert.Contains("headline clippings", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
