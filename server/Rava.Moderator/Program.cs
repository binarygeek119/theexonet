using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;

var contentRootPath = Path.GetFullPath(AppContext.BaseDirectory);

try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = contentRootPath,
    });

    builder.Configuration.AddRavaDataJsonFiles(contentRootPath);

    builder.Services.Configure<ModeratorPortalOptions>(
        builder.Configuration.GetSection(ModeratorPortalOptions.SectionName));

    var app = builder.Build();
    var portal = app.Services.GetRequiredService<IOptions<ModeratorPortalOptions>>().Value;
    var publicUrl = string.IsNullOrWhiteSpace(portal.PublicUrl)
        ? new ModeratorPortalOptions().PublicUrl.TrimEnd('/')
        : portal.PublicUrl.TrimEnd('/');

    var webRootPath = Path.Combine(contentRootPath, "wwwroot");
    IFileProvider fileProvider = app.Environment.WebRootFileProvider;
    if (Directory.Exists(webRootPath))
    {
        fileProvider = new CompositeFileProvider(
            new PhysicalFileProvider(webRootPath),
            app.Environment.WebRootFileProvider);
    }

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        DefaultFileNames = ["moderator.html", "index.html"]
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider
    });

    app.MapGet("/moderator", () => Results.Redirect("/moderator.html"));

    app.Logger.LogInformation(
        "theexonet moderator portal listening on {Urls} (public URL: {PublicUrl})",
        builder.Configuration["Urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:7050",
        publicUrl);

    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("theexonet moderator portal startup failed.");
    Console.Error.WriteLine(ex);
    Environment.Exit(1);
}
