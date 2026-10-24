using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildAndBounce.Core
{
    /// <summary>
    /// Service that guarantees there is always exactly one active AudioListener in the scene.
    /// Resolves duplicate AudioListeners by prioritizing the primary camera (tagged "MainCamera"),
    /// disables and cleans up extraneous listeners, and adds one to the main camera if none exist.
    /// </summary>
    public static class AudioListenerGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnforceSingleListener();
        }

        /// <summary>
        /// Audits all AudioListener components in the scene and ensures exactly one is active.
        /// Can be called at runtime or from editor setup tools.
        /// </summary>
        /// <returns>The single active AudioListener in the scene, or null if no camera exists.</returns>
        public static AudioListener EnforceSingleListener()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (listeners == null || listeners.Length == 0)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    Debug.Log("[AudioListenerGuard] No AudioListener found in scene. Adding one to Main Camera.");
                    return mainCam.gameObject.AddComponent<AudioListener>();
                }
                return null;
            }

            if (listeners.Length == 1)
            {
                if (!listeners[0].enabled)
                {
                    listeners[0].enabled = true;
                }
                return listeners[0];
            }

            // More than 1 listener found: identify primary listener (tagged MainCamera or on Camera.main)
            AudioListener primary = null;
            Camera main = Camera.main;

            for (int i = 0; i < listeners.Length; i++)
            {
                var candidate = listeners[i];
                if (candidate.gameObject.CompareTag("MainCamera") || (main != null && candidate.gameObject == main.gameObject))
                {
                    primary = candidate;
                    break;
                }
            }

            // Fallback: pick first active listener
            if (primary == null)
            {
                for (int i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i].enabled)
                    {
                        primary = listeners[i];
                        break;
                    }
                }
            }

            if (primary == null)
            {
                primary = listeners[0];
            }

            primary.enabled = true;

            // Immediately disable and remove duplicates
            for (int i = 0; i < listeners.Length; i++)
            {
                var duplicate = listeners[i];
                if (duplicate != primary)
                {
                    duplicate.enabled = false;
                    Debug.LogWarning($"[AudioListenerGuard] Duplicate AudioListener detected on '{duplicate.gameObject.name}'. Disabling and removing to ensure exactly one listener.", duplicate.gameObject);

                    if (Application.isPlaying)
                    {
                        Object.Destroy(duplicate);
                    }
                    else
                    {
                        Object.DestroyImmediate(duplicate);
                    }
                }
            }

            return primary;
        }
    }
}
