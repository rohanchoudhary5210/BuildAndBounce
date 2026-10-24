using System;
using BuildAndBounce.Level;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// Tracks run duration with millisecond precision from CourseStart to CourseFinish.
    /// Provides formatted time strings and C# events for HUD display (M2) and ghost records (M4).
    /// </summary>
    public class CourseTimer : MonoBehaviour
    {
        [Header("Debug Display")]
        [Tooltip("Shows a legacy IMGUI timer in the top-left during play mode if no uGUI HUD is present.")]
        [SerializeField] private bool _showDebugUI = false;

        private float _elapsedTime;
        private bool _isRunning;
        private bool _isCompleted;

        public float ElapsedTime => _elapsedTime;
        public bool IsRunning => _isRunning;
        public bool IsCompleted => _isCompleted;

        public static event Action OnTimerStarted;
        public static event Action<float> OnTimerFinished;

        private void OnEnable()
        {
            CourseStart.OnCourseStarted += HandleCourseStarted;
            CourseFinish.OnCourseFinished += HandleCourseFinished;
            CourseStart.OnCourseReset += ResetTimer;
        }

        private void OnDisable()
        {
            CourseStart.OnCourseStarted -= HandleCourseStarted;
            CourseFinish.OnCourseFinished -= HandleCourseFinished;
            CourseStart.OnCourseReset -= ResetTimer;
        }

        private void Update()
        {
            if (_isRunning)
            {
                _elapsedTime += Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (!_showDebugUI) return;

            GUI.color = Color.white;
            GUI.Box(new Rect(16, 16, 180, 56), "");
            GUI.Label(new Rect(26, 20, 160, 20), "<b>BUILD & BOUNCE</b>");

            string status = _isCompleted ? "FINISHED" : (_isRunning ? "RUNNING" : "READY");
            GUI.Label(new Rect(26, 42, 160, 24), $"Time: {GetFormattedTime()} ({status})");
        }

        private void HandleCourseStarted()
        {
            _elapsedTime = 0f;
            _isRunning = true;
            _isCompleted = false;
            OnTimerStarted?.Invoke();
        }

        private void HandleCourseFinished()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _isCompleted = true;
            OnTimerFinished?.Invoke(_elapsedTime);
        }

        /// <summary>
        /// Formats elapsed time into MM:SS.fff (e.g. "01:23.456").
        /// </summary>
        public string GetFormattedTime() => FormatTime(_elapsedTime);

        /// <summary>
        /// Formats seconds into MM:SS.fff (e.g. "01:23.456").
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--.---";
            int minutes = (int)(seconds / 60f);
            int sec = (int)(seconds % 60f);
            int fraction = (int)((seconds * 1000f) % 1000f);

            return $"{minutes:D2}:{sec:D2}.{fraction:D3}";
        }

        public void ResetTimer()
        {
            _elapsedTime = 0f;
            _isRunning = false;
            _isCompleted = false;
        }
    }
}
