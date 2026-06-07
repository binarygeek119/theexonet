using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class ProfileCompletionEvaluatorTests
{
    [Fact]
    public void Evaluate_RequiresGender_WhenEmpty()
    {
        var status = ProfileCompletionEvaluator.Evaluate(string.Empty, string.Empty, "en");
        Assert.True(status.Required);
        Assert.Contains(status.MissingFields, f => f.FieldId == ProfileCompletionEvaluator.FieldGender);
    }

    [Fact]
    public void Evaluate_RequiresLocale_WhenEmpty()
    {
        var status = ProfileCompletionEvaluator.Evaluate("male", string.Empty, string.Empty);
        Assert.True(status.Required);
        Assert.Contains(status.MissingFields, f => f.FieldId == ProfileCompletionEvaluator.FieldLocale);
    }

    [Fact]
    public void Evaluate_RequiresPronouns_ForNonBinaryWithoutChoice()
    {
        var status = ProfileCompletionEvaluator.Evaluate("non-binary", string.Empty, "en");
        Assert.True(status.Required);
        Assert.Contains(status.MissingFields, f => f.FieldId == ProfileCompletionEvaluator.FieldPreferredPronouns);
    }

    [Fact]
    public void Evaluate_Complete_ForMale()
    {
        var status = ProfileCompletionEvaluator.Evaluate("male", string.Empty, "en");
        Assert.False(status.Required);
        Assert.Empty(status.MissingFields);
    }

    [Fact]
    public void Evaluate_Complete_ForNonBinaryWithPronouns()
    {
        var status = ProfileCompletionEvaluator.Evaluate("non-binary", "they-them", "en");
        Assert.False(status.Required);
    }
}
