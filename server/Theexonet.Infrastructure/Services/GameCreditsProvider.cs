using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Interfaces;

namespace Theexonet.Infrastructure.Services;

public class GameCreditsProvider(
    IWebHostEnvironment environment,
    IOptionsMonitor<GameCreditsOptions> options) : IGameCreditsConfig
{
    private GameCreditsValues _values = GameCreditsCsvLoader.CreateDefault();
    private DateTime _cachedWriteUtc = DateTime.MinValue;

    public decimal SignUp => GetValues().SignUp;

    public decimal BirthdayBonus => GetValues().BirthdayBonus;

    public decimal CompanyNameReclaimFee => GetValues().CompanyNameReclaimFee;

    public void Reload()
    {
        _cachedWriteUtc = DateTime.MinValue;
        _ = GetValues();
    }

    private GameCreditsValues GetValues()
    {
        var settings = options.CurrentValue;
        var path = TheexonetDataPaths.ResolveFile(environment.ContentRootPath, settings.CreditsFile);

        if (!File.Exists(path))
        {
            return _values;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(path);
        if (lastWriteUtc != _cachedWriteUtc)
        {
            _values = GameCreditsCsvLoader.LoadFromFile(path);
            _cachedWriteUtc = lastWriteUtc;
        }

        return _values;
    }
}
