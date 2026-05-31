using Microsoft.Extensions.Options;
using Rava.Core.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ModeratorPortalOptions>(
    builder.Configuration.GetSection(ModeratorPortalOptions.SectionName));

var app = builder.Build();
var portal = app.Services.GetRequiredService<IOptions<ModeratorPortalOptions>>().Value;

app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = ["moderator.html", "index.html"]
});
app.UseStaticFiles();

app.MapGet("/moderator", () => Results.Redirect("/moderator.html"));

app.Logger.LogInformation(
    "RAVA moderator portal listening on {Urls} (public URL: {PublicUrl})",
    builder.Configuration["Urls"] ?? "http://0.0.0.0:7050",
    portal.PublicUrl.TrimEnd('/'));

app.Run();
