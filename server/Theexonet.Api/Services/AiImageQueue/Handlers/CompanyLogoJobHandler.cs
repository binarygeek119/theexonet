using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.AiImageQueue.Handlers;

public sealed class CompanyLogoJobHandler(
    AppDbContext db,
    CompanyLogoQueueService companyLogoQueueService) : IAiImageJobHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Kind => AiImageJobKinds.CompanyLogo;

    public string Describe(string payloadJson)
    {
        var payload = Deserialize(payloadJson);
        return payload is null ? "Company logo" : $"Company logo for mine {payload.MineId}";
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var payload = Deserialize(payloadJson);
        if (payload is null || payload.QueueEntityId == Guid.Empty || payload.MineId == Guid.Empty)
        {
            return (false, "Invalid company logo job payload.");
        }

        var entry = await db.CompanyLogoQueue.FirstOrDefaultAsync(
            item => item.Id == payload.QueueEntityId,
            ct);
        if (entry is null)
        {
            return (false, "Company logo queue entry no longer exists.");
        }

        if (entry.Status is CompanyLogoQueueStatuses.Completed or CompanyLogoQueueStatuses.Failed)
        {
            return (true, null);
        }

        return await companyLogoQueueService.ProcessQueueEntryAsync(entry, ct);
    }

    private static CompanyLogoJobPayload? Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<CompanyLogoJobPayload>(payloadJson, SerializerOptions);
}
