using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReporterPortraitPromptsTests
{
    public OffworldNewsReporterPortraitPromptsTests()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "offworld-news-reporters.csv");
        OffworldNewsReporterCatalog.Configure(AppContext.BaseDirectory, Path.GetFileName(csvPath));
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
    public void BuildAvatarPrompt_includes_portrait_gender()
    {
        var female = OffworldNewsReporterCatalog.All.First(r => r.Gender == OffworldNewsReporterPortraitGender.Female);
        var male = OffworldNewsReporterCatalog.All.First(r => r.Gender == OffworldNewsReporterPortraitGender.Male);

        Assert.Contains("clearly female adult woman journalist", OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(female));
        Assert.Contains("clearly male adult man journalist", OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(male));
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
}
