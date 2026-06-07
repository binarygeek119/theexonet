using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Theexonet.Api.Controllers;
using Theexonet.Api.Services;
using Theexonet.Api.Services.Market;
using Theexonet.Api.Services.CompanyLogo;
using Theexonet.Api.Services.OpenAi;
using Theexonet.Api.Services.ExonetAiAssetScan;
using Theexonet.Api.Services.AiImageQueue;
using Theexonet.Api.Services.AiImageQueue.Handlers;
using Theexonet.Api.Services.Foreverfall;
using Theexonet.Api.Services.LunarWeather;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Api.Services.TestingDummyFriends;
using Theexonet.Api.Services.VoidCorp;
using Theexonet.Core.Configuration;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Constants;
using Theexonet.Core.Services;
using Theexonet.Infrastructure;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Migrations;
using Theexonet.Infrastructure.Services;

var contentRootPath = Path.GetFullPath(AppContext.BaseDirectory);
var webRootPath = Path.Combine(contentRootPath, "html");
Directory.CreateDirectory(webRootPath);

foreach (var updatedSettingsPath in AppSettingsTemplateMerger.ApplyMissingKeys(contentRootPath))
{
    Console.WriteLine($"App settings: added missing keys from template -> {updatedSettingsPath}");
}

void FailStartup(string message, Exception? ex = null)
{
    Console.Error.WriteLine("theexonet API startup failed.");
    Console.Error.WriteLine(message);
    if (ex is not null)
    {
        Console.Error.WriteLine(ex);
    }

    Environment.Exit(1);
}

static void MigrateLegacyReporterPortraitAssets(string legacyRoot, string targetRoot)
{
    if (!Directory.Exists(legacyRoot))
    {
        return;
    }

    Directory.CreateDirectory(targetRoot);
    foreach (var slugDir in Directory.EnumerateDirectories(legacyRoot))
    {
        var slug = Path.GetFileName(slugDir);
        if (string.IsNullOrEmpty(slug))
        {
            continue;
        }

        var destDir = Path.Combine(targetRoot, slug);
        Directory.CreateDirectory(destDir);

        foreach (var sourceFile in Directory.EnumerateFiles(slugDir, "*.jpg"))
        {
            var fileName = Path.GetFileName(sourceFile);
            var destFile = Path.Combine(destDir, fileName);
            if (!File.Exists(destFile))
            {
                File.Copy(sourceFile, destFile, overwrite: false);
                continue;
            }

            var sourceInfo = new FileInfo(sourceFile);
            var destInfo = new FileInfo(destFile);
            if (sourceInfo.Length > destInfo.Length)
            {
                File.Copy(sourceFile, destFile, overwrite: true);
            }
        }
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRootPath,
    WebRootPath = webRootPath,
});

builder.Configuration.AddTheexonetDataJsonFiles(contentRootPath);

var dataRootPath = TheexonetDataPaths.Resolve(contentRootPath);
var imagesRootPath = TheexonetDataPaths.ResolveImagesRoot(contentRootPath, webRootPath);
var offworldNewsOptionsForPaths =
    builder.Configuration.GetSection(OffworldNewsOptions.SectionName).Get<OffworldNewsOptions>()
    ?? new OffworldNewsOptions();
var offworldNewsCacheRoot = TheexonetDataPaths.ResolveOffworldNewsCacheRoot(
    contentRootPath,
    webRootPath,
    offworldNewsOptionsForPaths.CacheDirectory);
var lunarWeatherOptionsForPaths =
    builder.Configuration.GetSection(LunarWeatherOptions.SectionName).Get<LunarWeatherOptions>()
    ?? new LunarWeatherOptions();
var lunarWeatherCacheRoot = TheexonetDataPaths.ResolveOffworldNewsCacheRoot(
    contentRootPath,
    webRootPath,
    lunarWeatherOptionsForPaths.CacheDirectory);
var foreverfallOptionsForPaths =
    builder.Configuration.GetSection(ForeverfallOptions.SectionName).Get<ForeverfallOptions>()
    ?? new ForeverfallOptions();
var foreverfallCacheRoot = TheexonetDataPaths.ResolveOffworldNewsCacheRoot(
    contentRootPath,
    webRootPath,
    foreverfallOptionsForPaths.CacheDirectory);
