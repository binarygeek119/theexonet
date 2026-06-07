using UnityEngine;

namespace Theexonet.Core.Config
{
    [CreateAssetMenu(fileName = "SupplyTypeConfig", menuName = "Theexonet/Supply Type Config")]
    public class SupplyTypeConfig : ScriptableObject
    {
        public SupplyTypeEntry[] supplyTypes =
        {
            new() { supplyType = Enums.SupplyType.DrillBits, displayName = "Drill Bits", futureMarketSymbol = "XLI", color = new Color(0.7f, 0.5f, 0.2f), basePrice = 85f, description = "Boosts mining speed" },
            new() { supplyType = Enums.SupplyType.FuelCells, displayName = "Fuel Cells", futureMarketSymbol = "XLE", color = new Color(0.2f, 0.6f, 0.9f), basePrice = 110f, description = "Powers hauling operations" },
            new() { supplyType = Enums.SupplyType.LifeSupport, displayName = "Life Support", futureMarketSymbol = "XLV", color = new Color(0.3f, 0.8f, 0.5f), basePrice = 95f, description = "Keeps workers efficient" },
            new() { supplyType = Enums.SupplyType.CommModules, displayName = "Comm Modules", futureMarketSymbol = "XLK", color = new Color(0.5f, 0.7f, 1f), basePrice = 130f, description = "Improves management systems" }
        };

        public SupplyTypeEntry Get(Enums.SupplyType supplyType)
        {
            foreach (var entry in supplyTypes)
            {
                if (entry.supplyType == supplyType)
                {
                    return entry;
                }
            }

            return supplyTypes[0];
        }
    }

    [System.Serializable]
    public class SupplyTypeEntry
    {
        public Enums.SupplyType supplyType;
        public string displayName;
        public string futureMarketSymbol;
        public Color color = Color.white;
        public float basePrice;
        public string description;
    }
}
