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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // "Play from any scene starts the game" is the rule, and the Dev Arena is the one
            // exception: it is its own scene and builds its own world, so booting the hub here
            // would stack the whole menu on top of the fight being examined. Compiled away
            // entirely outside a development build, where the scene does not exist.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "DevArena") return;
#endif
            GameObject host = new GameObject("Emberfall");
            host.AddComponent<GameApp>();
        }
    }
}
