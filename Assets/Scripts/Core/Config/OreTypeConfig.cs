using UnityEngine;

namespace Rava.Core.Config
{
    [CreateAssetMenu(fileName = "OreTypeConfig", menuName = "Rava/Ore Type Config")]
    public class OreTypeConfig : ScriptableObject
    {
        public OreTypeEntry[] oreTypes =
        {
            new() { oreType = Enums.OreType.Ferroxite, displayName = "Ferroxite", color = new Color(0.6f, 0.45f, 0.3f), basePrice = 120f },
            new() { oreType = Enums.OreType.Voidium, displayName = "Voidium", color = new Color(0.4f, 0.2f, 0.7f), basePrice = 280f },
            new() { oreType = Enums.OreType.Stellarite, displayName = "Stellarite", color = new Color(0.9f, 0.8f, 0.3f), basePrice = 450f },
            new() { oreType = Enums.OreType.SalvageScrap, displayName = "Salvage Scrap", color = new Color(0.5f, 0.5f, 0.55f), basePrice = 40f, isEmergencySource = true }
        };

        public OreTypeEntry Get(Enums.OreType oreType)
        {
            foreach (var entry in oreTypes)
            {
                if (entry.oreType == oreType)
                {
                    return entry;
                }
            }

            return oreTypes[0];
        }
    }

    [System.Serializable]
    public class OreTypeEntry
    {
        public Enums.OreType oreType;
        public string displayName;
        public Color color = Color.gray;
        public float basePrice;
        public bool isEmergencySource;
    }
}
