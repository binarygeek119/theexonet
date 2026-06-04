using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OpenAiConnectionResolverTests
{
    [Fact]
    public void ApiKey_prefers_global_OpenAi_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ApiKey"] = "sk-global",
                ["OffworldNews:ApiKey"] = "sk-legacy-news",
            })
            .Build();

        var resolver = new OpenAiConnectionResolver(
            Options.Create(new OpenAiOptions { ApiKey = "sk-global" }),
            configuration);

        Assert.Equal("sk-global", resolver.ApiKey);
    }

    [Fact]
    public void ApiKey_falls_back_to_legacy_OffworldNews_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OffworldNews:ApiKey"] = "sk-legacy-news",
            })
            .Build();

        var resolver = new OpenAiConnectionResolver(
            Options.Create(new OpenAiOptions()),
            configuration);

        Assert.Equal("sk-legacy-news", resolver.ApiKey);
    }
}
