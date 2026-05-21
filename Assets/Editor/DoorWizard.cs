using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Manual conversion wizard for v3 hangar doors. Necessary because the v3 asset
// pack ships hangar buildings as single-mesh FBX exports — no pre-separated,
// pre-named door GameObjects — so we cannot programmatically pick a door out
// of a wall. The designer selects the candidate mesh, picks a menu, the wizard
// re-pivots it correctly and attaches DoorController or SlidingDoor.
//
// Workflow (per door):
//   1. In the Scene view, click the mesh that looks like a door.
//   2. PRISM-7 ▸ Doors ▸ Scan IndustrialMap_v3 Door Candidates  — to triage.
//   3. With one or more meshes selected, pick the matching converter:
//        ▸ Convert Selection To Hinged Door (Left Edge)
//        ▸ Convert Selection To Hinged Door (Right Edge)
//        ▸ Convert Selection To Shutter (Slide Up)
//        ▸ Convert Selection To Sliding Door (Slide Right)
//        ▸ Convert Selection To Sliding Door (Slide Left)
//   4. Test [E] in Play Mode. If the swing/slide goes the wrong way, undo
//      and pick a different variant from the same menu.
//
// All conversions wrap the mesh in a pivot holder so the door rotates/slides
// around the correct edge without modifying the original mesh.
public static class DoorWizard
{
    private const string V3RootName = "IndustrialMap_v3";

    // ── Scan ────────────────────────────────────────────────────────────────

