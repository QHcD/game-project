using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Industrial Set v3.0 package ships its main demo as a SCENE
// (Map_v2.unity inside the import folder, despite the v2 naming) rather than a
// single root prefab. This tool loads that scene additively, moves every root
// GameObject into the currently-open gameplay scene under an
// "IndustrialMap_v3" parent, then unloads the source scene — giving you a
// single tidy hierarchy node you can position/disable like a prefab.
//
// Menu:
//   PRISM-7 ▸ Map ▸ Import Industrial v3 Demo (Map_v2 scene) Into Active Scene
//   PRISM-7 ▸ Map ▸ Import Industrial v3 Demo (Map_v1 scene) Into Active Scene
//
// Safe to run multiple times — each run creates a fresh, numbered parent.
public static class IndustrialV3SceneImporter
{
    private const string V3MainScenePath = "Assets/_Shared/Imports/RPG_FPS_game_assets_industrial/Map_v2.unity";
    private const string V3SmallScenePath = "Assets/_Shared/Imports/RPG_FPS_game_assets_industrial/Map_v1.unity";

    [MenuItem("PRISM-7/Map/Import Industrial v3 Demo (Map_v2 scene) Into Active Scene")]
    private static void ImportMain() => ImportSceneAsGroup(V3MainScenePath, "IndustrialMap_v3");

    [MenuItem("PRISM-7/Map/Import Industrial v3 Demo (Map_v1 scene) Into Active Scene")]
    private static void ImportSmall() => ImportSceneAsGroup(V3SmallScenePath, "IndustrialMap_v3_small");

    private static void ImportSceneAsGroup(string sourceScenePath, string parentName)
    {
        if (!System.IO.File.Exists(sourceScenePath))
        {
            EditorUtility.DisplayDialog(
                "v3 Importer",
                $"Source scene not found:\n{sourceScenePath}\n\nYou must import Industrial Set v3 manually from Unity Package Manager first.",
                "OK");
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            EditorUtility.DisplayDialog("v3 Importer", "No active scene. Open your gameplay scene first.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Load source scene additively.
        Scene src = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Additive);
        if (!src.IsValid())
        {
            Debug.LogError($"[v3 Importer] Failed to open {sourceScenePath}");
            return;
        }

        // Create the parent in the active scene.
        var parent = new GameObject(UniqueName(activeScene, parentName));
        Undo.RegisterCreatedObjectUndo(parent, "Import v3 Map");
        SceneManager.MoveGameObjectToScene(parent, activeScene);

        // Move every root of the source scene under the parent, into the active scene.
        // We skip Lights/Camera/AudioListener clones so we don't fight your existing rig.
        var roots = src.GetRootGameObjects();
        int moved = 0, skipped = 0;
        var skippedNames = new List<string>();
        foreach (var root in roots)
        {
            if (IsRigDuplicate(root))
            {
                skipped++;
                skippedNames.Add(root.name);
                continue;
            }
            SceneManager.MoveGameObjectToScene(root, activeScene);
            Undo.SetTransformParent(root.transform, parent.transform, "Reparent v3 root");
            moved++;
        }

        // Close the source scene without saving (we lifted its content).
        EditorSceneManager.CloseScene(src, removeScene: true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        Selection.activeGameObject = parent;
        EditorGUIUtility.PingObject(parent);

        Debug.Log($"[v3 Importer] Moved {moved} root(s) under '{parent.name}'. Skipped rig duplicates: {skipped} [{string.Join(", ", skippedNames)}]");
        Debug.Log("[v3 Importer] Next: position the parent if needed, then run PRISM-7 ▸ Doors / Map utilities.");
    }

    private static bool IsRigDuplicate(GameObject go)
    {
        if (go.GetComponent<Camera>() != null) return true;
        if (go.GetComponent<AudioListener>() != null) return true;
        // Don't strip lights — the demo map's lighting is part of what you're importing.
        return false;
    }

    private static string UniqueName(Scene scene, string baseName)
    {
        var existing = new HashSet<string>();
        foreach (var r in scene.GetRootGameObjects()) existing.Add(r.name);
        if (!existing.Contains(baseName)) return baseName;
        for (int i = 2; i < 100; i++)
        {
            var n = $"{baseName}_{i}";
            if (!existing.Contains(n)) return n;
        }
        return baseName + "_X";
    }
}
