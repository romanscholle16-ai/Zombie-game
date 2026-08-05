using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Boots the game automatically when play starts, in whatever scene happens
    /// to be open — including a completely empty one.
    ///
    /// That is deliberate: the entire game is generated in code, so there is
    /// nothing to author in a scene, and nothing to wire up in the inspector.
    /// Open the project, press Play, and it runs.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Launch()
        {
            if (Game.Instance != null) return;
            var go = new GameObject("Rotgrid");
            go.AddComponent<Game>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
