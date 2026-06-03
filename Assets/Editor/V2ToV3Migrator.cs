using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Helpers for swapping Industrial Set v2 out of the active scene before
// dropping in the v3 map. Two menu items:
//
//   PRISM-7 ▸ Map ▸ Disable v2 Map In Active Scene
//     Finds every scene object whose prefab source lives under the v2 import
//     folder and reparents it under a single disabled OLD_IndustrialSetV2_Disabled
//     root so nothing renders, but nothing is destroyed.
//
//   PRISM-7 ▸ Map ▸ Report v2 Usage In Active Scene
//     Read-only — logs what would be touched without modifying the scene.
public static class V2ToV3Migrator
{
    // Adjust if the v2 assets live somewhere else in your project.
    private const string V2PathFragment = "RPG_FPS_game_assets_industrial";
    private const string V2MapName      = "Map_v2";
    private const string DisabledRootName = "OLD_IndustrialSetV2_Disabled";

    [MenuItem("PRISM-7/Map/Report v2 Usage In Active Scene")]
    private static void Report()
    {
        var hits = FindV2Roots();
        Debug.Log($"[V2ToV3Migrator] Found {hits.Count} root v2 object(s) in active scene.");
        foreach (var h in hits) Debug.Log($"  • {GetScenePath(h.transform)}  ←  {AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(h))}", h);
    }

    [MenuItem("PRISM-7/Map/Disable v2 Map In Active Scene")]
    private static void DisableV2()
    {
        var scene = SceneManager.GetActiveScene();
        var hits = FindV2Roots();
        if (hits.Count == 0)
        {
            EditorUtility.DisplayDialog("v2 Migrator", "No v2 objects found in the active scene.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "v2 Migrator",
                $"Move {hits.Count} v2 root object(s) under '{DisabledRootName}' and disable them?\n\nNothing is deleted — fully reversible by re-parenting.",
                "Disable", "Cancel"))
            return;

        var disabledRoot = GameObject.Find("/" + DisabledRootName);
        if (disabledRoot == null)
        {
            disabledRoot = new GameObject(DisabledRootName);
            Undo.RegisterCreatedObjectUndo(disabledRoot, "Create v2 Disabled Root");
            SceneManager.MoveGameObjectToScene(disabledRoot, scene);
        }

        foreach (var go in hits)
        {
            Undo.SetTransformParent(go.transform, disabledRoot.transform, "Reparent v2 → Disabled");
        }
        Undo.RecordObject(disabledRoot, "Disable v2 Root");
        disabledRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[V2ToV3Migrator] Disabled {hits.Count} v2 root(s). Now drag your v3 map prefab into the scene.");
    }

    // Find "root" v2 objects — i.e. the topmost ancestor in the scene that traces
    // back to a v2 asset. Avoids reparenting deep children individually.
    private static List<GameObject> FindV2Roots()
    {
        var roots = new HashSet<GameObject>();
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            ScanRecursive(root.transform, roots);
        }
        return new List<GameObject>(roots);
    }

    private static void ScanRecursive(Transform t, HashSet<GameObject> hits)
    {
        if (IsV2(t.gameObject))
        {
            hits.Add(t.gameObject);
            return; // Don't descend — we've found the topmost v2 ancestor here.
        }
        for (int i = 0; i < t.childCount; i++) ScanRecursive(t.GetChild(i), hits);
    }

    private static bool IsV2(GameObject go)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
        if (src == null) return false;
        var path = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.Contains(V2PathFragment)) return false;
        // Heuristic: anything under the industrial import folder that isn't
        // referenced from a v3 subfolder is treated as v2. Tighten if you have a
        // mixed v2/v3 import.
        return path.Contains("Map_v1") || path.Contains(V2MapName) || !path.Contains("_v3");
    }

    private static string GetScenePath(Transform t)
    {
        var sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }
}
