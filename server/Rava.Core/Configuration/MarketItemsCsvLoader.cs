using Rava.Core.Enums;

namespace Rava.Core.Configuration;

public static class MarketItemsCsvLoader
{
    public static MarketItemsCatalog LoadFromFile(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : MarketItemsCatalog.CreateDefault();

    public static MarketItemsCatalog Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return MarketItemsCatalog.CreateDefault();
        }

        var orePrices = new Dictionary<OreType, decimal>();
        var supplyPrices = new Dictionary<SupplyType, decimal>();
        var consumption = new Dictionary<SupplyType, decimal>();
        var symbols = new Dictionary<SupplyType, string>();
        var referenceCloses = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
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

            decimal? dailyConsumption = columns.Count > 3 && decimal.TryParse(columns[3].Trim(), out var parsedConsumption)
                ? parsedConsumption
                : null;
            var stockSymbol = columns.Count > 4 ? columns[4].Trim() : string.Empty;
            decimal? referenceClose = columns.Count > 5 && decimal.TryParse(columns[5].Trim(), out var parsedReferenceClose)
                ? parsedReferenceClose
                : null;

            if (category.Equals("Ore", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<OreType>(itemType, true, out var oreType))
            {
                orePrices[oreType] = basePrice;
                continue;
            }

            if (category.Equals("Supply", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SupplyType>(itemType, true, out var supplyType))
            {
                supplyPrices[supplyType] = basePrice;
                if (dailyConsumption.HasValue)
                {
                    consumption[supplyType] = dailyConsumption.Value;
                }

                if (!string.IsNullOrWhiteSpace(stockSymbol))
                {
                    symbols[supplyType] = stockSymbol.ToUpperInvariant();
                    if (referenceClose.HasValue)
                    {
                        referenceCloses[stockSymbol.ToUpperInvariant()] = referenceClose.Value;
                    }
                }
            }
        }

        if (orePrices.Count == 0 && supplyPrices.Count == 0)
        {
            return MarketItemsCatalog.CreateDefault();
        }

        var defaults = MarketItemsCatalog.CreateDefault();
        foreach (var oreType in Enum.GetValues<OreType>())
        {
            orePrices.TryAdd(oreType, defaults.GetOreBasePrice(oreType));
        }

        foreach (var supplyType in Enum.GetValues<SupplyType>())
        {
            supplyPrices.TryAdd(supplyType, defaults.GetSupplyBasePrice(supplyType));
            consumption.TryAdd(supplyType, defaults.GetSupplyDailyConsumption(supplyType));
            symbols.TryAdd(supplyType, defaults.GetSupplyStockSymbol(supplyType));

            var symbol = symbols[supplyType];
            if (!referenceCloses.ContainsKey(symbol))
            {
                var defaultReference = defaults.GetReferenceClose(symbol);
                if (defaultReference > 0)
                {
                    referenceCloses[symbol] = defaultReference;
                }
            }
        }

        return new MarketItemsCatalog(orePrices, supplyPrices, consumption, symbols, referenceCloses);
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

    private static bool IsHeaderRow(IReadOnlyList<string> columns)
    {
        return columns[0].Equals("Category", StringComparison.OrdinalIgnoreCase)
            || columns[1].Equals("ItemType", StringComparison.OrdinalIgnoreCase)
            || columns[1].Equals("Item", StringComparison.OrdinalIgnoreCase);
    }
}
