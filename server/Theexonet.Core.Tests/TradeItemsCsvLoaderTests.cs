using Theexonet.Core.Configuration;
using Theexonet.Core.Enums;

namespace Theexonet.Core.Tests;

public class TradeItemsCsvLoaderTests
{
    [Fact]
    public void Parse_loads_ore_and_supply_trade_items()
    {
        const string csv = """
            Category,ItemType,BasePrice,DisplayName,Color,UiSymbol,EmergencySource
            Ore,Ferroxite,120,Ferroxite,#996644,,
            Supply,DrillBits,85,Drill Bits,#b38033,XLI,
            """;

        var catalog = TradeItemsCsvLoader.Parse(csv);

        Assert.Equal(120m, catalog.GetOreItem(OreType.Ferroxite).BasePrice);
        Assert.Equal("Ferroxite", catalog.GetOreItem(OreType.Ferroxite).DisplayName);
        Assert.Equal(85m, catalog.GetSupplyItem(SupplyType.DrillBits).BasePrice);
        Assert.Equal("XLI", catalog.GetSupplyItem(SupplyType.DrillBits).UiSymbol);
    }

    [Fact]
    public void Parse_marks_emergency_source_ore()
    {
        const string csv = """
            Category,ItemType,BasePrice,DisplayName,Color,UiSymbol,EmergencySource
            Ore,SalvageScrap,40,Salvage Scrap,#80808c,,true
            """;

        var catalog = TradeItemsCsvLoader.Parse(csv);

        Assert.True(catalog.GetOreItem(OreType.SalvageScrap).IsEmergencySource);
    }
}
