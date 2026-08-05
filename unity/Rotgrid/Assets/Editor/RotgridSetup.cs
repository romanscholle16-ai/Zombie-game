#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Editor conveniences. The game boots itself via Bootstrap, so none of this
    /// is required — it just makes the project tidy to open and to build.
    /// </summary>
    public static class RotgridSetup
    {
        const string ScenePath = "Assets/Scenes/Boot.unity";

        [MenuItem("Rotgrid/Create Play Scene")]
        public static void CreatePlayScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Rotgrid");
            go.AddComponent<Game>();
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            Debug.Log("Rotgrid: created " + ScenePath + ". Press Play to run.");
        }

        [MenuItem("Rotgrid/Add Boot Scene To Build Settings")]
        public static void AddSceneToBuild()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == ScenePath) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Rotgrid/Open Boot Scene")]
        public static void OpenBootScene()
        {
            if (System.IO.File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else CreatePlayScene();
        }
    }
}
#endif
