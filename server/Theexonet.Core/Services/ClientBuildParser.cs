using System.Text.RegularExpressions;

namespace Theexonet.Core.Services;

public static partial class ClientBuildParser
{
    [GeneratedRegex(
        """<meta\s+name=["']theexonet-html-build["']\s+content=["']([^"']*)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlBuildMetaRegex();

    public static string? ParseHtmlBuild(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = HtmlBuildMetaRegex().Match(html);
        if (!match.Success)
        {
            return null;
        }

        var build = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(build) ? null : build;
    }
}
