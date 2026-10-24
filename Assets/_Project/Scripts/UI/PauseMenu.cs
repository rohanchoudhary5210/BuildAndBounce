using System;
using BuildAndBounce.Level;
using BuildAndBounce.Player;
using BuildAndBounce.Race;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BuildAndBounce.UI
{
    /// <summary>
    /// Handles pause state (Time.timeScale = 0), cursor unlock, and menu navigation.
    /// Responds to Escape key or gamepad Start button.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("UI References (Optional - Auto-builds if unassigned)")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _quitButton;

        private bool _isPaused;
        public bool IsPaused => _isPaused;

        public static event Action<bool> OnPauseStateChanged;

        private void Awake()
        {
            if (_menuPanel == null)
            {
                BuildPauseMenuHierarchy();
            }

            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
            }

            // Ensure an EventSystem exists in the scene for UI button interaction & gamepad navigation
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        private void Update()
        {
            // Toggle pause on Escape or Gamepad Start
            bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool startPressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;

            if (escapePressed || startPressed)
            {
                if (_isPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }

        public void Pause()
        {
            _isPaused = true;
            Time.timeScale = 0f;

            if (_menuPanel != null)
            {
                _menuPanel.SetActive(true);
            }

            // Unlock and reveal cursor for menu interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Highlight first button for immediate gamepad/keyboard navigation
            if (_resumeButton != null)
            {
                _resumeButton.Select();
            }

            OnPauseStateChanged?.Invoke(true);
        }

        public void Resume()
        {
            _isPaused = false;
            Time.timeScale = 1f;

            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
            }

            // Lock and hide cursor for platformer gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            OnPauseStateChanged?.Invoke(false);
        }

        public void RestartRun()
        {
            Resume();

            // Reset timer
            var timer = UnityEngine.Object.FindAnyObjectByType<CourseTimer>();
            if (timer != null)
            {
                timer.ResetTimer();
            }

            // Teleport player back to start
            var start = UnityEngine.Object.FindAnyObjectByType<CourseStart>();
            var respawn = UnityEngine.Object.FindAnyObjectByType<PlayerRespawn>();
            if (respawn != null && start != null)
            {
                respawn.RespawnAt(start.transform.position + Vector3.up * 1.5f);
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildPauseMenuHierarchy()
        {
            var canvasObj = new GameObject("PauseMenu_Canvas");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // Above HUD

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Fullscreen backdrop overlay
            _menuPanel = new GameObject("PausePanel");
            _menuPanel.transform.SetParent(canvasObj.transform, false);
            var panelRect = _menuPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var bgImg = _menuPanel.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.12f, 0.88f);

            // Container Box
            var boxObj = new GameObject("Box");
            boxObj.transform.SetParent(_menuPanel.transform, false);
            var boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(400f, 380f);

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(boxObj.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, 0f);
            titleRect.sizeDelta = new Vector2(360f, 60f);

            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "PAUSED";
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // Buttons
            _resumeButton = CreateButton("ResumeButton", boxObj.transform, new Vector2(0f, -90f), "RESUME", Resume);
            _restartButton = CreateButton("RestartButton", boxObj.transform, new Vector2(0f, -170f), "RESTART RUN", RestartRun);
            _quitButton = CreateButton("QuitButton", boxObj.transform, new Vector2(0f, -250f), "QUIT", QuitGame);
        }

        private Button CreateButton(string name, Transform parent, Vector2 pos, string label, UnityEngine.Events.UnityAction action)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(280f, 54f);

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.28f, 0.42f, 0.75f, 1f);
            colors.selectedColor = new Color(0.28f, 0.42f, 0.75f, 1f);
            colors.pressedColor = new Color(0.12f, 0.16f, 0.25f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(action);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }
    }
}