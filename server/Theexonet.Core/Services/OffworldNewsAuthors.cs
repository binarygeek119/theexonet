namespace Theexonet.Core.Services;

/// <summary>ONN byline pool — stories pick a reporter deterministically per edition and slot.</summary>
public static class OffworldNewsAuthors
{
    public static IReadOnlyList<string> Reporters => OffworldNewsReporterCatalog.DisplayNames;

    public static string PickForStory(DateOnly editionDate, int storyIndex) =>
        OffworldNewsReporterCatalog.PickForStory(editionDate, storyIndex).DisplayName;
}
