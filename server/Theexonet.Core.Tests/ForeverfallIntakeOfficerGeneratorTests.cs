using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class ForeverfallIntakeOfficerGeneratorTests
{
    [Fact]
    public void Resolve_IsDeterministicForDate()
    {
        var date = new DateOnly(2026, 6, 7);

        var first = ForeverfallIntakeOfficerGenerator.Resolve(date);
        var second = ForeverfallIntakeOfficerGenerator.Resolve(date);

        Assert.Equal(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first));
    }
}