    [MenuItem("PRISM-7/Doors/Scan IndustrialMap_v3 Door Candidates")]
    private static void Scan()
    {
        var root = FindRoot();
        if (root == null) return;

        int gates = 0, hangarChildren = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            string n = t.name.ToLowerInvariant();
            var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
            string srcPath = src != null ? AssetDatabase.GetAssetPath(src) : "";

            if (n.Contains("gate") || n.Contains("door") || n.Contains("shutter") || srcPath.Contains("Gate"))
            {
                Debug.Log($"[DoorWizard] CANDIDATE (named): {GetScenePath(t)}  ←  {srcPath}", t.gameObject);
                gates++;
            }
            else if (srcPath.Contains("Hangar") && n.StartsWith("object"))
            {
                Debug.Log($"[DoorWizard] possible hangar door-panel: {GetScenePath(t)}  size={GetBoundsSize(t.gameObject):F1}", t.gameObject);
                hangarChildren++;
            }
        }
        Debug.Log($"[DoorWizard] Scan complete — {gates} named door/gate(s), {hangarChildren} unnamed hangar sub-mesh(es). Click a log line to ping the object.");
    }

    // ── Hinged ──────────────────────────────────────────────────────────────

    [MenuItem("PRISM-7/Doors/Convert Selection To Hinged Door (Left Edge)")]
    private static void HingeLeft() => HingeSelection(useRightEdge: false);

    [MenuItem("PRISM-7/Doors/Convert Selection To Hinged Door (Right Edge)")]
    private static void HingeRight() => HingeSelection(useRightEdge: true);

    private static void HingeSelection(bool useRightEdge)
    {
        var doorType = System.Type.GetType("DoorController, Assembly-CSharp");
        if (doorType == null) { ErrorMissing("DoorController"); return; }

        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (!ValidateForConversion(go, out var rend)) continue;
            var pivot = WrapWithPivot(go, rend.bounds, atRightEdge: useRightEdge, atTop: false);

            // Attach DoorController to the pivot. Configure for [E] interaction only.
            var comp = Undo.AddComponent(pivot, doorType);
            var so = new SerializedObject(comp);
            so.FindProperty("openOnStart").boolValue = false;
            so.FindProperty("interactiveToggle").boolValue = true;
            so.FindProperty("openAngle").floatValue = useRightEdge ? -90f : 90f;
            so.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[DoorWizard] Converted {n} object(s) to hinged door ({(useRightEdge ? "right" : "left")} edge).");
    }

    // ── Sliding / Shutter ───────────────────────────────────────────────────

    [MenuItem("PRISM-7/Doors/Convert Selection To Shutter (Slide Up)")]
    private static void SlideUp() => SlideSelection(Vector3.up, atTop: false);

    [MenuItem("PRISM-7/Doors/Convert Selection To Sliding Door (Slide Right)")]
    private static void SlideRight() => SlideSelection(Vector3.right, atTop: false);

    [MenuItem("PRISM-7/Doors/Convert Selection To Sliding Door (Slide Left)")]
    private static void SlideLeft() => SlideSelection(Vector3.left, atTop: false);

    private static void SlideSelection(Vector3 axis, bool atTop)
    {
        var slidingType = System.Type.GetType("SlidingDoor, Assembly-CSharp");
        if (slidingType == null) { ErrorMissing("SlidingDoor"); return; }

        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (!ValidateForConversion(go, out var rend)) continue;
            // For a sliding door we do not need a hinge pivot — wrap centered.
            var pivot = WrapWithPivot(go, rend.bounds, atRightEdge: false, atTop: atTop, centered: true);

            var comp = Undo.AddComponent(pivot, slidingType);
            var so = new SerializedObject(comp);
            so.FindProperty("slideAxis").vector3Value = axis;
            // Slide distance defaults to the door's size along that axis.
            Vector3 worldSize = rend.bounds.size;
            float dist = Mathf.Abs(Vector3.Dot(worldSize, axis.normalized));
            so.FindProperty("slideDistance").floatValue = Mathf.Max(1f, dist);
            so.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[DoorWizard] Converted {n} object(s) to sliding door (axis={axis}).");
    }

    // ── Decorative ──────────────────────────────────────────────────────────

    [MenuItem("PRISM-7/Doors/Disable Colliders On Selection (Decorative)")]
    private static void DisableColliders()
    {
        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (c.isTrigger) continue;
                Undo.RecordObject(c, "Disable Door Collider");
                c.enabled = false;
                n++;
            }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[DoorWizard] Disabled {n} collider(s).");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static GameObject FindRoot()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects())
            if (r.name.StartsWith(V3RootName)) return r;
        Debug.LogWarning($"[DoorWizard] No '{V3RootName}*' root in active scene.");
        return null;
    }

    private static bool ValidateForConversion(GameObject go, out Renderer rend)
    {
        rend = go.GetComponent<Renderer>();
        if (rend == null) rend = go.GetComponentInChildren<Renderer>();
        if (rend == null)
        {
            Debug.LogWarning($"[DoorWizard] {go.name} has no renderer; skipping.", go);
            return false;
        }
        // Skip if already wrapped.
        if (go.transform.parent != null && go.transform.parent.name.StartsWith("DoorPivot_"))
        {
            Debug.LogWarning($"[DoorWizard] {go.name} is already wrapped; skipping.", go);
            return false;
        }
        return true;
    }

    // Wraps a mesh GameObject in a parent pivot positioned at the hinge edge
    // (or centered for sliding doors). The original mesh becomes a child of
    // the pivot with no transformation — meaning DoorController rotating the
    // pivot rotates the mesh around the hinge, not its own center.
    private static GameObject WrapWithPivot(GameObject door, Bounds worldBounds, bool atRightEdge, bool atTop, bool centered = false)
    {
        Transform origParent = door.transform.parent;
        int siblingIdx = door.transform.GetSiblingIndex();

        var pivot = new GameObject($"DoorPivot_{door.name}");
        Undo.RegisterCreatedObjectUndo(pivot, "Create Door Pivot");
        pivot.transform.SetParent(origParent, worldPositionStays: true);
        pivot.transform.SetSiblingIndex(siblingIdx);

        Vector3 pivotWorldPos = centered ? worldBounds.center
            : new Vector3(
                atRightEdge ? worldBounds.max.x : worldBounds.min.x,
                atTop ? worldBounds.max.y : worldBounds.min.y,
                worldBounds.center.z);
        pivot.transform.position = pivotWorldPos;
        pivot.transform.rotation = door.transform.rotation;

        Undo.SetTransformParent(door.transform, pivot.transform, "Reparent door under pivot");
        return pivot;
    }

    private static Vector3 GetBoundsSize(GameObject go)
    {
        var r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.size : Vector3.zero;
    }

    private static string GetScenePath(Transform t)
    {
        var sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }

    private static void ErrorMissing(string typeName)
    {
        EditorUtility.DisplayDialog("Door Wizard",
            $"Could not find {typeName} in Assembly-CSharp. Make sure the script compiles.", "OK");
    }
}
