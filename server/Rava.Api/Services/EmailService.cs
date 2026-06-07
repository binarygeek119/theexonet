using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Rava.Core.Interfaces;

namespace Rava.Api.Services;

public class SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendPasswordResetAsync(
        string toEmail,
        string username,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var message = BuildPasswordResetMessage(settings, toEmail, username, resetUrl);

        await SendAsync(settings, message, cancellationToken);
        logger.LogInformation("Password reset email sent to {Email}", toEmail);
    }

    public async Task SendProfileFlagAsync(
        string toEmail,
        string username,
        string comment,
        string profileUrl,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var message = BuildProfileFlagMessage(settings, toEmail, username, comment, profileUrl);

        await SendAsync(settings, message, cancellationToken);
        logger.LogInformation("Profile flag email sent to {Email}", toEmail);
    }

    public async Task SendBanAppealToAdminAsync(
        string toEmail,
        string adminUsername,
        string playerUsername,
        string playerEmail,
        string banSummary,
        string message,
        string adminPortalUrl,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var mimeMessage = BuildBanAppealMessage(
            settings,
            toEmail,
            adminUsername,
            playerUsername,
            playerEmail,
            banSummary,
            message,
            adminPortalUrl);

        await SendAsync(settings, mimeMessage, cancellationToken);
        logger.LogInformation("Ban appeal email sent to admin {Email}", toEmail);
    }

    public async Task SendAccountBanAsync(
        string toEmail,
        string username,
        string banLevelLabel,
        string reason,
        bool isPermanent,
        DateTime? expiresAtUtc,
        string loginUrl,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var message = BuildAccountBanMessage(
            settings,
            toEmail,
            username,
            banLevelLabel,
            reason,
            isPermanent,
            expiresAtUtc,
            loginUrl);

        await SendAsync(settings, message, cancellationToken);
        logger.LogInformation("Account ban email sent to {Email}", toEmail);
    }

    public async Task SendAccountWarningAsync(
        string toEmail,
        string username,
        string reason,
        DateTime expiresAtUtc,
        string loginUrl,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var message = BuildAccountWarningMessage(
            settings,
            toEmail,
            username,
            reason,
            expiresAtUtc,
            loginUrl);

        await SendAsync(settings, message, cancellationToken);
        logger.LogInformation("Account warning email sent to {Email}", toEmail);
    }

    private async Task SendAsync(EmailOptions settings, MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        var secureSocketOptions = ResolveSecureSocketOptions(settings);
        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "SMTP send failed via {Host}:{Port} as {Username}. Check Email settings in appsettings.json.",
                settings.Host,
                settings.Port,
                settings.Username);
            throw;
        }
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(EmailOptions settings)
    {
        if (settings.UseSsl || settings.Port == 465)
        {
            return SecureSocketOptions.SslOnConnect;
        }

        if (settings.UseStartTls || settings.Port == 587)
        {
            return SecureSocketOptions.StartTls;
        }

        return SecureSocketOptions.Auto;
    }

    private static MimeMessage BuildPasswordResetMessage(EmailOptions settings, string toEmail, string username, string resetUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Reset your theexonet password";

        var body = $"""
            Hi {username},

            We received a request to reset your theexonet password.

            Open this link to choose a new password (expires in 1 hour):
            {resetUrl}

            If you did not request this, you can ignore this email.

            — theexonet Command
            """;

        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    private static MimeMessage BuildProfileFlagMessage(
        EmailOptions settings,
        string toEmail,
        string username,
        string comment,
        string profileUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Action required: update your theexonet profile";

        var body = $"""
            Hi {username},

            Your theexonet profile was flagged for review. Please remove or change the content called out below.

            Moderator comment:
            {comment}

            Open your profile to make changes:
            {profileUrl}

            After you save profile updates, the flag will be cleared automatically.

            — theexonet Command
            """;

        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    private static MimeMessage BuildBanAppealMessage(
        EmailOptions settings,
        string toEmail,
        string adminUsername,
        string playerUsername,
        string playerEmail,
        string banSummary,
        string message,
        string adminPortalUrl)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(toEmail));
        mimeMessage.Subject = $"Ban removal request from {playerUsername}";

        var body = $"""
            Hi {adminUsername},

            A banned player submitted a request to remove their ban.

            Player: {playerUsername}
            Email: {playerEmail}
            Ban: {banSummary}

            Message:
            {message}

            Review appeals in the admin portal:
            {adminPortalUrl}

            — theexonet Command
            """;

        mimeMessage.Body = new TextPart("plain") { Text = body };
        return mimeMessage;
    }

    private static MimeMessage BuildAccountBanMessage(
        EmailOptions settings,
        string toEmail,
        string username,
        string banLevelLabel,
        string reason,
        bool isPermanent,
        DateTime? expiresAtUtc,
        string loginUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Your theexonet account has been banned";

        var durationLine = isPermanent
            ? "Duration: Permanent (life ban)"
            : expiresAtUtc is null
                ? "Duration: Banned"
                : $"Duration: {banLevelLabel} — banned until {expiresAtUtc.Value:yyyy-MM-dd HH:mm} UTC";

        var reasonLine = string.IsNullOrWhiteSpace(reason)
            ? "Reason: No reason was provided."
            : $"Reason: {reason.Trim()}";

        var body = $"""
            Hi {username},

            Your theexonet account has been banned.

            {durationLine}
            {reasonLine}

            You will not be able to sign in or play until the ban ends. When you try to log in, you will see this ban notice in the game.

            Sign in page:
            {loginUrl}

            If you believe this ban was a mistake, you can submit a ban appeal from the login screen after signing in with your username and password.

            — theexonet Command
            """;

        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    private static MimeMessage BuildAccountWarningMessage(
        EmailOptions settings,
        string toEmail,
        string username,
        string reason,
        DateTime expiresAtUtc,
        string loginUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Account warning on theexonet";

        var reasonLine = string.IsNullOrWhiteSpace(reason)
            ? "Reason: No reason was provided."
            : $"Reason: {reason.Trim()}";

        var body = $"""
            Hi {username},

            Your theexonet account received a moderation warning.

            {reasonLine}
            Warning expires: {expiresAtUtc:yyyy-MM-dd HH:mm} UTC

            The next time you sign in, you must read and acknowledge this warning before you can continue playing.

            Sign in page:
            {loginUrl}

            Further violations may result in a temporary or permanent account ban.

            — theexonet Command
            """;

        message.Body = new TextPart("plain") { Text = body };
        return message;
    }
}

