using UnityEngine;

namespace BuildAndBounce.Net
{
    /// <summary>
    /// Configuration ScriptableObject for the online course sharing backend (M5).
    /// Supports both live Firebase REST endpoints and an offline simulation mode for local play/testing.
    /// </summary>
    [CreateAssetMenu(fileName = "BackendConfig", menuName = "Build & Bounce/Net/Backend Config")]
    public class CourseBackendConfig : ScriptableObject
    {
        [Header("Backend Mode")]
        [Tooltip("When enabled, simulates cloud upload/download locally in persistent storage. Ideal for offline testing and demos.")]
        [SerializeField] private bool _useSimulationMode = true;

        [Header("Firebase REST Settings")]
        [Tooltip("Firebase Project ID (e.g. 'build-and-bounce-ugc').")]
        [SerializeField] private string _firebaseProjectId = "build-and-bounce-ugc";

        [Tooltip("Firebase Web API Key for anonymous authentication.")]
        [SerializeField] private string _firebaseApiKey = "";

        [Tooltip("Simulated network latency in seconds (only active in Simulation Mode).")]
        [SerializeField] private float _simulatedLatency = 0.35f;

        public bool UseSimulationMode => _useSimulationMode;
        public string FirebaseProjectId => _firebaseProjectId;
        public string FirebaseApiKey => _firebaseApiKey;
        public float SimulatedLatency => _simulatedLatency;

        public string GetFirestoreBaseUrl() =>
            $"https://firestore.googleapis.com/v1/projects/{_firebaseProjectId}/databases/(default)/documents";

        public string GetAuthUrl() =>
            $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_firebaseApiKey}";
    }
}
