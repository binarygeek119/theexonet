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

    builder.Services.Configure<AdminPortalOptions>(
        builder.Configuration.GetSection(AdminPortalOptions.SectionName));

    var app = builder.Build();
    var portal = app.Services.GetRequiredService<IOptions<AdminPortalOptions>>().Value;
    var publicUrl = string.IsNullOrWhiteSpace(portal.PublicUrl)
        ? new AdminPortalOptions().PublicUrl.TrimEnd('/')
        : portal.PublicUrl.TrimEnd('/');

    // Serve every file under publish/wwwroot from disk so deploy rsync works without
    // rebuilding the static web assets manifest baked into Rava.Admin.dll.
    var webRootPath = Path.Combine(contentRootPath, "wwwroot");
    var webRoot = Directory.Exists(webRootPath)
        ? new PhysicalFileProvider(webRootPath)
        : app.Environment.WebRootFileProvider;

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = webRoot,
        DefaultFileNames = ["admin.html", "index.html"]
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = webRoot
    });

    app.MapGet("/admin", () => Results.Redirect("/admin.html"));

    app.Logger.LogInformation(
        "RAVA admin portal listening on {Urls} (public URL: {PublicUrl})",
        builder.Configuration["Urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:7000",
        publicUrl);

    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("RAVA admin portal startup failed.");
    Console.Error.WriteLine(ex);
    Environment.Exit(1);
}