public class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendPasswordResetAsync(
        string toEmail,
        string username,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Email not configured. Password reset for {Username} ({Email}): {ResetUrl}",
            username,
            toEmail,
            resetUrl);
        return Task.CompletedTask;
    }

    public Task SendProfileFlagAsync(
        string toEmail,
        string username,
        string comment,
        string profileUrl,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Email not configured. Profile flag for {Username} ({Email}): {Comment} — {ProfileUrl}",
            username,
            toEmail,
            comment,
            profileUrl);
        return Task.CompletedTask;
    }

    public Task SendBanAppealToAdminAsync(
        string toEmail,
        string adminUsername,
        string playerUsername,
        string playerEmail,
        string banSummary,
        string message,
        string adminPortalUrl,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Email not configured. Ban appeal for {PlayerUsername} ({PlayerEmail}) to admin {AdminUsername} ({AdminEmail}): {Message} — {AdminPortalUrl}",
            playerUsername,
            playerEmail,
            adminUsername,
            toEmail,
            message,
            adminPortalUrl);
        return Task.CompletedTask;
    }

    public Task SendAccountBanAsync(
        string toEmail,
        string username,
        string banLevelLabel,
        string reason,
        bool isPermanent,
        DateTime? expiresAtUtc,
        string loginUrl,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Email not configured. Account ban for {Username} ({Email}): {BanLevel} — {Reason} — {LoginUrl}",
            username,
            toEmail,
            banLevelLabel,
            reason,
            loginUrl);
        return Task.CompletedTask;
    }

    public Task SendAccountWarningAsync(
        string toEmail,
        string username,
        string reason,
        DateTime expiresAtUtc,
        string loginUrl,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Email not configured. Account warning for {Username} ({Email}): {Reason} — expires {ExpiresAt} — {LoginUrl}",
            username,
            toEmail,
            reason,
            expiresAtUtc,
            loginUrl);
        return Task.CompletedTask;
    }
}
