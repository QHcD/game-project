#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds assets not referenced by any scene in the build or any prefab under <c>Assets</c>, then
/// optionally deletes low-risk types (materials, textures). Excludes <c>Resources</c>, packages,
/// and large third-party trees so runtime loads and vendor assets stay intact.
///
/// Menu: <b>PRISM / Cleanup / …</b>
/// </summary>
public static class PrismUnusedAssetCleanup
{
    private static readonly string[] ExcludePathFragments =
    {
        "/Packages/", "/Editor/", "/Resources/", "/StreamingAssets/", "/Plugins/",
        "TextMesh Pro", "TutorialInfo", "/_Recovery/",
        "StarterAssets", "RPG_FPS_game_assets_industrial", "FirstPersonMelee"
    };

    private static bool IsExcludedPath(string path)
    {
        path = path.Replace('\\', '/');
        for (int i = 0; i < ExcludePathFragments.Length; i++)
        {
            if (path.IndexOf(ExcludePathFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static void AddDependenciesRecursive(string assetPath, HashSet<string> used)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            return;
        if (used.Contains(assetPath))
            return;

        string[] deps = AssetDatabase.GetDependencies(assetPath, true);
        for (int i = 0; i < deps.Length; i++)
        {
            string d = deps[i].Replace('\\', '/');
            if (d.StartsWith("Assets/", StringComparison.Ordinal))
                used.Add(d);
        }
    }

    private static HashSet<string> BuildUsedSet()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s == null || !s.enabled) continue;
            if (string.IsNullOrEmpty(s.path)) continue;
            AddDependenciesRecursive(s.path, used);
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (IsExcludedPath(p)) continue;
            AddDependenciesRecursive(p, used);
        }

        return used;
    }

    private static bool IsSafeToDelete(string path, Type mainType)
    {
        if (mainType == typeof(MonoScript) || mainType == typeof(SceneAsset) || mainType == typeof(DefaultAsset))
            return false;
        if (typeof(ScriptableObject).IsAssignableFrom(mainType) && mainType != typeof(Material))
            return false;

        if (mainType == typeof(Material))
            return true;
        if (typeof(Texture).IsAssignableFrom(mainType))
            return true;

        return false;
    }

    [MenuItem("PRISM/Cleanup/Scan Unreferenced Assets (Console Log)", false, 200)]
    public static void ScanOnly()
    {
        HashSet<string> used = BuildUsedSet();
        string[] all = AssetDatabase.GetAllAssetPaths();
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            string p = all[i].Replace('\\', '/');
            if (!p.StartsWith("Assets/", StringComparison.Ordinal)) continue;
            if (IsExcludedPath(p)) continue;
            if (AssetDatabase.IsValidFolder(p)) continue;
            if (used.Contains(p)) continue;

            Type t = AssetDatabase.GetMainAssetTypeAtPath(p);
            if (t == null) continue;
            if (!IsSafeToDelete(p, t)) continue;

            Debug.Log($"[PrismCleanup] Unreferenced (safe type): {p}  ({t.Name})");
            count++;
        }

        Debug.Log($"[PrismCleanup] Scan done. Unreferenced candidates (materials/textures only): {count}.");
    }

    [MenuItem("PRISM/Cleanup/DELETE Unreferenced Materials & Textures…", false, 201)]
    public static void DeleteWithConfirm()
    {
        if (!EditorUtility.DisplayDialog(
                "PRISM — Delete unreferenced assets",
                "This deletes Materials and Textures under Assets/ that are not dependencies of any " +
                "enabled build scene or any prefab (except excluded vendor folders).\n\n" +
                "Make a Git commit or backup first. MonoBehaviour scripts and ScriptableObjects are never deleted.\n\n" +
                "Continue?",
                "Delete",
                "Cancel"))
            return;

        HashSet<string> used = BuildUsedSet();
        string[] all = AssetDatabase.GetAllAssetPaths();
        int deleted = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < all.Length; i++)
            {
                string p = all[i].Replace('\\', '/');
                if (!p.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (IsExcludedPath(p)) continue;
                if (AssetDatabase.IsValidFolder(p)) continue;
                if (used.Contains(p)) continue;

                Type t = AssetDatabase.GetMainAssetTypeAtPath(p);
                if (t == null || !IsSafeToDelete(p, t)) continue;

                if (AssetDatabase.DeleteAsset(p))
                {
                    deleted++;
                    Debug.Log($"[PrismCleanup] Deleted: {p}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Prism cleanup", $"Deleted {deleted} unreferenced material/texture assets.", "OK");
    }
}
#endif
