using Microsoft.Extensions.Options;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;

namespace Rava.Api.Services;

public class PlayerModerationEmailNotifier(
    IEmailService emailService,
    IOptions<EmailOptions> emailOptions) : IPlayerModerationNotifier
{
    public Task NotifyBanAsync(
        string toEmail,
        string username,
        PlayerBanDto ban,
        CancellationToken cancellationToken = default) =>
        emailService.SendAccountBanAsync(
            toEmail,
            username,
            ban.BanLevelLabel,
            ban.Reason ?? string.Empty,
            ban.IsPermanent,
            ban.ExpiresAt,
            emailOptions.Value.AppBaseUrl.TrimEnd('/'),
            cancellationToken);

    public Task NotifyWarningAsync(
        string toEmail,
        string username,
        PlayerWarningDto warning,
        CancellationToken cancellationToken = default) =>
        emailService.SendAccountWarningAsync(
            toEmail,
            username,
            warning.Reason,
            warning.ExpiresAt,
            emailOptions.Value.AppBaseUrl.TrimEnd('/'),
            cancellationToken);
}
