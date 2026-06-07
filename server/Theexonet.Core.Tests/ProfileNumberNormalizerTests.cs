using Theexonet.Core.Services;
using Theexonet.Core.Validation;

namespace Theexonet.Core.Tests;

public class ProfileNumberNormalizerTests
{
    [Fact]
    public void Normalize_accepts_sci_fi_format()
    {
        Assert.Equal("!K7R-8842-9F3A", ProfileNumberNormalizer.Normalize("!k7r-8842-9f3a"));
    }

    [Fact]
    public void Normalize_accepts_sci_fi_without_dashes()
    {
        Assert.Equal("!K7R-8842-9F3A", ProfileNumberNormalizer.Normalize("K7R88429F3A"));
    }

    [Fact]
    public void Normalize_accepts_legacy_phone_format()
    {
        Assert.Equal("!(555)555-5555", ProfileNumberNormalizer.Normalize("!(555)555-5555"));
    }

    [Fact]
    public void Normalize_accepts_legacy_digits_only()
    {
        Assert.Equal("!(555)555-5555", ProfileNumberNormalizer.Normalize("5555555555"));
    }

    [Fact]
    public void Generate_uses_sci_fi_pattern()
    {
        var number = ProfileNumberGenerator.Generate();

        Assert.Matches(@"^![0-9A-Z]{3}-\d{4}-[0-9A-Z]{4}$", number);
        Assert.DoesNotContain('I', number);
        Assert.DoesNotContain('O', number);
    }
}
