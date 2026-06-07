using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;

var contentRootPath = Path.GetFullPath(AppContext.BaseDirectory);

try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = contentRootPath,
    });

    builder.Configuration.AddTheexonetDataJsonFiles(contentRootPath);

    builder.Services.Configure<AdminPortalOptions>(
        builder.Configuration.GetSection(AdminPortalOptions.SectionName));

    var app = builder.Build();
    var portal = app.Services.GetRequiredService<IOptions<AdminPortalOptions>>().Value;
    var publicUrl = string.IsNullOrWhiteSpace(portal.PublicUrl)
        ? new AdminPortalOptions().PublicUrl.TrimEnd('/')
        : portal.PublicUrl.TrimEnd('/');

    // Prefer files on disk (deploy rsync) but fall back to the publish manifest when a
    // page is missing — wwwroot is shared with Theexonet.Status so admin.html may not exist yet.
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
        DefaultFileNames = ["admin.html", "index.html"]
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider
    });

    app.MapGet("/admin", () => Results.Redirect("/admin.html"));

    app.Logger.LogInformation(
        "theexonet admin portal listening on {Urls} (public URL: {PublicUrl})",
        builder.Configuration["Urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:7000",
        publicUrl);

    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("theexonet admin portal startup failed.");
    Console.Error.WriteLine(ex);
    Environment.Exit(1);
}
