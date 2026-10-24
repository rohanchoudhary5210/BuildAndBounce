using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// In-game UI for M3 Level Builder:
    /// - Top toolbar with clean HorizontalLayoutGroup for actions (Undo, Redo, Clear, Save, Load, Share)
    /// - Center badge with live placed block counter
    /// - Test Play toggle button in header
    /// - Controls guide banner floating above palette
    /// - Palette selector with high-contrast cards, color stripes, and hotkey labels
    /// - Toast notification overlay
    /// </summary>
    public class BuilderHUD : MonoBehaviour
    {
        [Header("References (Auto-built if unassigned)")]
        [SerializeField] private LevelBuilder _builder;
        [SerializeField] private BuildAndBounce.UI.OnlineShareHUD _shareHud;
        [SerializeField] private GameObject _hudRoot;
        [SerializeField] private Button _modeToggleButton;
        [SerializeField] private TextMeshProUGUI _modeToggleText;
        [SerializeField] private Button _undoButton;
        [SerializeField] private Button _redoButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _clearButton;
        [SerializeField] private Button _shareButton;
        [SerializeField] private TextMeshProUGUI _blockCountText;
        [SerializeField] private GameObject _toastRoot;
        [SerializeField] private TextMeshProUGUI _statusToastText;
        [SerializeField] private Transform _paletteContainer;

        private readonly List<Button> _paletteButtons = new List<Button>();
        private readonly List<Image> _paletteCardBgs = new List<Image>();
        private Coroutine _toastRoutine;

        private static readonly Color CardBgDefault = new Color(0.11f, 0.14f, 0.20f, 0.98f);
        private static readonly Color CardBgSelected = new Color(0.18f, 0.23f, 0.34f, 1f);
        private static readonly Color AccentCyan = new Color(0.0f, 0.9f, 1.0f, 1f);

        private void Awake()
        {
            if (_builder == null)
            {
                Debug.LogError("[BuilderHUD] LevelBuilder reference is missing! Wire it in Inspector or run Builder Setup Tool.", this);
            }
            if (_shareHud == null)
            {
                Debug.LogError("[BuilderHUD] OnlineShareHUD reference is missing! Wire it in Inspector or run Builder Setup Tool.", this);
            }

            if (_hudRoot == null)
            {
                var existingCanvas = transform.Find("BuilderHUD_Canvas");
                if (existingCanvas != null)
                {
                    DestroyImmediate(existingCanvas.gameObject);
                }
                BuildHUDHierarchy();
            }
        }

        private void OnEnable()
        {
            LevelBuilder.OnModeChanged += HandleModeChanged;
            LevelBuilder.OnSelectedBlockChanged += HandleSelectedBlockChanged;
            LevelBuilder.OnBlockCountChanged += HandleBlockCountChanged;
            LevelBuilder.OnValidationFailed += HandleValidationFailed;
            if (_builder != null && _builder.History != null)
            {
                _builder.History.OnHistoryChanged += UpdateHistoryButtons;
            }
        }

        private void OnDisable()
        {
            LevelBuilder.OnModeChanged -= HandleModeChanged;
            LevelBuilder.OnSelectedBlockChanged -= HandleSelectedBlockChanged;
            LevelBuilder.OnBlockCountChanged -= HandleBlockCountChanged;
            LevelBuilder.OnValidationFailed -= HandleValidationFailed;
            if (_builder != null && _builder.History != null)
            {
                _builder.History.OnHistoryChanged -= UpdateHistoryButtons;
            }
        }

        private void HandleValidationFailed(string message)
        {
            ShowToast($"<color=#FF5555>Cannot Test Play:</color> {message}");
        }

        private void Start()
        {
            PopulatePalette();
            UpdateHistoryButtons();
            int count = _builder != null && _builder.PlacedBlocks != null ? _builder.PlacedBlocks.Count : 0;
            HandleBlockCountChanged(count);

            if (count == 0)
            {
                ShowToast("<color=#00E5FF>Build Mode:</color> Place Start & Finish pads, then press [Tab] to Test Play!");
            }
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

        private void HandleBlockCountChanged(int count)
        {
            if (_blockCountText != null)
            {
                _blockCountText.text = $"<b>BUILD MODE</b>  •  <color=#00E5FF>{count}</color> Blocks";
            }
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

            var course = _builder.ExportCourseData($"Course_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
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

        public void OpenShareDialog()
        {
            if (_shareHud != null)
            {
                _shareHud.ToggleDialog();
            }
            else
            {
                SaveCourse();
            }
        }

        public void ShowToast(string message)
        {
            if (_statusToastText == null || _toastRoot == null) return;

            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            _statusToastText.text = message;
            _toastRoot.SetActive(true);
            yield return new WaitForSecondsRealtime(3.5f);
            _toastRoot.SetActive(false);
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

            for (int i = 0; i < _paletteButtons.Count; i++)
            {
                if (_paletteButtons[i] != null) Destroy(_paletteButtons[i].gameObject);
            }
            _paletteButtons.Clear();
            _paletteCardBgs.Clear();

            var catalog = _builder.Catalog;
            for (int i = 0; i < catalog.Count; i++)
            {
                int index = i;
                var block = catalog.GetBlock(i);

                // Card container button
                var btnObj = new GameObject($"Btn_{block.Id}");
                btnObj.transform.SetParent(_paletteContainer, false);

                var rect = btnObj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(110f, 62f);

                var cardBg = btnObj.AddComponent<Image>();
                cardBg.color = CardBgDefault;
                _paletteCardBgs.Add(cardBg);

                var btn = btnObj.AddComponent<Button>();
                btn.onClick.AddListener(() => _builder.SelectBlock(index));

                // Top Accent Stripe
                var stripeObj = new GameObject("Stripe");
                stripeObj.transform.SetParent(btnObj.transform, false);
                var sRect = stripeObj.AddComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0f, 1f);
                sRect.anchorMax = new Vector2(1f, 1f);
                sRect.pivot = new Vector2(0.5f, 1f);
                sRect.anchoredPosition = Vector2.zero;
                sRect.sizeDelta = new Vector2(0f, 5f);
                var sImg = stripeObj.AddComponent<Image>();
                sImg.color = block.ThemeColor;

                // Hotkey badge [1]..[7]
                if (i < 9)
                {
                    var badgeObj = new GameObject("HotkeyBadge");
                    badgeObj.transform.SetParent(btnObj.transform, false);
                    var bRect = badgeObj.AddComponent<RectTransform>();
                    bRect.anchorMin = new Vector2(1f, 1f);
                    bRect.anchorMax = new Vector2(1f, 1f);
                    bRect.pivot = new Vector2(1f, 1f);
                    bRect.anchoredPosition = new Vector2(-4f, -7f);
                    bRect.sizeDelta = new Vector2(18f, 14f);

                    var bTmp = badgeObj.AddComponent<TextMeshProUGUI>();
                    bTmp.text = $"{i + 1}";
                    bTmp.fontSize = 10;
                    bTmp.fontStyle = FontStyles.Bold;
                    bTmp.alignment = TextAlignmentOptions.Center;
                    bTmp.color = new Color(0.55f, 0.65f, 0.8f, 1f);
                }

                // Block Label
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                var lRect = labelObj.AddComponent<RectTransform>();
                lRect.anchorMin = Vector2.zero;
                lRect.anchorMax = Vector2.one;
                lRect.offsetMin = new Vector2(6f, 4f);
                lRect.offsetMax = new Vector2(-6f, -12f);

                var tmp = labelObj.AddComponent<TextMeshProUGUI>();
                tmp.text = block.DisplayName;
                tmp.fontSize = 12;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.textWrappingMode = TextWrappingModes.Normal;

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
                bool isSelected = (i == sel);

                if (isSelected)
                {
                    if (outline == null) outline = _paletteButtons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = AccentCyan;
                    outline.effectDistance = new Vector2(2.5f, 2.5f);
                    if (i < _paletteCardBgs.Count && _paletteCardBgs[i] != null)
                    {
                        _paletteCardBgs[i].color = CardBgSelected;
                    }
                }
                else
                {
                    if (outline != null) Destroy(outline);
                    if (i < _paletteCardBgs.Count && _paletteCardBgs[i] != null)
                    {
                        _paletteCardBgs[i].color = CardBgDefault;
                    }
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

            // 1. Top Bar
            var topBar = new GameObject("TopBar");
            topBar.transform.SetParent(canvasObj.transform, false);
            var tbRect = topBar.AddComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0f, 1f);
            tbRect.anchorMax = new Vector2(1f, 1f);
            tbRect.pivot = new Vector2(0.5f, 1f);
            tbRect.anchoredPosition = Vector2.zero;
            tbRect.sizeDelta = new Vector2(0f, 60f);

            var tbBg = topBar.AddComponent<Image>();
            tbBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

            // Left actions layout container
            var leftGroup = new GameObject("LeftActionsGroup");
            leftGroup.transform.SetParent(topBar.transform, false);
            var lgRect = leftGroup.AddComponent<RectTransform>();
            lgRect.anchorMin = new Vector2(0f, 0.5f);
            lgRect.anchorMax = new Vector2(0f, 0.5f);
            lgRect.pivot = new Vector2(0f, 0.5f);
            lgRect.anchoredPosition = new Vector2(16f, 0f);
            lgRect.sizeDelta = new Vector2(600f, 44f);

            var hLayout = leftGroup.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 8f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;

            _undoButton = CreateToolButton(leftGroup.transform, "Undo", UndoAction, 78f);
            _redoButton = CreateToolButton(leftGroup.transform, "Redo", RedoAction, 78f);
            _clearButton = CreateToolButton(leftGroup.transform, "Clear", ClearCourse, 72f);
            _saveButton = CreateToolButton(leftGroup.transform, "Save", SaveCourse, 72f);
            _loadButton = CreateToolButton(leftGroup.transform, "Load", LoadLatestCourse, 72f);
            _shareButton = CreateToolButton(leftGroup.transform, "Share [O]", OpenShareDialog, 92f, AccentCyan);

            // Center: Block Count Badge
            var centerBadge = new GameObject("CenterBadge");
            centerBadge.transform.SetParent(topBar.transform, false);
            var cbRect = centerBadge.AddComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0.5f, 0.5f);
            cbRect.anchorMax = new Vector2(0.5f, 0.5f);
            cbRect.pivot = new Vector2(0.5f, 0.5f);
            cbRect.anchoredPosition = Vector2.zero;
            cbRect.sizeDelta = new Vector2(250f, 36f);

            var cbBg = centerBadge.AddComponent<Image>();
            cbBg.color = new Color(0.12f, 0.16f, 0.24f, 0.9f);
            cbBg.raycastTarget = false;

            var cbTextObj = new GameObject("Text");
            cbTextObj.transform.SetParent(centerBadge.transform, false);
            var cbTRect = cbTextObj.AddComponent<RectTransform>();
            cbTRect.anchorMin = Vector2.zero;
            cbTRect.anchorMax = Vector2.one;

            _blockCountText = cbTextObj.AddComponent<TextMeshProUGUI>();
            _blockCountText.text = "<b>BUILD MODE</b>  •  <color=#00E5FF>0</color> Blocks";
            _blockCountText.fontSize = 14;
            _blockCountText.alignment = TextAlignmentOptions.Center;
            _blockCountText.color = Color.white;
            _blockCountText.raycastTarget = false;

            // Right action: Mode toggle (Play / Edit)
            var rightGroup = new GameObject("RightActionsGroup");
            rightGroup.transform.SetParent(topBar.transform, false);
            var rgRect = rightGroup.AddComponent<RectTransform>();
            rgRect.anchorMin = new Vector2(1f, 0.5f);
            rgRect.anchorMax = new Vector2(1f, 0.5f);
            rgRect.pivot = new Vector2(1f, 0.5f);
            rgRect.anchoredPosition = new Vector2(-16f, 0f);
            rgRect.sizeDelta = new Vector2(170f, 42f);

            _modeToggleButton = CreateToolButton(rightGroup.transform, "TEST PLAY [Tab]", ToggleMode, 170f, Color.white, new Color(0.08f, 0.68f, 0.36f, 1f));
            _modeToggleText = _modeToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (_modeToggleText != null) _modeToggleText.fontSize = 14;

            // 2. Status Toast (Center Top, below toolbar)
            _toastRoot = new GameObject("Toast");
            _toastRoot.transform.SetParent(canvasObj.transform, false);
            var tRect = _toastRoot.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f);
            tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -74f);
            tRect.sizeDelta = new Vector2(650f, 46f);

            var toastBg = _toastRoot.AddComponent<Image>();
            toastBg.color = new Color(0.07f, 0.1f, 0.16f, 0.95f);
            toastBg.raycastTarget = false;

            var toastTextObj = new GameObject("ToastText");
            toastTextObj.transform.SetParent(_toastRoot.transform, false);
            var ttRect = toastTextObj.AddComponent<RectTransform>();
            ttRect.anchorMin = Vector2.zero;
            ttRect.anchorMax = Vector2.one;
            ttRect.offsetMin = new Vector2(16f, 4f);
            ttRect.offsetMax = new Vector2(-16f, -4f);

            _statusToastText = toastTextObj.AddComponent<TextMeshProUGUI>();
            _statusToastText.alignment = TextAlignmentOptions.Center;
            _statusToastText.fontSize = 18;
            _statusToastText.color = Color.white;
            _statusToastText.raycastTarget = false;
            _toastRoot.SetActive(false);

            // 3. Controls Hint Banner (Floating above bottom palette)
            var hintBar = new GameObject("ControlsHintBar");
            hintBar.transform.SetParent(canvasObj.transform, false);
            var hbRect = hintBar.AddComponent<RectTransform>();
            hbRect.anchorMin = new Vector2(0.5f, 0f);
            hbRect.anchorMax = new Vector2(0.5f, 0f);
            hbRect.pivot = new Vector2(0.5f, 0f);
            hbRect.anchoredPosition = new Vector2(0f, 90f);
            hbRect.sizeDelta = new Vector2(880f, 28f);

            var hbBg = hintBar.AddComponent<Image>();
            hbBg.color = new Color(0.06f, 0.08f, 0.12f, 0.82f);
            hbBg.raycastTarget = false;

            var hbTextObj = new GameObject("HintText");
            hbTextObj.transform.SetParent(hintBar.transform, false);
            var hbtRect = hbTextObj.AddComponent<RectTransform>();
            hbtRect.anchorMin = Vector2.zero;
            hbtRect.anchorMax = Vector2.one;

            var hbTmp = hbTextObj.AddComponent<TextMeshProUGUI>();
            hbTmp.text = "<color=#00E5FF>LMB</color> Place   <color=#00E5FF>RMB</color> Orbit   <color=#00E5FF>WASD</color> Pan   <color=#00E5FF>Q/E</color> Height   <color=#00E5FF>R</color> Rotate   <color=#00E5FF>X</color> Delete   <color=#00E5FF>1-7</color> Palette   <color=#10B981>Tab</color> Play";
            hbTmp.fontSize = 12;
            hbTmp.fontStyle = FontStyles.Bold;
            hbTmp.alignment = TextAlignmentOptions.Center;
            hbTmp.color = new Color(0.85f, 0.88f, 0.95f, 1f);
            hbTmp.raycastTarget = false;

            // 4. Bottom Palette Bar
            var bottomBar = new GameObject("PaletteBar");
            bottomBar.transform.SetParent(canvasObj.transform, false);
            var bbRect = bottomBar.AddComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0.5f, 0f);
            bbRect.anchorMax = new Vector2(0.5f, 0f);
            bbRect.pivot = new Vector2(0.5f, 0f);
            bbRect.anchoredPosition = new Vector2(0f, 12f);
            bbRect.sizeDelta = new Vector2(880f, 76f);

            var bbBg = bottomBar.AddComponent<Image>();
            bbBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

            var layout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 7, 7);
            layout.childAlignment = TextAnchor.MiddleCenter;

            _paletteContainer = bottomBar.transform;
        }

        private Button CreateToolButton(
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction action,
            float width = 76f,
            Color? textColor = null,
            Color? bgColor = null)
        {
            var btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 36f);

            var img = btnObj.AddComponent<Image>();
            img.color = bgColor ?? new Color(0.14f, 0.18f, 0.26f, 1f);

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var tRect = textObj.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor ?? new Color(0.92f, 0.94f, 0.98f, 1f);

            return btn;
        }
    }
}
