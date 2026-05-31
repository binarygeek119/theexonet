using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Docs.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DocsPortalOptions>(
    builder.Configuration.GetSection(DocsPortalOptions.SectionName));

var app = builder.Build();
var portal = app.Services.GetRequiredService<IOptions<DocsPortalOptions>>().Value;
var contentRoot = Path.GetFullPath(
    Path.Combine(app.Environment.ContentRootPath, portal.ContentPath));

var catalog = new MarkdownDocCatalog(contentRoot);
var renderer = new MarkdownDocRenderer(catalog, portal);

app.UseStaticFiles();

app.MapGet("/", () => Results.Content(renderer.RenderPage("index"), "text/html; charset=utf-8"));
app.MapGet("/{slug}", (string slug) =>
{
    if (!catalog.TryResolve(slug, out _, out _))
    {
        return Results.Content(
            renderer.RenderPage(null),
            "text/html; charset=utf-8",
            statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Content(renderer.RenderPage(slug), "text/html; charset=utf-8");
});

app.Logger.LogInformation(
    "RAVA docs portal listening on {Urls} (public URL: {PublicUrl}, content: {ContentRoot})",
    builder.Configuration["Urls"] ?? "http://0.0.0.0:9000",
    portal.PublicUrl.TrimEnd('/'),
    contentRoot);

app.Run();
