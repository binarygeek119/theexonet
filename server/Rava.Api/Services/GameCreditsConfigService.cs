using System.Text.Json;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Dtos;

namespace Rava.Api.Services;

public class GameCreditsConfigService(
    IWebHostEnvironment environment,
    IOptionsMonitor<GameCreditsOptions> optionsMonitor)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string FilePath => Path.Combine(environment.ContentRootPath, "credits.json");

    public GameCreditsConfigResponse GetConfig()
    {
        var credits = optionsMonitor.CurrentValue;
        return new GameCreditsConfigResponse(
            new GameCreditsConfigDto(credits.SignUp, credits.BirthdayBonus),
            FilePath);
    }

    public async Task<(GameCreditsConfigDto? Credits, string? Error)> SaveAsync(
        decimal signUp,
        decimal birthdayBonus,
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

        var payload = new Dictionary<string, Dictionary<string, decimal>>
        {
            [GameCreditsOptions.SectionName] = new()
            {
                [nameof(GameCreditsOptions.SignUp)] = signUp,
                [nameof(GameCreditsOptions.BirthdayBonus)] = birthdayBonus
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json + Environment.NewLine, ct);

        return (new GameCreditsConfigDto(signUp, birthdayBonus), null);
    }
}
