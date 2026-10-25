using System;
using System.Collections.Generic;
using BuildAndBounce.Level;
using BuildAndBounce.Player;
using BuildAndBounce.Race;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BuildAndBounce.Builder
{
    public enum BuilderMode
    {
        Build,
        Play
    }

    /// <summary>
    /// Central manager for M3 Level Builder (UGC):
    /// - Manages grid snapping, placement ghost/preview, and block rotation.
    /// - Executes Place and Delete operations via the Command pattern (CommandHistory).
    /// - Serializes/deserializes courses to JSON with share codes.
    /// - Smoothly toggles between Build mode and Play test mode.
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        [Header("Configuration Assets")]
        [SerializeField] private BlockCatalog _catalog;
        [SerializeField] private BuilderGrid _grid;

        [Header("References")]
        [SerializeField] private Transform _courseContainer;
        [SerializeField] private BuilderCamera _builderCamera;
        [SerializeField] private BuilderInputReader _inputReader;

        [Header("Mode Switch References")]
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private CinemachineCamera _playerCamera;
        [SerializeField] private RaceGhostManager _raceGhostManager;
        [SerializeField] private CourseStart _courseStart;

        [Header("Preview Colors")]
        [SerializeField] private Color _validPlacementColor = new Color(0.2f, 0.9f, 0.3f, 0.5f);
        [SerializeField] private Color _invalidPlacementColor = new Color(0.9f, 0.2f, 0.2f, 0.5f);

        [Header("Layer Masks")]
        [SerializeField] private LayerMask _buildRaycastLayers = ~0;

        private BuilderMode _currentMode = BuilderMode.Build;
        private int _selectedBlockIndex = 0;
        private float _currentRotationY = 0f;

        private readonly CommandHistory _history = new CommandHistory(50);
        private readonly Dictionary<GameObject, BlockData> _placedBlocks = new Dictionary<GameObject, BlockData>();
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private Camera _cachedBuilderCamera;

        private GameObject _previewInstance;
        private Renderer[] _previewRenderers;
        private Material _previewMaterial;

        private Vector3 _currentPlacementPosition;
        private bool _isPlacementValid;

        // Share code stays the same until the layout changes, so ghosts and leaderboards
        // keep matching the course across test runs (a new random code every export broke both).
        private string _shareCode = "";
        private bool _layoutChanged = true;

        public BuilderMode CurrentMode => _currentMode;
        public BlockCatalog Catalog => _catalog;
        public CommandHistory History => _history;
        public int SelectedBlockIndex => _selectedBlockIndex;
        public IReadOnlyDictionary<GameObject, BlockData> PlacedBlocks => _placedBlocks;

        public static event Action<BuilderMode> OnModeChanged;
        public static event Action<BlockData> OnSelectedBlockChanged;
        public static event Action<int> OnBlockCountChanged;
        public static event Action<string> OnValidationFailed;

        private void Awake()
        {
            if (_grid == null) _grid = GetComponent<BuilderGrid>() ?? gameObject.AddComponent<BuilderGrid>();

            if (_courseContainer == null)
            {
                var container = new GameObject("UGC_Course");
                container.transform.SetParent(transform, false);
                _courseContainer = container.transform;
            }

            if (_builderCamera == null)
            {
                _builderCamera = FindAnyObjectByType<BuilderCamera>(FindObjectsInactive.Include);
            }
            if (_builderCamera != null)
            {
                _cachedBuilderCamera = _builderCamera.GetComponent<Camera>();
            }
            if (_cachedBuilderCamera == null)
            {
                Debug.LogError("[LevelBuilder] BuilderCamera reference is missing! Wire it in Inspector or run Builder Setup Tool.", this);
            }

            if (_playerController == null)
            {
                _playerController = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (_playerController == null)
                {
                    Debug.LogError("[LevelBuilder] PlayerController reference is missing! Wire it in Inspector or run Builder Setup Tool.", this);
                }
            }
            if (_playerCamera == null)
            {
                _playerCamera = FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
                if (_playerCamera == null)
                {
                    Debug.LogError("[LevelBuilder] PlayerCamera (CinemachineCamera) reference is missing! Wire it in Inspector or run Builder Setup Tool.", this);
                }
            }
            if (_raceGhostManager == null)
            {
                _raceGhostManager = FindAnyObjectByType<RaceGhostManager>(FindObjectsInactive.Include);
            }
            if (_courseStart == null)
            {
                _courseStart = FindAnyObjectByType<CourseStart>(FindObjectsInactive.Include);
                if (_courseStart == null)
                {
                    Debug.LogError("[LevelBuilder] CourseStart reference is missing! Wire it in Inspector or run Builder Setup Tool.", this);
                }
            }

            if (_inputReader == null)
            {
                _inputReader = GetComponent<BuilderInputReader>() ?? gameObject.AddComponent<BuilderInputReader>();
            }

            EnsureCatalogLoaded();
            CreatePreviewMaterial();
        }

        private void OnEnable()
        {
            BuildAndBounce.UI.PauseMenu.OnPauseStateChanged += HandlePauseStateChanged;
        }

        private void OnDisable()
        {
            BuildAndBounce.UI.PauseMenu.OnPauseStateChanged -= HandlePauseStateChanged;
        }

        private void HandlePauseStateChanged(bool isPaused)
        {
            if (_previewInstance != null)
            {
                _previewInstance.SetActive(!isPaused && _currentMode == BuilderMode.Build);
            }

            if (!isPaused && _currentMode == BuilderMode.Build)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private Camera GetBuilderCamera()
        {
            if (_cachedBuilderCamera == null && _builderCamera != null)
            {
                _cachedBuilderCamera = _builderCamera.GetComponent<Camera>();
            }
            if (_cachedBuilderCamera == null)
            {
                Debug.LogError("[LevelBuilder] Missing BuilderCamera reference! Wire it in Inspector or run Builder Setup Tool.", this);
            }
            return _cachedBuilderCamera;
        }

        private void Start()
        {
            UpdatePreviewInstance();
            SetMode(BuilderMode.Build);
        }

        private void Update()
        {
            // Do not handle builder inputs if game is paused
            if (Time.timeScale == 0f)
            {
                if (_previewInstance != null && _previewInstance.activeSelf)
                {
                    _previewInstance.SetActive(false);
                }
                return;
            }

            // Restore preview if unpaused
            if (_previewInstance != null && !_previewInstance.activeSelf && _currentMode == BuilderMode.Build)
            {
                _previewInstance.SetActive(true);
            }

            // Typing a share code must not fire builder hotkeys (X deletes, Z undoes, Tab toggles mode...)
            if (BuilderInputReader.IsTypingInTextField) return;

            // Toggle mode on Action Map ToggleMode (Tab / Gamepad Select)
            if (_inputReader != null && _inputReader.ToggleModeTriggered)
            {
                ToggleMode();
            }

            if (_currentMode == BuilderMode.Build)
            {
                HandleBuildInput();
                UpdatePlacementPreview();
            }
        }

        public void SetMode(BuilderMode mode)
        {
            if (mode == BuilderMode.Play)
            {
                var validation = LevelValidator.Validate(_placedBlocks.Values);
                if (!validation.IsValid)
                {
                    Debug.LogWarning($"[LevelBuilder] Cannot test play: {validation.Message}");
                    OnValidationFailed?.Invoke(validation.Message);
                    return;
                }
            }

            _currentMode = mode;

            bool isBuild = _currentMode == BuilderMode.Build;

            if (_inputReader != null)
            {
                if (isBuild) _inputReader.EnableBuilderInputs();
                else _inputReader.DisableBuilderInputs();
            }

            // Toggle preview
            if (_previewInstance != null)
            {
                _previewInstance.SetActive(isBuild);
            }

            // Toggle cameras
            if (_builderCamera != null)
            {
                _builderCamera.gameObject.SetActive(isBuild);
            }

            // Moving platforms only move in Play mode, so blocks are edited and saved at their placed position
            SetMovingPlatformsRunning(!isBuild);

            // Toggle player and gameplay camera
            if (_playerController != null)
            {
                _playerController.gameObject.SetActive(!isBuild);

                if (isBuild)
                {
                    // Stop the timer, ghost recording and playback while editing
                    CourseStart.TriggerCourseReset();
                }
                else
                {
                    // Set active course ID for ghost recording and playback
                    var course = ExportCourseData();
                    if (_raceGhostManager != null && course != null)
                    {
                        _raceGhostManager.SetCourseId(course.shareCode);
                    }

                    // Resolve the Start pad of the built course (validation guarantees exactly one)
                    CourseStart activeStart = null;
                    foreach (var kvp in _placedBlocks)
                    {
                        if (kvp.Key != null && kvp.Key.activeInHierarchy)
                        {
                            activeStart = kvp.Key.GetComponentInChildren<CourseStart>();
                            if (activeStart != null) break;
                        }
                    }
                    if (activeStart == null)
                    {
                        activeStart = FindAnyObjectByType<CourseStart>(FindObjectsInactive.Exclude);
                    }
                    _courseStart = activeStart;

                    // PlayerRespawn must know the UGC start too, otherwise falling or "Restart Run"
                    // sends the player back to the scene's original start pad.
                    var respawn = _playerController.GetComponent<PlayerRespawn>();
                    if (respawn != null && _courseStart != null)
                    {
                        respawn.SetCourseStart(_courseStart);
                    }

                    // Trigger unified course reset (CourseStart, CourseFinish, Checkpoints, PlayerRespawn, CourseTimer, GhostRecorder)
                    CourseStart.TriggerCourseReset();

                    if (respawn != null)
                    {
                        respawn.Respawn();
                    }
                }
            }

            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(!isBuild);
            }

            // Cursor state
            if (isBuild)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            OnModeChanged?.Invoke(_currentMode);
        }

        private void SetMovingPlatformsRunning(bool running)
        {
            foreach (var kvp in _placedBlocks)
            {
                if (kvp.Key == null) continue;
                foreach (var platform in kvp.Key.GetComponentsInChildren<MovingPlatform>(true))
                {
                    if (!running) platform.ResetToStart();
                    platform.enabled = running;
                }
            }
        }

        public void ToggleMode()
        {
            SetMode(_currentMode == BuilderMode.Build ? BuilderMode.Play : BuilderMode.Build);
        }

        public void SelectBlock(int index)
        {
            if (_catalog == null || _catalog.Count == 0) return;

            _selectedBlockIndex = Mathf.Clamp(index, 0, _catalog.Count - 1);
            UpdatePreviewInstance();
            OnSelectedBlockChanged?.Invoke(_catalog.GetBlock(_selectedBlockIndex));
        }

        public void RotateBlock()
        {
            _currentRotationY = (_currentRotationY + 90f) % 360f;
            if (_previewInstance != null)
            {
                _previewInstance.transform.rotation = Quaternion.Euler(0f, _currentRotationY, 0f);
            }
        }

        public void SelectNextBlock()
        {
            if (_catalog != null && _catalog.Count > 0)
            {
                SelectBlock((_selectedBlockIndex + 1) % _catalog.Count);
            }
        }

        public void SelectPreviousBlock()
        {
            if (_catalog != null && _catalog.Count > 0)
            {
                SelectBlock((_selectedBlockIndex - 1 + _catalog.Count) % _catalog.Count);
            }
        }

        private void HandleBuildInput()
        {
            // Do not build through UI or while paused
            if (Time.timeScale == 0f || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }

            // Direct keyboard hotkeys 1-9 for palette block selection
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) SelectBlock(0);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) SelectBlock(1);
                else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) SelectBlock(2);
                else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) SelectBlock(3);
                else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) SelectBlock(4);
                else if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) SelectBlock(5);
                else if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) SelectBlock(6);
                else if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) SelectBlock(7);
                else if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) SelectBlock(8);
            }

            if (_inputReader == null) return;

            // Q/E fly the camera up/down while the right mouse button is held, so ignore palette keys then
            bool isFlyingCamera = Mouse.current != null && Mouse.current.rightButton.isPressed;

            // Palette navigation (Next / Previous block via Action Map)
            if (!isFlyingCamera && _inputReader.NextBlockTriggered)
            {
                SelectNextBlock();
            }
            else if (!isFlyingCamera && _inputReader.PrevBlockTriggered)
            {
                SelectPreviousBlock();
            }

            // Rotate
            if (_inputReader.RotateTriggered)
            {
                RotateBlock();
            }

            // Undo
            if (_inputReader.UndoTriggered)
            {
                _history.Undo();
                return;
            }

            // Redo
            if (_inputReader.RedoTriggered)
            {
                _history.Redo();
                return;
            }

            // Place
            if (_inputReader.PlaceTriggered && _isPlacementValid)
            {
                var blockData = _catalog.GetBlock(_selectedBlockIndex);
                if (blockData != null)
                {
                    // Enforce exactly 1 start platform at build-time:
                    // If placing a Start platform and one already exists, relocate it by removing the old one first
                    if (blockData.Category == BlockCategory.StartFinish && blockData.Id.ToLowerInvariant().Contains("start"))
                    {
                        GameObject existingStart = null;
                        foreach (var kvp in _placedBlocks)
                        {
                            if (kvp.Value != null && kvp.Value.Category == BlockCategory.StartFinish && kvp.Value.Id.ToLowerInvariant().Contains("start"))
                            {
                                existingStart = kvp.Key;
                                break;
                            }
                        }
                        if (existingStart != null)
                        {
                            // Go through the history (not Destroy) so Undo/Redo can't resurrect a second Start pad
                            _history.ExecuteCommand(new DeleteBlockCommand(this, existingStart, _placedBlocks[existingStart]));
                        }
                    }

                    var cmd = new PlaceBlockCommand(this, blockData, _currentPlacementPosition, Quaternion.Euler(0f, _currentRotationY, 0f));
                    _history.ExecuteCommand(cmd);

                    // Helpful workflow: If player just placed the Start Platform, advance selection to Finish Platform
                    if (_selectedBlockIndex == 0 && _catalog.Count > 1)
                    {
                        SelectBlock(1);
                    }
                }
            }

            // Delete
            if (_inputReader.DeleteTriggered)
            {
                DeleteBlockUnderCursor();
            }
        }

        private void DeleteBlockUnderCursor()
        {
            Camera cam = GetBuilderCamera();
            if (cam == null) return;

            Vector2 screenPos = _inputReader != null ? _inputReader.CursorPosition : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, _buildRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var root = hit.collider.transform;
                while (root.parent != null && root.parent != _courseContainer)
                {
                    root = root.parent;
                }

                if (_placedBlocks.TryGetValue(root.gameObject, out var blockData))
                {
                    var cmd = new DeleteBlockCommand(this, root.gameObject, blockData);
                    _history.ExecuteCommand(cmd);
                }
            }
        }

        private void UpdatePlacementPreview()
        {
            if (_previewInstance == null) return;

            Camera cam = GetBuilderCamera();
            if (cam == null) return;

            Vector2 screenPos = _inputReader != null ? _inputReader.CursorPosition : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = cam.ScreenPointToRay(screenPos);
            var blockData = _catalog.GetBlock(_selectedBlockIndex);
            Vector3 dims = blockData != null ? blockData.Dimensions : Vector3.one;

            // A 90/270 degree turn swaps the footprint's width and depth
            if (Mathf.RoundToInt(_currentRotationY / 90f) % 2 != 0)
            {
                dims = new Vector3(dims.z, dims.y, dims.x);
            }

            // Ignore trigger volumes (start/checkpoint/rider triggers sit above pads); they made blocks float
            if (Physics.Raycast(ray, out RaycastHit hit, 150f, _buildRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                _currentPlacementPosition = _grid.GetPlacementPosition(hit, dims);
            }
            else
            {
                _currentPlacementPosition = _grid.GetFloorPlacementPosition(ray, dims);
            }

            _previewInstance.transform.position = _currentPlacementPosition;
            _previewInstance.transform.rotation = Quaternion.Euler(0f, _currentRotationY, 0f);

            // Check collision bounds to ensure no overlapping pieces
            _isPlacementValid = CheckPlacementValid(_currentPlacementPosition, dims);
            SetPreviewColor(_isPlacementValid ? _validPlacementColor : _invalidPlacementColor);
        }

        private bool CheckPlacementValid(Vector3 center, Vector3 dims)
        {
            // Small inset to prevent adjacent faces from triggering false collisions
            Vector3 halfExtents = dims * 0.48f;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, Quaternion.identity, _buildRaycastLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i] != null && _overlapBuffer[i].transform.IsChildOf(_courseContainer))
                {
                    return false; // overlapping another placed block
                }
            }
            return true;
        }

        private void SetPreviewColor(Color color)
        {
            if (_previewMaterial != null)
            {
                _previewMaterial.color = color;
            }
        }

        private void UpdatePreviewInstance()
        {
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
            }

            var blockData = _catalog.GetBlock(_selectedBlockIndex);
            if (blockData == null) return;

            if (blockData.Prefab != null)
            {
                _previewInstance = Instantiate(blockData.Prefab);
            }
            else
            {
                _previewInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _previewInstance.transform.localScale = blockData.Dimensions;
            }

            _previewInstance.name = "PlacementPreview";

            // Strip colliders and scripts from preview
            var colliders = _previewInstance.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) Destroy(colliders[i]);

            var scripts = _previewInstance.GetComponentsInChildren<MonoBehaviour>();
            for (int i = 0; i < scripts.Length; i++) Destroy(scripts[i]);

            // Apply preview transparent material
            _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < _previewRenderers.Length; i++)
            {
                _previewRenderers[i].material = _previewMaterial;
            }
        }

        private void CreatePreviewMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            _previewMaterial = new Material(shader) { color = _validPlacementColor };
            BuildAndBounce.Core.TransparentMaterial.Apply(_previewMaterial);
        }

        public GameObject InstantiateBlockDirect(BlockData data, Vector3 position, Quaternion rotation)
        {
            GameObject instance;
            if (data.Prefab != null)
            {
                instance = Instantiate(data.Prefab, position, rotation, _courseContainer);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(_courseContainer, false);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.transform.localScale = data.Dimensions;

                var renderer = instance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = data.SharedMaterial;
                }
            }

            instance.name = $"{data.Id}_{_placedBlocks.Count + 1}";

            // Disabled before Start() runs, so the platform records its waypoints from the placed position
            if (_currentMode == BuilderMode.Build)
            {
                foreach (var platform in instance.GetComponentsInChildren<MovingPlatform>(true))
                {
                    platform.enabled = false;
                }
            }

            RegisterPlacedBlock(instance, data);
            return instance;
        }

        public void RegisterPlacedBlock(GameObject obj, BlockData data)
        {
            if (obj != null && data != null)
            {
                _placedBlocks[obj] = data;
                _layoutChanged = true;
                OnBlockCountChanged?.Invoke(_placedBlocks.Count);
            }
        }

        public void UnregisterPlacedBlock(GameObject obj)
        {
            if (obj != null)
            {
                _placedBlocks.Remove(obj);
                _layoutChanged = true;
                OnBlockCountChanged?.Invoke(_placedBlocks.Count);
            }
        }

        public CourseData GetCourseData() => ExportCourseData();

        public CourseData ExportCourseData(string courseName = "Custom Course")
        {
            var data = new CourseData { courseName = courseName };
            foreach (var kvp in _placedBlocks)
            {
                if (kvp.Key == null || !kvp.Key.activeInHierarchy) continue;

                var blockData = kvp.Value;
                var blockTransform = kvp.Key.transform;

                // A moving platform saved mid-ride would shift its whole path on reload
                var platform = kvp.Key.GetComponentInChildren<MovingPlatform>(true);
                Vector3 position = platform != null ? platform.HomePosition : blockTransform.position;

                data.blocks.Add(new PlacedBlockData(blockData.Id, position, blockTransform.eulerAngles));
            }
            if (_layoutChanged || string.IsNullOrEmpty(_shareCode))
            {
                _shareCode = BuildAndBounce.Net.ShareCodeGenerator.Generate(6);
                _layoutChanged = false;
            }
            data.shareCode = _shareCode;
            return data;
        }

        public void LoadCourseData(CourseData data)
        {
            if (data == null) return;

            ClearCourse();

            for (int i = 0; i < data.blocks.Count; i++)
            {
                var pb = data.blocks[i];
                var blockData = _catalog.GetBlockById(pb.blockId);
                if (blockData != null)
                {
                    InstantiateBlockDirect(blockData, pb.position, Quaternion.Euler(pb.rotation));
                }
            }

            // Keep the loaded code so ghosts and leaderboards stay attached to this course
            _shareCode = data.shareCode;
            _layoutChanged = string.IsNullOrEmpty(_shareCode);

            Debug.Log($"[LevelBuilder] Loaded course '{data.courseName}' with {data.blocks.Count} blocks (Code: {data.shareCode})");
            OnBlockCountChanged?.Invoke(_placedBlocks.Count);

            if (_raceGhostManager != null)
            {
                _raceGhostManager.SetCourseId(data.shareCode);
            }
        }

        public void ClearCourse()
        {
            _history.Clear();
            foreach (var kvp in _placedBlocks)
            {
                if (kvp.Key != null) Destroy(kvp.Key);
            }
            _placedBlocks.Clear();

            // Destroy any remaining blocks in container, including hidden/inactive ones
            if (_courseContainer != null)
            {
                for (int i = _courseContainer.childCount - 1; i >= 0; i--)
                {
                    var child = _courseContainer.GetChild(i);
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }

            _shareCode = "";
            _layoutChanged = true;
            OnBlockCountChanged?.Invoke(0);
        }

        private void EnsureCatalogLoaded()
        {
            if (_catalog != null) return;

            // Generate built-in default block catalog if not assigned
            _catalog = ScriptableObject.CreateInstance<BlockCatalog>();

            _catalog.AddBlock(new BlockData("start_pad", "Start Pad", BlockCategory.StartFinish, null, new Vector3(4f, 0.5f, 4f), new Color(0.2f, 0.85f, 0.35f)));
            _catalog.AddBlock(new BlockData("finish_pad", "Finish Pad", BlockCategory.StartFinish, null, new Vector3(5f, 0.5f, 5f), new Color(1f, 0.65f, 0.1f)));
            _catalog.AddBlock(new BlockData("platform_cube_1x1", "1x1 Block", BlockCategory.Platform, null, new Vector3(1f, 1f, 1f), new Color(0.6f, 0.6f, 0.7f)));
            _catalog.AddBlock(new BlockData("platform_step_3x3", "3x3 Platform", BlockCategory.Platform, null, new Vector3(3f, 0.5f, 3f), new Color(0.45f, 0.5f, 0.65f)));
            _catalog.AddBlock(new BlockData("checkpoint_pad", "Checkpoint", BlockCategory.Checkpoint, null, new Vector3(3f, 0.5f, 3f), new Color(0.9f, 0.85f, 0.2f)));
            _catalog.AddBlock(new BlockData("hazard_spikes", "Hazard", BlockCategory.Hazard, null, new Vector3(2f, 0.3f, 2f), new Color(0.9f, 0.2f, 0.25f)));
            _catalog.AddBlock(new BlockData("moving_platform", "Moving Platform", BlockCategory.Moving, null, new Vector3(3f, 0.5f, 3f), new Color(0.2f, 0.75f, 0.95f)));
        }
    }
}