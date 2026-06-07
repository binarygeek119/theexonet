using Theexonet.Core.Constants;

namespace Theexonet.Core.Tests;

public class AiImageJobKindsTests
{
    [Theory]
    [InlineData(AiImageJobKinds.OnnReporterAvatar, true)]
    [InlineData(AiImageJobKinds.OnnReporterBackground, true)]
    [InlineData(AiImageJobKinds.OnnStoryImage, false)]
    [InlineData(AiImageJobKinds.ForeverfallPortrait, false)]
    public void IsOnnReporterKind_matches_reporter_job_kinds(string kind, bool expected) =>
        Assert.Equal(expected, AiImageJobKinds.IsOnnReporterKind(kind));

    [Theory]
    [InlineData(AiImageJobKinds.OnnEditionStories, true)]
    [InlineData(AiImageJobKinds.ForeverfallIntake, true)]
    [InlineData(AiImageJobKinds.LunarWeatherBulletin, true)]
    [InlineData(AiImageJobKinds.OnnStoryImage, false)]
    [InlineData(AiImageJobKinds.VoidCorpProduct, false)]
    public void IsText_matches_text_job_kinds(string kind, bool expected) =>
        Assert.Equal(expected, AiGenerationJobKinds.IsText(kind));

    [Theory]
    [InlineData(AiImageJobKinds.OnnEditionStories, false)]
    [InlineData(AiImageJobKinds.CompanyLogo, true)]
    public void IsImage_matches_non_text_job_kinds(string kind, bool expected) =>
        Assert.Equal(expected, AiGenerationJobKinds.IsImage(kind));

    [Fact]
    public void ResolveGroupKinds_onn_includes_text_and_image_kinds()
    {
        var kinds = AiGenerationJobKinds.ResolveGroupKinds("onn");
        Assert.Contains(AiImageJobKinds.OnnEditionStories, kinds);
        Assert.Contains(AiImageJobKinds.OnnStoryImage, kinds);
        Assert.Contains(AiImageJobKinds.OnnReporterAvatar, kinds);
    }

    [Fact]
    public void ResolveGroupKinds_foreverfall_includes_intake_and_portrait()
    {
        var kinds = AiGenerationJobKinds.ResolveGroupKinds("foreverfall");
        Assert.Contains(AiImageJobKinds.ForeverfallIntake, kinds);
        Assert.Contains(AiImageJobKinds.ForeverfallPortrait, kinds);
    }

    [Fact]
    public void ResolveGroupKinds_unknown_returns_empty() =>
        Assert.Empty(AiGenerationJobKinds.ResolveGroupKinds("unknown-feature"));
}
