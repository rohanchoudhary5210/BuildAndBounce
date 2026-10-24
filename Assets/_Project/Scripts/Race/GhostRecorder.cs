using System;
using System.Collections.Generic;
using BuildAndBounce.Level;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// Records player position and rotation at a deterministic fixed tick rate during a course run.
    /// Emits a completed GhostRunData container upon crossing CourseFinish.
    /// </summary>
    public class GhostRecorder : MonoBehaviour
    {
        [Header("Target & Sampling")]
        [Tooltip("Transform of the player to track. Auto-detected if unassigned.")]
        [SerializeField] private Transform _target;

        [Tooltip("Sample rate in Hertz (e.g. 20 Hz = 20 samples per second, 1 every 50ms).")]
        [SerializeField] private float _sampleRate = 20f;

        [Header("Course Identifier")]
        [Tooltip("Identifier of the course being recorded.")]
        [SerializeField] private string _courseId = "default";

        private float _sampleInterval;
        private float _timeSinceLastSample;
        private bool _isRecording;
        private float _currentRunTime;
        private readonly List<GhostFrame> _recordedFrames = new List<GhostFrame>(2048);

        public bool IsRecording => _isRecording;
        public int RecordedFrameCount => _recordedFrames.Count;
        public string CourseId => _courseId;

        public static event Action<GhostRunData> OnRunRecorded;

        public void SetCourseId(string courseId)
        {
            _courseId = string.IsNullOrEmpty(courseId) ? "default" : courseId;
        }

        private void Awake()
        {
            _sampleInterval = 1f / Mathf.Max(1f, _sampleRate);
            if (_target == null)
            {
                var player = UnityEngine.Object.FindAnyObjectByType<BuildAndBounce.Player.PlayerController>();
                if (player != null) _target = player.transform;
            }
        }

        private void OnEnable()
        {
            CourseStart.OnCourseStarted += HandleCourseStarted;
            CourseFinish.OnCourseFinished += HandleCourseFinished;
            CourseStart.OnCourseReset += ResetRecording;
        }

        private void OnDisable()
        {
            CourseStart.OnCourseStarted -= HandleCourseStarted;
            CourseFinish.OnCourseFinished -= HandleCourseFinished;
            CourseStart.OnCourseReset -= ResetRecording;
        }

        public void ResetRecording()
        {
            _isRecording = false;
            _currentRunTime = 0f;
            _timeSinceLastSample = 0f;
            _recordedFrames.Clear();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void HandleCourseStarted()
        {
            if (_target == null)
            {
                var player = UnityEngine.Object.FindAnyObjectByType<BuildAndBounce.Player.PlayerController>();
                if (player != null) _target = player.transform;
            }

            _recordedFrames.Clear();
            _isRecording = true;
            _currentRunTime = 0f;
            _timeSinceLastSample = _sampleInterval; // Trigger initial sample on tick 0
        }

        private void FixedUpdate()
        {
            if (!_isRecording || _target == null) return;

            _currentRunTime += Time.fixedDeltaTime;
            _timeSinceLastSample += Time.fixedDeltaTime;

            if (_timeSinceLastSample >= _sampleInterval)
            {
                _timeSinceLastSample -= _sampleInterval;
                _recordedFrames.Add(new GhostFrame(_currentRunTime, _target.position, _target.rotation));
            }
        }

        private void HandleCourseFinished()
        {
            if (!_isRecording) return;
            _isRecording = false;

            if (_target != null)
            {
                // Guarantee a final frame at the exact finish moment
                _recordedFrames.Add(new GhostFrame(_currentRunTime, _target.position, _target.rotation));
            }

            var runData = new GhostRunData(_courseId, "Player", _currentRunTime);
            runData.frames.AddRange(_recordedFrames);

            OnRunRecorded?.Invoke(runData);
        }

        public void AbortRecording()
        {
            _isRecording = false;
            _recordedFrames.Clear();
        }
    }
}
