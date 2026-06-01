using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Rava.Api.Controllers;
using Rava.Api.Services;
using Rava.Api.Services.Market;
using Rava.Api.Services.OffworldNews;
using Rava.Core.Configuration;
using Rava.Core.Interfaces;
using Rava.Core.Services;
using Rava.Infrastructure;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Migrations;
using Rava.Infrastructure.Services;

var contentRootPath = Path.GetFullPath(AppContext.BaseDirectory);
var webRootPath = Path.Combine(contentRootPath, "html");
Directory.CreateDirectory(webRootPath);

void FailStartup(string message, Exception? ex = null)
{
    Console.Error.WriteLine("RAVA API startup failed.");
    Console.Error.WriteLine(message);
    if (ex is not null)
    {
        Console.Error.WriteLine(ex);
    }

    Environment.Exit(1);
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRootPath,
    WebRootPath = webRootPath,
});

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<MarketOptions>(builder.Configuration.GetSection(MarketOptions.SectionName));
builder.Services.Configure<TradeOptions>(builder.Configuration.GetSection(TradeOptions.SectionName));
builder.Services.Configure<GameCreditsOptions>(builder.Configuration.GetSection(GameCreditsOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<ModeratorOptions>(builder.Configuration.GetSection(ModeratorOptions.SectionName));
builder.Services.Configure<HateSpeechOptions>(builder.Configuration.GetSection(HateSpeechOptions.SectionName));
builder.Services.Configure<ModeratorPortalOptions>(builder.Configuration.GetSection(ModeratorPortalOptions.SectionName));
builder.Services.Configure<AdminPortalOptions>(builder.Configuration.GetSection(AdminPortalOptions.SectionName));
builder.Services.Configure<HostingOptions>(builder.Configuration.GetSection(HostingOptions.SectionName));
builder.Services.Configure<OffworldNewsOptions>(builder.Configuration.GetSection(OffworldNewsOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<PlayerGameService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<PlayerBanService>();
builder.Services.AddScoped<StaffModerationPolicy>();
builder.Services.AddScoped<BanAppealService>();
builder.Services.AddScoped<StaffMessageService>();
builder.Services.AddScoped<PlayerMessageService>();
builder.Services.AddScoped<PeerMessageService>();
builder.Services.AddScoped<PlayerToStaffMessageService>();
builder.Services.AddScoped<MessageLogService>();
builder.Services.AddSingleton<HateSpeechTermsProvider>();
builder.Services.AddSingleton<IMarketItemsCatalog, MarketItemsProvider>();
builder.Services.AddSingleton<ITradeItemsCatalog, TradeItemsProvider>();
builder.Services.AddScoped<HateSpeechScanner>();
builder.Services.AddScoped<PlayerWarningService>();
builder.Services.AddScoped<MessageModerationService>();
builder.Services.AddScoped<GameCreditsConfigService>();
builder.Services.AddSingleton<GameCreditsProvider>();
builder.Services.AddSingleton<IGameCreditsConfig>(sp => sp.GetRequiredService<GameCreditsProvider>());
builder.Services.AddScoped<SpecialEventService>();
builder.Services.AddScoped<PlayerProfileUpgrader>();
builder.Services.AddScoped<CompanyNameService>();
builder.Services.AddScoped<TradeAuctionService>();
builder.Services.AddScoped<PublicProfileService>();
builder.Services.AddHttpClient(OpenAiOffworldNewsGenerator.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddSingleton<OffworldNewsService>();
builder.Services.AddSingleton<IProfileAvatarStorage>(sp =>
    new LocalProfileAvatarStorage(new ProfileAvatarStorageOptions
    {
        WebRootPath = webRootPath
    }));
builder.Services.AddSingleton<IProfileBackgroundStorage>(sp =>
    new LocalProfileBackgroundStorage(new ProfileBackgroundStorageOptions
    {
        WebRootPath = webRootPath
    }));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = ProfileBackgroundUploadLimits.MaxBytes;
});
builder.Services.AddScoped<IDataMigration, ProfileDefaultsMigration>();
builder.Services.AddScoped<IDataMigration, ProfileNumberMigration>();
builder.Services.AddScoped<IDataMigration, CompanyNameMigration>();
builder.Services.AddScoped<PlayerDataMigrationRunner>();
builder.Services.AddSingleton<IMineSimulationService, MineSimulationService>();
builder.Services.AddSingleton<IStarterMineGenerator, StarterMineGenerator>();
builder.Services.AddSingleton<MockMarketGenerator>();
builder.Services.AddHttpClient<YahooFinanceMarketDataProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddSingleton<FallbackMarketDataProvider>();
builder.Services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<FallbackMarketDataProvider>());
builder.Services.AddHostedService<MarketMidnightRefreshService>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.Enabled && !string.IsNullOrWhiteSpace(emailOptions.Host))
{
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, LoggingEmailService>();
}

var jwtKey = builder.Configuration["Jwt:Key"] ?? "RavaDevSecretKey_ChangeInProduction_MinLength32!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.Requirements.Add(new AdminRequirement()));
    options.AddPolicy("Moderator", policy => policy.Requirements.Add(new ModeratorRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, ModeratorAuthorizationHandler>();
builder.Services.AddSingleton<ServerRuntimeInfo>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    FailStartup(
        "Database connection string is missing. Copy server/Rava.Api/appsettings.json.example " +
        "to /var/www/publish/appsettings.json and set ConnectionStrings:DefaultConnection.");
}

WebApplication app;
try
{
    app = builder.Build();
}
catch (Exception ex)
{
    FailStartup(
        "Failed to build the application. Check appsettings.json, credits.csv, and deploy files under /var/www/publish.",
        ex);
    return;
}

app.Logger.LogInformation(
    "Content root: {ContentRoot}. Web root: {WebRoot}. Profile uploads: {AvatarPath}. Profile backgrounds: {BackgroundPath}",
    contentRootPath,
    webRootPath,
    Path.Combine(webRootPath, ProfileAvatarStorageOptions.RelativeFolder),
    Path.Combine(webRootPath, ProfileBackgroundStorageOptions.RelativeFolder));

try
{
    var parsedConnection = new NpgsqlConnectionStringBuilder(connectionString);
    app.Logger.LogInformation(
        "PostgreSQL target: {Host}:{Port}/{Database} as {Username}",
        parsedConnection.Host,
        parsedConnection.Port,
        parsedConnection.Database,
        parsedConnection.Username);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not parse PostgreSQL connection string for startup logging.");
}

if (emailOptions.Enabled && !string.IsNullOrWhiteSpace(emailOptions.Host)){
    app.Logger.LogInformation(
        "SMTP email enabled via {Host}:{Port} ({FromAddress})",
        emailOptions.Host,
        emailOptions.Port,
        emailOptions.FromAddress);
}
else
{
    app.Logger.LogWarning(
        "Email disabled. Password reset links are logged to the API console only. Set Email:Enabled=true in appsettings.json.");
}

var marketOptions = builder.Configuration.GetSection(MarketOptions.SectionName).Get<MarketOptions>() ?? new MarketOptions();
if (marketOptions.UseLiveData)
{
    app.Logger.LogInformation(
        "Live US market prices enabled (Yahoo Finance, refresh at UTC midnight). Items file: {ItemsFile}",
        Path.Combine(contentRootPath, marketOptions.ItemsFile));
}
else
{
    app.Logger.LogWarning("Live market disabled. Using mock supply prices.");
}

var tradeOptions = builder.Configuration.GetSection(TradeOptions.SectionName).Get<TradeOptions>() ?? new TradeOptions();
var tradeItemsPath = Path.Combine(contentRootPath, tradeOptions.ItemsFile);
app.Logger.LogInformation(
    "Trade market items file: {ItemsFile}",
    tradeItemsPath);

var creditsOptions = builder.Configuration.GetSection(GameCreditsOptions.SectionName).Get<GameCreditsOptions>() ?? new GameCreditsOptions();
var creditsPath = Path.Combine(contentRootPath, creditsOptions.CreditsFile);
app.Logger.LogInformation("Game credits spreadsheet: {CreditsFile}", creditsPath);

var configuredAdminUsernames = app.Configuration
    .GetSection(AdminOptions.SectionName)
    .Get<AdminOptions>()?.Usernames ?? [];
if (configuredAdminUsernames.Length == 0)
{
    app.Logger.LogWarning(
        "Admin portal: no usernames configured. Set Admin:Usernames in appsettings.json.");
}
else
{
    app.Logger.LogInformation(
        "Admin portal enabled for: {AdminUsernames}",
        string.Join(", ", configuredAdminUsernames));
}

var configuredModeratorUsernames = app.Configuration
    .GetSection(ModeratorOptions.SectionName)
    .Get<ModeratorOptions>()?.Usernames ?? [];
if (configuredModeratorUsernames.Length == 0)
{
    app.Logger.LogWarning(
        "Moderator portal: no usernames configured. Set Moderator:Usernames in appsettings.json.");
}
else
{
    app.Logger.LogInformation(
        "Moderator portal enabled for: {ModeratorUsernames}",
        string.Join(", ", configuredModeratorUsernames));
}

var hateSpeechOptions = app.Configuration.GetSection(HateSpeechOptions.SectionName).Get<HateSpeechOptions>() ?? new HateSpeechOptions();
if (!hateSpeechOptions.Enabled)
{
    app.Logger.LogWarning("Hate speech scanner disabled. Set HateSpeech:Enabled=true in appsettings.json.");
}
else
{
    var termsProvider = app.Services.GetRequiredService<HateSpeechTermsProvider>();
    var hateSpeechPath = Path.Combine(contentRootPath, hateSpeechOptions.TermsFile);
    var badLanguagePath = Path.Combine(contentRootPath, hateSpeechOptions.BadLanguageFile);
    var politicalPath = Path.Combine(contentRootPath, hateSpeechOptions.PoliticalTermsFile);
    var sexualPath = Path.Combine(contentRootPath, hateSpeechOptions.SexualTermsFile);
    var (hateSpeechCount, badLanguageCount, politicalCount, sexualCount) = termsProvider.GetTermCounts();
    var termCount = termsProvider.GetTerms().Count;
    app.Logger.LogInformation(
        "Hate speech scanner enabled with {TermCount} terms ({HateSpeechCount} hate speech, {BadLanguageCount} bad language, {PoliticalCount} political, {SexualCount} sexual) from {HateSpeechPath}, {BadLanguagePath}, {PoliticalPath}, and {SexualPath}",
        termCount,
        hateSpeechCount,
        badLanguageCount,
        politicalCount,
        sexualCount,
        hateSpeechPath,
        badLanguagePath,
        politicalPath,
        sexualPath);
}

app.UseMiddleware<Rava.Api.Middleware.DatabaseExceptionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.EnsureCreated();
        await DatabaseSchemaUpdater.ApplyAsync(db);
        await scope.ServiceProvider.GetRequiredService<PlayerDataMigrationRunner>()
            .RunPendingAsync();
    }
    catch (Exception ex)
    {
        FailStartup(
            "Could not connect to PostgreSQL or create the database schema. " +
            "Verify ConnectionStrings:DefaultConnection in appsettings.json.",
            ex);
    }

    try
    {
        Directory.CreateDirectory(Path.Combine(webRootPath, ProfileAvatarStorageOptions.RelativeFolder));
        Directory.CreateDirectory(Path.Combine(webRootPath, ProfileBackgroundStorageOptions.RelativeFolder));
    }
    catch (Exception ex)
    {
        FailStartup(
            $"Could not create profile image folders under {webRootPath}. " +
            "Ensure www-data can write under html/images/. Run: sudo chown -R www-data:www-data /var/www/publish",
            ex);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

var hostingOptions = app.Configuration.GetSection(HostingOptions.SectionName).Get<HostingOptions>() ?? new HostingOptions();
var serveGameUi = hostingOptions.ServeGameUi ?? !app.Environment.IsProduction();

if (serveGameUi)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}
else
{
    app.Logger.LogInformation("API-only hosting: game UI disabled on this host.");
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(webRootPath, "images")),
        RequestPath = "/images"
    });
}

app.UseAuthentication();
app.UseMiddleware<Rava.Api.Middleware.PlayerBanMiddleware>();
app.UseAuthorization();
app.MapControllers();

if (serveGameUi)
{
    app.MapGet("/admin", () => Results.Redirect("/admin.html"));
    app.MapGet("/moderator", () => Results.Redirect("/moderator.html"));
    app.MapFallbackToFile("index.html");
}
else
{
    app.MapGet("/", () => Results.Content(
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>RAVA API</title>
        </head>
        <body>API status is OK</body>
        </html>
        """,
        "text/html; charset=utf-8"));
}

try
{
    app.Run();
}
catch (Exception ex)
{
    FailStartup("Unhandled error while running the API.", ex);
}
