using BuildAndBounce.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildAndBounce.Editor
{
    /// <summary>
    /// Editor utility to automatically connect and validate references on the Player GameObject
    /// and configure the Cinemachine third-person camera system with collision de-occlusion.
    /// </summary>
    public static class PlayerSetupTool
    {
        [MenuItem("Build & Bounce/Wire Player References")]
        public static void WirePlayerReferences()
        {
            // 1. Locate Player via PlayerController (avoid string-based GameObject.Find)
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "Wire Player",
                    "Could not find a GameObject with a PlayerController component in the active scene.",
                    "OK");
                return;
            }

            GameObject player = controller.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(player, "Wire Player References");

            // 2. Configure CharacterController
            if (!player.TryGetComponent<CharacterController>(out var cc))
            {
                cc = Undo.AddComponent<CharacterController>(player);
            }

            // For a standard Unity Capsule primitive, the mesh is centered at local (0, 0, 0).
            // Setting center to Vector3.zero perfectly matches the visual capsule bounds so it doesn't sink into the ground.
            cc.center = Vector3.zero;
            cc.height = 2f;
            cc.radius = 0.5f;

            // Remove redundant CapsuleCollider from the 3D primitive to avoid physics conflicts with CharacterController
            if (player.TryGetComponent<CapsuleCollider>(out var redundantCollider))
            {
                Undo.DestroyObjectImmediate(redundantCollider);
            }

            // Lift player above floor if currently placed at or below ground
            if (player.transform.position.y < 1.1f)
            {
                player.transform.position = new Vector3(player.transform.position.x, 2f, player.transform.position.z);
            }

            // 3. Configure PlayerInput
            if (!player.TryGetComponent<PlayerInput>(out var playerInput))
            {
                playerInput = Undo.AddComponent<PlayerInput>(player);
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
                inputReader = Undo.AddComponent<PlayerInputReader>(player);
            }

            var soReader = new SerializedObject(inputReader);
            var propInput = soReader.FindProperty("_playerInput");
            if (propInput != null)
            {
                propInput.objectReferenceValue = playerInput;
            }
            soReader.ApplyModifiedProperties();

            // 5. Configure PlayerController Data & Camera
            var dataAsset = EnsurePlayerControllerDataAsset();

            var soController = new SerializedObject(controller);
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

        [MenuItem("Build & Bounce/Setup Third-Person Camera")]
        public static void SetupThirdPersonCamera()
        {
            // Find player via FindAnyObjectByType only (no GameObject.Find)
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Camera Setup", "Could not find a PlayerController component in the scene.", "OK");
                return;
            }

            GameObject player = controller.gameObject;
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Camera Setup", "Could not find a Main Camera in the active scene.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(mainCam.gameObject, "Setup Cinemachine Camera");

            // 1. Ensure CinemachineBrain on Main Camera
            if (!mainCam.TryGetComponent<CinemachineBrain>(out var brain))
            {
                brain = Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
            }
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.4f);
            EditorUtility.SetDirty(brain);

            // 2. Find or create CinemachineCamera GameObject
            var vcam = UnityEngine.Object.FindAnyObjectByType<CinemachineCamera>();
            if (vcam == null)
            {
                var vcamObj = new GameObject("CM_ThirdPersonCamera");
                Undo.RegisterCreatedObjectUndo(vcamObj, "Create Cinemachine Camera");
                vcam = vcamObj.AddComponent<CinemachineCamera>();
            }

            Undo.RecordObject(vcam, "Configure Cinemachine Camera");
            vcam.Target.TrackingTarget = player.transform;

            // Compute target offset dynamically from player CharacterController bounds
            float targetHeight = 1.4f;
            if (player.TryGetComponent<CharacterController>(out var cc))
            {
                targetHeight = cc.center.y + (cc.height * 0.25f);
            }

            // 3. Configure CinemachineOrbitalFollow
            if (!vcam.TryGetComponent<CinemachineOrbitalFollow>(out var orbital))
            {
                orbital = Undo.AddComponent<CinemachineOrbitalFollow>(vcam.gameObject);
            }
            Undo.RecordObject(orbital, "Configure Orbital Follow");
            orbital.TargetOffset = new Vector3(0f, targetHeight, 0f);
            orbital.Radius = 5.0f;
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            EditorUtility.SetDirty(orbital);

            // 4. Configure CinemachineRotationComposer (Aim tracking)
            if (!vcam.TryGetComponent<CinemachineRotationComposer>(out var composer))
            {
                composer = Undo.AddComponent<CinemachineRotationComposer>(vcam.gameObject);
            }
            Undo.RecordObject(composer, "Configure Rotation Composer");
            composer.TargetOffset = new Vector3(0f, targetHeight, 0f);
            EditorUtility.SetDirty(composer);

            // 5. Configure CinemachineDeoccluder (Collision avoidance)
            if (!vcam.TryGetComponent<CinemachineDeoccluder>(out var deoccluder))
            {
                deoccluder = Undo.AddComponent<CinemachineDeoccluder>(vcam.gameObject);
            }
            Undo.RecordObject(deoccluder, "Configure Deoccluder");
            deoccluder.CollideAgainst = ~LayerMask.GetMask("Ignore Raycast");
            deoccluder.IgnoreTag = "Player";
            deoccluder.AvoidObstacles.Enabled = true;
            EditorUtility.SetDirty(deoccluder);

            // 6. Configure CinemachineInputAxisController (Drive orbit from Look action)
            if (!vcam.TryGetComponent<CinemachineInputAxisController>(out var axisController))
            {
                axisController = Undo.AddComponent<CinemachineInputAxisController>(vcam.gameObject);
            }
            Undo.RecordObject(axisController, "Configure Input Axis Controller");
            axisController.AutoEnableInputs = true;
            axisController.SynchronizeControllers();

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputActions != null)
            {
                var lookAction = inputActions.FindAction("Player/Look");
                if (lookAction != null)
                {
                    var lookRef = InputActionReference.Create(lookAction);
                    foreach (var c in axisController.Controllers)
                    {
                        if (c != null && c.Input != null)
                        {
                            c.Input.InputAction = lookRef;
                            c.Input.Gain = 1f;
                        }
                    }
                }
            }
            EditorUtility.SetDirty(axisController);

            // 7. Ensure PlayerController references Main Camera
            Undo.RecordObject(controller, "Set Camera Transform");
            var soController = new SerializedObject(controller);
            var propCam = soController.FindProperty("_cameraTransform");
            if (propCam != null)
            {
                propCam.objectReferenceValue = mainCam.transform;
            }
            soController.ApplyModifiedProperties();

            EditorUtility.SetDirty(vcam);
            EditorUtility.SetDirty(mainCam);
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.scene);

            EditorUtility.DisplayDialog(
                "Cinemachine Configured",
                "Cinemachine 3 Third-Person Camera configured successfully!\n\n" +
                "- CinemachineBrain on Main Camera\n" +
                "- CinemachineCamera with OrbitalFollow (Radius 5m)\n" +
                "- RotationComposer aiming at player\n" +
                "- CinemachineDeoccluder (Collision avoidance against obstacles)\n" +
                "- CinemachineInputAxisController driving orbit from 'Player/Look'",
                "Awesome!");
            Debug.Log("[Build & Bounce] Cinemachine 3 camera setup complete!", vcam);
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
