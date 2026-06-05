using Rava.Core.Services;

namespace Rava.Core.Tests;

public class ForeverfallPortraitImageAssignerTests
{
    private static ForeverfallImageRegistry BuildRegistry(int count)
    {
        var images = Enumerable.Range(1, count)
            .Select(number => new ForeverfallImageRegistryEntry(
                ForeverfallPortraitImageAssigner.FormatImageId(number),
                number % 2 == 0 ? "female" : "male",
                DateTime.UtcNow,
                $"FF-{number:D4}.jpg"))
            .ToList();
        return new ForeverfallImageRegistry(images, count + 1);
    }

    [Fact]
    public void Assign_NoDuplicateImageIdsWithinBatchWhenReusing()
    {
        var registry = BuildRegistry(500);
        var date = new DateOnly(2026, 6, 1);
        var assignments = ForeverfallPortraitImageAssigner.Assign(date, 8, 7, registry, maxImages: 500);

        Assert.Equal(15, assignments.Count);
        Assert.All(assignments, assignment => Assert.False(assignment.NeedsGeneration));

        var imageIds = assignments.Select(assignment => assignment.ImageId).ToList();
        Assert.Equal(imageIds.Count, imageIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Assign_GeneratesNewPortraitsUntilPoolCap()
    {
        var registry = BuildRegistry(495);
        var date = new DateOnly(2026, 6, 2);
        var assignments = ForeverfallPortraitImageAssigner.Assign(date, 7, 8, registry, maxImages: 500);

        Assert.Equal(15, assignments.Count);
        Assert.Equal(5, assignments.Count(assignment => assignment.NeedsGeneration));
        Assert.Equal(10, assignments.Count(assignment => !assignment.NeedsGeneration));

        var reuseIds = assignments.Where(assignment => !assignment.NeedsGeneration).Select(assignment => assignment.ImageId);
        Assert.Equal(reuseIds.Count(), reuseIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SelectUniqueImageIds_ReturnsRequestedCountWithoutDuplicates()
    {
        var registry = BuildRegistry(30);
        var ids = ForeverfallPortraitImageAssigner.SelectUniqueImageIds(
            new DateOnly(2026, 6, 3),
            15,
            registry.Images);

        Assert.Equal(15, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }
}
