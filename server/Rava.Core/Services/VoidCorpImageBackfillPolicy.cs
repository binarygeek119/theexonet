namespace Rava.Core.Services;

public static class VoidCorpImageBackfillPolicy
{
    public static bool ShouldEnqueueAfterSync(VoidCorpCatalogSyncResult syncResult) =>
        syncResult.Added > 0 || syncResult.MissingImages > 0;
}