var voidCorpOptionsForPaths =
    builder.Configuration.GetSection(VoidCorpOptions.SectionName).Get<VoidCorpOptions>()
    ?? new VoidCorpOptions();
var voidCorpCacheRoot = TheexonetDataPaths.ResolveOffworldNewsCacheRoot(
    contentRootPath,
    webRootPath,
    voidCorpOptionsForPaths.CacheDirectory);
var hostingPaths = new TheexonetHostingPaths
{
    DataRoot = dataRootPath,
    ImagesRoot = imagesRootPath,
    OffworldNewsCacheRoot = offworldNewsCacheRoot,
    LunarWeatherCacheRoot = lunarWeatherCacheRoot,
    ForeverfallCacheRoot = foreverfallCacheRoot,
    VoidCorpCacheRoot = voidCorpCacheRoot,
    WebRoot = webRootPath,
};
TheexonetDataFileBootstrap.EnsureFromPublish(contentRootPath, offworldNewsOptionsForPaths.ReportersFile);
OffworldNewsReporterCatalog.Configure(contentRootPath, offworldNewsOptionsForPaths.ReportersFile);
LunarWeatherRelayCatalog.Configure(contentRootPath, lunarWeatherOptionsForPaths.RelaysFile);
builder.Services.AddSingleton(hostingPaths);

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
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.Configure<OffworldNewsOptions>(builder.Configuration.GetSection(OffworldNewsOptions.SectionName));
builder.Services.Configure<LunarWeatherOptions>(builder.Configuration.GetSection(LunarWeatherOptions.SectionName));
builder.Services.Configure<ForeverfallOptions>(builder.Configuration.GetSection(ForeverfallOptions.SectionName));
builder.Services.Configure<VoidCorpOptions>(builder.Configuration.GetSection(VoidCorpOptions.SectionName));
builder.Services.Configure<AiImageQueueOptions>(builder.Configuration.GetSection(AiImageQueueOptions.SectionName));
builder.Services.Configure<ExonetAiAssetScannerOptions>(builder.Configuration.GetSection(ExonetAiAssetScannerOptions.SectionName));
builder.Services.AddSingleton<OpenAiConnectionResolver>();
builder.Services.Configure<CompanyLogoOptions>(builder.Configuration.GetSection(CompanyLogoOptions.SectionName));
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<PlayerGameService>();
builder.Services.AddScoped<CompanyLogoQueueService>();
builder.Services.AddScoped<ICompanyLogoGenerator, CompanyLogoGenerator>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<AdminTestingActionsService>();
builder.Services.AddScoped<IPlayerModerationNotifier, PlayerModerationEmailNotifier>();
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
builder.Services.AddScoped<PlayerActivityService>();
builder.Services.AddScoped<ReporterFriendshipService>();
builder.Services.AddSingleton<OpenAiUsageTracker>();
builder.Services.AddSingleton<OpenAiBillingProbe>();
builder.Services.AddSingleton<OpenAiStatusDetailService>();
builder.Services.AddTransient<OpenAiUsageLoggingHandler>();
builder.Services.AddHttpClient(OpenAiOffworldNewsGenerator.HttpClientName, client =>
    {
        client.Timeout = TimeSpan.FromMinutes(3);
    })
    .AddHttpMessageHandler<OpenAiUsageLoggingHandler>();
builder.Services.AddScoped<AiImageQueueService>();
builder.Services.AddSingleton<AiImageQueuePublisher>();
builder.Services.AddScoped<IAiImageJobHandler, OnnEditionStoriesJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, OnnReporterAvatarJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, OnnReporterBackgroundJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, OnnStoryImageJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, ForeverfallIntakeJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, ForeverfallPortraitJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, LunarWeatherBulletinJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, VoidCorpProductJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, TestingDummyAvatarJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, TestingDummyBackgroundJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, TestingDummyLogoJobHandler>();
builder.Services.AddScoped<IAiImageJobHandler, CompanyLogoJobHandler>();
builder.Services.AddHostedService<AiImageQueueProcessorService>();
builder.Services.AddHostedService<AiImageQueueRecoveryService>();
builder.Services.AddSingleton<OffworldNewsReporterPortraitGenerator>(sp =>
    new OffworldNewsReporterPortraitGenerator(
        sp.GetRequiredService<OpenAiConnectionResolver>(),
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
        sp.GetRequiredService<TheexonetHostingPaths>().OffworldNewsReportersAssetsRoot,
        sp.GetRequiredService<ILogger<OffworldNewsReporterPortraitGenerator>>()));
