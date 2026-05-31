using Rava.Core.Enums;

namespace Rava.Core.Interfaces;

public interface IMarketItemsCatalog
{
    decimal GetOreBasePrice(OreType oreType);

    decimal GetSupplyBasePrice(SupplyType supplyType);

    decimal GetSupplyDailyConsumption(SupplyType supplyType);

    string GetSupplyStockSymbol(SupplyType supplyType);

    decimal GetReferenceClose(string stockSymbol);
}
