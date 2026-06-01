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
    public void BuildBackgroundPrompt_includes_bureau()
    {
        var reporter = OffworldNewsReporterCatalog.All[1];
        var prompt = OffworldNewsReporterPortraitPrompts.BuildBackgroundPrompt(reporter);

        Assert.Contains(reporter.Bureau, prompt);
        Assert.Contains("no people", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
