using Theexonet.Core.Enums;
using Theexonet.Core.Models;

namespace Theexonet.Core.Interfaces;

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
