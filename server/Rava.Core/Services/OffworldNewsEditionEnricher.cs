using Rava.Core.Dtos;

namespace Rava.Core.Services;

/// <summary>
/// Maps story bylines to the ONN reporter roster (fixes legacy AI-invented author names in cached editions).
/// </summary>
public static class OffworldNewsEditionEnricher
{
    public static OffworldNewsEditionDto EnrichAuthors(OffworldNewsEditionDto edition)
    {
        var stories = edition.Stories
            .Select((story, index) => EnrichAuthor(story, edition.EditionDate, index))
            .ToList();
        return edition with { Stories = stories };
    }

    public static OffworldNewsStoryDto EnrichAuthor(OffworldNewsStoryDto story, DateOnly editionDate, int storyIndex)
    {
        var reporter = OffworldNewsReporterCatalog.Resolve(story.AuthorSlug)
            ?? OffworldNewsReporterCatalog.Resolve(story.Author)
            ?? OffworldNewsReporterCatalog.PickForStory(editionDate, storyIndex);

        return story with
        {
            Author = reporter.DisplayName,
            AuthorSlug = reporter.Slug,
        };
    }
}
