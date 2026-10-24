using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Handles serialization and deserialization of course data to JSON,
    /// file system persistence in Application.persistentDataPath, and share code generation.
    /// </summary>
    public static class CourseSerializer
    {
        private const string CourseSubfolder = "Courses";
        private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 32 characters (no 0, 1, I, O)

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
        /// Generates a short, human-readable share code (e.g. 'BNCE-7K') derived from course content.
        /// </summary>
        public static string GenerateShareCode(CourseData data)
        {
            if (data == null || data.blocks == null || data.blocks.Count == 0)
            {
                return "BNCE-EMPTY";
            }

            // Deterministic 32-bit FNV-1a hash over course name and block signatures
            uint hash = 2166136261;
            void AddString(string s)
            {
                if (string.IsNullOrEmpty(s)) return;
                byte[] bytes = Encoding.UTF8.GetBytes(s);
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 16777619;
                }
            }

            AddString(data.courseName);
            for (int i = 0; i < data.blocks.Count; i++)
            {
                var b = data.blocks[i];
                AddString(b.blockId);
                AddString(b.position.ToString("F1"));
                AddString(b.rotation.ToString("F0"));
            }

            // Encode lower 20 bits of hash into 4 base-32 characters
            char[] code = new char[4];
            uint temp = hash;
            for (int i = 0; i < 4; i++)
            {
                code[i] = CodeAlphabet[(int)(temp % (uint)CodeAlphabet.Length)];
                temp /= (uint)CodeAlphabet.Length;
            }

            return $"BNCE-{new string(code)}";
        }

        /// <summary>
        /// Saves CourseData to disk as a JSON file.
        /// </summary>
        public static string SaveCourse(CourseData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrEmpty(data.shareCode))
            {
                data.shareCode = GenerateShareCode(data);
            }

            string sanitizedName = SanitizeFileName(data.courseName);
            if (string.IsNullOrEmpty(sanitizedName)) sanitizedName = "Course";

            string filePath = Path.Combine(CoursesDirectory, $"{sanitizedName}.json");
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
        /// Returns a list of all saved course files in the persistent storage folder.
        /// </summary>
        public static List<string> GetSavedCourseFiles()
        {
            var results = new List<string>();
            if (Directory.Exists(CoursesDirectory))
            {
                results.AddRange(Directory.GetFiles(CoursesDirectory, "*.json"));
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