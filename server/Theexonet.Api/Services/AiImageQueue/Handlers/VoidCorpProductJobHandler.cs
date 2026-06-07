using System.Text.Json;
using Theexonet.Api.Services.VoidCorp;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class VoidCorpProductJobHandler(
    VoidCorpProductImageGenerator imageGenerator,
    TheexonetHostingPaths hostingPaths) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.VoidCorpProduct;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null ? "VoidCorp product image" : $"VoidCorp product {payload.Slug}";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Slug))
        {
            return (false, "Invalid VoidCorp product job payload.");
        }

        var document = VoidCorpCatalogSync.Load(hostingPaths.VoidCorpCacheRoot);
        var product = document.Products.FirstOrDefault(
            entry => string.Equals(entry.Slug, payload.Slug, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            return (false, $"VoidCorp product {payload.Slug} not found.");
        }

        return await imageGenerator.GenerateAndSaveAsync(product, ct);
    }

    private static VoidCorpProductJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<VoidCorpProductJobPayload>(payloadJson, SerializerOptions);
}
