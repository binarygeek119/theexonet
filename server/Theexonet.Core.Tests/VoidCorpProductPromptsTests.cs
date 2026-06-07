using Theexonet.Core.Enums;
using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class VoidCorpProductPromptsTests
{
    [Theory]
    [InlineData(nameof(SupplyType.DrillBits), "drill bit", "cutter")]
    [InlineData(nameof(SupplyType.FuelCells), "fuel cell", "propellant")]
    [InlineData(nameof(SupplyType.LifeSupport), "life support", "O2 scrubber")]
    [InlineData(nameof(SupplyType.CommModules), "communications", "antenna")]
    public void BuildImagePrompt_uses_item_specific_visual_theme(string slug, string expectedA, string expectedB)
    {
        var prompt = VoidCorpProductPrompts.BuildImagePrompt(
            slug,
            "VoidCorp Test Item",
            "Boosts mining speed",
            "Precision consumables",
            "#b38033");

        Assert.Contains(expectedA, prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedB, prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VoidCorp Test Item", prompt);
        Assert.Contains("#b38033", prompt);
        Assert.Contains("no text", prompt);
    }

    [Fact]
    public void BuildImagePrompt_falls_back_for_unknown_slug()
    {
        var prompt = VoidCorpProductPrompts.BuildImagePrompt(
            "UnknownWidget",
            "Mystery Widget",
            "Supports frontier mining operations",
            "Industrial-grade equipment");

        Assert.Contains("Mystery Widget", prompt);
        Assert.Contains("industrial asteroid mining supply hardware", prompt);
    }
}
