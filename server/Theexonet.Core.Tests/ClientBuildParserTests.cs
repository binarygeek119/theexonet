using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class ClientBuildParserTests
{
    [Fact]
    public void ParseHtmlBuild_ReadsMetaTag()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
              <meta name="theexonet-html-build" content="20260607-live-updates">
            </head>
            </html>
            """;

        var build = ClientBuildParser.ParseHtmlBuild(html);

        Assert.Equal("20260607-live-updates", build);
    }

    [Fact]
    public void ParseHtmlBuild_ReturnsNullWhenMissing()
    {
        Assert.Null(ClientBuildParser.ParseHtmlBuild("<html></html>"));
        Assert.Null(ClientBuildParser.ParseHtmlBuild(null));
    }
}
