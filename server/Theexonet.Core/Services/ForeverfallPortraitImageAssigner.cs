namespace Theexonet.Core.Services;

public sealed record ForeverfallImageRegistryEntry(
    string ImageId,
    string? GenderHint,
    DateTime CreatedAt,
    string FileName);

public sealed record ForeverfallImageRegistry(
    IReadOnlyList<ForeverfallImageRegistryEntry> Images,
    int NextImageNumber);

public sealed record ForeverfallPortraitAssignment(
    string ImageId,
    bool NeedsGeneration,
    string Gender);

public static class ForeverfallPortraitImageAssigner
{
    private const int AssignSalt = 0xFF22;

    public static IReadOnlyList<ForeverfallPortraitAssignment> Assign(
        DateOnly intakeDate,
        int maleCount,
        int femaleCount,
        ForeverfallImageRegistry registry,
        int maxImages)
    {
        var slots = new List<string>(maleCount + femaleCount);
        slots.AddRange(Enumerable.Repeat("male", maleCount));
        slots.AddRange(Enumerable.Repeat("female", femaleCount));

        var shuffleRng = CreateRandom(intakeDate, 0x01);
        var shuffledSlots = slots.OrderBy(_ => shuffleRng.Next()).ToList();

        var poolCount = registry.Images.Count;
        var newPortraitBudget = Math.Max(0, maxImages - poolCount);
        var newCount = Math.Min(shuffledSlots.Count, newPortraitBudget);
        var reuseCount = shuffledSlots.Count - newCount;

        var reuseIds = SelectUniqueImageIds(intakeDate, reuseCount, registry.Images);
        var assignments = new List<ForeverfallPortraitAssignment>(shuffledSlots.Count);
        var nextNumber = Math.Max(1, registry.NextImageNumber);
        var reuseIndex = 0;

        for (var slotIndex = 0; slotIndex < shuffledSlots.Count; slotIndex++)
        {
            var gender = shuffledSlots[slotIndex];
            if (slotIndex < newCount)
            {
                var imageId = FormatImageId(nextNumber++);
                assignments.Add(new ForeverfallPortraitAssignment(imageId, NeedsGeneration: true, gender));
                continue;
            }

            var imageIdReuse = reuseIds[reuseIndex++];
            assignments.Add(new ForeverfallPortraitAssignment(imageIdReuse, NeedsGeneration: false, gender));
        }

        return assignments;
    }

    public static IReadOnlyList<string> SelectUniqueImageIds(
        DateOnly intakeDate,
        int count,
        IReadOnlyList<ForeverfallImageRegistryEntry> registry)
    {
        if (count <= 0 || registry.Count == 0)
        {
            return [];
        }

        var rng = CreateRandom(intakeDate, 0x02);
        var shuffled = registry.Select(entry => entry.ImageId).OrderBy(_ => rng.Next()).ToList();
        if (count <= shuffled.Count)
        {
            return shuffled.Take(count).ToList();
        }

        var result = new List<string>(count);
        var index = 0;
        while (result.Count < count)
        {
            if (index >= shuffled.Count)
            {
                shuffled = shuffled.OrderBy(_ => rng.Next()).ToList();
                index = 0;
            }

            var candidate = shuffled[index++];
            if (result.Count == 0 || !string.Equals(result[^1], candidate, StringComparison.Ordinal))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    public static string FormatImageId(int number) => $"FF-{number:D4}";

    private static Random CreateRandom(DateOnly intakeDate, int salt) =>
        new(HashCode.Combine(intakeDate.DayNumber, salt, AssignSalt));
}
