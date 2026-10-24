using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// In-game UI for M3 Level Builder:
    /// - Palette selector for blocks
    /// - One-click mode toggle (Play / Build)
    /// - Undo, Redo, Clear, Save, and Load buttons
    /// - Share code popup and validation alerts
    /// </summary>
    public class BuilderHUD : MonoBehaviour
    {
        [Header("References (Auto-built if unassigned)")]
        [SerializeField] private LevelBuilder _builder;
        [SerializeField] private GameObject _hudRoot;
        [SerializeField] private Button _modeToggleButton;
        [SerializeField] private TextMeshProUGUI _modeToggleText;
        [SerializeField] private Button _undoButton;
        [SerializeField] private Button _redoButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _clearButton;
        [SerializeField] private TextMeshProUGUI _statusToastText;
        [SerializeField] private Transform _paletteContainer;

        private readonly List<Button> _paletteButtons = new List<Button>();
        private Coroutine _toastRoutine;

        private void Awake()
        {
            if (_builder == null) _builder = UnityEngine.Object.FindAnyObjectByType<LevelBuilder>();

            if (_hudRoot == null)
            {
                BuildHUDHierarchy();
            }
        }

        private void OnEnable()
        {
            LevelBuilder.OnModeChanged += HandleModeChanged;
            LevelBuilder.OnSelectedBlockChanged += HandleSelectedBlockChanged;
            if (_builder != null && _builder.History != null)
            {
                _builder.History.OnHistoryChanged += UpdateHistoryButtons;
            }
        }

        private void OnDisable()
        {
            LevelBuilder.OnModeChanged -= HandleModeChanged;
            LevelBuilder.OnSelectedBlockChanged -= HandleSelectedBlockChanged;
            if (_builder != null && _builder.History != null)
            {
                _builder.History.OnHistoryChanged -= UpdateHistoryButtons;
            }
        }

        private void Start()
        {
            PopulatePalette();
            UpdateHistoryButtons();
        }

        private void HandleModeChanged(BuilderMode mode)
        {
            if (_hudRoot != null)
            {
                _hudRoot.SetActive(mode == BuilderMode.Build);
            }

            if (_modeToggleText != null)
            {
                _modeToggleText.text = mode == BuilderMode.Build ? "TEST PLAY [Tab]" : "EDIT [Tab]";
            }
        }

        private void HandleSelectedBlockChanged(BlockData data)
        {
            UpdatePaletteSelection();
        }

        public void ToggleMode()
        {
            if (_builder != null)
            {
                _builder.ToggleMode();
            }
        }

        public void UndoAction()
        {
            if (_builder != null) _builder.History.Undo();
        }

        public void RedoAction()
        {
            if (_builder != null) _builder.History.Redo();
        }

        public void SaveCourse()
        {
            if (_builder == null) return;

            var validation = LevelValidator.Validate(_builder.PlacedBlocks.Values);
            if (!validation.IsValid)
            {
                ShowToast($"<color=#FF5555>{validation.Message}</color>");
                return;
            }

            var course = _builder.ExportCourseData("MyCourse");
            CourseSerializer.SaveCourse(course);
            ShowToast($"<color=#55FF55>SAVED!</color> Share Code: <b>{course.shareCode}</b>");
        }

        public void LoadLatestCourse()
        {
            if (_builder == null) return;

            var files = CourseSerializer.GetSavedCourseFiles();
            if (files.Count == 0)
            {
                ShowToast("<color=#FFAA00>No saved courses found.</color>");
                return;
            }

            var course = CourseSerializer.LoadCourse(files[files.Count - 1]);
            if (course != null)
            {
                _builder.LoadCourseData(course);
                ShowToast($"<color=#55FF55>LOADED:</color> {course.courseName} ({course.shareCode})");
            }
        }

        public void ClearCourse()
        {
            if (_builder != null)
            {
                _builder.ClearCourse();
                ShowToast("Course cleared.");
            }
        }

        public void ShowToast(string message)
        {
            if (_statusToastText == null) return;

            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            _statusToastText.text = message;
            _statusToastText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(3.5f);
            _statusToastText.gameObject.SetActive(false);
        }

        private void UpdateHistoryButtons()
        {
            if (_builder == null || _builder.History == null) return;

            if (_undoButton != null) _undoButton.interactable = _builder.History.CanUndo;
            if (_redoButton != null) _redoButton.interactable = _builder.History.CanRedo;
        }

        private void PopulatePalette()
        {
            if (_builder == null || _builder.Catalog == null || _paletteContainer == null) return;

            // Clear previous buttons
            for (int i = 0; i < _paletteButtons.Count; i++)
            {
                if (_paletteButtons[i] != null) Destroy(_paletteButtons[i].gameObject);
            }
            _paletteButtons.Clear();

            var catalog = _builder.Catalog;
            for (int i = 0; i < catalog.Count; i++)
            {
                int index = i;
                var block = catalog.GetBlock(i);

                var btnObj = new GameObject($"Btn_{block.Id}");
                btnObj.transform.SetParent(_paletteContainer, false);

                var rect = btnObj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(100f, 60f);

                var img = btnObj.AddComponent<Image>();
                img.color = block.ThemeColor;

                var btn = btnObj.AddComponent<Button>();
                btn.onClick.AddListener(() => _builder.SelectBlock(index));

                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                var lRect = labelObj.AddComponent<RectTransform>();
                lRect.anchorMin = Vector2.zero;
                lRect.anchorMax = Vector2.one;
                lRect.offsetMin = new Vector2(4f, 4f);
                lRect.offsetMax = new Vector2(-4f, -4f);

                var tmp = labelObj.AddComponent<TextMeshProUGUI>();
                tmp.text = block.DisplayName;
                tmp.fontSize = 12;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                _paletteButtons.Add(btn);
            }

            UpdatePaletteSelection();
        }

        private void UpdatePaletteSelection()
        {
            if (_builder == null) return;
            int sel = _builder.SelectedBlockIndex;

            for (int i = 0; i < _paletteButtons.Count; i++)
            {
                if (_paletteButtons[i] == null) continue;
                var outline = _paletteButtons[i].GetComponent<Outline>();
                if (i == sel)
                {
                    if (outline == null) outline = _paletteButtons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.white;
                    outline.effectDistance = new Vector2(3f, 3f);
                }
                else if (outline != null)
                {
                    Destroy(outline);
                }
            }
        }

        private void BuildHUDHierarchy()
        {
            var canvasObj = new GameObject("BuilderHUD_Canvas");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _hudRoot = canvasObj;

            // Top Toolbar Bar
            var topBar = new GameObject("TopBar");
            topBar.transform.SetParent(canvasObj.transform, false);
            var tbRect = topBar.AddComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0f, 1f);
            tbRect.anchorMax = new Vector2(1f, 1f);
            tbRect.pivot = new Vector2(0.5f, 1f);
            tbRect.anchoredPosition = Vector2.zero;
            tbRect.sizeDelta = new Vector2(0f, 60f);

            var tbBg = topBar.AddComponent<Image>();
            tbBg.color = new Color(0.08f, 0.1f, 0.16f, 0.9f);

            // Left actions: Undo, Redo, Clear, Save, Load
            _undoButton = CreateToolButton(topBar.transform, new Vector2(60f, -30f), "Undo", UndoAction);
            _redoButton = CreateToolButton(topBar.transform, new Vector2(140f, -30f), "Redo", RedoAction);
            _clearButton = CreateToolButton(topBar.transform, new Vector2(230f, -30f), "Clear", ClearCourse);
            _saveButton = CreateToolButton(topBar.transform, new Vector2(320f, -30f), "Save", SaveCourse);
            _loadButton = CreateToolButton(topBar.transform, new Vector2(400f, -30f), "Load", LoadLatestCourse);

            // Right action: Mode toggle (Play / Edit)
            _modeToggleButton = CreateToolButton(topBar.transform, new Vector2(-120f, -30f), "PLAY [Tab]", ToggleMode);
            var mtRect = _modeToggleButton.GetComponent<RectTransform>();
            mtRect.anchorMin = new Vector2(1f, 1f);
            mtRect.anchorMax = new Vector2(1f, 1f);
            mtRect.sizeDelta = new Vector2(160f, 42f);
            _modeToggleButton.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.35f, 1f);
            _modeToggleText = _modeToggleButton.GetComponentInChildren<TextMeshProUGUI>();

            // Status Toast (Center Top)
            var toastObj = new GameObject("Toast");
            toastObj.transform.SetParent(canvasObj.transform, false);
            var tRect = toastObj.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 0.9f);
            tRect.anchorMax = new Vector2(0.5f, 0.9f);
            tRect.pivot = new Vector2(0.5f, 0.5f);
            tRect.sizeDelta = new Vector2(600f, 50f);

            _statusToastText = toastObj.AddComponent<TextMeshProUGUI>();
            _statusToastText.alignment = TextAlignmentOptions.Center;
            _statusToastText.fontSize = 22;
            _statusToastText.color = Color.white;
            toastObj.SetActive(false);

            // Bottom Palette Bar
            var bottomBar = new GameObject("PaletteBar");
            bottomBar.transform.SetParent(canvasObj.transform, false);
            var bbRect = bottomBar.AddComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0.5f, 0f);
            bbRect.anchorMax = new Vector2(0.5f, 0f);
            bbRect.pivot = new Vector2(0.5f, 0f);
            bbRect.anchoredPosition = new Vector2(0f, 20f);
            bbRect.sizeDelta = new Vector2(800f, 80f);

            var bbBg = bottomBar.AddComponent<Image>();
            bbBg.color = new Color(0.08f, 0.1f, 0.16f, 0.9f);

            var layout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;

            _paletteContainer = bottomBar.transform;
        }

        private Button CreateToolButton(Transform parent, Vector2 pos, string label, UnityEngine.Events.UnityAction action)
        {
            var btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(70f, 36f);

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.3f, 1f);

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var tRect = textObj.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }
    }
}