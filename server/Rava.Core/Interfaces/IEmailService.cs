namespace Rava.Core.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string username, string resetUrl, CancellationToken cancellationToken = default);

    Task SendProfileFlagAsync(
        string toEmail,
        string username,
        string comment,
        string profileUrl,
        CancellationToken cancellationToken = default);

    Task SendBanAppealToAdminAsync(
        string toEmail,
        string adminUsername,
        string playerUsername,
        string playerEmail,
        string banSummary,
        string message,
        string adminPortalUrl,
        CancellationToken cancellationToken = default);
}
