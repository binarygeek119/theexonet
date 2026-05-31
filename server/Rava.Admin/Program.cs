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

    builder.Services.Configure<AdminPortalOptions>(
        builder.Configuration.GetSection(AdminPortalOptions.SectionName));

    var app = builder.Build();
    var portal = app.Services.GetRequiredService<IOptions<AdminPortalOptions>>().Value;
    var publicUrl = string.IsNullOrWhiteSpace(portal.PublicUrl)
        ? new AdminPortalOptions().PublicUrl.TrimEnd('/')
        : portal.PublicUrl.TrimEnd('/');

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        DefaultFileNames = ["admin.html", "index.html"]
    });
    app.UseStaticFiles();

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
