using UnityEngine;

namespace Rava.Core.Config
{
    [CreateAssetMenu(fileName = "GameContentConfig", menuName = "Rava/Game Content Config")]
    public class GameContentConfig : ScriptableObject
    {
        public OreTypeConfig oreConfig;
        public SupplyTypeConfig supplyConfig;
        public int gridSize = 8;
        public float emergencyBuybackRate = 0.5f;
        public string starterMineName = "Starter Claim Alpha";
    }
}
