using Theexonet.Core.Configuration;
using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

/// <summary>
/// Serializes access to <see cref="TheexonetDataPaths.EnvironmentVariable"/> and reporter catalog loading.
/// Bootstrap tests set THEEXONET_DATA_DIR while catalog tests must load the copy beside test output.
/// </summary>
internal static class ReporterCatalogTestSupport
{
    internal static readonly object DataDirGate = new();

    internal static void ConfigureFromTestOutput()
    {
        lock (DataDirGate)
        {
            var previous = Environment.GetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable, null);
                OffworldNewsReporterCatalog.Configure(
                    AppContext.BaseDirectory,
                    "offworld-news-reporters.csv");
            }
            finally
            {
                Environment.SetEnvironmentVariable(TheexonetDataPaths.EnvironmentVariable, previous);
            }
        }
    }
}
