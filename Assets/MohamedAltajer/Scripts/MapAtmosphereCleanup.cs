using System.Collections.Generic;
using UnityEngine;

public static class MapAtmosphereCleanup
{
    private static readonly string[] MapRootNames =
    {
        "IndustrialMap_v3",
        "IndustrialMap",
        "FbxMap",
        "UrbanArenaRoot"
    };

    public static int RemoveFromActiveScene()
    {
        int removed = 0;
        for (int i = 0; i < MapRootNames.Length; i++)
        {
            Transform root = GameObject.Find(MapRootNames[i])?.transform;
            if (root != null)
                removed += RemoveFromHierarchy(root);
        }

        return removed;
    }

    public static int RemoveFromHierarchy(Transform root)
    {
        if (root == null)
            return 0;

        var targets = new List<GameObject>(32);
        CollectTargets(root, targets);
        if (targets.Count == 0)
            return 0;

        targets.Sort((a, b) => GetDepth(b.transform).CompareTo(GetDepth(a.transform)));

        int removed = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            GameObject go = targets[i];
            if (go == null)
                continue;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);

            removed++;
        }

        return removed;
    }

    private static void CollectTargets(Transform root, List<GameObject> targets)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == root)
                continue;

            if (ShouldRemove(t))
                targets.Add(t.gameObject);
        }
    }

    private static bool ShouldRemove(Transform t)
    {
        string n = t.name.ToLowerInvariant();
        if (n == "particles")
            return true;

        if (n.Contains("dust") || n.Contains("smoke") || n.Contains("steam") || n.Contains("spark"))
            return true;

        if (n.Contains("fog") && t.GetComponent<ParticleSystem>() != null)
            return true;

        if (t.GetComponent<ParticleSystem>() == null)
            return false;

        Transform walk = t;
        for (int depth = 0; depth < 6 && walk != null; depth++)
        {
            string parentName = walk.name.ToLowerInvariant();
            if (parentName == "particles" || parentName.Contains("dust") || parentName.Contains("smoke"))
                return true;
            walk = walk.parent;
        }

        return false;
    }

    private static int GetDepth(Transform t)
    {
        int depth = 0;
        while (t.parent != null)
        {
            depth++;
            t = t.parent;
        }

        return depth;
    }
}
