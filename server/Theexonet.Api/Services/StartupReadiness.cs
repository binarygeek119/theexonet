namespace Theexonet.Api.Services;

public sealed class StartupReadiness
{
    private volatile bool _isDatabaseReady;

    public bool IsDatabaseReady => _isDatabaseReady;

    public void MarkDatabaseReady() => _isDatabaseReady = true;
}
