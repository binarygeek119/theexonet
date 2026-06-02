using Rava.Core.Services;

namespace Rava.Core.Tests;

public class CompanyLogoPromptsTests
{
    [Fact]
    public void Build_requests_transparent_corporate_emblem_without_text()
    {
        var prompt = CompanyLogoPrompts.Build(
            "Stellar Mining Co.",
            "binarygeek119",
            "Ready to mine.",
            "Gaming",
            "Epic Rap Battles of History",
            "Synthwave");

        Assert.Contains("transparent", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("corporate logo", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no text", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stellar Mining Co.", prompt, StringComparison.Ordinal);
    }
}
