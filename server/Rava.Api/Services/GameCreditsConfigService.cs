using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;
using Rava.Infrastructure.Services;

namespace Rava.Api.Services;

public class GameCreditsConfigService(
    IWebHostEnvironment environment,
    IOptionsMonitor<GameCreditsOptions> optionsMonitor,
    GameCreditsProvider creditsProvider)
{
    public string FilePath =>
        RavaDataPaths.ResolveFile(environment.ContentRootPath, optionsMonitor.CurrentValue.CreditsFile);

    public GameCreditsConfigResponse GetConfig()
    {
        creditsProvider.Reload();
        return new GameCreditsConfigResponse(
            new GameCreditsConfigDto(
                creditsProvider.SignUp,
                creditsProvider.BirthdayBonus,
                creditsProvider.CompanyNameReclaimFee),
            FilePath);
    }

    public async Task<(GameCreditsConfigDto? Credits, string? Error)> SaveAsync(
        decimal signUp,
        decimal birthdayBonus,
        decimal companyNameReclaimFee,
        CancellationToken ct)
    {
        if (signUp < 0)
        {
            return (null, "Sign-up credits cannot be negative.");
        }

        if (birthdayBonus < 0)
        {
            return (null, "Birthday bonus cannot be negative.");
        }

        if (companyNameReclaimFee < 0)
        {
            return (null, "Company name reclaim fee cannot be negative.");
        }

        var values = new GameCreditsValues
        {
            SignUp = signUp,
            BirthdayBonus = birthdayBonus,
            CompanyNameReclaimFee = companyNameReclaimFee,
        };

        await Task.Run(() => GameCreditsCsvLoader.SaveToFile(FilePath, values), ct);
        creditsProvider.Reload();

        return (new GameCreditsConfigDto(signUp, birthdayBonus, companyNameReclaimFee), null);
    }
}
