using System.Collections.Generic;
using BuildAndBounce.Race;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BuildAndBounce.UI
{
    /// <summary>
    /// Displays local course leaderboard rankings and personal best ghost status upon course completion (M4).
    /// Auto-builds a clean overlay panel hierarchy if unassigned.
    /// </summary>
    public class LeaderboardHUD : MonoBehaviour
    {
        [Header("UI Elements (Auto-built if unassigned)")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _finishTimeText;
        [SerializeField] private TextMeshProUGUI _recordBadgeText;
        [SerializeField] private Transform _entriesContainer;
        [SerializeField] private List<TextMeshProUGUI> _entryRows = new List<TextMeshProUGUI>();

        [Header("Scene References")]
        [SerializeField] private LeaderboardManager _leaderboardManager;
        [SerializeField] private RaceGhostManager _raceGhostManager;

        private bool _isOpen;

        private void Awake()
        {
            if (_panel == null)
            {
                BuildLeaderboardHierarchy();
            }

            if (_leaderboardManager == null)
            {
                Debug.LogError("[LeaderboardHUD] LeaderboardManager reference is missing! Wire it in Inspector or run Race Setup Tool.", this);
            }
            if (_raceGhostManager == null)
            {
                Debug.LogError("[LeaderboardHUD] RaceGhostManager reference is missing! Wire it in Inspector or run Race Setup Tool.", this);
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            CourseTimer.OnTimerStarted += HandleTimerStarted;
            CourseTimer.OnTimerFinished += HandleTimerFinished;
            PauseMenu.OnPauseStateChanged += HandlePauseStateChanged;
        }

        private void OnDisable()
        {
            CourseTimer.OnTimerStarted -= HandleTimerStarted;
            CourseTimer.OnTimerFinished -= HandleTimerFinished;
            PauseMenu.OnPauseStateChanged -= HandlePauseStateChanged;
        }

        private void HandlePauseStateChanged(bool isPaused)
        {
            if (isPaused && _isOpen)
            {
                Hide();
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Allow dismiss with Space, Enter, or Gamepad South
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            {
                Hide();
            }
            else if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                Hide();
            }
        }

        private void HandleTimerStarted()
        {
            Hide();
        }

        private void HandleTimerFinished(float finishTime)
        {
            ShowLeaderboard(finishTime);
        }

        public void ShowLeaderboard(float finishTime)
        {
            if (_panel == null) BuildLeaderboardHierarchy();

            _isOpen = true;
            _panel.SetActive(true);

            if (_finishTimeText != null)
            {
                _finishTimeText.text = $"YOUR TIME: <color=#00E5FF>{CourseTimer.FormatTime(finishTime)}</color>";
            }

            string courseId = _raceGhostManager != null ? _raceGhostManager.ActiveCourseId : "test_course";

            if (_leaderboardManager != null)
            {
                var data = _leaderboardManager.GetLeaderboard(courseId);
                bool isNewRecord = data.entries.Count > 0 && Mathf.Abs(data.entries[0].time - finishTime) < 0.001f;

                if (_recordBadgeText != null)
                {
                    _recordBadgeText.gameObject.SetActive(isNewRecord);
                    if (isNewRecord)
                    {
                        _recordBadgeText.text = "★ NEW PERSONAL BEST! GHOST SAVED ★";
                    }
                }

                // Populate entry rows
                for (int i = 0; i < _entryRows.Count; i++)
                {
                    if (i < data.entries.Count)
                    {
                        var entry = data.entries[i];
                        string rankColor = i == 0 ? "#FFD700" : (i == 1 ? "#C0C0C0" : (i == 2 ? "#CD7F32" : "#FFFFFF"));
                        string ghostTag = entry.hasGhost ? " <color=#00E5FF>[GHOST]</color>" : "";
                        _entryRows[i].text = $"<color={rankColor}>#{i + 1}</color>  {entry.playerName} - <b>{entry.GetFormattedTime()}</b>{ghostTag}";
                        _entryRows[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        _entryRows[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        public void Hide()
        {
            _isOpen = false;
            if (_panel != null) _panel.SetActive(false);
        }

        private void BuildLeaderboardHierarchy()
        {
            var existingCanvas = GetComponentInParent<Canvas>() ?? UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                _canvas = existingCanvas;
            }
            else
            {
                var canvasObj = new GameObject("LeaderboardCanvas");
                canvasObj.transform.SetParent(transform, false);
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Root panel
            _panel = new GameObject("LeaderboardPanel");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480, 420);

            var bgImg = _panel.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);

            // Title
            var titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_panel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 160);
            titleRect.sizeDelta = new Vector2(440, 40);
            _titleText = titleObj.AddComponent<TextMeshProUGUI>();
            _titleText.text = "COURSE LEADERBOARD";
            _titleText.fontSize = 24;
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = Color.white;

            // Finish Time
            var timeObj = new GameObject("FinishTimeText");
            timeObj.transform.SetParent(_panel.transform, false);
            var timeRect = timeObj.AddComponent<RectTransform>();
            timeRect.anchoredPosition = new Vector2(0, 120);
            timeRect.sizeDelta = new Vector2(440, 36);
            _finishTimeText = timeObj.AddComponent<TextMeshProUGUI>();
            _finishTimeText.text = "YOUR TIME: 00:00.000";
            _finishTimeText.fontSize = 20;
            _finishTimeText.alignment = TextAlignmentOptions.Center;
            _finishTimeText.color = Color.white;

            // Record Badge
            var badgeObj = new GameObject("RecordBadgeText");
            badgeObj.transform.SetParent(_panel.transform, false);
            var badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchoredPosition = new Vector2(0, 88);
            badgeRect.sizeDelta = new Vector2(440, 26);
            _recordBadgeText = badgeObj.AddComponent<TextMeshProUGUI>();
            _recordBadgeText.text = "★ NEW PERSONAL BEST! GHOST SAVED ★";
            _recordBadgeText.fontSize = 15;
            _recordBadgeText.fontStyle = FontStyles.Bold;
            _recordBadgeText.alignment = TextAlignmentOptions.Center;
            _recordBadgeText.color = new Color(1f, 0.85f, 0.2f);

            // Entries container
            var listObj = new GameObject("EntriesContainer");
            listObj.transform.SetParent(_panel.transform, false);
            var listRect = listObj.AddComponent<RectTransform>();
            listRect.anchoredPosition = new Vector2(0, -20);
            listRect.sizeDelta = new Vector2(400, 180);
            _entriesContainer = listObj.transform;

            _entryRows.Clear();
            for (int i = 0; i < 5; i++)
            {
                var rowObj = new GameObject($"Row_{i + 1}");
                rowObj.transform.SetParent(_entriesContainer, false);
                var rowRect = rowObj.AddComponent<RectTransform>();
                rowRect.anchoredPosition = new Vector2(0, 60 - i * 32);
                rowRect.sizeDelta = new Vector2(380, 28);
                var rowText = rowObj.AddComponent<TextMeshProUGUI>();
                rowText.fontSize = 16;
                rowText.alignment = TextAlignmentOptions.Left;
                rowText.color = Color.white;
                _entryRows.Add(rowText);
            }

            // Dismiss prompt
            var promptObj = new GameObject("DismissPrompt");
            promptObj.transform.SetParent(_panel.transform, false);
            var promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchoredPosition = new Vector2(0, -175);
            promptRect.sizeDelta = new Vector2(440, 26);
            var promptText = promptObj.AddComponent<TextMeshProUGUI>();
            promptText.text = "Press SPACE or SOUTH button to continue";
            promptText.fontSize = 13;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = new Color(0.7f, 0.7f, 0.7f);
        }
    }
}
