using BuildAndBounce.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildAndBounce.Editor
{
    /// <summary>
    /// One-click scene setup: creates the Player if missing, runs every setup tool in the right order,
    /// then SAVES the scene. Running the tools separately (or during Play mode) left scenes unsaved,
    /// so the Player and cameras vanished on the next session.
    /// </summary>
    public static class SceneSetupTool
    {
        [MenuItem("Build & Bounce/Setup Complete Scene (run this first)", priority = 0)]
        public static void SetupCompleteScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Build & Bounce", "Stop Play mode first. Setup changes made during Play mode are thrown away.", "OK");
                return;
            }

            if (Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include) == null)
            {
                var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                player.transform.position = new Vector3(0f, 1.5f, 0f);
                Undo.RegisterCreatedObjectUndo(player, "Create Player");
                player.AddComponent<PlayerController>(); // RequireComponent adds CharacterController + PlayerInputReader
                player.AddComponent<PlayerRespawn>();
            }

            LevelSetupTool.SetupTestCourse();   // course, HUD, pause menu, player wiring, Cinemachine camera
            BuilderSetupTool.SetupLevelBuilder();
            RaceSetupTool.SetupGhostRacing();

            var scene = EditorSceneManager.GetActiveScene();
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log($"[Build & Bounce] Scene '{scene.path}' set up and saved.");
            }
        }
    }
}
