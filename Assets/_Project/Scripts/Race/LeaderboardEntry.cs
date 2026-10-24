using System;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// A single record on a course's local leaderboard.
    /// </summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public string playerName = "Player";
        public float time;
        public string date = "";
        public bool hasGhost;

        public LeaderboardEntry()
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        public LeaderboardEntry(string playerName, float time, bool hasGhost = true)
        {
            this.playerName = playerName;
            this.time = time;
            this.hasGhost = hasGhost;
            this.date = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        }

        public string GetFormattedTime() => CourseTimer.FormatTime(time);
    }
}
