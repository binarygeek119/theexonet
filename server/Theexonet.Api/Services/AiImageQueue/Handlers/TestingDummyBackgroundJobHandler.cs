using System.Text.Json;
using Theexonet.Api.Services.TestingDummyFriends;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class TestingDummyBackgroundJobHandler(
    TestingDummyFriendsAssetGenerator generator,
    TheexonetHostingPaths hostingPaths) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.TestingDummyBackground;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        var profile = payload is null ? null : TestingDummyFriendsCatalog.TryGet(payload.ProfileIndex);
        return profile is null ? "Testing dummy background" : $"Testing dummy background for {profile.Username}";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        var profile = payload is null ? null : TestingDummyFriendsCatalog.TryGet(payload.ProfileIndex);
        if (profile is null)
        {
            return (false, "Invalid testing dummy background job payload.");
        }

        return await generator.GenerateBackgroundAsync(profile, hostingPaths.TestingDummyFriendsAssetsRoot, ct);
    }

    private static TestingDummyAssetJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<TestingDummyAssetJobPayload>(payloadJson, SerializerOptions);
}
