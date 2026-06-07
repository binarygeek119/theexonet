using Microsoft.Extensions.Logging;

namespace Theexonet.Infrastructure.Services;

internal static class MessageAuditLogger
{
    public static void LogSent(
        ILogger logger,
        string channel,
        string from,
        string to,
        Guid messageId,
        string body)
    {
        logger.LogInformation(
            "Message sent {Channel} from {From} to {To} id {MessageId}: {Body}",
            channel,
            from,
            to,
            messageId,
            body);
    }
}
