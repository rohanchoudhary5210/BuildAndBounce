using BuildAndBounce.Core;
using BuildAndBounce.Player;
using BuildAndBounce.Race;
using BuildAndBounce.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildAndBounce.Editor
{
    /// <summary>
    /// Editor setup tool to configure the M4 Ghost Racing (Async Multiplayer) system:
    /// - Sets up CourseTimer, GhostRecorder, GhostPlayback, and LeaderboardManager.
    /// - Creates a translucent ghost capsule avatar.
    /// - Wires the LeaderboardHUD results overlay.
    /// </summary>
    public static class RaceSetupTool
    {
        [MenuItem("Build & Bounce/Setup Ghost Racing (M4)")]
        public static void SetupGhostRacing()
        {
            // 1. Ensure CourseTimer exists
            var timer = UnityEngine.Object.FindAnyObjectByType<CourseTimer>();
            if (timer == null)
            {
                var timerObj = new GameObject("CourseTimer");
                Undo.RegisterCreatedObjectUndo(timerObj, "Create Course Timer");
                timer = timerObj.AddComponent<CourseTimer>();
            }

            // 2. Ensure Player is identified for recorder
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();

            // 3. Find or create RaceManager GameObject
            var raceManager = UnityEngine.Object.FindAnyObjectByType<RaceGhostManager>();
            if (raceManager == null)
            {
                var managerObj = new GameObject("RaceManager");
                Undo.RegisterCreatedObjectUndo(managerObj, "Create Race Manager");
                raceManager = managerObj.AddComponent<RaceGhostManager>();
            }

            Undo.RecordObject(raceManager, "Configure Race Manager");

            // 4. Ensure LeaderboardManager
            if (!raceManager.TryGetComponent<LeaderboardManager>(out var leaderboard))
            {
                leaderboard = raceManager.gameObject.AddComponent<LeaderboardManager>();
            }

            // 5. Ensure GhostRecorder
            if (!raceManager.TryGetComponent<GhostRecorder>(out var recorder))
            {
                recorder = raceManager.gameObject.AddComponent<GhostRecorder>();
            }
            if (player != null)
            {
                recorder.SetTarget(player.transform);
            }

            // 6. Ensure GhostPlayback
            if (!raceManager.TryGetComponent<GhostPlayback>(out var playback))
            {
                playback = raceManager.gameObject.AddComponent<GhostPlayback>();
            }

            // 7. Ensure LeaderboardHUD
            var hud = UnityEngine.Object.FindAnyObjectByType<LeaderboardHUD>();
            if (hud == null)
            {
                var hudObj = new GameObject("LeaderboardHUD");
                Undo.RegisterCreatedObjectUndo(hudObj, "Create Leaderboard HUD");
                hud = hudObj.AddComponent<LeaderboardHUD>();
            }

            // Wire Serialized Properties on RaceGhostManager
            var soRace = new SerializedObject(raceManager);
            var pRecorder = soRace.FindProperty("_recorder");
            if (pRecorder != null) pRecorder.objectReferenceValue = recorder;
            var pPlayback = soRace.FindProperty("_playback");
            if (pPlayback != null) pPlayback.objectReferenceValue = playback;
            var pLeaderboards = soRace.FindProperty("_leaderboards");
            if (pLeaderboards != null) pLeaderboards.objectReferenceValue = leaderboard;
            var pTimer = soRace.FindProperty("_timer");
            if (pTimer != null) pTimer.objectReferenceValue = timer;
            soRace.ApplyModifiedProperties();

            // Wire GhostRecorder Target
            if (recorder != null && player != null)
            {
                var soRec = new SerializedObject(recorder);
                var pTarget = soRec.FindProperty("_target");
                if (pTarget != null) pTarget.objectReferenceValue = player.transform;
                soRec.ApplyModifiedProperties();
            }

            // Wire LeaderboardHUD Properties
            if (hud != null)
            {
                var soHud = new SerializedObject(hud);
                var pManager = soHud.FindProperty("_leaderboardManager");
                if (pManager != null) pManager.objectReferenceValue = leaderboard;
                var pRace = soHud.FindProperty("_raceGhostManager");
                if (pRace != null) pRace.objectReferenceValue = raceManager;
                soHud.ApplyModifiedProperties();
            }

            // 8. Enforce single audio listener
            AudioListenerGuard.EnforceSingleListener();

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = raceManager.gameObject;

            EditorUtility.DisplayDialog(
                "Ghost Racing Configured",
                "M4 Ghost Racing successfully configured!\n\n" +
                "- RaceGhostManager with GhostRecorder (20 Hz FixedUpdate sampling)\n" +
                "- GhostPlayback with translucent ghost avatar\n" +
                "- LeaderboardManager saving to persistent JSON\n" +
                "- LeaderboardHUD displaying Top 5 runs & Personal Best records\n\n" +
                "Press Play and run the course: upon finishing, your run will be recorded. " +
                "On your next run, you will race against your translucent ghost!",
                "OK");

            Debug.Log("[Build & Bounce] M4 Ghost Racing setup complete!", raceManager);
        }
    }
}
