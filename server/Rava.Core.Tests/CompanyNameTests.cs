using Rava.Core.Services;
using Rava.Core.Validation;

namespace Rava.Core.Tests;

public class CompanyNameGeneratorTests
{
    [Fact]
    public void Generate_ReturnsReadableCompanyName()
    {
        var name = CompanyNameGenerator.Generate();
        Assert.True(name.Length >= 8);
        Assert.Contains(' ', name);
        Assert.Null(CompanyNameValidator.Validate(name));
    }
}

public class CompanyNameValidatorTests
{
    [Theory]
    [InlineData("Orion Vein Works 472")]
    [InlineData("Deep Core Co.")]
    [InlineData("A-1")]
    public void Validate_AcceptsValidNames(string name)
    {
        Assert.Null(CompanyNameValidator.Validate(name));
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("")]
    [InlineData("Bad@Name")]
    public void Validate_RejectsInvalidNames(string name)
    {
        Assert.NotNull(CompanyNameValidator.Validate(name));
    }

    [Fact]
    public void NormalizeKey_IsCaseInsensitive()
    {
        Assert.Equal(
            CompanyNameNormalizer.NormalizeKey("Orion Vein Works 472"),
            CompanyNameNormalizer.NormalizeKey("orion vein works 472"));
    }
}
