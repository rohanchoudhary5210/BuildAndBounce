using BuildAndBounce.Builder;
using BuildAndBounce.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildAndBounce.Editor
{
    /// <summary>
    /// Editor setup tool to configure the M3 Level Builder (UGC) system:
    /// - Creates/wires the LevelBuilder manager and BuilderGrid.
    /// - Creates a dedicated BuilderCamera for free-flight editing (without duplicating AudioListener).
    /// - Generates or wires the BlockCatalog ScriptableObject asset.
    /// - Configures the BuilderHUD UI overlay.
    /// </summary>
    public static class BuilderSetupTool
    {
        [MenuItem("Build & Bounce/Fix Audio Listeners")]
        public static void FixAudioListeners()
        {
            var primary = AudioListenerGuard.EnforceSingleListener();
            if (primary != null)
            {
                EditorUtility.DisplayDialog(
                    "Audio Listeners Fixed",
                    $"Successfully audited AudioListeners!\n\nSingle active AudioListener is on: '{primary.gameObject.name}'.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Audio Listeners",
                    "No cameras or AudioListeners found in scene.",
                    "OK");
            }
        }

        [MenuItem("Build & Bounce/Setup Level Builder")]
        public static void SetupLevelBuilder()
        {
            // 1. Ensure gameplay prefabs and catalog wiring
            BlockPrefabBuilder.EnsureBlockPrefabsAndCatalog();
            var catalog = EnsureBlockCatalogAsset();

            // 2. Find or create Builder Camera
            var builderCam = UnityEngine.Object.FindAnyObjectByType<BuilderCamera>(FindObjectsInactive.Include);
            if (builderCam == null)
            {
                var camObj = new GameObject("BuilderCamera");
                Undo.RegisterCreatedObjectUndo(camObj, "Create Builder Camera");
                var cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                builderCam = camObj.AddComponent<BuilderCamera>();
                builderCam.FocusOn(new Vector3(0f, 0f, 10f), 20f);
                camObj.SetActive(false); // Inactive until Build Mode is toggled
            }
            else
            {
                // Ensure no stray AudioListener exists on BuilderCamera
                if (builderCam.TryGetComponent<AudioListener>(out var strayListener))
                {
                    Undo.DestroyObjectImmediate(strayListener);
                }
            }

            // Ensure exactly one AudioListener exists in the scene (on Main Camera)
            AudioListenerGuard.EnforceSingleListener();

            // 3. Find or create LevelBuilder root
            var builder = UnityEngine.Object.FindAnyObjectByType<LevelBuilder>(FindObjectsInactive.Include);
            if (builder == null)
            {
                var builderObj = new GameObject("LevelBuilder");
                Undo.RegisterCreatedObjectUndo(builderObj, "Create Level Builder");
                builder = builderObj.AddComponent<LevelBuilder>();
            }

            Undo.RecordObject(builder, "Configure Level Builder");

            // 4. Configure BuilderGrid
            if (!builder.TryGetComponent<BuilderGrid>(out var grid))
            {
                grid = builder.gameObject.AddComponent<BuilderGrid>();
            }

            // 5. Configure BuilderHUD
            var hud = UnityEngine.Object.FindAnyObjectByType<BuilderHUD>(FindObjectsInactive.Include);
            if (hud == null)
            {
                var hudObj = new GameObject("BuilderHUD");
                Undo.RegisterCreatedObjectUndo(hudObj, "Create Builder HUD");
                hudObj.transform.SetParent(builder.transform, false);
                hud = hudObj.AddComponent<BuilderHUD>();
            }

            // 6. Ensure CourseBackendService and OnlineShareHUD (M5)
            var backend = UnityEngine.Object.FindAnyObjectByType<BuildAndBounce.Net.CourseBackendService>(FindObjectsInactive.Include);
            if (backend == null)
            {
                var backendObj = new GameObject("CourseBackendService");
                Undo.RegisterCreatedObjectUndo(backendObj, "Create Course Backend Service");
                backend = backendObj.AddComponent<BuildAndBounce.Net.CourseBackendService>();
            }

            var shareHud = UnityEngine.Object.FindAnyObjectByType<BuildAndBounce.UI.OnlineShareHUD>(FindObjectsInactive.Include);
            if (shareHud == null)
            {
                var shareHudObj = new GameObject("OnlineShareHUD");
                Undo.RegisterCreatedObjectUndo(shareHudObj, "Create Online Share HUD");
                shareHudObj.transform.SetParent(builder.transform, false);
                shareHud = shareHudObj.AddComponent<BuildAndBounce.UI.OnlineShareHUD>();
            }

            // Wire Serialized Properties
            var soBuilder = new SerializedObject(builder);
            var propCatalog = soBuilder.FindProperty("_catalog");
            if (propCatalog != null) propCatalog.objectReferenceValue = catalog;

            var propGrid = soBuilder.FindProperty("_grid");
            if (propGrid != null) propGrid.objectReferenceValue = grid;

            var propCam = soBuilder.FindProperty("_builderCamera");
            if (propCam != null) propCam.objectReferenceValue = builderCam;

            soBuilder.ApplyModifiedProperties();

            EditorUtility.SetDirty(builder);
            EditorUtility.SetDirty(builderCam);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);

            EditorUtility.DisplayDialog(
                "Level Builder Ready",
                "Level Builder (UGC) system configured successfully!\n\n" +
                "• Block Catalog: Assets/_Project/Data/BlockCatalog.asset\n" +
                "• Controls:\n" +
                "   - [Tab]: Toggle Build / Play mode\n" +
                "   - [Left Click]: Place selected block\n" +
                "   - [X Key]: Delete block under cursor\n" +
                "   - [R Key]: Rotate block 90°\n" +
                "   - [Ctrl+Z / Ctrl+Y]: Undo / Redo\n" +
                "   - [Right Mouse Drag]: Orbit builder camera\n" +
                "   - [WASD]: Pan builder camera",
                "Start Building!");

            Debug.Log("[Build & Bounce] Level Builder (M3) configured successfully!", builder);
        }

        private static BlockCatalog EnsureBlockCatalogAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:BlockCatalog");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<BlockCatalog>(path);
            }

            string dataFolder = "Assets/_Project/Data";
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }
            if (!AssetDatabase.IsValidFolder(dataFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }

            string assetPath = $"{dataFolder}/BlockCatalog.asset";
            var catalog = ScriptableObject.CreateInstance<BlockCatalog>();

            catalog.AddBlock(new BlockData("start_pad", "Start Pad", BlockCategory.StartFinish, null, new Vector3(4f, 0.5f, 4f), new Color(0.2f, 0.85f, 0.35f)));
            catalog.AddBlock(new BlockData("finish_pad", "Finish Pad", BlockCategory.StartFinish, null, new Vector3(5f, 0.5f, 5f), new Color(1f, 0.65f, 0.1f)));
            catalog.AddBlock(new BlockData("platform_cube_1x1", "1x1 Block", BlockCategory.Platform, null, new Vector3(1f, 1f, 1f), new Color(0.6f, 0.6f, 0.7f)));
            catalog.AddBlock(new BlockData("platform_step_3x3", "3x3 Platform", BlockCategory.Platform, null, new Vector3(3f, 0.5f, 3f), new Color(0.45f, 0.5f, 0.65f)));
            catalog.AddBlock(new BlockData("checkpoint_pad", "Checkpoint", BlockCategory.Checkpoint, null, new Vector3(3f, 0.5f, 3f), new Color(0.9f, 0.85f, 0.2f)));
            catalog.AddBlock(new BlockData("hazard_spikes", "Hazard", BlockCategory.Hazard, null, new Vector3(2f, 0.3f, 2f), new Color(0.9f, 0.2f, 0.25f)));
            catalog.AddBlock(new BlockData("moving_platform", "Moving Platform", BlockCategory.Moving, null, new Vector3(3f, 0.5f, 3f), new Color(0.2f, 0.75f, 0.95f)));

            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return catalog;
        }
    }
}