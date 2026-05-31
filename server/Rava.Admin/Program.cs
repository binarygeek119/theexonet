using Microsoft.Extensions.Options;
using Rava.Core.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AdminPortalOptions>(
    builder.Configuration.GetSection(AdminPortalOptions.SectionName));

var app = builder.Build();
var portal = app.Services.GetRequiredService<IOptions<AdminPortalOptions>>().Value;

app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = ["admin.html", "index.html"]
});
app.UseStaticFiles();

app.MapGet("/admin", () => Results.Redirect("/admin.html"));

app.Logger.LogInformation(
    "RAVA admin portal listening on {Urls} (public URL: {PublicUrl})",
    builder.Configuration["Urls"] ?? "http://0.0.0.0:7000",
    portal.PublicUrl.TrimEnd('/'));

app.Run();
