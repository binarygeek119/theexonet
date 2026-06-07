using System.Text.Json;
using Theexonet.Core.Dtos;

namespace Theexonet.Core.Tests;

public class AiGenerationQueuePayloadTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void OnnEditionStoriesJobPayload_roundtrips_json()
    {
        var payload = new OnnEditionStoriesJobPayload("2026-06-07", ForceRegenerate: true);
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var restored = JsonSerializer.Deserialize<OnnEditionStoriesJobPayload>(json, SerializerOptions);
        Assert.NotNull(restored);
        Assert.Equal("2026-06-07", restored.EditionDate);
        Assert.True(restored.ForceRegenerate);
    }

    [Fact]
    public void ForeverfallIntakeJobPayload_roundtrips_json()
    {
        var payload = new ForeverfallIntakeJobPayload("2026-06-07");
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var restored = JsonSerializer.Deserialize<ForeverfallIntakeJobPayload>(json, SerializerOptions);
        Assert.NotNull(restored);
        Assert.Equal("2026-06-07", restored.IntakeDate);
        Assert.False(restored.ForceRegenerate);
    }

    [Fact]
    public void LunarWeatherBulletinJobPayload_roundtrips_json()
    {
        var payload = new LunarWeatherBulletinJobPayload("2026-06-07", ForceRegenerate: true);
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var restored = JsonSerializer.Deserialize<LunarWeatherBulletinJobPayload>(json, SerializerOptions);
        Assert.NotNull(restored);
        Assert.Equal("2026-06-07", restored.BulletinDate);
        Assert.True(restored.ForceRegenerate);
    }
}
