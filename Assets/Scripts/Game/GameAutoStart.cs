using UnityEngine;

namespace Theexonet.Game
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

            var go = new GameObject("TheexonetGameBootstrap");
            go.AddComponent<GameBootstrap>();
        }
    }
}
