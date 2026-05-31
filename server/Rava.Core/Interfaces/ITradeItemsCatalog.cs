using Rava.Core.Enums;
using Rava.Core.Models;

namespace Rava.Core.Interfaces;

public interface ITradeItemsCatalog
{
    IReadOnlyList<TradeItemDefinition> GetAllItems();

    IReadOnlyList<TradeItemDefinition> GetOreItems();

    IReadOnlyList<TradeItemDefinition> GetSupplyItems();

    bool IsTradeableOre(OreType oreType);

    bool IsTradeableSupply(SupplyType supplyType);

    TradeItemDefinition GetOreItem(OreType oreType);

    TradeItemDefinition GetSupplyItem(SupplyType supplyType);
}
