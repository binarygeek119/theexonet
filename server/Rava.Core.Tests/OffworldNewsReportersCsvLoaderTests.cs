using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReportersCsvLoaderTests
{
    [Fact]
    public void SaveToFile_round_trips_reporter_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"onn-reporters-{Guid.NewGuid():N}.csv");
        var reporters = new[]
        {
            new OffworldNewsReporterProfile(
                "mira-solano",
                "Mira Solano",
                "Senior Markets Correspondent",
                "Markets",
                "Ceres Relay",
                "Calm numbers nerd",
                "Crisp, data-first sentences",
                "Directory bio with, comma",
                "ONN bio",
                "Kicker line",
                ["ore prices", "Rax flows"],
                OffworldNewsReporterPortraitGender.Female),
        };

        try
        {
            OffworldNewsReportersCsvLoader.SaveToFile(path, reporters);
            var loaded = OffworldNewsReportersCsvLoader.LoadFromFile(path);

            Assert.Single(loaded);
            Assert.Equal("mira-solano", loaded[0].Slug);
            Assert.Equal("Mira Solano", loaded[0].DisplayName);
            Assert.Equal("Directory bio with, comma", loaded[0].DirectoryBio);
            Assert.Equal(2, loaded[0].Specialties.Count);
            Assert.Equal(OffworldNewsReporterPortraitGender.Female, loaded[0].Gender);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
