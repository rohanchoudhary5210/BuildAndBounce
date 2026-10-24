using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// Root data container for course leaderboard rankings.
    /// Serialized directly to/from JSON.
    /// </summary>
    [Serializable]
    public class LeaderboardData
    {
        public string courseId = "default";
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

        public LeaderboardData() { }

        public LeaderboardData(string courseId)
        {
            this.courseId = courseId;
            this.entries = new List<LeaderboardEntry>();
        }

        public string ToJson() => JsonUtility.ToJson(this);

        public static LeaderboardData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<LeaderboardData>(json);
        }
    }
}
