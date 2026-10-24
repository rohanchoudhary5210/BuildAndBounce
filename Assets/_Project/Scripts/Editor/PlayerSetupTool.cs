using BuildAndBounce.Player;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
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

            // Ensure Player tag is "Player"
            if (!player.CompareTag("Player"))
            {
                player.tag = "Player";
            }

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

            // 5. Separate visual mesh into a child transform for safe squash and stretch
            Transform visualChild = player.transform.Find("Visual");
            if (visualChild == null)
            {
                var visualObj = new GameObject("Visual");
                Undo.RegisterCreatedObjectUndo(visualObj, "Create Player Visual Child");
                visualObj.transform.SetParent(player.transform, false);
                visualObj.transform.localPosition = Vector3.zero;
                visualObj.transform.localRotation = Quaternion.identity;
                visualObj.transform.localScale = Vector3.one;
                visualChild = visualObj.transform;

                // Move existing MeshFilter and MeshRenderer from root to Visual child
                if (player.TryGetComponent<MeshFilter>(out var rootFilter))
                {
                    var childFilter = visualObj.AddComponent<MeshFilter>();
                    childFilter.sharedMesh = rootFilter.sharedMesh;
                    Undo.DestroyObjectImmediate(rootFilter);
                }
                if (player.TryGetComponent<MeshRenderer>(out var rootRenderer))
                {
                    var childRenderer = visualObj.AddComponent<MeshRenderer>();
                    childRenderer.sharedMaterials = rootRenderer.sharedMaterials;
                    Undo.DestroyObjectImmediate(rootRenderer);
                }
            }

            // 6. Configure Game Feel Components (Visuals, Audio, Impulse Shake)
            if (!player.TryGetComponent<PlayerVisuals>(out var visuals))
            {
                visuals = Undo.AddComponent<PlayerVisuals>(player);
            }

            if (!player.TryGetComponent<PlayerAudio>(out var audio))
            {
                audio = Undo.AddComponent<PlayerAudio>(player);
            }

            if (!player.TryGetComponent<CinemachineImpulseSource>(out var impulse))
            {
                impulse = Undo.AddComponent<CinemachineImpulseSource>(player);
            }
            impulse.ImpulseDefinition.ImpulseDuration = 0.2f;
            impulse.ImpulseDefinition.DissipationDistance = 50f;

            // 7. Configure PlayerController Data & Camera
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

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    AutoConfigureCameraInternal(showDialog: false);
                }
            };
        }

        [MenuItem("Build & Bounce/Setup Third-Person Camera")]
        public static void SetupThirdPersonCamera()
        {
            AutoConfigureCameraInternal(showDialog: true);
        }

        public static void AutoConfigureCameraInternal(bool showDialog)
        {
            // Find player via FindAnyObjectByType only (no GameObject.Find)
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (controller == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Camera Setup", "Could not find a PlayerController component in the scene.", "OK");
                }
                return;
            }

            GameObject player = controller.gameObject;
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Camera Setup", "Could not find a Main Camera in the active scene.", "OK");
                }
                return;
            }

            // Ensure Player tag is "Player"
            if (!player.CompareTag("Player"))
            {
                Undo.RecordObject(player, "Set Player Tag");
                player.tag = "Player";
                EditorUtility.SetDirty(player);
            }

            Undo.RegisterFullObjectHierarchyUndo(mainCam.gameObject, "Setup Cinemachine Camera");

            // 1. Ensure CinemachineBrain on Main Camera
            if (!mainCam.TryGetComponent<CinemachineBrain>(out var brain))
            {
                brain = Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
            }
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.4f);
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
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
            vcam.Follow = player.transform;
            vcam.LookAt = player.transform;
            vcam.Target.TrackingTarget = player.transform;
            vcam.Target.LookAtTarget = player.transform;
            vcam.Target.CustomLookAtTarget = false;

            var soVcam = new SerializedObject(vcam);
            var targetProp = soVcam.FindProperty("Target");
            if (targetProp != null)
            {
                var trackingProp = targetProp.FindPropertyRelative("TrackingTarget");
                if (trackingProp != null) trackingProp.objectReferenceValue = player.transform;
                var lookAtProp = targetProp.FindPropertyRelative("LookAtTarget");
                if (lookAtProp != null) lookAtProp.objectReferenceValue = player.transform;
            }
            soVcam.ApplyModifiedProperties();

            // Compute target offset dynamically from player CharacterController bounds
            float targetHeight = 1.4f;
            if (player.TryGetComponent<CharacterController>(out var cc))
            {
                targetHeight = Mathf.Max(0.5f, cc.center.y + (cc.height * 0.25f));
            }

            // 3. Configure CinemachineOrbitalFollow
            if (!vcam.TryGetComponent<CinemachineOrbitalFollow>(out var orbital))
            {
                orbital = Undo.AddComponent<CinemachineOrbitalFollow>(vcam.gameObject);
            }
            Undo.RecordObject(orbital, "Configure Orbital Follow");

            // Read distance from serialized value if tuned, default 5m
            float distance = orbital.Radius > 0.1f ? orbital.Radius : 5.0f;
            orbital.Radius = distance;
            orbital.TargetOffset = new Vector3(0f, targetHeight, 0f);
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;

            // Pitch axis: start at 15 degrees elevation, range -20 to 60 degrees
            orbital.VerticalAxis.Value = 15f;
            orbital.VerticalAxis.Center = 15f;
            orbital.VerticalAxis.Range = new Vector2(-20f, 60f);
            orbital.VerticalAxis.Wrap = false;

            // Yaw axis: 360 degree orbit
            orbital.HorizontalAxis.Value = 0f;
            orbital.HorizontalAxis.Center = 0f;
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;

            // Lock radial scale so look input never shrinks camera distance
            orbital.RadialAxis.Value = 1f;
            orbital.RadialAxis.Center = 1f;
            orbital.RadialAxis.Range = new Vector2(1f, 1f);
            orbital.RadialAxis.Wrap = false;

            // Responsive position tracking (no lagging behind)
            orbital.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbital.TrackerSettings.PositionDamping = new Vector3(0.05f, 0.1f, 0.05f);
            orbital.TrackerSettings.RotationDamping = Vector3.zero;

            var soOrbital = new SerializedObject(orbital);
            var radiusProp = soOrbital.FindProperty("Radius");
            if (radiusProp != null) radiusProp.floatValue = distance;
            var offsetProp = soOrbital.FindProperty("TargetOffset");
            if (offsetProp != null) offsetProp.vector3Value = new Vector3(0f, targetHeight, 0f);
            soOrbital.ApplyModifiedProperties();
            EditorUtility.SetDirty(orbital);

            // 4. Configure CinemachineRotationComposer (Aim tracking)
            if (!vcam.TryGetComponent<CinemachineRotationComposer>(out var composer))
            {
                composer = Undo.AddComponent<CinemachineRotationComposer>(vcam.gameObject);
            }
            Undo.RecordObject(composer, "Configure Rotation Composer");
            composer.TargetOffset = new Vector3(0f, targetHeight, 0f);
            composer.Damping = new Vector2(0.05f, 0.05f);

            var soComposer = new SerializedObject(composer);
            var compOffsetProp = soComposer.FindProperty("TargetOffset");
            if (compOffsetProp != null) compOffsetProp.vector3Value = new Vector3(0f, targetHeight, 0f);
            soComposer.ApplyModifiedProperties();
            EditorUtility.SetDirty(composer);

            // 5. Configure CinemachineDeoccluder (Collision avoidance)
            if (!vcam.TryGetComponent<CinemachineDeoccluder>(out var deoccluder))
            {
                deoccluder = Undo.AddComponent<CinemachineDeoccluder>(vcam.gameObject);
            }
            Undo.RecordObject(deoccluder, "Configure Deoccluder");

            // Exclude Player layer and Ignore Raycast layer from obstacle detection
            int playerLayerMask = 1 << player.layer;
            int ignoreRaycastMask = LayerMask.GetMask("Ignore Raycast");
            deoccluder.CollideAgainst = ~(playerLayerMask | ignoreRaycastMask);
            deoccluder.IgnoreTag = "Player";
            deoccluder.MinimumDistanceFromTarget = 1.0f; // safely outside 0.5m player capsule
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.CameraRadius = 0.2f;
            deoccluder.AvoidObstacles.UseFollowTarget.Enabled = true;
            deoccluder.AvoidObstacles.UseFollowTarget.YOffset = targetHeight;
            deoccluder.AvoidObstacles.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            deoccluder.AvoidObstacles.SmoothingTime = 0.1f;
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
                        if (c == null || c.Input == null) continue;

                        if (c.Name.Contains("Look Orbit X") || (c.Name.Contains("Look") && !c.Name.Contains("Y")))
                        {
                            // Horizontal orbit yaw
                            c.Enabled = true;
                            c.Input.InputAction = lookRef;
                            c.Input.Gain = 1f;
                        }
                        else if (c.Name.Contains("Look Orbit Y"))
                        {
                            // Vertical pitch (inverted gain for natural upward tilt when looking up)
                            c.Enabled = true;
                            c.Input.InputAction = lookRef;
                            c.Input.Gain = -1f;
                        }
                        else if (c.Name.Contains("Scale") || c.Name.Contains("Zoom"))
                        {
                            // Never bind Look delta to radial orbit scale
                            c.Enabled = false;
                            c.Input.InputAction = null;
                        }
                    }
                }
            }
            EditorUtility.SetDirty(axisController);

            // 7. Ensure CinemachineImpulseListener on Virtual Camera for camera shake
            if (!vcam.TryGetComponent<CinemachineImpulseListener>(out var listener))
            {
                listener = Undo.AddComponent<CinemachineImpulseListener>(vcam.gameObject);
            }
            EditorUtility.SetDirty(listener);

            // 8. Ensure PlayerController references Main Camera
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

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Cinemachine Configured",
                    "Cinemachine 3 Third-Person Camera configured successfully!\n\n" +
                    "- CinemachineBrain on Main Camera (SmartUpdate)\n" +
                    $"- CinemachineCamera with OrbitalFollow (Radius {distance:F1}m, Sphere)\n" +
                    "- RotationComposer aiming at player\n" +
                    "- CinemachineDeoccluder (Collision avoidance excluding Player layer/tag)\n" +
                    "- CinemachineInputAxisController driving orbit from 'Player/Look'",
                    "Awesome!");
            }
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
