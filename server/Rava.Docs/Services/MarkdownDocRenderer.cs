using Markdig;
using Rava.Core.Configuration;
using Rava.Core.Constants;

namespace Rava.Docs.Services;

public sealed class MarkdownDocRenderer(MarkdownDocCatalog catalog, DocsPortalOptions options)
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string RenderPage(string? slug)
    {
        if (!catalog.TryResolve(slug, out var entry, out var markdown))
        {
            return RenderShell(
                "Page not found",
                "<p>That documentation page does not exist.</p><p><a href=\"/\">Back to docs home</a></p>",
                slug);
        }

        var body = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        return RenderShell(entry!.Title, body, entry.Slug);
    }

    private string RenderShell(string title, string bodyHtml, string? activeSlug)
    {
        var navItems = catalog.Entries
            .Select(item =>
            {
                var href = item.Slug == "index" ? "/" : $"/{item.Slug}";
                var activeClass = string.Equals(item.Slug, activeSlug, StringComparison.OrdinalIgnoreCase)
                    || (string.IsNullOrEmpty(activeSlug) && item.Slug == "index")
                    ? " active"
                    : string.Empty;
                return $"""<li><a class="doc-nav-link{activeClass}" href="{href}">{Escape(item.Title)}</a></li>""";
            });

        var navHtml = string.Join('\n', navItems);
        var gameUrl = Escape(options.GameUrl.TrimEnd('/') + "/");

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{{Escape(title)}} — {{Escape(options.SiteTitle)}}</title>
  <link rel="stylesheet" href="/css/docs.css">
</head>
<body>
  <div class="docs-layout">
    <header class="docs-header">
      <div>
        <p class="docs-eyebrow">Reactive Asteroid Venturing Agency</p>
        <h1>{{Escape(options.SiteTitle)}}</h1>
        <p class="docs-version">{{Escape(GameVersion.Display)}}</p>
      </div>
      <nav class="docs-header-links" aria-label="Related sites">
        <a href="{{gameUrl}}">Play RAVA</a>
      </nav>
    </header>
    <div class="docs-body">
      <aside class="docs-nav" aria-label="Documentation pages">
        <ul>
        {{navHtml}}
        </ul>
      </aside>
      <main class="docs-content">
        {{bodyHtml}}
      </main>
    </div>
  </div>
</body>
</html>
""";
    }

    private static string Escape(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
