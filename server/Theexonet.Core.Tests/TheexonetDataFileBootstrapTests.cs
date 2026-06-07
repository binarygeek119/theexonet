using Theexonet.Core.Configuration;

namespace Theexonet.Core.Tests;

public class TheexonetDataFileBootstrapTests
{
    [Fact]
    public void EnsureFromPublish_copies_missing_file_into_data_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "theexonet-bootstrap-" + Guid.NewGuid().ToString("N"));
        var publish = Path.Combine(root, "publish");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(publish);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(publish, "offworld-news-reporters.csv"), "Slug,DisplayName\na,A");

        var previous = Environment.GetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable);
        lock (ReporterCatalogTestSupport.DataDirGate)
        {
            Environment.SetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable, data);
            try
            {
                var resolved = TheexonetDataFileBootstrap.EnsureFromPublish(publish, "offworld-news-reporters.csv");
                Assert.Equal(Path.Combine(data, "offworld-news-reporters.csv"), resolved);
                Assert.True(File.Exists(resolved));
                Assert.Equal("Slug,DisplayName", File.ReadAllLines(resolved)[0]);
            }
            finally
            {
                Environment.SetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable, previous);
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureFromPublish_does_not_overwrite_existing_data_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "theexonet-bootstrap-" + Guid.NewGuid().ToString("N"));
        var publish = Path.Combine(root, "publish");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(publish);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(publish, "offworld-news-reporters.csv"), "from,publish");
        File.WriteAllText(Path.Combine(data, "offworld-news-reporters.csv"), "from,data");

        var previous = Environment.GetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable);
        lock (ReporterCatalogTestSupport.DataDirGate)
        {
            Environment.SetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable, data);
            try
            {
                var resolved = TheexonetDataFileBootstrap.EnsureFromPublish(publish, "offworld-news-reporters.csv");
                Assert.Equal("from,data", File.ReadAllText(resolved));
            }
            finally
            {
                Environment.SetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable, previous);
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
