using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

public static class PlayerJobHistoryMapper
{
    public static PlayerJobHistoryEntryDto ToDto(
        string jobSlug,
        string jobTitle,
        bool isCurrent,
        DateTime startedAtUtc,
        DateTime? endedAtUtc)
    {
        var workspaceModule = PlayerJobCatalog.TryGet(jobSlug)?.WorkspaceModule ?? string.Empty;
        return new(jobSlug, jobTitle, workspaceModule, isCurrent, startedAtUtc, endedAtUtc);
    }

    public static IReadOnlyList<PlayerJobHistoryEntryDto> OrderForDisplay(
        IEnumerable<PlayerJobHistoryEntryDto> entries)
    {
        return entries
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.StartedAtUtc)
            .ToList();
    }
}
