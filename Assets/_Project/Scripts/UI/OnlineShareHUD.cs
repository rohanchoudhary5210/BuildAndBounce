using BuildAndBounce.Builder;
using BuildAndBounce.Net;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BuildAndBounce.UI
{
    /// <summary>
    /// UI modal for cloud course sharing (M5):
    /// - Upload course and display share code (e.g. "BNB-8A4").
    /// - Input share code to download and load remote user-generated courses instantly.
    /// </summary>
    public class OnlineShareHUD : MonoBehaviour
    {
        [Header("UI References (Auto-built if unassigned)")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_InputField _shareCodeInput;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _lastGeneratedCodeText;
        [SerializeField] private Button _uploadButton;
        [SerializeField] private Button _downloadButton;
        [SerializeField] private Button _closeButton;

        private LevelBuilder _builder;

        private void Awake()
        {
            if (_panel == null)
            {
                BuildShareHUDHierarchy();
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void Start()
        {
            _builder = UnityEngine.Object.FindAnyObjectByType<LevelBuilder>();

            if (_uploadButton != null) _uploadButton.onClick.AddListener(OnUploadClicked);
            if (_downloadButton != null) _downloadButton.onClick.AddListener(OnDownloadClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(CloseDialog);
        }

        private void Update()
        {
            // Toggle dialog on 'O' key or Gamepad D-pad Up
            if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            {
                ToggleDialog();
            }
        }

        public void ToggleDialog()
        {
            if (_panel == null) return;
            bool active = !_panel.activeSelf;
            _panel.SetActive(active);

            if (active)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (_statusText != null) _statusText.text = "Share or download courses with friends!";
            }
        }

        public void CloseDialog()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        public void OnUploadClicked()
        {
            if (_builder == null) _builder = UnityEngine.Object.FindAnyObjectByType<LevelBuilder>();
            if (_builder == null) return;

            var course = _builder.GetCourseData();
            if (course == null || course.blocks.Count == 0)
            {
                SetStatus("<color=#FF5252>Cannot share an empty course. Place some blocks first!</color>");
                return;
            }

            SetStatus("Uploading course to cloud...");

            var backend = CourseBackendService.Instance ?? UnityEngine.Object.FindAnyObjectByType<CourseBackendService>();
            if (backend != null)
            {
                backend.UploadCourse(course, (success, code) =>
                {
                    if (success)
                    {
                        string formatted = ShareCodeGenerator.FormatCode(code);
                        GUIUtility.systemCopyBuffer = formatted;
                        SetStatus($"<color=#69F0AE>Course Uploaded!</color>\nShare Code: <b>{formatted}</b>\n<i>(Copied to clipboard)</i>");
                        if (_lastGeneratedCodeText != null)
                        {
                            _lastGeneratedCodeText.text = $"CODE: {formatted}";
                        }
                    }
                    else
                    {
                        SetStatus($"<color=#FF5252>Upload failed: {code}</color>");
                    }
                });
            }
            else
            {
                SetStatus("<color=#FF5252>CourseBackendService not found in scene.</color>");
            }
        }

        public void OnDownloadClicked()
        {
            if (_shareCodeInput == null || string.IsNullOrWhiteSpace(_shareCodeInput.text))
            {
                SetStatus("<color=#FF5252>Please enter a share code.</color>");
                return;
            }

            string code = _shareCodeInput.text.Trim();
            SetStatus($"Downloading course '{code}'...");

            var backend = CourseBackendService.Instance ?? UnityEngine.Object.FindAnyObjectByType<CourseBackendService>();
            if (backend != null)
            {
                backend.DownloadCourse(code, (success, courseData) =>
                {
                    if (success && courseData != null)
                    {
                        if (_builder == null) _builder = UnityEngine.Object.FindAnyObjectByType<LevelBuilder>();
                        if (_builder != null)
                        {
                            _builder.LoadCourseData(courseData);
                            SetStatus($"<color=#69F0AE>Loaded '{courseData.courseName}' ({courseData.blocks.Count} blocks)!</color>");
                        }
                    }
                    else
                    {
                        SetStatus($"<color=#FF5252>No course found for code '{code}'.</color>");
                    }
                });
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        private void BuildShareHUDHierarchy()
        {
            var existingCanvas = GetComponentInParent<Canvas>() ?? UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                _canvas = existingCanvas;
            }
            else
            {
                var canvasObj = new GameObject("ShareCanvas");
                canvasObj.transform.SetParent(transform, false);
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Panel root
            _panel = new GameObject("SharePanel");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(460, 360);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.10f, 0.16f, 0.96f);

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_panel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 140);
            titleRect.sizeDelta = new Vector2(420, 36);
            var title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "CLOUD COURSE SHARING (M5)";
            title.fontSize = 20;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;

            // Status Text
            var statusObj = new GameObject("Status");
            statusObj.transform.SetParent(_panel.transform, false);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchoredPosition = new Vector2(0, 75);
            statusRect.sizeDelta = new Vector2(400, 60);
            _statusText = statusObj.AddComponent<TextMeshProUGUI>();
            _statusText.text = "Share or download courses with friends!";
            _statusText.fontSize = 15;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = new Color(0.85f, 0.9f, 1f);

            // Upload Button
            var uploadBtnObj = new GameObject("UploadButton");
            uploadBtnObj.transform.SetParent(_panel.transform, false);
            var uploadRect = uploadBtnObj.AddComponent<RectTransform>();
            uploadRect.anchoredPosition = new Vector2(-105, 10);
            uploadRect.sizeDelta = new Vector2(190, 44);
            var uploadImg = uploadBtnObj.AddComponent<Image>();
            uploadImg.color = new Color(0.12f, 0.53f, 0.90f);
            _uploadButton = uploadBtnObj.AddComponent<Button>();

            var uploadTextObj = new GameObject("UploadText");
            uploadTextObj.transform.SetParent(uploadBtnObj.transform, false);
            var uploadTextRect = uploadTextObj.AddComponent<RectTransform>();
            uploadTextRect.sizeDelta = uploadRect.sizeDelta;
            var uploadText = uploadTextObj.AddComponent<TextMeshProUGUI>();
            uploadText.text = "Upload & Share";
            uploadText.fontSize = 16;
            uploadText.fontStyle = FontStyles.Bold;
            uploadText.alignment = TextAlignmentOptions.Center;
            uploadText.color = Color.white;

            // Input Field for code
            var inputObj = new GameObject("CodeInput");
            inputObj.transform.SetParent(_panel.transform, false);
            var inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(105, 10);
            inputRect.sizeDelta = new Vector2(190, 44);
            var inputImg = inputObj.AddComponent<Image>();
            inputImg.color = new Color(0.15f, 0.18f, 0.24f);

            var textCompObj = new GameObject("InputText");
            textCompObj.transform.SetParent(inputObj.transform, false);
            var textCompRect = textCompObj.AddComponent<RectTransform>();
            textCompRect.sizeDelta = new Vector2(170, 36);
            var textComp = textCompObj.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 16;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;

            _shareCodeInput = inputObj.AddComponent<TMP_InputField>();
            _shareCodeInput.textComponent = textComp;
            _shareCodeInput.characterLimit = 8;

            // Download Button
            var downloadBtnObj = new GameObject("DownloadButton");
            downloadBtnObj.transform.SetParent(_panel.transform, false);
            var downloadRect = downloadBtnObj.AddComponent<RectTransform>();
            downloadRect.anchoredPosition = new Vector2(0, -55);
            downloadRect.sizeDelta = new Vector2(400, 42);
            var downloadImg = downloadBtnObj.AddComponent<Image>();
            downloadImg.color = new Color(0.18f, 0.65f, 0.35f);
            _downloadButton = downloadBtnObj.AddComponent<Button>();

            var downloadTextObj = new GameObject("DownloadText");
            downloadTextObj.transform.SetParent(downloadBtnObj.transform, false);
            var downloadTextRect = downloadTextObj.AddComponent<RectTransform>();
            downloadTextRect.sizeDelta = downloadRect.sizeDelta;
            var downloadText = downloadTextObj.AddComponent<TextMeshProUGUI>();
            downloadText.text = "Download by Share Code";
            downloadText.fontSize = 16;
            downloadText.fontStyle = FontStyles.Bold;
            downloadText.alignment = TextAlignmentOptions.Center;
            downloadText.color = Color.white;

            // Close Button
            var closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(_panel.transform, false);
            var closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.anchoredPosition = new Vector2(0, -125);
            closeRect.sizeDelta = new Vector2(200, 36);
            var closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = new Color(0.35f, 0.38f, 0.44f);
            _closeButton = closeBtnObj.AddComponent<Button>();

            var closeTextObj = new GameObject("CloseText");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            var closeTextRect = closeTextObj.AddComponent<RectTransform>();
            closeTextRect.sizeDelta = closeRect.sizeDelta;
            var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "Close (O)";
            closeText.fontSize = 14;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;
        }
    }
}