builder.Services.AddSingleton<OffworldNewsService>();
builder.Services.AddSingleton<TestingDummyFriendsAssetGenerator>();
builder.Services.AddSingleton<TestingDummyFriendsAssetService>();
builder.Services.AddSingleton<OffworldNewsAdminSettingsStore>();
builder.Services.AddScoped<OffworldNewsReporterRosterAdminService>();
builder.Services.AddHostedService<OffworldNewsSchedulerService>();
builder.Services.AddSingleton<LunarWeatherAdminSettingsStore>();
builder.Services.AddSingleton<LunarWeatherService>();
builder.Services.AddHostedService<LunarWeatherSchedulerService>();
builder.Services.AddSingleton<ForeverfallAdminSettingsStore>();
builder.Services.AddSingleton<ForeverfallPenitentiaryService>();
builder.Services.AddHostedService<ForeverfallSchedulerService>();
builder.Services.AddSingleton<VoidCorpCatalogService>();
builder.Services.AddSingleton<VoidCorpProductImageGenerator>();
builder.Services.AddSingleton<VoidCorpMissingImageBackfillService>();
builder.Services.AddSingleton<VoidCorpAdminService>();
builder.Services.AddHostedService<VoidCorpSchedulerService>();
builder.Services.AddHostedService<ExonetAiAssetScannerService>();
builder.Services.AddSingleton<IProfileAvatarStorage>(sp =>
    new LocalProfileAvatarStorage(new ProfileAvatarStorageOptions
    {
        ImagesRootPath = imagesRootPath
    }));
builder.Services.AddSingleton<IProfileBackgroundStorage>(sp =>
    new LocalProfileBackgroundStorage(new ProfileBackgroundStorageOptions
    {
        ImagesRootPath = imagesRootPath
    }));
builder.Services.AddSingleton<ICompanyLogoStorage>(sp =>
    new LocalCompanyLogoStorage(new CompanyLogoStorageOptions
    {
        ImagesRootPath = imagesRootPath
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
builder.Services.AddHostedService<CompanyLogoMidnightSchedulerService>();
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

var jwtKey = builder.Configuration["Jwt:Key"] ?? "TheexonetDevSecretKey_ChangeInProduction_MinLength32!";
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
        "Database connection string is missing. Copy server/Theexonet.Api/appsettings.json.example " +
        "to /var/www/data/appsettings.json and set ConnectionStrings:DefaultConnection.");
}

WebApplication app;
try
{
    app = builder.Build();
}
catch (Exception ex)
{
    FailStartup(
        "Failed to build the application. Check appsettings.json and CSV files under /var/www/data.",
        ex);
    return;
}

app.UseMiddleware<Theexonet.Api.Middleware.TheexonetCorsMiddleware>();

app.Services.GetRequiredService<OffworldNewsAdminSettingsStore>().Load();
app.Services.GetRequiredService<LunarWeatherAdminSettingsStore>().Load();
app.Services.GetRequiredService<ForeverfallAdminSettingsStore>().Load();
app.Services.GetRequiredService<OpenAiUsageTracker>().Load();

var offworldNewsStartupOptions =
    app.Configuration.GetSection(OffworldNewsOptions.SectionName).Get<OffworldNewsOptions>()
    ?? new OffworldNewsOptions();
if (offworldNewsStartupOptions.Enabled)
{
    OffworldNewsStorageLayoutMigration.RunIfNeeded(offworldNewsCacheRoot, app.Logger);
    OffworldNewsReporterProfileMigration.RunIfNeeded(
        contentRootPath,
        offworldNewsStartupOptions.ReportersFile,
        app.Logger);
}

app.Logger.LogInformation(
    "Content root: {ContentRoot}. Web root: {WebRoot}. Data root: {DataRoot}. Offworld News cache: {OffworldNewsCache}. Profile uploads: {AvatarPath}. Profile backgrounds: {BackgroundPath}. Company logos: {CompanyLogoPath}",
    contentRootPath,
    webRootPath,
    dataRootPath,
    offworldNewsCacheRoot,
    Path.Combine(imagesRootPath, ProfileAvatarStorageOptions.RelativeFolder),
    Path.Combine(imagesRootPath, ProfileBackgroundStorageOptions.RelativeFolder),
    Path.Combine(imagesRootPath, CompanyLogoStorageOptions.RelativeFolder));

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
        "Live US market prices enabled (Yahoo Finance, refresh at API startup and UTC midnight). Items file: {ItemsFile}",
        Path.Combine(dataRootPath, marketOptions.ItemsFile));
}
else
{
    app.Logger.LogWarning("Live market disabled. Using mock supply prices.");
}

