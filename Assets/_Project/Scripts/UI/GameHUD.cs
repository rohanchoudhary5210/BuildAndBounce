using System.Collections;
using BuildAndBounce.Builder;
using BuildAndBounce.Level;
using BuildAndBounce.Race;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BuildAndBounce.UI
{
    /// <summary>
    /// In-game HUD display for M2:
    /// - Live run timer (MM:SS.fff)
    /// - Best Time / Personal Record (PB)
    /// - Animated Checkpoint notification banner
    /// - Course completion summary banner
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("UI Elements (Optional - Auto-builds if unassigned)")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private TextMeshProUGUI _bestTimeText;
        [SerializeField] private GameObject _checkpointBanner;
        [SerializeField] private TextMeshProUGUI _checkpointText;
        [SerializeField] private GameObject _finishBanner;
        [SerializeField] private TextMeshProUGUI _finishSummaryText;

        [Header("Scene References")]
        [SerializeField] private CourseTimer _timer;
        [SerializeField] private LevelBuilder _levelBuilder;

        private float _bestTime;
        private string _bestTimeKey;
        private Coroutine _checkpointAnimRoutine;
        private int _lastDisplayedMillis = -1;

        private void Awake()
        {
            _bestTimeKey = "PB_" + SceneManager.GetActiveScene().name;
            _bestTime = PlayerPrefs.GetFloat(_bestTimeKey, 0f);

            if (_timer == null)
            {
                _timer = GetComponentInParent<CourseTimer>();
                if (_timer == null)
                {
                    Debug.LogError("[GameHUD] CourseTimer reference is missing! Wire it in Inspector or run Level Setup Tool.", this);
                }
            }

            if (_levelBuilder == null)
            {
                Debug.LogError("[GameHUD] LevelBuilder reference is missing! Wire it in Inspector or run Builder Setup Tool.", this);
            }

            if (_timerText == null)
            {
                BuildHUDHierarchy();
            }

            UpdateBestTimeDisplay();
            if (_checkpointBanner != null) _checkpointBanner.SetActive(false);
            if (_finishBanner != null) _finishBanner.SetActive(false);
        }

        private void Start()
        {
            if (_levelBuilder != null)
            {
                HandleModeChanged(_levelBuilder.CurrentMode);
            }
        }

        private void OnEnable()
        {
            CourseTimer.OnTimerStarted += HandleTimerStarted;
            CourseTimer.OnTimerFinished += HandleTimerFinished;
            Checkpoint.OnCheckpointActivated += HandleCheckpointReached;
            LevelBuilder.OnModeChanged += HandleModeChanged;
        }

        private void OnDisable()
        {
            CourseTimer.OnTimerStarted -= HandleTimerStarted;
            CourseTimer.OnTimerFinished -= HandleTimerFinished;
            Checkpoint.OnCheckpointActivated -= HandleCheckpointReached;
            LevelBuilder.OnModeChanged -= HandleModeChanged;
        }

        private void HandleModeChanged(BuilderMode mode)
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(mode == BuilderMode.Play);
            }
        }

        private void Update()
        {
            if (_timer == null || !_timer.IsRunning || _timerText == null) return;

            int currentMillis = Mathf.FloorToInt(_timer.ElapsedTime * 1000f);
            if (currentMillis != _lastDisplayedMillis)
            {
                _lastDisplayedMillis = currentMillis;
                _timerText.text = _timer.GetFormattedTime();
            }
        }

        private void HandleTimerStarted()
        {
            _lastDisplayedMillis = -1;
            if (_finishBanner != null) _finishBanner.SetActive(false);
            if (_checkpointBanner != null) _checkpointBanner.SetActive(false);
            if (_timerText != null) _timerText.color = Color.white;
        }

        private void HandleTimerFinished(float finalTime)
        {
            _lastDisplayedMillis = -1;
            if (_timerText != null)
            {
                _timerText.text = CourseTimer.FormatTime(finalTime);
                _timerText.color = new Color(1f, 0.85f, 0.2f); // Gold highlight on finish
            }

            bool isNewRecord = _bestTime <= 0f || finalTime < _bestTime;
            if (isNewRecord)
            {
                _bestTime = finalTime;
                PlayerPrefs.SetFloat(_bestTimeKey, _bestTime);
                PlayerPrefs.Save();
                UpdateBestTimeDisplay();
            }

            ShowFinishBanner(finalTime, isNewRecord);
        }

        private void HandleCheckpointReached(Checkpoint cp)
        {
            if (_checkpointAnimRoutine != null)
            {
                StopCoroutine(_checkpointAnimRoutine);
            }
            _checkpointAnimRoutine = StartCoroutine(AnimateCheckpointBanner());
        }

        private void UpdateBestTimeDisplay()
        {
            if (_bestTimeText == null) return;

            if (_bestTime > 0f)
            {
                _bestTimeText.text = $"PB: {CourseTimer.FormatTime(_bestTime)}";
            }
            else
            {
                _bestTimeText.text = "PB: --:--.---";
            }
        }

        private void ShowFinishBanner(float finalTime, bool isNewRecord)
        {
            if (_finishBanner == null || _finishSummaryText == null) return;

            string recordNotice = isNewRecord ? "<color=#FFDF00>★ NEW PERSONAL BEST! ★</color>\n" : "";
            _finishSummaryText.text = $"{recordNotice}Final Time: <b>{CourseTimer.FormatTime(finalTime)}</b>";
            _finishBanner.SetActive(true);
        }

        private IEnumerator AnimateCheckpointBanner()
        {
            if (_checkpointBanner == null) yield break;

            _checkpointBanner.SetActive(true);
            var rect = _checkpointBanner.GetComponent<RectTransform>();
            var group = _checkpointBanner.GetComponent<CanvasGroup>();
            if (group == null) group = _checkpointBanner.AddComponent<CanvasGroup>();

            float duration = 1.8f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                // Scale pop-in then fade out
                if (t < 0.2f)
                {
                    float inT = t / 0.2f;
                    rect.localScale = Vector3.Lerp(new Vector3(0.6f, 0.6f, 1f), Vector3.one, inT);
                    group.alpha = inT;
                }
                else if (t > 0.75f)
                {
                    float outT = (t - 0.75f) / 0.25f;
                    group.alpha = 1f - outT;
                }
                else
                {
                    rect.localScale = Vector3.one;
                    group.alpha = 1f;
                }

                yield return null;
            }

            _checkpointBanner.SetActive(false);
        }

        private void BuildHUDHierarchy()
        {
            // Canvas
            var canvasObj = new GameObject("GameHUD_Canvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Live Timer (Top Center) with sleek pill backing
            var timerContainer = new GameObject("TimerContainer");
            timerContainer.transform.SetParent(canvasObj.transform, false);
            var tcRect = timerContainer.AddComponent<RectTransform>();
            tcRect.anchorMin = new Vector2(0.5f, 1f);
            tcRect.anchorMax = new Vector2(0.5f, 1f);
            tcRect.pivot = new Vector2(0.5f, 1f);
            tcRect.anchoredPosition = new Vector2(0f, -16f);
            tcRect.sizeDelta = new Vector2(240f, 50f);

            var tcImg = timerContainer.AddComponent<Image>();
            tcImg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);

            var timerObj = new GameObject("TimerText");
            timerObj.transform.SetParent(timerContainer.transform, false);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = Vector2.one;
            timerRect.sizeDelta = Vector2.zero;

            _timerText = timerObj.AddComponent<TextMeshProUGUI>();
            _timerText.text = "00:00.000";
            _timerText.fontSize = 32;
            _timerText.alignment = TextAlignmentOptions.Center;
            _timerText.fontStyle = FontStyles.Bold;
            _timerText.color = Color.white;

            // Personal Best (Top Right) with sleek pill backing
            var pbContainer = new GameObject("PBContainer");
            pbContainer.transform.SetParent(canvasObj.transform, false);
            var pbCRect = pbContainer.AddComponent<RectTransform>();
            pbCRect.anchorMin = new Vector2(1f, 1f);
            pbCRect.anchorMax = new Vector2(1f, 1f);
            pbCRect.pivot = new Vector2(1f, 1f);
            pbCRect.anchoredPosition = new Vector2(-24f, -16f);
            pbCRect.sizeDelta = new Vector2(200f, 44f);

            var pbCImg = pbContainer.AddComponent<Image>();
            pbCImg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);

            var pbObj = new GameObject("PBText");
            pbObj.transform.SetParent(pbContainer.transform, false);
            var pbRect = pbObj.AddComponent<RectTransform>();
            pbRect.anchorMin = Vector2.zero;
            pbRect.anchorMax = Vector2.one;
            pbRect.sizeDelta = Vector2.zero;

            _bestTimeText = pbObj.AddComponent<TextMeshProUGUI>();
            _bestTimeText.text = "PB: --:--.---";
            _bestTimeText.fontSize = 17;
            _bestTimeText.fontStyle = FontStyles.Bold;
            _bestTimeText.alignment = TextAlignmentOptions.Center;
            _bestTimeText.color = new Color(1f, 0.85f, 0.2f, 1f);

            // Checkpoint Banner (Center-Top)
            _checkpointBanner = new GameObject("CheckpointBanner");
            _checkpointBanner.transform.SetParent(canvasObj.transform, false);
            var cpRect = _checkpointBanner.AddComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(0.5f, 0.75f);
            cpRect.anchorMax = new Vector2(0.5f, 0.75f);
            cpRect.pivot = new Vector2(0.5f, 0.5f);
            cpRect.sizeDelta = new Vector2(400f, 60f);

            var cpBg = _checkpointBanner.AddComponent<Image>();
            cpBg.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);

            var cpTextObj = new GameObject("Text");
            cpTextObj.transform.SetParent(_checkpointBanner.transform, false);
            var cpTextRect = cpTextObj.AddComponent<RectTransform>();
            cpTextRect.anchorMin = Vector2.zero;
            cpTextRect.anchorMax = Vector2.one;
            cpTextRect.offsetMin = Vector2.zero;
            cpTextRect.offsetMax = Vector2.zero;

            _checkpointText = cpTextObj.AddComponent<TextMeshProUGUI>();
            _checkpointText.text = "★ CHECKPOINT REACHED! ★";
            _checkpointText.fontSize = 22;
            _checkpointText.fontStyle = FontStyles.Bold;
            _checkpointText.alignment = TextAlignmentOptions.Center;
            _checkpointText.color = new Color(1f, 0.88f, 0.2f);

            // Finish Banner (Center)
            _finishBanner = new GameObject("FinishBanner");
            _finishBanner.transform.SetParent(canvasObj.transform, false);
            var finRect = _finishBanner.AddComponent<RectTransform>();
            finRect.anchorMin = new Vector2(0.5f, 0.55f);
            finRect.anchorMax = new Vector2(0.5f, 0.55f);
            finRect.pivot = new Vector2(0.5f, 0.5f);
            finRect.sizeDelta = new Vector2(500f, 130f);

            var finBg = _finishBanner.AddComponent<Image>();
            finBg.color = new Color(0.08f, 0.1f, 0.15f, 0.92f);

            var finTextObj = new GameObject("Text");
            finTextObj.transform.SetParent(_finishBanner.transform, false);
            var finTextRect = finTextObj.AddComponent<RectTransform>();
            finTextRect.anchorMin = Vector2.zero;
            finTextRect.anchorMax = Vector2.one;
            finTextRect.offsetMin = new Vector2(16f, 16f);
            finTextRect.offsetMax = new Vector2(-16f, -16f);

            _finishSummaryText = finTextObj.AddComponent<TextMeshProUGUI>();
            _finishSummaryText.text = "COURSE COMPLETED!";
            _finishSummaryText.fontSize = 26;
            _finishSummaryText.alignment = TextAlignmentOptions.Center;
            _finishSummaryText.color = Color.white;
        }
    }
}