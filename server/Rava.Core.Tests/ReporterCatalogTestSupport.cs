using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

/// <summary>
/// Serializes access to <see cref="RavaDataPaths.EnvironmentVariable"/> and reporter catalog loading.
/// Bootstrap tests set RAVA_DATA_DIR while catalog tests must load the copy beside test output.
/// </summary>
internal static class ReporterCatalogTestSupport
{
    internal static readonly object DataDirGate = new();

    internal static void ConfigureFromTestOutput()
    {
        lock (DataDirGate)
        {
            var previous = Environment.GetEnvironmentVariable(RavaDataPaths.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(RavaDataPaths.EnvironmentVariable, null);
                OffworldNewsReporterCatalog.Configure(
                    AppContext.BaseDirectory,
                    "offworld-news-reporters.csv");
            }
            finally
            {
                Environment.SetEnvironmentVariable(RavaDataPaths.EnvironmentVariable, previous);
            }
        }
    }
}
