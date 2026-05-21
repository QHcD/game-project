using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Quick triage tool for door meshes in v3 (or any) map. Three actions:
//
//   PRISM-7 ▸ Doors ▸ List Door-Like Meshes
//     Logs every GameObject whose name matches *Door*/*Gate*. Click a log line
//     to ping it in the hierarchy.
//
//   PRISM-7 ▸ Doors ▸ Attach DoorController To Selection
//     Adds the existing PRISM-7 DoorController (with openOnStart = false,
//     interactiveToggle = true) to each selected GameObject so [E] opens it.
//
//   PRISM-7 ▸ Doors ▸ Disable Colliders On Selection
//     Disables every Collider under the selected GameObjects. Use for static
//     door meshes that just need to stop blocking — keeps the visual, drops
//     the wall.
public static class DoorFinder
{
    private static readonly string[] NameHints = { "door", "gate" };

    [MenuItem("PRISM-7/Doors/List Door-Like Meshes")]
    private static void ListDoors()
    {
        int count = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                string n = t.name.ToLowerInvariant();
                foreach (var hint in NameHints)
                {
                    if (n.Contains(hint))
                    {
                        Debug.Log($"[DoorFinder] {GetScenePath(t)}", t.gameObject);
                        count++;
                        break;
                    }
                }
            }
        }
        Debug.Log($"[DoorFinder] {count} door-like mesh(es) found.");
    }

    [MenuItem("PRISM-7/Doors/Attach DoorController To Selection")]
    private static void AttachController()
    {
        var doorType = System.Type.GetType("DoorController, Assembly-CSharp");
        if (doorType == null)
        {
            EditorUtility.DisplayDialog("Door Finder", "DoorController script not found in Assembly-CSharp.", "OK");
            return;
        }

        int added = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent(doorType) != null) continue;
            Undo.AddComponent(go, doorType);
            added++;
        }

        // Default the new components to interactive-only (no openOnStart) via SerializedObject.
        foreach (var go in Selection.gameObjects)
        {
            var c = go.GetComponent(doorType);
            if (c == null) continue;
            var so = new SerializedObject(c);
            var openOnStart = so.FindProperty("openOnStart");
            if (openOnStart != null) openOnStart.boolValue = false;
            var interactive = so.FindProperty("interactiveToggle");
            if (interactive != null) interactive.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[DoorFinder] Added DoorController to {added} object(s).");
    }

    [MenuItem("PRISM-7/Doors/Disable Colliders On Selection")]
    private static void DisableColliders()
    {
        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (col.isTrigger) continue;
                Undo.RecordObject(col, "Disable Door Collider");
                col.enabled = false;
                n++;
            }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[DoorFinder] Disabled {n} collider(s).");
    }

    private static string GetScenePath(Transform t)
    {
        var sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }
}
