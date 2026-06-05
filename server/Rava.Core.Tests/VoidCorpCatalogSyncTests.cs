using Rava.Core.Configuration;
using Rava.Core.Enums;
using Rava.Core.Models;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class VoidCorpCatalogSyncTests
{
    [Fact]
    public void Sync_adds_new_supply_rows_to_catalog()
    {
        var root = CreateTempDirectory();
        try
        {
            var supplies = new List<TradeItemDefinition>
            {
                CreateSupply(SupplyType.DrillBits, "Drill Bits", 85m),
                CreateSupply(SupplyType.FuelCells, "Fuel Cells", 110m),
            };

            var result = VoidCorpCatalogSync.Sync(root, supplies);

            Assert.Equal(2, result.Added);
            var catalog = VoidCorpCatalogSync.Load(root);
            Assert.Equal(2, catalog.Products.Count);
            Assert.Contains(catalog.Products, entry => entry.Slug == "DrillBits");
            Assert.Contains(catalog.Products, entry => entry.Summary == "Boosts mining speed");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Sync_preserves_openai_copy_on_resync()
    {
        var root = CreateTempDirectory();
        try
        {
            var supplies = new List<TradeItemDefinition>
            {
                CreateSupply(SupplyType.DrillBits, "Drill Bits", 85m),
            };

            VoidCorpCatalogSync.Sync(root, supplies);
            var catalog = VoidCorpCatalogSync.Load(root);
            var existing = catalog.Products.Single();
            var preserved = existing with
            {
                Description = "Custom AI marketing copy.",
                Source = "openai",
            };
            VoidCorpCatalogSync.Save(root, catalog with { Products = [preserved] });

            var result = VoidCorpCatalogSync.Sync(root, supplies);

            Assert.Equal(0, result.Added);
            var reloaded = VoidCorpCatalogSync.Load(root);
            var entry = reloaded.Products.Single();
            Assert.Equal("Custom AI marketing copy.", entry.Description);
            Assert.Equal("openai", entry.Source);
            Assert.Equal(85m, entry.BasePrice);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Sync_updates_price_and_display_name_from_trade_items()
    {
        var root = CreateTempDirectory();
        try
        {
            VoidCorpCatalogSync.Sync(root, [CreateSupply(SupplyType.LifeSupport, "Life Support", 95m)]);

            var updatedSupply = CreateSupply(SupplyType.LifeSupport, "VoidCorp Life Support Pack", 120m);
            var result = VoidCorpCatalogSync.Sync(root, [updatedSupply]);

            Assert.Equal(1, result.Updated);
            var entry = VoidCorpCatalogSync.Load(root).Products.Single();
            Assert.Equal("VoidCorp Life Support Pack", entry.DisplayName);
            Assert.Equal(120m, entry.BasePrice);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static TradeItemDefinition CreateSupply(SupplyType supplyType, string displayName, decimal basePrice) =>
        new()
        {
            Category = ItemCategory.Supply,
            ItemType = supplyType.ToString(),
            DisplayName = displayName,
            BasePrice = basePrice,
            Color = "#888888",
            UiSymbol = "XLI",
        };

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"voidcorp-sync-{Guid.NewGuid():N}");
}
