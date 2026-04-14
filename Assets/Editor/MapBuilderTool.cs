using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapBuilderTool
{
    private const string ContainerName = "_MapExpansion_Container";

    // Offset from origin — expansion starts here so it wraps around the existing NukeTown map
    private static readonly Vector3 ExpansionOrigin = new Vector3(-80f, 0f, -80f);

    private static readonly string[] SearchFolders =
    {
        "Assets/StarterAssets/Environment/Prefabs",
        "Assets/StarterAssets/Environment/Art/Models"
    };

    [MenuItem("Tools/PRISM-7/Auto-Expand Map")]
    public static void AutoExpandMap()
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Auto Expand Map");

        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("[MapBuilderTool] No active scene is loaded.");
                return;
            }

            int groundLayer = EnsureLayer("Ground");
            GameObject container = CreateOrResetContainer(activeScene);
            List<GameObject> environmentAssets = FindEnvironmentAssets();

            if (environmentAssets.Count == 0)
            {
                Debug.LogWarning("[MapBuilderTool] No environment assets found under StarterAssets/Environment.");
                return;
            }

            BuildLargePlayground(environmentAssets, container.transform, groundLayer);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[MapBuilderTool] Placed {container.transform.childCount} pieces in '{activeScene.name}' under '{ContainerName}'.");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    // ──────────────────────────────────────────────
    // Layout
    // ──────────────────────────────────────────────

    private static void BuildLargePlayground(List<GameObject> assets, Transform parent, int groundLayer)
    {
        var floors   = assets.Where(a => HasName(a, "ground", "box", "block")).ToList();
        var stairs   = assets.Where(a => HasName(a, "stairs")).ToList();
        var ramps    = assets.Where(a => HasName(a, "ramp")).ToList();
        var accents  = assets.Where(a => HasName(a, "wall", "tunnel", "structure")).ToList();

        if (floors.Count == 0) { Debug.LogWarning("[MapBuilderTool] No floor pieces found."); return; }

        Vector3 o = ExpansionOrigin;

        // ── Zone A: Wide open ground plaza (7 lanes × 10 rows) ──────────────
        const float laneW   = 16f;
        const float rowD    = 12f;
        const int   lanes   = 7;
        const int   rows    = 10;

        for (int row = 0; row < rows; row++)
        {
            for (int lane = 0; lane < lanes; lane++)
            {
                GameObject asset = floors[(row * lanes + lane) % floors.Count];
                Vector3 pos = o + new Vector3(lane * laneW, 0f, row * rowD);
                Quaternion rot = (row + lane) % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                var piece = InstantiateWithUndo(asset, pos, rot, parent, groundLayer);
                SnapBottomToY(piece, 0f);
            }
        }

        // ── Zone B: Right wing — corridor with walls on both sides ────────
        float rightX = o.x + lanes * laneW + 6f;
        const int corridorLen = 12;
        const float corridorSpacing = 9f;

        for (int i = 0; i < corridorLen; i++)
        {
            GameObject floorAsset = floors[i % floors.Count];
            Vector3 floorPos = new Vector3(rightX, 0f, o.z + i * corridorSpacing);
            var fp = InstantiateWithUndo(floorAsset, floorPos, Quaternion.identity, parent, groundLayer);
            SnapBottomToY(fp, 0f);

            if (accents.Count > 0)
            {
                // Left wall
                var wl = InstantiateWithUndo(accents[i % accents.Count], floorPos + new Vector3(-8f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), parent, groundLayer);
                SnapBottomToY(wl, 0f);
                // Right wall
                var wr = InstantiateWithUndo(accents[i % accents.Count], floorPos + new Vector3(8f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), parent, groundLayer);
                SnapBottomToY(wr, 0f);
            }
        }

        // ── Zone C: Left wing — open area with tunnels ───────────────────
        float leftX = o.x - 90f;
        const int leftRows = 8;

        for (int i = 0; i < leftRows; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                GameObject asset = floors[(i + j) % floors.Count];
                Vector3 pos = new Vector3(leftX + j * laneW, 0f, o.z + i * rowD);
                var p = InstantiateWithUndo(asset, pos, Quaternion.identity, parent, groundLayer);
                SnapBottomToY(p, 0f);
            }

            if (accents.Count > 0 && i % 3 == 1)
            {
                var tunnel = InstantiateWithUndo(accents[i % accents.Count],
                    new Vector3(leftX + 2 * laneW, 0f, o.z + i * rowD),
                    Quaternion.Euler(0f, 0f, 0f), parent, groundLayer);
                SnapBottomToY(tunnel, 0f);
            }
        }

        // ── Zone D: Far end — elevated platforms via stairs / ramps ───────
        float farZ = o.z + rows * rowD + 10f;
        float elevH1 = 3f;
        float elevH2 = 6f;

        // Stairs leading up
        for (int i = 0; i < stairs.Count; i++)
        {
            var sp = InstantiateWithUndo(stairs[i], new Vector3(o.x + i * 20f, 0f, farZ), Quaternion.identity, parent, groundLayer);
            SnapBottomToY(sp, 0f);
        }

        // Mid-elevation platforms (level 1)
        float midZ = farZ + 14f;
        const int midPlatforms = 9;
        for (int i = 0; i < midPlatforms; i++)
        {
            GameObject asset = floors[i % floors.Count];
            float xPos = o.x + (i - midPlatforms / 2) * 14f;
            var p = InstantiateWithUndo(asset, new Vector3(xPos, 0f, midZ), Quaternion.identity, parent, groundLayer);
            SnapBottomToY(p, elevH1);
        }

        // High elevation platforms (level 2)
        float highZ = midZ + 16f;
        const int highPlatforms = 5;
        for (int i = 0; i < highPlatforms; i++)
        {
            GameObject asset = floors[i % floors.Count];
            float xPos = o.x + (i - highPlatforms / 2) * 18f;
            var p = InstantiateWithUndo(asset, new Vector3(xPos, 0f, highZ), Quaternion.identity, parent, groundLayer);
            SnapBottomToY(p, elevH2);
        }

        // Ramps up to level 2 from level 1
        for (int i = 0; i < Mathf.Min(ramps.Count, 4); i++)
        {
            float xPos = o.x + (i - 1) * 22f;
            var rp = InstantiateWithUndo(ramps[i % ramps.Count], new Vector3(xPos, 0f, midZ + 8f),
                Quaternion.Euler(0f, i % 2 == 0 ? 0f : 180f, 0f), parent, groundLayer);
            SnapBottomToY(rp, 0f);
        }

        // Accent structures scattered around far zone
        for (int i = 0; i < accents.Count; i++)
        {
            float ax = o.x - 20f + (i % 6) * 22f;
            float az = farZ - 8f + (i / 6) * 12f;
            var ap = InstantiateWithUndo(accents[i], new Vector3(ax, 0f, az), Quaternion.Euler(0f, (i % 4) * 90f, 0f), parent, groundLayer);
            SnapBottomToY(ap, 0f);
        }

        // ── Zone E: Rear ground extension behind existing map ─────────────
        float rearZ = o.z - 60f;
        const int rearRows = 6;
        const int rearLanes = 9;

        for (int row = 0; row < rearRows; row++)
        {
            for (int lane = 0; lane < rearLanes; lane++)
            {
                GameObject asset = floors[(row + lane) % floors.Count];
                Vector3 pos = new Vector3(o.x - 10f + lane * laneW, 0f, rearZ + row * rowD);
                Quaternion rot = (row + lane) % 3 == 0 ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                var p = InstantiateWithUndo(asset, pos, rot, parent, groundLayer);
                SnapBottomToY(p, 0f);
            }
        }
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static GameObject CreateOrResetContainer(Scene scene)
    {
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == ContainerName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject container = new GameObject(ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Create Map Expansion Container");
        SceneManager.MoveGameObjectToScene(container, scene);
        container.transform.position = Vector3.zero;
        return container;
    }

    private static List<GameObject> FindEnvironmentAssets()
    {
        var results = new List<(GameObject asset, int priority)>();
        var seenPaths = new HashSet<string>();

        foreach (string folder in SearchFolders)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seenPaths.Add(path)) continue;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !IsEnvironmentPiece(asset.name)) continue;

                results.Add((asset, GetPriority(asset.name)));
            }
        }

        return results
            .OrderBy(item => item.priority)
            .ThenBy(item => item.asset.name)
            .Select(item => item.asset)
            .ToList();
    }

    private static bool IsEnvironmentPiece(string assetName)
    {
        string lower = assetName.ToLowerInvariant();
        if (lower.Contains("environment_prefab")) return false;

        return lower.Contains("ground") || lower.Contains("box")   ||
               lower.Contains("block")  || lower.Contains("ramp")  ||
               lower.Contains("stairs") || lower.Contains("wall")  ||
               lower.Contains("tunnel") || lower.Contains("structure");
    }

    private static int GetPriority(string assetName)
    {
        string lower = assetName.ToLowerInvariant();
        if (lower.Contains("ground") || lower.Contains("box") || lower.Contains("block")) return 0;
        if (lower.Contains("stairs"))  return 1;
        if (lower.Contains("ramp"))    return 2;
        if (lower.Contains("tunnel") || lower.Contains("wall") || lower.Contains("structure")) return 3;
        return 10;
    }

    private static GameObject InstantiateWithUndo(GameObject asset, Vector3 position, Quaternion rotation, Transform parent, int groundLayer)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        if (instance == null)
        {
            instance = Object.Instantiate(asset, parent);
            instance.name = asset.name;
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
        Undo.SetTransformParent(instance.transform, parent, "Parent Environment Piece");
        instance.transform.SetPositionAndRotation(position, rotation);

        ConfigureInstance(instance, groundLayer);
        return instance;
    }

    private static void ConfigureInstance(GameObject root, int groundLayer)
    {
        SetLayerRecursive(root, groundLayer);
        root.tag = "Untagged";
        GameObjectUtility.SetStaticEditorFlags(root,
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.NavigationStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic);

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;

            MeshCollider mc = Undo.AddComponent<MeshCollider>(filter.gameObject);
            mc.sharedMesh = filter.sharedMesh;
            mc.convex = false;
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }

    private static void SnapBottomToY(GameObject go, float targetY)
    {
        Bounds b = CalculateBounds(go);
        go.transform.position += new Vector3(0f, targetY - b.min.y, 0f);
    }

    private static Bounds CalculateBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
            return b;
        }

        return new Bounds(go.transform.position, Vector3.one);
    }

    private static bool HasName(GameObject asset, params string[] tokens)
    {
        string lower = asset.name.ToLowerInvariant();
        return tokens.Any(lower.Contains);
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            SerializedProperty lp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(lp.stringValue))
            {
                lp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return i;
            }
        }

        Debug.LogWarning($"[MapBuilderTool] Could not create layer '{layerName}'. Using Default.");
        return 0;
    }
}
