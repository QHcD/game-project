#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves project assets into the five credited team folders using
/// <see cref="AssetDatabase.MoveAsset"/> so Unity keeps .meta GUIDs and
/// scene/prefab references stay intact.
///
/// Menu: <b>PRISM / Team Credits / …</b>
///
/// <b>Important:</b> Commit or back up before running. Third-party roots such as
/// third-party import internals are intentionally kept with their original names.
/// </summary>
public static class TeamCreditsAssetReorganizer
{
    public const string HamedRoot          = "Assets/Hamed";
    public const string MurtadhaRoot       = "Assets/Murtadha";
    public const string MohamedAltajerRoot = "Assets/MohamedAltajer";
    public const string AliAlhawajRoot     = "Assets/AliAlhawaj";
    public const string MohamedAmanRoot    = "Assets/MohamedAman";

    [MenuItem("PRISM/Team Credits/Create Student Folder Roots Only (No File Moves)", false, 2)]
    public static void CreateFolderRootsOnly()
    {
        EnsureFolder(HamedRoot);
        EnsureFolder($"{HamedRoot}/Scripts");
        EnsureFolder($"{HamedRoot}/Prefabs");
        EnsureFolder($"{HamedRoot}/Materials");

        EnsureFolder(MurtadhaRoot);
        EnsureFolder($"{MurtadhaRoot}/Scripts");
        EnsureFolder($"{MurtadhaRoot}/Prefabs");
        EnsureFolder($"{MurtadhaRoot}/Materials");

        EnsureFolder(MohamedAltajerRoot);
        EnsureFolder($"{MohamedAltajerRoot}/Scripts");
        EnsureFolder($"{MohamedAltajerRoot}/Prefabs");
        EnsureFolder($"{MohamedAltajerRoot}/Materials");

        EnsureFolder(AliAlhawajRoot);
        EnsureFolder($"{AliAlhawajRoot}/Scripts");
        EnsureFolder($"{AliAlhawajRoot}/Prefabs");
        EnsureFolder($"{AliAlhawajRoot}/Materials");

        EnsureFolder(MohamedAmanRoot);
        EnsureFolder($"{MohamedAmanRoot}/Scripts");
        EnsureFolder($"{MohamedAmanRoot}/Prefabs");
        EnsureFolder($"{MohamedAmanRoot}/Materials");

        EnsureFolder("Assets/_Shared");
        EnsureFolder("Assets/_Shared/Fonts");
        EnsureFolder("Assets/_Shared/Maps");
        EnsureFolder("Assets/_Shared/Imports");
        EnsureFolder("Assets/_Shared/Project_Core");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TeamReorganize] Student folder roots created under Assets/ (organizational only).");
    }

    [MenuItem("PRISM/Team Credits/Preview Reorganization (Console Only)", false, 1)]
    public static void Preview()
    {
        List<KeyValuePair<string, string>> moves = BuildMoves();
        for (int i = 0; i < moves.Count; i++)
            Debug.Log($"[TeamReorganize] {moves[i].Key}  -->  {moves[i].Value}");
        Debug.Log($"[TeamReorganize] Total operations: {moves.Count}");
    }

    [MenuItem("PRISM/Team Credits/Reorganize Assets Into Member Folders", false, 0)]
    public static void Execute()
    {
        if (!EditorUtility.DisplayDialog(
                "PRISM-7 — Team folder reorganization",
                "This moves assets into Hamed, Murtadha, MohamedAltajer, AliAlhawaj, MohamedAman, and _Shared using Unity's AssetDatabase (GUIDs preserved).\n\n" +
                "Make a Git commit or full backup first.\n\n" +
                "Continue?",
                "Move assets",
                "Cancel"))
            return;

        List<KeyValuePair<string, string>> moves = BuildMoves();
        AssetDatabase.StartAssetEditing();
        try
        {
            int ok = 0, skipped = 0, failed = 0;
            foreach (var pair in moves)
            {
                string from = pair.Key.Replace('\\', '/');
                string to   = pair.Value.Replace('\\', '/');

                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(from)))
                {
                    Debug.LogWarning($"[TeamReorganize] Missing (skipped): {from}");
                    skipped++;
                    continue;
                }

                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(to)))
                {
                    Debug.LogWarning($"[TeamReorganize] Destination already exists (skipped): {to}");
                    skipped++;
                    continue;
                }

                EnsureParentFolderExists(to);
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err))
                {
                    Debug.LogError($"[TeamReorganize] {err}  ({from} -> {to})");
                    failed++;
                }
                else
                {
                    ok++;
                }
            }

            Debug.Log($"[TeamReorganize] Finished. Moved: {ok}, skipped: {skipped}, failed: {failed}.");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Team reorganization",
            "Done. Run Play Mode, open MainMenu + GameScene, and verify.\n\n" +
            "GameManager / SessionManager use type names only — no script path updates required.",
            "OK");
    }

    private static void EnsureParentFolderExists(string assetPath)
    {
        string parent = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(parent)) return;
        EnsureFolder(parent.Replace('\\', '/'));
    }

    private static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath)) return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string name   = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(name)) return;

        if (!string.IsNullOrEmpty(parent) && parent != "Assets")
            EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent ?? "Assets", name);
    }

    /// <summary>
    /// Asset moves were applied directly to this project hierarchy. This tool now
    /// keeps the menu entry available without re-moving already organized assets.
    /// </summary>
    private static List<KeyValuePair<string, string>> BuildMoves()
    {
        return new List<KeyValuePair<string, string>>();
    }
}
#endif
