using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Docs.Services;

var contentRootPath = Path.GetFullPath(AppContext.BaseDirectory);

try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = contentRootPath,
    });

    builder.Configuration.AddTheexonetDataJsonFiles(contentRootPath);

    builder.Services.Configure<DocsPortalOptions>(
        builder.Configuration.GetSection(DocsPortalOptions.SectionName));

    var app = builder.Build();
    var portal = app.Services.GetRequiredService<IOptions<DocsPortalOptions>>().Value;
    var publicUrl = string.IsNullOrWhiteSpace(portal.PublicUrl)
        ? new DocsPortalOptions().PublicUrl.TrimEnd('/')
        : portal.PublicUrl.TrimEnd('/');
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
        "theexonet docs portal listening on {Urls} (public URL: {PublicUrl}, content: {ContentRoot})",
        builder.Configuration["Urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:9000",
        publicUrl,
        contentRoot);

    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("theexonet docs portal startup failed.");
    Console.Error.WriteLine(ex);
    Environment.Exit(1);
}
