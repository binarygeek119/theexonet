using Theexonet.Core.Configuration;
using Theexonet.Core.Enums;

namespace Theexonet.Core.Tests;

public class MarketItemsCsvLoaderTests
{
    [Fact]
    public void Parse_loads_ore_and_supply_rows()
    {
        const string csv = """
            Category,ItemType,BasePrice,DailyConsumption,StockSymbol,ReferenceClose
            Ore,Ferroxite,120,,,
            Supply,DrillBits,85,0.5,CAT,350
            """;

        var catalog = MarketItemsCsvLoader.Parse(csv);

        Assert.Equal(120m, catalog.GetOreBasePrice(OreType.Ferroxite));
        Assert.Equal(85m, catalog.GetSupplyBasePrice(SupplyType.DrillBits));
        Assert.Equal(0.5m, catalog.GetSupplyDailyConsumption(SupplyType.DrillBits));
        Assert.Equal("CAT", catalog.GetSupplyStockSymbol(SupplyType.DrillBits));
        Assert.Equal(350m, catalog.GetReferenceClose("CAT"));
    }
}
