using BuildAndBounce.Builder;
using BuildAndBounce.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BuildAndBounce.UI
{
    /// <summary>
    /// Cross-platform on-screen touch controls for mobile devices (M7).
    /// Provides a virtual thumbstick for camera-relative movement and action buttons for Jump, Dash, and Mode toggle.
    /// Auto-enables on mobile platforms or when explicitly forced in the inspector for editor testing.
    /// </summary>
    public class MobileTouchControls : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Configuration")]
        [Tooltip("Force display in editor or desktop builds for testing.")]
        [SerializeField] private bool _forceShowForTesting = false;

        [Header("UI References (Auto-built if unassigned)")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _joystickBackground;
        [SerializeField] private RectTransform _joystickHandle;
        [SerializeField] private Button _jumpButton;
        [SerializeField] private Button _dashButton;
        [SerializeField] private Button _buildToggleButton;

        private PlayerInputReader _inputReader;
        private LevelBuilder _levelBuilder;
        private Vector2 _joystickInput;
        private float _joystickRadius = 60f;

        private void Awake()
        {
            bool isMobile = Application.isMobilePlatform || _forceShowForTesting;
            if (!isMobile)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_joystickBackground == null)
            {
                BuildMobileHierarchy();
            }
        }

        private void Start()
        {
            _inputReader = UnityEngine.Object.FindAnyObjectByType<PlayerInputReader>();
            _levelBuilder = UnityEngine.Object.FindAnyObjectByType<LevelBuilder>();

            if (_buildToggleButton != null)
            {
                _buildToggleButton.onClick.AddListener(OnBuildToggleClicked);
            }
        }

        private void Update()
        {
            if (_inputReader == null)
            {
                _inputReader = UnityEngine.Object.FindAnyObjectByType<PlayerInputReader>();
            }

            if (_inputReader != null)
            {
                _inputReader.SetVirtualMoveInput(_joystickInput);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_joystickBackground == null || _joystickHandle == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _joystickBackground,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                localPoint = Vector2.ClampMagnitude(localPoint, _joystickRadius);
                _joystickHandle.anchoredPosition = localPoint;
                _joystickInput = localPoint / _joystickRadius;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_joystickHandle != null)
            {
                _joystickHandle.anchoredPosition = Vector2.zero;
            }
            _joystickInput = Vector2.zero;
            if (_inputReader != null)
            {
                _inputReader.SetVirtualMoveInput(Vector2.zero);
            }
        }

        public void OnJumpDown()
        {
            if (_inputReader != null) _inputReader.TriggerVirtualJumpPress();
        }

        public void OnJumpUp()
        {
            if (_inputReader != null) _inputReader.TriggerVirtualJumpRelease();
        }

        public void OnDashClicked()
        {
            if (_inputReader != null) _inputReader.TriggerVirtualDash();
        }

        private void OnBuildToggleClicked()
        {
            if (_levelBuilder == null) _levelBuilder = UnityEngine.Object.FindAnyObjectByType<LevelBuilder>();
            if (_levelBuilder != null) _levelBuilder.ToggleMode();
        }

        private void BuildMobileHierarchy()
        {
            var existingCanvas = GetComponentInParent<Canvas>() ?? UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                _canvas = existingCanvas;
            }
            else
            {
                var canvasObj = new GameObject("MobileControlsCanvas");
                canvasObj.transform.SetParent(transform, false);
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Joystick Root
            var joyBgObj = new GameObject("JoystickBg");
            joyBgObj.transform.SetParent(_canvas.transform, false);
            _joystickBackground = joyBgObj.AddComponent<RectTransform>();
            _joystickBackground.anchorMin = new Vector2(0f, 0f);
            _joystickBackground.anchorMax = new Vector2(0f, 0f);
            _joystickBackground.pivot = new Vector2(0.5f, 0.5f);
            _joystickBackground.anchoredPosition = new Vector2(130, 130);
            _joystickBackground.sizeDelta = new Vector2(150, 150);

            var joyBgImg = joyBgObj.AddComponent<Image>();
            joyBgImg.color = new Color(1f, 1f, 1f, 0.18f);

            var joyHandleObj = new GameObject("JoystickHandle");
            joyHandleObj.transform.SetParent(joyBgObj.transform, false);
            _joystickHandle = joyHandleObj.AddComponent<RectTransform>();
            _joystickHandle.sizeDelta = new Vector2(60, 60);
            var handleImg = joyHandleObj.AddComponent<Image>();
            handleImg.color = new Color(0.2f, 0.7f, 1f, 0.65f);

            // Jump Button
            var jumpObj = new GameObject("TouchJumpButton");
            jumpObj.transform.SetParent(_canvas.transform, false);
            var jumpRect = jumpObj.AddComponent<RectTransform>();
            jumpRect.anchorMin = new Vector2(1f, 0f);
            jumpRect.anchorMax = new Vector2(1f, 0f);
            jumpRect.pivot = new Vector2(0.5f, 0.5f);
            jumpRect.anchoredPosition = new Vector2(-110, 110);
            jumpRect.sizeDelta = new Vector2(85, 85);

            var jumpImg = jumpObj.AddComponent<Image>();
            jumpImg.color = new Color(0.15f, 0.85f, 0.45f, 0.65f);
            _jumpButton = jumpObj.AddComponent<Button>();

            var jumpTrigger = jumpObj.AddComponent<EventTrigger>();
            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener((_) => OnJumpDown());
            jumpTrigger.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener((_) => OnJumpUp());
            jumpTrigger.triggers.Add(upEntry);

            var jumpTextObj = new GameObject("Text");
            jumpTextObj.transform.SetParent(jumpObj.transform, false);
            var jumpTextRect = jumpTextObj.AddComponent<RectTransform>();
            jumpTextRect.sizeDelta = jumpRect.sizeDelta;
            var jumpText = jumpTextObj.AddComponent<TextMeshProUGUI>();
            jumpText.text = "JUMP";
            jumpText.fontSize = 17;
            jumpText.fontStyle = FontStyles.Bold;
            jumpText.alignment = TextAlignmentOptions.Center;
            jumpText.color = Color.white;

            // Dash Button
            var dashObj = new GameObject("TouchDashButton");
            dashObj.transform.SetParent(_canvas.transform, false);
            var dashRect = dashObj.AddComponent<RectTransform>();
            dashRect.anchorMin = new Vector2(1f, 0f);
            dashRect.anchorMax = new Vector2(1f, 0f);
            dashRect.pivot = new Vector2(0.5f, 0.5f);
            dashRect.anchoredPosition = new Vector2(-210, 80);
            dashRect.sizeDelta = new Vector2(65, 65);

            var dashImg = dashObj.AddComponent<Image>();
            dashImg.color = new Color(0.95f, 0.55f, 0.1f, 0.65f);
            _dashButton = dashObj.AddComponent<Button>();
            _dashButton.onClick.AddListener(OnDashClicked);

            var dashTextObj = new GameObject("Text");
            dashTextObj.transform.SetParent(dashObj.transform, false);
            var dashTextRect = dashTextObj.AddComponent<RectTransform>();
            dashTextRect.sizeDelta = dashRect.sizeDelta;
            var dashText = dashTextObj.AddComponent<TextMeshProUGUI>();
            dashText.text = "DASH";
            dashText.fontSize = 13;
            dashText.fontStyle = FontStyles.Bold;
            dashText.alignment = TextAlignmentOptions.Center;
            dashText.color = Color.white;

            // Build / Play Toggle Button (Top Right)
            var toggleObj = new GameObject("TouchBuildToggle");
            toggleObj.transform.SetParent(_canvas.transform, false);
            var toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1f, 1f);
            toggleRect.anchorMax = new Vector2(1f, 1f);
            toggleRect.pivot = new Vector2(1f, 1f);
            toggleRect.anchoredPosition = new Vector2(-20, -20);
            toggleRect.sizeDelta = new Vector2(130, 42);

            var toggleImg = toggleObj.AddComponent<Image>();
            toggleImg.color = new Color(0.2f, 0.5f, 0.9f, 0.75f);
            _buildToggleButton = toggleObj.AddComponent<Button>();

            var toggleTextObj = new GameObject("Text");
            toggleTextObj.transform.SetParent(toggleObj.transform, false);
            var toggleTextRect = toggleTextObj.AddComponent<RectTransform>();
            toggleTextRect.sizeDelta = toggleRect.sizeDelta;
            var toggleText = toggleTextObj.AddComponent<TextMeshProUGUI>();
            toggleText.text = "BUILD / PLAY";
            toggleText.fontSize = 13;
            toggleText.fontStyle = FontStyles.Bold;
            toggleText.alignment = TextAlignmentOptions.Center;
            toggleText.color = Color.white;
        }
    }
}
