using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

public static class PlayerJobHistoryMapper
{
    public static PlayerJobHistoryEntryDto ToDto(
        string jobSlug,
        string jobTitle,
        bool isCurrent,
        DateTime startedAtUtc,
        DateTime? endedAtUtc) =>
        new(jobSlug, jobTitle, isCurrent, startedAtUtc, endedAtUtc);

    public static IReadOnlyList<PlayerJobHistoryEntryDto> OrderForDisplay(
        IEnumerable<PlayerJobHistoryEntryDto> entries)
    {
        return entries
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.StartedAtUtc)
            .ToList();
    }
}
