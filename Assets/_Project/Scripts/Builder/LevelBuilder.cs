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

        private GameObject _previewInstance;
        private Renderer[] _previewRenderers;
        private Material _previewMaterial;

        private Vector3 _currentPlacementPosition;
        private bool _isPlacementValid;

        public BuilderMode CurrentMode => _currentMode;
        public BlockCatalog Catalog => _catalog;
        public CommandHistory History => _history;
        public int SelectedBlockIndex => _selectedBlockIndex;
        public IReadOnlyDictionary<GameObject, BlockData> PlacedBlocks => _placedBlocks;

        public static event Action<BuilderMode> OnModeChanged;
        public static event Action<BlockData> OnSelectedBlockChanged;
        public static event Action<int> OnBlockCountChanged;

        private void Awake()
        {
            if (_grid == null) _grid = GetComponent<BuilderGrid>() ?? gameObject.AddComponent<BuilderGrid>();

            if (_courseContainer == null)
            {
                var container = new GameObject("UGC_Course");
                container.transform.SetParent(transform, false);
                _courseContainer = container.transform;
            }

            EnsureCatalogLoaded();
            CreatePreviewMaterial();
        }

        private void Start()
        {
            UpdatePreviewInstance();
            SetMode(BuilderMode.Build);
        }

        private void Update()
        {
            // Toggle mode on Tab
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
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
                    return;
                }
            }

            _currentMode = mode;

            bool isBuild = _currentMode == BuilderMode.Build;

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

            // Toggle player and gameplay camera
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            var vcam = UnityEngine.Object.FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include);

            if (player != null)
            {
                player.gameObject.SetActive(!isBuild);

                if (!isBuild)
                {
                    // Set active course ID for ghost recording and playback
                    var course = ExportCourseData();
                    var ghostManager = UnityEngine.Object.FindAnyObjectByType<BuildAndBounce.Race.RaceGhostManager>();
                    if (ghostManager != null && course != null)
                    {
                        ghostManager.SetCourseId(course.shareCode);
                    }

                    // Trigger unified course reset (CourseStart, CourseFinish, Checkpoints, PlayerRespawn, CourseTimer, GhostRecorder)
                    CourseStart.TriggerCourseReset();

                    var startPad = UnityEngine.Object.FindAnyObjectByType<CourseStart>();
                    var respawn = player.GetComponent<PlayerRespawn>();
                    if (startPad != null && respawn != null)
                    {
                        respawn.RespawnAt(startPad.SpawnPosition, startPad.SpawnRotation);
                    }
                }
            }

            if (vcam != null)
            {
                vcam.gameObject.SetActive(!isBuild);
            }

            // Cursor state
            if (isBuild)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            OnModeChanged?.Invoke(_currentMode);
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

        private void HandleBuildInput()
        {
            // Do not build through UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Keyboard.current == null || Mouse.current == null) return;

            // Quick palette hotkeys [1] - [7]
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectBlock(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectBlock(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectBlock(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectBlock(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) SelectBlock(4);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) SelectBlock(5);
            if (Keyboard.current.digit7Key.wasPressedThisFrame) SelectBlock(6);

            // Rotate on R key
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RotateBlock();
            }

            // Undo on Ctrl+Z
            if (Keyboard.current.zKey.wasPressedThisFrame && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
            {
                _history.Undo();
                return;
            }

            // Redo on Ctrl+Y
            if (Keyboard.current.yKey.wasPressedThisFrame && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
            {
                _history.Redo();
                return;
            }

            // Place on Left Click
            if (Mouse.current.leftButton.wasPressedThisFrame && _isPlacementValid)
            {
                var blockData = _catalog.GetBlock(_selectedBlockIndex);
                if (blockData != null)
                {
                    var cmd = new PlaceBlockCommand(this, blockData, _currentPlacementPosition, Quaternion.Euler(0f, _currentRotationY, 0f));
                    _history.ExecuteCommand(cmd);
                }
            }

            // Delete on Right Click (when not dragging orbit) or X key
            if (Keyboard.current.xKey.wasPressedThisFrame)
            {
                DeleteBlockUnderCursor();
            }
        }

        private void DeleteBlockUnderCursor()
        {
            Camera cam = _builderCamera != null ? _builderCamera.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, _buildRaycastLayers))
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

            Camera cam = _builderCamera != null ? _builderCamera.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var blockData = _catalog.GetBlock(_selectedBlockIndex);
            Vector3 dims = blockData != null ? blockData.Dimensions : Vector3.one;

            if (Physics.Raycast(ray, out RaycastHit hit, 150f, _buildRaycastLayers))
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
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, Quaternion.Euler(0f, _currentRotationY, 0f), _buildRaycastLayers);

            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i].transform.IsChildOf(_courseContainer))
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
            if (_previewMaterial.HasProperty("_Surface")) _previewMaterial.SetFloat("_Surface", 1); // Transparent
            if (_previewMaterial.HasProperty("_Blend")) _previewMaterial.SetFloat("_Blend", 0);
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
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    renderer.material = new Material(shader) { color = data.ThemeColor };
                }
            }

            instance.name = $"{data.Id}_{_placedBlocks.Count + 1}";
            RegisterPlacedBlock(instance, data);
            return instance;
        }

        public void RegisterPlacedBlock(GameObject obj, BlockData data)
        {
            if (obj != null && data != null)
            {
                _placedBlocks[obj] = data;
                OnBlockCountChanged?.Invoke(_placedBlocks.Count);
            }
        }

        public void UnregisterPlacedBlock(GameObject obj)
        {
            if (obj != null)
            {
                _placedBlocks.Remove(obj);
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
                var transform = kvp.Key.transform;
                data.blocks.Add(new PlacedBlockData(blockData.Id, transform.position, transform.eulerAngles));
            }
            data.shareCode = CourseSerializer.GenerateShareCode(data);
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

            Debug.Log($"[LevelBuilder] Loaded course '{data.courseName}' with {data.blocks.Count} blocks (Code: {data.shareCode})");
            OnBlockCountChanged?.Invoke(_placedBlocks.Count);

            var ghostManager = UnityEngine.Object.FindAnyObjectByType<BuildAndBounce.Race.RaceGhostManager>();
            if (ghostManager != null)
            {
                ghostManager.SetCourseId(data.shareCode);
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