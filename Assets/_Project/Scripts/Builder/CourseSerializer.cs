using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BuildAndBounce.Net;
using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Handles serialization and deserialization of course data to JSON,
    /// file system persistence in Application.persistentDataPath using share code/timestamp filenames.
    /// </summary>
    public static class CourseSerializer
    {
        private const string CourseSubfolder = "Courses";

        public static string CoursesDirectory
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, CourseSubfolder);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
        }

        /// <summary>
        /// Serializes CourseData to a clean JSON string.
        /// </summary>
        public static string ToJson(CourseData data, bool prettyPrint = true)
        {
            if (data == null) return string.Empty;
            return JsonUtility.ToJson(data, prettyPrint);
        }

        /// <summary>
        /// Deserializes a JSON string back into CourseData.
        /// </summary>
        public static CourseData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonUtility.FromJson<CourseData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CourseSerializer] Failed to parse course JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves CourseData to disk as a JSON file using the course share code (or timestamp) as filename.
        /// </summary>
        public static string SaveCourse(CourseData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrEmpty(data.shareCode))
            {
                data.shareCode = ShareCodeGenerator.Generate(6);
            }

            string fileName = !string.IsNullOrEmpty(data.shareCode)
                ? SanitizeFileName(data.shareCode)
                : $"course_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            string filePath = Path.Combine(CoursesDirectory, $"{fileName}.json");
            string json = ToJson(data, prettyPrint: true);

            File.WriteAllText(filePath, json, Encoding.UTF8);
            Debug.Log($"[CourseSerializer] Course saved successfully to {filePath} (Share Code: {data.shareCode})");
            return filePath;
        }

        /// <summary>
        /// Loads CourseData from disk by course name or file path.
        /// </summary>
        public static CourseData LoadCourse(string courseNameOrPath)
        {
            string path = courseNameOrPath;
            if (!File.Exists(path))
            {
                string candidate = Path.Combine(CoursesDirectory, $"{SanitizeFileName(courseNameOrPath)}.json");
                if (File.Exists(candidate)) path = candidate;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CourseSerializer] Could not find course file at: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            return FromJson(json);
        }

        /// <summary>
        /// Returns all saved course files in the persistent storage folder, oldest first.
        /// </summary>
        public static List<string> GetSavedCourseFiles()
        {
            var results = new List<string>();
            if (Directory.Exists(CoursesDirectory))
            {
                results.AddRange(Directory.GetFiles(CoursesDirectory, "*.json"));
                // Oldest first, so "Load latest" (last entry) really is the most recent save
                results.Sort((a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
            }
            return results;
        }

        private static string SanitizeFileName(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (Array.IndexOf(invalids, c) < 0 && c != ' ')
                {
                    sb.Append(c);
                }
                else if (c == ' ')
                {
                    sb.Append('_');
                }
            }
            return sb.ToString();
        }
    }
}