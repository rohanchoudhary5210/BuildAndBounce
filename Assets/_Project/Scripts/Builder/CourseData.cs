using System;
using System.Collections.Generic;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Root data container representing a complete user-generated level.
    /// Serialized directly to/from JSON for course saving, loading, and sharing.
    /// </summary>
    [Serializable]
    public class CourseData
    {
        public string courseName = "New Course";
        public string authorName = "Player";
        public string shareCode = "";
        public int version = 1;
        public string createdDate = "";
        public List<PlacedBlockData> blocks = new List<PlacedBlockData>();

        public CourseData()
        {
            createdDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}