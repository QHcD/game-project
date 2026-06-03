using UnityEditor;
using UnityEngine;

// Bakes an invisible tilted BoxCollider over a stair / step mesh so the player
// walks up smoothly instead of bumping each step. Select one or more stair
// GameObjects in the hierarchy, then PRISM-7 ▸ Map ▸ Bake Ramp Collider Over Selection.
//
// The created child is named COLLIDER_Ramp_Stairs_NN with no renderer — just a
// BoxCollider sized to the visual bounds and tilted to match the stair's rise/run.
public static class RampColliderBaker
{
    private const string MenuPath = "PRISM-7/Map/Bake Ramp Collider Over Selection";

    [MenuItem(MenuPath)]
    private static void BakeOverSelection()
    {
        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog("Ramp Baker", "Select one or more stair GameObjects first.", "OK");
            return;
        }

        int baked = 0;
        foreach (var go in targets)
        {
            if (BakeRamp(go)) baked++;
        }
        Debug.Log($"[RampColliderBaker] Baked {baked} ramp collider(s).");
    }

    private static bool BakeRamp(GameObject stairs)
    {
        var renderers = stairs.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[RampColliderBaker] {stairs.name} has no renderers, skipping.", stairs);
            return false;
        }

        // World-space bounds covering the whole stair mesh group.
        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

        // Make a child holder so the ramp follows the stairs if moved.
        var holder = new GameObject(NextChildName(stairs, "COLLIDER_Ramp_Stairs"));
        Undo.RegisterCreatedObjectUndo(holder, "Bake Ramp Collider");
        holder.transform.SetParent(stairs.transform, worldPositionStays: true);
        holder.transform.position = world.center;
        holder.transform.rotation = Quaternion.identity;

        // Place the box flat across the footprint, then tilt it so the top face
        // becomes a ramp from the low edge to the high edge of the stairs.
        // We assume stairs rise on the local +Z direction of their bounds — works
        // for the vast majority of straight stair meshes. If a particular stair
        // looks wrong, rotate the COLLIDER_Ramp_Stairs_NN child 180° on Y.
        var box = holder.AddComponent<BoxCollider>();
        Vector3 size = stairs.transform.InverseTransformVector(world.size);
        size = new Vector3(Mathf.Abs(size.x), 0.2f, Mathf.Abs(size.z));
        box.size = size;
        box.center = Vector3.zero;

        float rise = world.size.y;
        float run  = world.size.z;
        float tiltDeg = (run > 0.01f) ? Mathf.Atan2(rise, run) * Mathf.Rad2Deg : 0f;
        holder.transform.localRotation = Quaternion.Euler(-tiltDeg, 0f, 0f);

        // Drop holder so its low edge meets the floor at world.min.y.
        Vector3 worldPos = holder.transform.position;
        worldPos.y = world.min.y + 0.05f;
        holder.transform.position = worldPos;

        return true;
    }

    private static string NextChildName(GameObject parent, string prefix)
    {
        for (int i = 1; i < 100; i++)
        {
            string candidate = $"{prefix}_{i:00}";
            if (parent.transform.Find(candidate) == null) return candidate;
        }
        return $"{prefix}_XX";
    }
}
