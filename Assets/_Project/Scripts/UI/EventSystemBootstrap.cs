using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BuildAndBounce.UI
{
    /// <summary>
    /// All HUDs build their canvases at runtime, but nothing created an EventSystem,
    /// so no button could be clicked. This adds one (Input System module) to every loaded scene that lacks it.
    /// </summary>
    public static class EventSystemBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
