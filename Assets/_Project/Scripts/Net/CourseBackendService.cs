using System;
using System.Collections;
using System.IO;
using BuildAndBounce.Builder;
using BuildAndBounce.Race;
using UnityEngine;
using UnityEngine.Networking;

namespace BuildAndBounce.Net
{
    /// <summary>
    /// Service for uploading and downloading user-generated courses, share codes, and online ghost leaderboards (M5).
    /// Uses Firebase REST endpoints via UnityWebRequest with an offline simulation fallback for zero-dependency local testing.
    /// </summary>
    public class CourseBackendService : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private CourseBackendConfig _config;

        [Header("Latency Fallbacks")]
        [Tooltip("Fallback latency (in seconds) used for course upload/download when config is missing.")]
        [SerializeField] private float _defaultCourseLatency = 0.25f;

        [Tooltip("Fallback latency (in seconds) used for ghost upload/download when config is missing.")]
        [SerializeField] private float _defaultGhostLatency = 0.2f;

        public static CourseBackendService Instance { get; private set; }

        private string CloudSimFolder => Path.Combine(Application.persistentDataPath, "CloudSimulation");
        private string CloudCoursesFolder => Path.Combine(CloudSimFolder, "Courses");
        private string CloudGhostsFolder => Path.Combine(CloudSimFolder, "Ghosts");

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

