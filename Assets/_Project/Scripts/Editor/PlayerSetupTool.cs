using BuildAndBounce.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildAndBounce.Editor
{
    /// <summary>
    /// Editor utility to automatically connect and validate references on the Player GameObject
    /// in the current scene, including Input Actions, PlayerControllerData, and Camera.
    /// </summary>
    public static class PlayerSetupTool
    {
        [MenuItem("Build & Bounce/Wire Player References")]
        public static void WirePlayerReferences()
        {
            // 1. Locate Player GameObject in active scene
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                var controller = Object.FindAnyObjectByType<PlayerController>();
                if (controller != null)
                {
                    player = controller.gameObject;
                }
            }

            if (player == null)
            {
                EditorUtility.DisplayDialog(
                    "Wire Player",
                    "Could not find a GameObject named 'Player' or with a PlayerController component in the active scene.",
                    "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(player, "Wire Player References");

            // 2. Configure CharacterController
            if (!player.TryGetComponent<CharacterController>(out var cc))
            {
                cc = player.AddComponent<CharacterController>();
            }

            // For a standard Unity Capsule primitive, the mesh is centered at local (0, 0, 0).
            // Setting center to Vector3.zero perfectly matches the visual capsule bounds so it doesn't sink into the ground.
            cc.center = Vector3.zero;
            cc.height = 2f;
            cc.radius = 0.5f;

            // Remove redundant CapsuleCollider from the 3D primitive to avoid physics conflicts with CharacterController
            if (player.TryGetComponent<CapsuleCollider>(out var redundantCollider))
            {
                Object.DestroyImmediate(redundantCollider);
            }

            // Lift player above floor if currently placed at or below ground
            if (player.transform.position.y < 1.1f)
            {
                player.transform.position = new Vector3(player.transform.position.x, 2f, player.transform.position.z);
            }

            // 3. Configure PlayerInput
            if (!player.TryGetComponent<PlayerInput>(out var playerInput))
            {
                playerInput = player.AddComponent<PlayerInput>();
            }

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputActions != null)
            {
                var soInput = new SerializedObject(playerInput);
                var actionsProp = soInput.FindProperty("m_Actions");
                if (actionsProp != null)
                {
                    actionsProp.objectReferenceValue = inputActions;
                }

                var defaultMapProp = soInput.FindProperty("m_DefaultActionMap");
                if (defaultMapProp != null)
                {
                    defaultMapProp.stringValue = "Player";
                }
                soInput.ApplyModifiedProperties();
            }

            // 4. Configure PlayerInputReader
            if (!player.TryGetComponent<PlayerInputReader>(out var inputReader))
            {
                inputReader = player.AddComponent<PlayerInputReader>();
            }

            var soReader = new SerializedObject(inputReader);
            var propInput = soReader.FindProperty("_playerInput");
            if (propInput != null)
            {
                propInput.objectReferenceValue = playerInput;
            }
            soReader.ApplyModifiedProperties();

            // 5. Configure PlayerController
            if (!player.TryGetComponent<PlayerController>(out var playerController))
            {
                playerController = player.AddComponent<PlayerController>();
            }

            var dataAsset = EnsurePlayerControllerDataAsset();

            var soController = new SerializedObject(playerController);
            var propData = soController.FindProperty("_data");
            if (propData != null)
            {
                propData.objectReferenceValue = dataAsset;
            }

            var propCam = soController.FindProperty("_cameraTransform");
            if (propCam != null && Camera.main != null)
            {
                propCam.objectReferenceValue = Camera.main.transform;
            }
            soController.ApplyModifiedProperties();

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.scene);

            EditorUtility.DisplayDialog(
                "Success",
                "Player references wired successfully!\n\n- Input Actions attached\n- PlayerControllerData attached\n- Camera reference attached",
                "Awesome");
            Debug.Log("[Build & Bounce] Player references wired successfully!", player);
        }

        private static PlayerControllerData EnsurePlayerControllerDataAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:PlayerControllerData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<PlayerControllerData>(path);
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

            string assetPath = $"{dataFolder}/PlayerControllerData.asset";
            var data = ScriptableObject.CreateInstance<PlayerControllerData>();
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return data;
        }
    }
}
