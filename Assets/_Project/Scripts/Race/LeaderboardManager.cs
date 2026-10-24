using System;
using System.IO;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// Service managing persistent local course leaderboards and best ghost records.
    /// Saves run times and ghost recordings to JSON files in Application.persistentDataPath.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        [Header("Leaderboard Settings")]
        [Tooltip("Maximum entries retained per course leaderboard.")]
        [SerializeField] private int _maxEntriesPerCourse = 10;

        public static LeaderboardManager Instance { get; private set; }

        public static event Action<float> OnNewPersonalBest;
        public static event Action<string, LeaderboardData> OnLeaderboardUpdated;

        private string LeaderboardFolder => Path.Combine(Application.persistentDataPath, "Leaderboards");
        private string GhostFolder => Path.Combine(Application.persistentDataPath, "Ghosts");

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(LeaderboardFolder)) Directory.CreateDirectory(LeaderboardFolder);
                if (!Directory.Exists(GhostFolder)) Directory.CreateDirectory(GhostFolder);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardManager] Failed to create directories: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the personal best time for a course in seconds, or float.MaxValue if no run exists.
        /// </summary>
        public float GetBestTime(string courseId)
        {
            var data = GetLeaderboard(courseId);
            if (data != null && data.entries.Count > 0)
            {
                return data.entries[0].time;
            }
            return float.MaxValue;
        }

        /// <summary>
        /// Retrieves the leaderboard data for the specified course.
        /// </summary>
        public LeaderboardData GetLeaderboard(string courseId)
        {
            if (string.IsNullOrEmpty(courseId)) courseId = "default";
            string path = GetLeaderboardFilePath(courseId);

            if (!File.Exists(path))
            {
                return new LeaderboardData(courseId);
            }

            try
            {
                string json = File.ReadAllText(path);
                return LeaderboardData.FromJson(json) ?? new LeaderboardData(courseId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] Could not read leaderboard for '{courseId}': {ex.Message}");
                return new LeaderboardData(courseId);
            }
        }

        /// <summary>
        /// Retrieves the recorded best ghost run for a course, or null if none exists.
        /// </summary>
        public GhostRunData GetBestGhost(string courseId)
        {
            if (string.IsNullOrEmpty(courseId)) courseId = "default";
            string path = GetGhostFilePath(courseId);

            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return GhostRunData.FromJson(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] Could not read best ghost for '{courseId}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Submits a newly completed run. Saves to leaderboard and updates ghost if it is a new personal best.
        /// </summary>
        /// <returns>True if this run achieved a new personal best time.</returns>
        public bool SubmitRun(string courseId, string playerName, float time, GhostRunData ghostData)
        {
            if (string.IsNullOrEmpty(courseId)) courseId = "default";
            if (string.IsNullOrEmpty(playerName)) playerName = "Player";
            if (time <= 0f) return false;

            EnsureDirectoriesExist();

            var data = GetLeaderboard(courseId);
            float previousBest = data.entries.Count > 0 ? data.entries[0].time : float.MaxValue;
            bool isNewBest = time < previousBest;

            // Add new entry and sort ascending by time
            data.entries.Add(new LeaderboardEntry(playerName, time, ghostData != null));
            data.entries.Sort((a, b) => a.time.CompareTo(b.time));

            // Truncate to maximum entries
            if (data.entries.Count > _maxEntriesPerCourse)
            {
                data.entries.RemoveRange(_maxEntriesPerCourse, data.entries.Count - _maxEntriesPerCourse);
            }

            // Save leaderboard JSON
            try
            {
                string leaderboardJson = data.ToJson();
                File.WriteAllText(GetLeaderboardFilePath(courseId), leaderboardJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardManager] Failed to save leaderboard: {ex.Message}");
            }

            // If new best and ghost data provided, save best ghost
            if (isNewBest && ghostData != null && ghostData.FrameCount > 0)
            {
                try
                {
                    ghostData.courseId = courseId;
                    ghostData.playerName = playerName;
                    ghostData.finishTime = time;
                    File.WriteAllText(GetGhostFilePath(courseId), ghostData.ToJson());
                    Debug.Log($"[LeaderboardManager] New personal best ghost saved for course '{courseId}': {CourseTimer.FormatTime(time)} ({ghostData.FrameCount} frames)");
                    OnNewPersonalBest?.Invoke(time);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LeaderboardManager] Failed to save best ghost: {ex.Message}");
                }
            }

            OnLeaderboardUpdated?.Invoke(courseId, data);
            return isNewBest;
        }

        private string GetLeaderboardFilePath(string courseId)
        {
            string cleanId = SanitizeFileName(courseId);
            return Path.Combine(LeaderboardFolder, $"{cleanId}_leaderboard.json");
        }

        private string GetGhostFilePath(string courseId)
        {
            string cleanId = SanitizeFileName(courseId);
            return Path.Combine(GhostFolder, $"{cleanId}_best_ghost.json");
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