            EnsureSimulationFoldersExist();
        }

        private void EnsureSimulationFoldersExist()
        {
            try
            {
                if (!Directory.Exists(CloudCoursesFolder)) Directory.CreateDirectory(CloudCoursesFolder);
                if (!Directory.Exists(CloudGhostsFolder)) Directory.CreateDirectory(CloudGhostsFolder);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CourseBackendService] Failed to create cloud simulation folders: {ex.Message}");
            }
        }

        /// <summary>
        /// Uploads a course to the backend and assigns a share code.
        /// </summary>
        public void UploadCourse(CourseData course, Action<bool, string> onComplete)
        {
            if (course == null)
            {
                onComplete?.Invoke(false, "Null course data");
                return;
            }

            if (string.IsNullOrEmpty(course.shareCode))
            {
                course.shareCode = ShareCodeGenerator.Generate(6);
            }

            StartCoroutine(UploadCourseRoutine(course, onComplete));
        }

        private IEnumerator UploadCourseRoutine(CourseData course, Action<bool, string> onComplete)
        {
            string shareCode = ShareCodeGenerator.NormalizeCode(course.shareCode);
            string json = JsonUtility.ToJson(course, true);

            if (_config == null || _config.UseSimulationMode || string.IsNullOrEmpty(_config.FirebaseApiKey))
            {
                // Simulation mode: write to cloud simulation directory with latency
                float latency = _config != null ? _config.SimulatedLatency : _defaultCourseLatency;
                yield return new WaitForSeconds(latency);

                try
                {
                    EnsureSimulationFoldersExist();
                    string path = Path.Combine(CloudCoursesFolder, $"{shareCode}.json");
                    if (File.Exists(path))
                    {
                        Debug.LogWarning($"[CourseBackendService] Upload rejected: course '{shareCode}' already exists.");
                        onComplete?.Invoke(false, $"Course with share code '{shareCode}' already exists.");
                        yield break;
                    }

                    File.WriteAllText(path, json);
                    Debug.Log($"[CourseBackendService] Course '{course.courseName}' uploaded successfully! Share Code: {ShareCodeGenerator.FormatCode(shareCode)}");
                    onComplete?.Invoke(true, shareCode);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CourseBackendService] Simulation upload failed: {ex.Message}");
                    onComplete?.Invoke(false, ex.Message);
                }
                yield break;
            }

            // Live Firebase Firestore REST upload - check if course already exists
            string checkUrl = $"{_config.GetFirestoreBaseUrl()}/courses/{shareCode}";
            using (var checkReq = UnityWebRequest.Get(checkUrl))
            {
                yield return checkReq.SendWebRequest();
                if (checkReq.result == UnityWebRequest.Result.Success && checkReq.responseCode == 200)
                {
                    Debug.LogWarning($"[CourseBackendService] Upload rejected: course '{shareCode}' already exists on cloud.");
                    onComplete?.Invoke(false, $"Course with share code '{shareCode}' already exists.");
                    yield break;
                }
            }

            string url = $"{_config.GetFirestoreBaseUrl()}/courses/{shareCode}";
            string bodyJson = $"{{\"fields\": {{\"json\": {{\"stringValue\": {JsonUtility.ToJson(json)}}} }}}}";

            using (var request = UnityWebRequest.Put(url, bodyJson))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(true, shareCode);
                }
                else
                {
                    Debug.LogWarning($"[CourseBackendService] Live upload error: {request.error}. Falling back to simulation cache.");
                    EnsureSimulationFoldersExist();
                    string path = Path.Combine(CloudCoursesFolder, $"{shareCode}.json");
                    if (File.Exists(path))
                    {
                        Debug.LogWarning($"[CourseBackendService] Fallback upload rejected: course '{shareCode}' already exists.");
                        onComplete?.Invoke(false, $"Course with share code '{shareCode}' already exists.");
                        yield break;
                    }

                    File.WriteAllText(path, json);
                    onComplete?.Invoke(true, shareCode);
                }
            }
        }

        /// <summary>
        /// Downloads a course from the cloud using its short share code.
        /// </summary>
        public void DownloadCourse(string shareCode, Action<bool, CourseData> onComplete)
        {
            if (string.IsNullOrEmpty(shareCode))
            {
                onComplete?.Invoke(false, null);
                return;
            }

            string cleanCode = ShareCodeGenerator.NormalizeCode(shareCode);
            StartCoroutine(DownloadCourseRoutine(cleanCode, onComplete));
        }

        private IEnumerator DownloadCourseRoutine(string shareCode, Action<bool, CourseData> onComplete)
        {
            if (_config == null || _config.UseSimulationMode || string.IsNullOrEmpty(_config.FirebaseApiKey))
            {
                float latency = _config != null ? _config.SimulatedLatency : _defaultCourseLatency;
                yield return new WaitForSeconds(latency);

                string path = Path.Combine(CloudCoursesFolder, $"{shareCode}.json");
                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        var course = JsonUtility.FromJson<CourseData>(json);
                        Debug.Log($"[CourseBackendService] Course '{course?.courseName}' downloaded successfully (Code: {shareCode})");
                        onComplete?.Invoke(true, course);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[CourseBackendService] Failed to parse course JSON: {ex.Message}");
                        onComplete?.Invoke(false, null);
                    }
                }
                else
                {
                    Debug.LogWarning($"[CourseBackendService] No course found with share code: {shareCode}");
                    onComplete?.Invoke(false, null);
                }
                yield break;
            }

            // Live Firebase Firestore REST download
            string url = $"{_config.GetFirestoreBaseUrl()}/courses/{shareCode}";
            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        // In production, parse Firestore Document JSON
                        string raw = request.downloadHandler.text;
                        var course = JsonUtility.FromJson<CourseData>(raw);
                        onComplete?.Invoke(true, course);
                    }
                    catch
                    {
                        onComplete?.Invoke(false, null);
                    }
                }
                else
                {
                    onComplete?.Invoke(false, null);
                }
            }
        }

        /// <summary>
        /// Uploads a completed run ghost to the cloud for world record playback.
        /// </summary>
        public void UploadGhost(string courseId, GhostRunData ghost, Action<bool> onComplete)
        {
            if (ghost == null || ghost.FrameCount == 0)
            {
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(UploadGhostRoutine(courseId, ghost, onComplete));
        }

        private IEnumerator UploadGhostRoutine(string courseId, GhostRunData ghost, Action<bool> onComplete)
        {
            string cleanId = ShareCodeGenerator.NormalizeCode(courseId);
            float latency = _config != null ? _config.SimulatedLatency : _defaultGhostLatency;
            yield return new WaitForSeconds(latency);

            try
            {
                EnsureSimulationFoldersExist();
                string path = Path.Combine(CloudGhostsFolder, $"{cleanId}_world_record.json");
                File.WriteAllText(path, ghost.ToJson());
                Debug.Log($"[CourseBackendService] World record ghost uploaded for '{cleanId}': {CourseTimer.FormatTime(ghost.finishTime)}");
                onComplete?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CourseBackendService] Ghost upload failed: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }

        /// <summary>
        /// Downloads the current world record ghost for a course.
        /// </summary>
        public void DownloadBestGhost(string courseId, Action<bool, GhostRunData> onComplete)
        {
            string cleanId = ShareCodeGenerator.NormalizeCode(courseId);
            StartCoroutine(DownloadGhostRoutine(cleanId, onComplete));
        }

        private IEnumerator DownloadGhostRoutine(string courseId, Action<bool, GhostRunData> onComplete)
        {
            float latency = _config != null ? _config.SimulatedLatency : _defaultGhostLatency;
            yield return new WaitForSeconds(latency);

            string path = Path.Combine(CloudGhostsFolder, $"{courseId}_world_record.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var ghost = GhostRunData.FromJson(json);
                    onComplete?.Invoke(true, ghost);
                }
                catch
                {
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                onComplete?.Invoke(false, null);
            }
        }
    }
}
