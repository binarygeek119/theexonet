using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services;

public sealed class ClientBuildInfo : IHostedService, IDisposable
{
    private readonly ILiveUpdateBroadcaster _broadcaster;
    private readonly ILogger<ClientBuildInfo> _logger;
    private readonly string _indexPath;
    private readonly object _sync = new();
    private FileSystemWatcher? _watcher;
    private string _htmlBuild = "";

    public ClientBuildInfo(ILiveUpdateBroadcaster broadcaster, ILogger<ClientBuildInfo> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
        _indexPath = Path.Combine(AppContext.BaseDirectory, "html", "index.html");
    }

    public string HtmlBuild
    {
        get
        {
            lock (_sync)
            {
                return _htmlBuild;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        RefreshBuild(publishIfChanged: false);
        StartWatcher();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _watcher?.Dispose();

    private void StartWatcher()
    {
        try
        {
            var directory = Path.GetDirectoryName(_indexPath);
            var fileName = Path.GetFileName(_indexPath);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !File.Exists(_indexPath))
            {
                _logger.LogWarning("Client build watcher skipped; index.html not found at {Path}", _indexPath);
                return;
            }

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _watcher.Changed += OnIndexChanged;
            _watcher.Created += OnIndexChanged;
            _watcher.Renamed += OnIndexChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start client build watcher for {Path}", _indexPath);
        }
    }

    private void OnIndexChanged(object sender, FileSystemEventArgs e) =>
        RefreshBuild(publishIfChanged: true);

    private void RefreshBuild(bool publishIfChanged)
    {
        string? previous;
        string? next;

        lock (_sync)
        {
            previous = string.IsNullOrWhiteSpace(_htmlBuild) ? null : _htmlBuild;
            next = ReadBuildFromDisk();
            _htmlBuild = next ?? "";
        }

        if (!publishIfChanged || string.IsNullOrWhiteSpace(next) || string.Equals(previous, next, StringComparison.Ordinal))
        {
            return;
        }

        _logger.LogInformation("Client HTML build changed from {Previous} to {Next}", previous ?? "(none)", next);
        _broadcaster.PublishGlobal(new LiveUpdateEventDto(LiveUpdateTypes.ClientBuild, HtmlBuild: next));
    }

    private string? ReadBuildFromDisk()
    {
        try
        {
            if (!File.Exists(_indexPath))
            {
                return null;
            }

            var html = File.ReadAllText(_indexPath);
            return ClientBuildParser.ParseHtmlBuild(html);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read client HTML build from {Path}", _indexPath);
            return null;
        }
    }
}
