using System.Text.Json;

namespace Theexonet.Api.Services;

public sealed class ServerRuntimeInfo
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DateTime StartedUtc { get; }
    public DateTime FirstRunUtc { get; }

    public ServerRuntimeInfo(ILogger<ServerRuntimeInfo> logger)
    {
        StartedUtc = DateTime.UtcNow;
        var path = Path.Combine(AppContext.BaseDirectory, "server-runtime.json");
        FirstRunUtc = LoadOrCreateFirstRunUtc(path);

        logger.LogInformation(
            "theexonet API process started at {StartedUtc:u}. First recorded run: {FirstRunUtc:u}",
            StartedUtc,
            FirstRunUtc);

        if (FirstRunUtc == StartedUtc || (StartedUtc - FirstRunUtc).TotalSeconds < 2)
        {
            logger.LogInformation(
                "theexonet API server first run logged at {FirstRunUtc:u} UTC",
                FirstRunUtc);
        }
    }

    public double UptimeSeconds => (DateTime.UtcNow - StartedUtc).TotalSeconds;

    private static DateTime LoadOrCreateFirstRunUtc(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var record = JsonSerializer.Deserialize<ServerRuntimeRecord>(json);
                if (record?.FirstRunUtc is DateTime stored)
                {
                    return DateTime.SpecifyKind(stored, DateTimeKind.Utc);
                }
            }
        }
        catch
        {
            // Fall through and recreate the record below.
        }

        var firstRunUtc = DateTime.UtcNow;
        try
        {
            var record = new ServerRuntimeRecord(firstRunUtc);
            File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
        }
        catch
        {
            // Startup should continue even if the runtime file cannot be written.
        }

        return firstRunUtc;
    }

    private sealed record ServerRuntimeRecord(DateTime FirstRunUtc);
}
