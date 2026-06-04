using Rava.Core.Dtos;

namespace Rava.Core.Interfaces;

public interface IPlayerModerationNotifier
{
    Task NotifyBanAsync(
        string toEmail,
        string username,
        PlayerBanDto ban,
        CancellationToken cancellationToken = default);

    Task NotifyWarningAsync(
        string toEmail,
        string username,
        PlayerWarningDto warning,
        CancellationToken cancellationToken = default);
}
