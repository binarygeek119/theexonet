namespace Theexonet.Core.Interfaces;

public interface IAiImageJobHandler
{
    string Kind { get; }

    string Describe(string payloadJson);

    Task<(bool Ok, string? Error)> ExecuteAsync(string payloadJson, CancellationToken ct);
}
