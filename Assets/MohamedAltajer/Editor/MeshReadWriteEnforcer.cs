// MeshReadWriteEnforcer.cs — Editor-only
// Automatically enables Read/Write on any model imported into Resources/Maps/
// so RuntimeNavMeshBuilder can always read the mesh data at runtime.
//
// Also provides a manual menu item: PRISM > Fix All Map Mesh Read/Write
// to bulk-fix every FBX/OBJ under Resources/Maps in one click.

using UnityEditor;
using UnityEngine;

public class MeshReadWriteEnforcer : AssetPostprocessor
{
    // ── Auto-fix on import ──────────────────────────────────────────────────
    // Runs every time a model is imported or re-imported.
    private void OnPreprocessModel()
    {
        // Only auto-fix assets inside the Maps resource folder.
        if (!assetPath.Contains("Resources/Maps")) return;

        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            Debug.Log($"[MeshReadWriteEnforcer] Enabled Read/Write on: {assetPath}");
        }
    }

    // ── Manual bulk-fix ─────────────────────────────────────────────────────
    [MenuItem("PRISM/Fix All Map Mesh Read-Write")]
    private static void FixAllMapMeshes()
    {
        string[] folders = { "Assets/MohamedAltajer/Prefabs/Environment/Resources/Maps" };
        string[] guids = AssetDatabase.FindAssets("t:Model", folders);

        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                fixedCount++;
                Debug.Log($"[MeshReadWriteEnforcer] Fixed: {path}");
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("PRISM",
                $"Enabled Read/Write on {fixedCount} map model(s).\n" +
                "NavMesh runtime baking will now work correctly.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("PRISM",
                "All map models already have Read/Write enabled.",
                "OK");
        }
    }
}
