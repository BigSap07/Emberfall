using UnityEngine;

namespace Emberfall.Game
{
    /// <summary>
    /// The entire game boots from code, so the shipped scene can stay empty.
    /// That keeps the project free of fragile serialised scene data and means
    /// pressing Play from any scene starts the game.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (GameApp.Instance != null) return;
            GameObject host = new GameObject("Emberfall");
            host.AddComponent<GameApp>();
        }
    }
}
