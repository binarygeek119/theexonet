using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReportersCsvLoaderTests
{
    [Fact]
    public void Parse_reads_quoted_fields_and_specialties()
    {
        const string csv = """
            Slug,DisplayName,Title,Beat,Bureau,Personality,WritingVoice,DirectoryBio,OnnBio,StoryKicker,Specialties
            test-reporter,Test Reporter,Title,Markets,Ceres,"Short personality","Write like this.","Directory bio here.","ONN bio here.","Signed off.","one;two;three"
            """;

        var reporters = OffworldNewsReportersCsvLoader.Parse(csv);

        Assert.Single(reporters);
        var reporter = reporters[0];
        Assert.Equal("test-reporter", reporter.Slug);
        Assert.Equal("Test Reporter", reporter.DisplayName);
        Assert.Equal(3, reporter.Specialties.Count);
        Assert.Equal("one", reporter.Specialties[0]);
    }
}
