using Theexonet.Core.Enums;

namespace Theexonet.Core.Models;

public sealed class TradeItemDefinition
{
    public ItemCategory Category { get; init; }

    public string ItemType { get; init; } = string.Empty;

    public decimal BasePrice { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Color { get; init; } = "#888888";

    public string? UiSymbol { get; init; }

    public bool IsEmergencySource { get; init; }
}
