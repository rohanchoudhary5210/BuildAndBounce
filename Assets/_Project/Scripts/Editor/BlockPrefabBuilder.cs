using System.IO;
using BuildAndBounce.Builder;
using BuildAndBounce.Level;
using UnityEditor;
using UnityEngine;

namespace BuildAndBounce.Editor
{
    /// <summary>
    /// Editor tool to generate functional gameplay prefabs for builder blocks
    /// (Start, Finish, Checkpoint, Hazard, MovingPlatform, Static platforms)
    /// and wire them into BlockCatalog.asset.
    /// </summary>
    [InitializeOnLoad]
    public static class BlockPrefabBuilder
    {
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string MaterialsFolder = "Assets/_Project/Materials";
        private const string CatalogPath = "Assets/_Project/Data/BlockCatalog.asset";

        static BlockPrefabBuilder()
        {
            EditorApplication.delayCall += EnsureBlockPrefabsAndCatalog;
        }

        [MenuItem("Build & Bounce/Create Block Prefabs")]
        public static void EnsureBlockPrefabsAndCatalog()
        {
            EnsureFolders();

            // 1. Create or get shared materials
            var matStart = EnsureMaterial("Mat_StartPad", new Color(0.2f, 0.85f, 0.35f));
            var matFinish = EnsureMaterial("Mat_FinishPad", new Color(1f, 0.65f, 0.1f));
            var mat1x1 = EnsureMaterial("Mat_1x1Block", new Color(0.6f, 0.6f, 0.7f));
            var mat3x3 = EnsureMaterial("Mat_3x3Platform", new Color(0.45f, 0.5f, 0.65f));
            var matCheck = EnsureMaterial("Mat_Checkpoint", new Color(0.9f, 0.85f, 0.2f));
            var matHazard = EnsureMaterial("Mat_Hazard", new Color(0.9f, 0.2f, 0.25f));
            var matMoving = EnsureMaterial("Mat_MovingPlatform", new Color(0.2f, 0.75f, 0.95f));

            // 2. Build and save prefabs
            var prefabStart = BuildStartPadPrefab(matStart);
            var prefabFinish = BuildFinishPadPrefab(matFinish);
            var prefab1x1 = BuildStaticPlatformPrefab("PlatformCube1x1", new Vector3(1f, 1f, 1f), mat1x1);
            var prefab3x3 = BuildStaticPlatformPrefab("PlatformStep3x3", new Vector3(3f, 0.5f, 3f), mat3x3);
            var prefabCheck = BuildCheckpointPrefab(matCheck);
            var prefabHazard = BuildHazardPrefab(matHazard);
            var prefabMoving = BuildMovingPlatformPrefab(matMoving);

            AssetDatabase.SaveAssets();

            // 3. Assign prefabs to BlockCatalog.asset
            WireCatalog(prefabStart, prefabFinish, prefab1x1, prefab3x3, prefabCheck, prefabHazard, prefabMoving);

            Debug.Log("[Build & Bounce] All builder block prefabs successfully created and wired to BlockCatalog!");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject BuildStartPadPrefab(Material mat)
        {
            string path = $"{PrefabsFolder}/StartPad.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("StartPad");

            // Solid base collider
            var solidCol = root.AddComponent<BoxCollider>();
            solidCol.size = new Vector3(4f, 0.5f, 4f);
            solidCol.center = Vector3.zero;
            solidCol.isTrigger = false;

            // Trigger collider
            var triggerCol = root.AddComponent<BoxCollider>();
            triggerCol.size = new Vector3(3.8f, 2f, 3.8f);
            triggerCol.center = new Vector3(0f, 1f, 0f);
            triggerCol.isTrigger = true;

            // SpawnPoint child
            var spawnObj = new GameObject("SpawnPoint");
            spawnObj.transform.SetParent(root.transform, false);
            spawnObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            // Visual mesh
            CreateVisualChild(root.transform, new Vector3(4f, 0.5f, 4f), mat);

            // CourseStart component
            var startComp = root.AddComponent<CourseStart>();
            var so = new SerializedObject(startComp);
            var spProp = so.FindProperty("_spawnPoint");
            if (spProp != null) spProp.objectReferenceValue = spawnObj.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildFinishPadPrefab(Material mat)
        {
            string path = $"{PrefabsFolder}/FinishPad.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("FinishPad");

            var solidCol = root.AddComponent<BoxCollider>();
            solidCol.size = new Vector3(5f, 0.5f, 5f);
            solidCol.center = Vector3.zero;
            solidCol.isTrigger = false;

            var triggerCol = root.AddComponent<BoxCollider>();
            triggerCol.size = new Vector3(4.8f, 2f, 4.8f);
            triggerCol.center = new Vector3(0f, 1f, 0f);
            triggerCol.isTrigger = true;

            CreateVisualChild(root.transform, new Vector3(5f, 0.5f, 5f), mat);

            root.AddComponent<CourseFinish>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildCheckpointPrefab(Material mat)
        {
            string path = $"{PrefabsFolder}/CheckpointPad.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("CheckpointPad");

            var solidCol = root.AddComponent<BoxCollider>();
            solidCol.size = new Vector3(3f, 0.5f, 3f);
            solidCol.center = Vector3.zero;
            solidCol.isTrigger = false;

            var triggerCol = root.AddComponent<BoxCollider>();
            triggerCol.size = new Vector3(2.8f, 2f, 2.8f);
            triggerCol.center = new Vector3(0f, 1f, 0f);
            triggerCol.isTrigger = true;

            var respawnObj = new GameObject("RespawnPoint");
            respawnObj.transform.SetParent(root.transform, false);
            respawnObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            var visual = CreateVisualChild(root.transform, new Vector3(3f, 0.5f, 3f), mat);

            var cp = root.AddComponent<Checkpoint>();
            var so = new SerializedObject(cp);
            var rpProp = so.FindProperty("_respawnPoint");
            if (rpProp != null) rpProp.objectReferenceValue = respawnObj.transform;
            var irProp = so.FindProperty("_indicatorRenderer");
            if (irProp != null && visual != null) irProp.objectReferenceValue = visual.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildHazardPrefab(Material mat)
        {
            string path = $"{PrefabsFolder}/HazardSpikes.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("HazardSpikes");

            var triggerCol = root.AddComponent<BoxCollider>();
            triggerCol.size = new Vector3(2f, 0.8f, 2f);
            triggerCol.center = new Vector3(0f, 0.4f, 0f);
            triggerCol.isTrigger = true;

            CreateVisualChild(root.transform, new Vector3(2f, 0.3f, 2f), mat);

            root.AddComponent<KillZone>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildMovingPlatformPrefab(Material mat)
        {
            string path = $"{PrefabsFolder}/MovingPlatform.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("MovingPlatform");

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var solidCol = root.AddComponent<BoxCollider>();
            solidCol.size = new Vector3(3f, 0.5f, 3f);
            solidCol.center = Vector3.zero;
            solidCol.isTrigger = false;

            var riderTrigger = root.AddComponent<BoxCollider>();
            riderTrigger.size = new Vector3(2.8f, 0.6f, 2.8f);
            riderTrigger.center = new Vector3(0f, 0.55f, 0f);
            riderTrigger.isTrigger = true;

            CreateVisualChild(root.transform, new Vector3(3f, 0.5f, 3f), mat);

            root.AddComponent<MovingPlatform>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildStaticPlatformPrefab(string name, Vector3 size, Material mat)
        {
            string path = $"{PrefabsFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(name);

            var solidCol = root.AddComponent<BoxCollider>();
            solidCol.size = size;
            solidCol.center = Vector3.zero;
            solidCol.isTrigger = false;

            CreateVisualChild(root.transform, size, mat);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateVisualChild(Transform parent, Vector3 size, Material mat)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = size;

            // Remove default collider from primitive
            var col = visual.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;

            return visual;
        }

        private static void WireCatalog(
            GameObject pStart,
            GameObject pFinish,
            GameObject p1x1,
            GameObject p3x3,
            GameObject pCheck,
            GameObject pHazard,
            GameObject pMoving)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BlockCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BlockCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var so = new SerializedObject(catalog);
            var blocksProp = so.FindProperty("_blocks");

            // Define block mappings
            var mappings = new (string id, string name, BlockCategory cat, GameObject prefab, Vector3 dims, Color color)[]
            {
                ("start_pad", "Start Pad", BlockCategory.StartFinish, pStart, new Vector3(4f, 0.5f, 4f), new Color(0.2f, 0.85f, 0.35f)),
                ("finish_pad", "Finish Pad", BlockCategory.StartFinish, pFinish, new Vector3(5f, 0.5f, 5f), new Color(1f, 0.65f, 0.1f)),
                ("platform_cube_1x1", "1x1 Block", BlockCategory.Platform, p1x1, new Vector3(1f, 1f, 1f), new Color(0.6f, 0.6f, 0.7f)),
                ("platform_step_3x3", "3x3 Platform", BlockCategory.Platform, p3x3, new Vector3(3f, 0.5f, 3f), new Color(0.45f, 0.5f, 0.65f)),
                ("checkpoint_pad", "Checkpoint", BlockCategory.Checkpoint, pCheck, new Vector3(3f, 0.5f, 3f), new Color(0.9f, 0.85f, 0.2f)),
                ("hazard_spikes", "Hazard", BlockCategory.Hazard, pHazard, new Vector3(2f, 0.3f, 2f), new Color(0.9f, 0.2f, 0.25f)),
                ("moving_platform", "Moving Platform", BlockCategory.Moving, pMoving, new Vector3(3f, 0.5f, 3f), new Color(0.2f, 0.75f, 0.95f))
            };

            blocksProp.arraySize = mappings.Length;
            for (int i = 0; i < mappings.Length; i++)
            {
                var elem = blocksProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("_id").stringValue = mappings[i].id;
                elem.FindPropertyRelative("_displayName").stringValue = mappings[i].name;
                elem.FindPropertyRelative("_category").enumValueIndex = (int)mappings[i].cat;
                elem.FindPropertyRelative("_prefab").objectReferenceValue = mappings[i].prefab;
                elem.FindPropertyRelative("_dimensions").vector3Value = mappings[i].dims;
                elem.FindPropertyRelative("_themeColor").colorValue = mappings[i].color;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
