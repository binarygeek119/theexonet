using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Rava.Api.Controllers;
using Rava.Api.Services;
using Rava.Api.Services.Market;
using Rava.Core.Configuration;
using Rava.Core.Interfaces;
using Rava.Core.Services;
using Rava.Infrastructure;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Migrations;
using Rava.Infrastructure.Services;

var contentRootPath = Directory.GetCurrentDirectory();
var webRootPath = Path.Combine(contentRootPath, "html");
Directory.CreateDirectory(webRootPath);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = webRootPath,
});

builder.Configuration.AddJsonFile("credits.json", optional: false, reloadOnChange: true);

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<MarketOptions>(builder.Configuration.GetSection(MarketOptions.SectionName));
builder.Services.Configure<GameCreditsOptions>(builder.Configuration.GetSection(GameCreditsOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<ModeratorOptions>(builder.Configuration.GetSection(ModeratorOptions.SectionName));
builder.Services.Configure<HateSpeechOptions>(builder.Configuration.GetSection(HateSpeechOptions.SectionName));
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
builder.Services.AddScoped<HateSpeechScanner>();
builder.Services.AddScoped<PlayerWarningService>();
builder.Services.AddScoped<MessageModerationService>();
builder.Services.AddScoped<GameCreditsConfigService>();
builder.Services.AddScoped<SpecialEventService>();
builder.Services.AddScoped<PlayerProfileUpgrader>();
builder.Services.AddSingleton<IProfileAvatarStorage>(sp =>
    new LocalProfileAvatarStorage(new ProfileAvatarStorageOptions
    {
        WebRootPath = webRootPath
    }));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = ProfileAvatarUploadLimits.MaxBytes;
});
builder.Services.AddScoped<IDataMigration, ProfileDefaultsMigration>();
builder.Services.AddScoped<IDataMigration, ProfileNumberMigration>();
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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing. Copy server/Rava.Api/appsettings.json.example " +
        "to appsettings.json and set ConnectionStrings:DefaultConnection.");
}

var app = builder.Build();

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
        "Live US market prices enabled (Yahoo Finance, refresh at UTC midnight). Symbols: CAT, XOM, JNJ, QCOM");
}
else
{
    app.Logger.LogWarning("Live market disabled. Using mock supply prices.");
}

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
        throw new InvalidOperationException(
            "Could not connect to PostgreSQL or create the database schema. " +
            "Verify ConnectionStrings:DefaultConnection in appsettings.json.",
            ex);
    }

    try
    {
        Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "profiles"));
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Could not create {Path.Combine(webRootPath, "uploads", "profiles")}. " +
            "Ensure the service user can write under html/uploads/.",
            ex);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<Rava.Api.Middleware.PlayerBanMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/admin", () => Results.Redirect("/admin.html"));
app.MapGet("/moderator", () => Results.Redirect("/moderator.html"));
app.MapFallbackToFile("index.html");

app.Run();
