using BuildAndBounce.Level;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// Master coordinator for M4 Ghost Racing.
    /// Connects GhostRecorder, GhostPlayback, LeaderboardManager, and CourseTimer.
    /// Loads the best ghost for the active course on start, and submits newly achieved records on finish.
    /// </summary>
    public class RaceGhostManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private GhostRecorder _recorder;
        [SerializeField] private GhostPlayback _playback;
        [SerializeField] private LeaderboardManager _leaderboards;
        [SerializeField] private CourseTimer _timer;

        [Header("Active Course")]
        [Tooltip("Identifier for the active course. Updated when loading UGC courses.")]
        [SerializeField] private string _activeCourseId = "test_course";

        [Tooltip("Local player display name for leaderboard submissions.")]
        [SerializeField] private string _playerName = "Player";

        private GhostRunData _lastRecordedGhost;

        public string ActiveCourseId => _activeCourseId;
        public string PlayerName => _playerName;
        public GhostPlayback Playback => _playback;
        public GhostRecorder Recorder => _recorder;
        public LeaderboardManager Leaderboards => _leaderboards;

        private void Awake()
        {
            if (_recorder == null) _recorder = GetComponent<GhostRecorder>() ?? gameObject.AddComponent<GhostRecorder>();
            if (_playback == null) _playback = GetComponent<GhostPlayback>() ?? gameObject.AddComponent<GhostPlayback>();
            if (_leaderboards == null) _leaderboards = UnityEngine.Object.FindAnyObjectByType<LeaderboardManager>() ?? gameObject.AddComponent<LeaderboardManager>();
            if (_timer == null) _timer = UnityEngine.Object.FindAnyObjectByType<CourseTimer>();
        }

        private void OnEnable()
        {
            GhostRecorder.OnRunRecorded += HandleRunRecorded;
            CourseTimer.OnTimerFinished += HandleCourseFinished;
        }

        private void OnDisable()
        {
            GhostRecorder.OnRunRecorded -= HandleRunRecorded;
            CourseTimer.OnTimerFinished -= HandleCourseFinished;
        }

        private void Start()
        {
            LoadBestGhostForActiveCourse();
        }

        public void SetCourseId(string courseId)
        {
            _activeCourseId = string.IsNullOrEmpty(courseId) ? "test_course" : courseId;
            if (_recorder != null)
            {
                _recorder.SetCourseId(_activeCourseId);
            }
            else
            {
                var rec = GetComponent<GhostRecorder>() ?? UnityEngine.Object.FindAnyObjectByType<GhostRecorder>();
                if (rec != null) rec.SetCourseId(_activeCourseId);
            }
            LoadBestGhostForActiveCourse();
        }

        public void SetPlayerName(string name)
        {
            _playerName = string.IsNullOrEmpty(name) ? "Player" : name;
        }

        public void LoadBestGhostForActiveCourse()
        {
            if (_leaderboards == null || _playback == null) return;

            var bestGhost = _leaderboards.GetBestGhost(_activeCourseId);
            _playback.LoadGhost(bestGhost);

            if (bestGhost != null)
            {
                Debug.Log($"[RaceGhostManager] Loaded previous best ghost for course '{_activeCourseId}': {CourseTimer.FormatTime(bestGhost.finishTime)} ({bestGhost.FrameCount} frames)");
            }
        }

        private void HandleRunRecorded(GhostRunData runData)
        {
            _lastRecordedGhost = runData;
        }

        private void HandleCourseFinished(float finishTime)
        {
            if (_leaderboards == null) return;

            bool isNewBest = _leaderboards.SubmitRun(_activeCourseId, _playerName, finishTime, _lastRecordedGhost);

            if (isNewBest && _lastRecordedGhost != null)
            {
                // Immediately reload the new best ghost so the very next run races against it
                _playback.LoadGhost(_lastRecordedGhost);
                Debug.Log($"[RaceGhostManager] New best ghost active! Time: {CourseTimer.FormatTime(finishTime)}");
            }
        }
    }
}
