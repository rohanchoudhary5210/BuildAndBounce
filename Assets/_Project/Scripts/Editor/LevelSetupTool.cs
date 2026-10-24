using BuildAndBounce.Level;
using BuildAndBounce.Player;
using BuildAndBounce.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildAndBounce.Editor
{
    /// <summary>
    /// Editor utility to generate a complete, playable M1 platformer test course in the current scene.
    /// Includes Start pad, moving platform, checkpoint, stepping blocks, finish pad, and fall kill zone.
    /// </summary>
    public static class LevelSetupTool
    {
        [MenuItem("Build & Bounce/Setup M1 Test Course")]
        public static void SetupTestCourse()
        {
            // Remove previous test course if exists to allow clean re-generation
            GameObject existing = GameObject.Find("M1_TestCourse");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject courseRoot = new GameObject("M1_TestCourse");
            Undo.RegisterCreatedObjectUndo(courseRoot, "Setup M1 Test Course");

            // 1. Start Platform (Green)
            GameObject startPad = CreatePlatform("StartPlatform", courseRoot.transform, new Vector3(0f, 0f, 0f), new Vector3(4f, 0.5f, 4f), new Color(0.2f, 0.8f, 0.3f));
            var startComp = startPad.AddComponent<CourseStart>();
            var startTrigger = startPad.AddComponent<BoxCollider>();
            startTrigger.isTrigger = true;
            startTrigger.center = new Vector3(0f, 1f, 0f);
            startTrigger.size = new Vector3(3.8f, 2f, 3.8f);

            // 2. Moving Platform (Cyan) ping-pongs across 8-meter gap
            GameObject movingPlat = CreatePlatform("MovingPlatform", courseRoot.transform, new Vector3(0f, 0f, 6f), new Vector3(3f, 0.5f, 3f), new Color(0.2f, 0.7f, 0.9f));
            var mpComp = movingPlat.AddComponent<MovingPlatform>();

            // Rider trigger zone on top of moving platform
            var riderTrigger = movingPlat.AddComponent<BoxCollider>();
            riderTrigger.isTrigger = true;
            riderTrigger.center = new Vector3(0f, 0.6f, 0f);
            riderTrigger.size = new Vector3(2.8f, 0.5f, 2.8f);

            // 3. Middle Checkpoint Platform (Yellow)
            GameObject checkPad = CreatePlatform("CheckpointPlatform", courseRoot.transform, new Vector3(0f, 0f, 18f), new Vector3(4f, 0.5f, 4f), new Color(0.9f, 0.8f, 0.2f));
            var cpComp = checkPad.AddComponent<Checkpoint>();
            var cpTrigger = checkPad.AddComponent<BoxCollider>();
            cpTrigger.isTrigger = true;
            cpTrigger.center = new Vector3(0f, 1f, 0f);
            cpTrigger.size = new Vector3(3.8f, 2f, 3.8f);

            // 4. Stepping Stones / Raised Platforms
            CreatePlatform("Step_1", courseRoot.transform, new Vector3(0f, 0.5f, 24f), new Vector3(2.5f, 0.5f, 2.5f), new Color(0.6f, 0.6f, 0.7f));
            CreatePlatform("Step_2", courseRoot.transform, new Vector3(0f, 1.3f, 28f), new Vector3(2.5f, 0.5f, 2.5f), new Color(0.6f, 0.6f, 0.7f));
            CreatePlatform("Step_3", courseRoot.transform, new Vector3(0f, 2.2f, 32f), new Vector3(2.5f, 0.5f, 2.5f), new Color(0.6f, 0.6f, 0.7f));

            // 5. Finish Platform (Gold)
            GameObject finishPad = CreatePlatform("FinishPlatform", courseRoot.transform, new Vector3(0f, 3.0f, 38f), new Vector3(5f, 0.5f, 5f), new Color(1f, 0.6f, 0.1f));
            var finishComp = finishPad.AddComponent<CourseFinish>();
            var finishTrigger = finishPad.AddComponent<BoxCollider>();
            finishTrigger.isTrigger = true;
            finishTrigger.center = new Vector3(0f, 1f, 0f);
            finishTrigger.size = new Vector3(4.8f, 2f, 4.8f);

            // 6. Kill Zone (Red, low floor volume)
            GameObject killObj = new GameObject("KillZone");
            killObj.transform.SetParent(courseRoot.transform, false);
            killObj.transform.position = new Vector3(0f, -8f, 20f);
            var killBox = killObj.AddComponent<BoxCollider>();
            killBox.isTrigger = true;
            killBox.size = new Vector3(80f, 2f, 80f);
            killObj.AddComponent<KillZone>();

            // 7. Ensure Player has PlayerRespawn and is positioned at Start
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            GameObject player = controller != null ? controller.gameObject : null;

            if (player != null)
            {
                if (!player.TryGetComponent<PlayerRespawn>(out _))
                {
                    player.AddComponent<PlayerRespawn>();
                }

                // Teleport player above start platform
                if (player.TryGetComponent<CharacterController>(out var cc))
                {
                    cc.enabled = false;
                    player.transform.position = new Vector3(0f, 1.5f, 0f);
                    player.transform.rotation = Quaternion.identity;
                    cc.enabled = true;
                }
            }

            // 8. Ensure CourseTimer exists
            if (UnityEngine.Object.FindAnyObjectByType<CourseTimer>() == null)
            {
                GameObject timerObj = new GameObject("CourseManager");
                timerObj.transform.SetParent(courseRoot.transform, false);
                timerObj.AddComponent<CourseTimer>();
            }

            EditorUtility.SetDirty(courseRoot);
            if (player != null) EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(courseRoot.scene);

            EditorUtility.DisplayDialog(
                "Course Created",
                "M1 Test Course successfully generated in scene!\n\n" +
                "1. Start Pad (Green)\n" +
                "2. Moving Platform (Cyan) - Jitter-free riding\n" +
                "3. Checkpoint (Yellow) - Respawns here if you fall\n" +
                "4. Stepping Stones - Double jump / dash practice\n" +
                "5. Finish Pad (Gold) - Stops timer\n" +
                "6. Kill Zone (Below) - Triggers instant respawn\n\n" +
                "Press Play (▶) to run the course!",
                "Let's Race!");
            Debug.Log("[Build & Bounce] M1 Test Course generated successfully!", courseRoot);
        }

        private static GameObject CreatePlatform(string name, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = size;

            // Apply solid color material
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader) { color = color };
                    renderer.sharedMaterial = mat;
                }
            }

            return obj;
        }
    }
}
