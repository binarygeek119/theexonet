#if UNITY_EDITOR
using Rava.Core.Config;
using UnityEditor;
using UnityEngine;

namespace Rava.Editor
{
    public static class GameContentCreator
    {
        [MenuItem("Rava/Create Default Game Content Assets")]
        public static void CreateAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Config"))
            {
                AssetDatabase.CreateFolder("Assets", "Config");
            }

            var ore = ScriptableObject.CreateInstance<OreTypeConfig>();
            AssetDatabase.CreateAsset(ore, "Assets/Config/DefaultOreConfig.asset");

            var supply = ScriptableObject.CreateInstance<SupplyTypeConfig>();
            AssetDatabase.CreateAsset(supply, "Assets/Config/DefaultSupplyConfig.asset");

            var content = ScriptableObject.CreateInstance<GameContentConfig>();
            content.oreConfig = ore;
            content.supplyConfig = supply;
            AssetDatabase.CreateAsset(content, "Assets/Config/GameContentConfig.asset");

            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = content;
            Debug.Log("Created default Rava game content assets in Assets/Config/");
        }
    }
}
#endif
