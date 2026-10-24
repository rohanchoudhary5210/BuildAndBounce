using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// Complete recorded ghost run for a course.
    /// Serialized directly to JSON for local persistence and future online backend sharing (M5).
    /// </summary>
    [Serializable]
    public class GhostRunData
    {
        public string courseId = "default";
        public string playerName = "Player";
        public float finishTime;
        public string recordedDate = "";
        public List<GhostFrame> frames = new List<GhostFrame>();

        public int FrameCount => frames != null ? frames.Count : 0;

        public GhostRunData()
        {
            recordedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public GhostRunData(string courseId, string playerName, float finishTime)
        {
            this.courseId = courseId;
            this.playerName = playerName;
            this.finishTime = finishTime;
            this.recordedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            this.frames = new List<GhostFrame>();
        }

        public string ToJson() => JsonUtility.ToJson(this);

        public static GhostRunData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<GhostRunData>(json);
        }
    }
}
