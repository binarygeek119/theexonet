using UnityEngine;

namespace Rava.Game
{
    public static class GameAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (Object.FindAnyObjectByType<GameBootstrap>() != null)
            {
                return;
            }

            var go = new GameObject("RavaGameBootstrap");
            go.AddComponent<GameBootstrap>();
        }
    }
}