var tradeOptions = builder.Configuration.GetSection(TradeOptions.SectionName).Get<TradeOptions>() ?? new TradeOptions();
var tradeItemsPath = TheexonetDataPaths.ResolveFile(contentRootPath, tradeOptions.ItemsFile);
app.Logger.LogInformation(
    "Trade market items file: {ItemsFile}",
    tradeItemsPath);

var creditsOptions = builder.Configuration.GetSection(GameCreditsOptions.SectionName).Get<GameCreditsOptions>() ?? new GameCreditsOptions();
var creditsPath = TheexonetDataPaths.ResolveFile(contentRootPath, creditsOptions.CreditsFile);
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
    var hateSpeechPath = TheexonetDataPaths.ResolveFile(contentRootPath, hateSpeechOptions.TermsFile);
    var badLanguagePath = TheexonetDataPaths.ResolveFile(contentRootPath, hateSpeechOptions.BadLanguageFile);
    var politicalPath = TheexonetDataPaths.ResolveFile(contentRootPath, hateSpeechOptions.PoliticalTermsFile);
    var sexualPath = TheexonetDataPaths.ResolveFile(contentRootPath, hateSpeechOptions.SexualTermsFile);
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

app.UseMiddleware<Theexonet.Api.Middleware.DatabaseExceptionMiddleware>();

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

}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var hostingOptions = app.Configuration.GetSection(HostingOptions.SectionName).Get<HostingOptions>() ?? new HostingOptions();
var serveGameUi = hostingOptions.ServeGameUi ?? !app.Environment.IsProduction();
var resolvedHostingPaths = app.Services.GetRequiredService<TheexonetHostingPaths>();
var reportersAssetsRoot = resolvedHostingPaths.OffworldNewsReportersAssetsRoot;
Directory.CreateDirectory(reportersAssetsRoot);
ForeverfallStoragePaths.EnsureDirectories(foreverfallCacheRoot);
VoidCorpStoragePaths.EnsureDirectories(voidCorpCacheRoot);
Directory.CreateDirectory(Path.Combine(offworldNewsCacheRoot, OffworldNewsStoragePaths.ImagesFolder));
LunarWeatherStoragePaths.EnsureEditionDirectory(lunarWeatherCacheRoot);
Directory.CreateDirectory(imagesRootPath);
Directory.CreateDirectory(Path.Combine(imagesRootPath, ProfileAvatarStorageOptions.RelativeFolder));
Directory.CreateDirectory(Path.Combine(imagesRootPath, ProfileBackgroundStorageOptions.RelativeFolder));
Directory.CreateDirectory(Path.Combine(imagesRootPath, CompanyLogoStorageOptions.RelativeFolder));
var legacyReportersAssetsRoot = Path.Combine(webRootPath, "exonet", "offworld-news", "reporters");
MigrateLegacyReporterPortraitAssets(legacyReportersAssetsRoot, reportersAssetsRoot);

var reporterFileProviders = new List<IFileProvider>
{
    new PhysicalFileProvider(reportersAssetsRoot),
};
if (Directory.Exists(legacyReportersAssetsRoot))
{
    reporterFileProviders.Add(new PhysicalFileProvider(legacyReportersAssetsRoot));
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new CompositeFileProvider(reporterFileProviders),
    RequestPath = OffworldNewsReporterPaths.PublicReportersPath,
});

