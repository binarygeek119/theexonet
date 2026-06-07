using Theexonet.Core.Enums;
using Theexonet.Core.Models;

namespace Theexonet.Core.Configuration;

public static class TradeItemsCsvLoader
{
    public static TradeItemsCatalog LoadFromFile(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : TradeItemsCatalog.CreateDefault();

    public static TradeItemsCatalog Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return TradeItemsCatalog.CreateDefault();
        }

        var oreItems = new Dictionary<OreType, TradeItemDefinition>();
        var supplyItems = new Dictionary<SupplyType, TradeItemDefinition>();
        var skipHeader = true;

        foreach (var rawLine in csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = SplitCsvLine(line);
            if (columns.Count < 3)
            {
                continue;
            }

            if (skipHeader && IsHeaderRow(columns))
            {
                skipHeader = false;
                continue;
            }

            skipHeader = false;

            var category = columns[0].Trim();
            var itemType = columns[1].Trim();
            if (!decimal.TryParse(columns[2].Trim(), out var basePrice))
            {
                continue;
            }

            var displayName = columns.Count > 3 && !string.IsNullOrWhiteSpace(columns[3])
                ? columns[3].Trim()
                : itemType;
            var color = columns.Count > 4 && !string.IsNullOrWhiteSpace(columns[4])
                ? columns[4].Trim()
                : "#888888";
            var uiSymbol = columns.Count > 5 ? NullIfBlank(columns[5]) : null;
            var isEmergencySource = columns.Count > 6 && IsTruthy(columns[6]);

            if (category.Equals("Ore", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<OreType>(itemType, true, out var oreType))
            {
                oreItems[oreType] = new TradeItemDefinition
                {
                    Category = ItemCategory.Ore,
                    ItemType = oreType.ToString(),
                    BasePrice = basePrice,
                    DisplayName = displayName,
                    Color = color,
                    IsEmergencySource = isEmergencySource
                };
                continue;
            }

            if (category.Equals("Supply", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SupplyType>(itemType, true, out var supplyType))
            {
                supplyItems[supplyType] = new TradeItemDefinition
                {
                    Category = ItemCategory.Supply,
                    ItemType = supplyType.ToString(),
                    BasePrice = basePrice,
                    DisplayName = displayName,
                    Color = color,
                    UiSymbol = uiSymbol
                };
            }
        }

        if (oreItems.Count == 0 && supplyItems.Count == 0)
        {
            return TradeItemsCatalog.CreateDefault();
        }

        var defaults = TradeItemsCatalog.CreateDefault();
        foreach (var oreType in Enum.GetValues<OreType>())
        {
            oreItems.TryAdd(oreType, defaults.GetOreItem(oreType));
        }

        foreach (var supplyType in Enum.GetValues<SupplyType>())
        {
            supplyItems.TryAdd(supplyType, defaults.GetSupplyItem(supplyType));
        }

        return new TradeItemsCatalog(oreItems, supplyItems);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var columns = new List<string>();
        var current = string.Empty;
        var inQuotes = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                columns.Add(current);
                current = string.Empty;
                continue;
            }

            current += character;
        }

        columns.Add(current);
        return columns;
    }

    private static bool IsHeaderRow(IReadOnlyList<string> columns) =>
        columns[0].Equals("Category", StringComparison.OrdinalIgnoreCase)
        || columns[1].Equals("ItemType", StringComparison.OrdinalIgnoreCase)
        || columns[1].Equals("Item", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfBlank(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool IsTruthy(string value) =>
        value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Trim() == "1"
        || value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
}
