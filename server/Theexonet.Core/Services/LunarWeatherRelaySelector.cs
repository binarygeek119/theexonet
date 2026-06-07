using Theexonet.Core.Configuration;

namespace Theexonet.Core.Services;

public static class LunarWeatherRelaySelector
{
    private const int SelectorSalt = 0x1DEA;

    public static int ResolveOperationalCount(DateOnly date, LunarWeatherOptions options)
    {
        var poolSize = Math.Max(1, options.RelayPoolSize);
        var target = Math.Clamp(options.TargetOperationalCount, 1, poolSize);
        var variance = Math.Max(0, options.OperationalVariance);
        var min = Math.Clamp(options.MinOperationalCount, 1, poolSize);
        var max = Math.Clamp(options.MaxOperationalCount, min, poolSize);

        var rng = CreateRandom(date, salt: 0x30);
        var delta = (int)Math.Round((rng.NextDouble() + rng.NextDouble() - 1.0) * variance);
        return Math.Clamp(target + delta, min, max);
    }

    public static IReadOnlyList<LunarWeatherRelayProfile> SelectOperationalRelays(
        DateOnly date,
        IReadOnlyList<LunarWeatherRelayProfile> pool,
        int operationalCount)
    {
        if (pool.Count == 0)
        {
            return [];
        }

        operationalCount = Math.Clamp(operationalCount, 0, pool.Count);
        var rng = CreateRandom(date, salt: 0x0B);
        var shuffled = pool.OrderBy(_ => rng.Next()).ToList();
        return shuffled.Take(operationalCount).OrderBy(relay => relay.Id, StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<LunarWeatherRelayProfile> SelectOutageRelays(
        IReadOnlyList<LunarWeatherRelayProfile> pool,
        IReadOnlyList<LunarWeatherRelayProfile> operational) =>
        pool
            .Where(relay => operational.All(op => !string.Equals(op.Id, relay.Id, StringComparison.Ordinal)))
            .OrderBy(relay => relay.Id, StringComparer.Ordinal)
            .ToList();

    private static Random CreateRandom(DateOnly date, int salt) =>
        new(HashCode.Combine(date.DayNumber, salt, SelectorSalt));
}