var testingDummyFriendsAssetsRoot = resolvedHostingPaths.TestingDummyFriendsAssetsRoot;
Directory.CreateDirectory(testingDummyFriendsAssetsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(testingDummyFriendsAssetsRoot),
    RequestPath = TestingDummyFriendsPaths.PublicRootPath,
});

var profileDefaultsRoot = Path.Combine(webRootPath, "images", "profile-defaults");
if (Directory.Exists(profileDefaultsRoot))
{
    // Must register before /images/profile uploads: that path is a prefix of /images/profile-defaults.
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(profileDefaultsRoot),
        RequestPath = ProfileAvatarPresets.PublicPath
    });
}

var offworldNewsPlaceholdersRoot = Path.Combine(webRootPath, "exonet", "offworld-news", "placeholders");
if (Directory.Exists(offworldNewsPlaceholdersRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(offworldNewsPlaceholdersRoot),
        RequestPath = "/exonet/offworld-news/placeholders"
    });
}

var foreverfallPlaceholdersRoot = Path.Combine(webRootPath, "exonet", "foreverfall-penitentiary", "placeholders");
if (Directory.Exists(foreverfallPlaceholdersRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(foreverfallPlaceholdersRoot),
        RequestPath = "/exonet/foreverfall-penitentiary/placeholders"
    });
}

var voidCorpPlaceholdersRoot = Path.Combine(webRootPath, "exonet", "voidcorp", "placeholders");
if (Directory.Exists(voidCorpPlaceholdersRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(voidCorpPlaceholdersRoot),
        RequestPath = "/exonet/voidcorp/placeholders"
    });
}

var voidCorpProductsRoot = Path.Combine(voidCorpCacheRoot, VoidCorpStoragePaths.ImagesFolder);
Directory.CreateDirectory(voidCorpProductsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(voidCorpProductsRoot),
    RequestPath = VoidCorpStoragePaths.PublicProductsPath
});

if (serveGameUi)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(offworldNewsCacheRoot, "images")),
        RequestPath = $"/{TheexonetDataPaths.OffworldNewsPublicPath}/images"
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(foreverfallCacheRoot, "images")),
        RequestPath = ForeverfallStoragePaths.PublicImagesPath
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(imagesRootPath, ProfileAvatarStorageOptions.RelativeFolder)),
        RequestPath = $"/{ProfileAvatarStorageOptions.PublicUrlPath}"
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(imagesRootPath, ProfileBackgroundStorageOptions.RelativeFolder)),
        RequestPath = $"/{ProfileBackgroundStorageOptions.PublicUrlPath}"
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(imagesRootPath, CompanyLogoStorageOptions.RelativeFolder)),
        RequestPath = $"/{CompanyLogoStorageOptions.PublicUrlPath}"
    });
    app.UseDefaultFiles();
    app.UseStaticFiles();
}
else
{
    app.Logger.LogInformation("API-only hosting: game UI disabled on this host.");
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(offworldNewsCacheRoot, "images")),
        RequestPath = $"/{TheexonetDataPaths.OffworldNewsPublicPath}/images"
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(foreverfallCacheRoot, "images")),
        RequestPath = ForeverfallStoragePaths.PublicImagesPath
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imagesRootPath),
        RequestPath = "/images"
    });
}

app.UseAuthentication();
app.UseMiddleware<Theexonet.Api.Middleware.PlayerActivityMiddleware>();
app.UseMiddleware<Theexonet.Api.Middleware.PlayerModerationMiddleware>();
app.UseAuthorization();
app.MapControllers();

var faviconPath = Path.Combine(webRootPath, "favicon.svg");
app.MapGet("/favicon.svg", () =>
    File.Exists(faviconPath)
        ? Results.File(faviconPath, "image/svg+xml")
        : Results.NotFound());
app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg", permanent: false));

if (serveGameUi)
{
    app.MapGet("/admin", () => Results.Redirect("/admin.html"));
    app.MapGet("/moderator", () => Results.Redirect("/moderator.html"));
    app.MapFallbackToFile("index.html");
}
else
{
    app.MapGet("/", () => Results.Redirect("/api/status"));
}

try
{
    app.Run();
}
catch (Exception ex)
{
    FailStartup("Unhandled error while running the API.", ex);
}
