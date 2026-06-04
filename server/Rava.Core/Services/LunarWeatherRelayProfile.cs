namespace Rava.Core.Services;

public sealed record LunarWeatherRelayProfile(
    string Id,
    string Slug,
    string Name,
    string Region,
    string Sector,
    string BodyType);
