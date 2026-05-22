using System.Collections.Generic;
using UnityEngine;

public static class EnvironmentGroundAnchor
{
    private const float MinSnapGap = 0.08f;
    private const float MaxRayDistance = 1200f;
    private const int MaxPasses = 4;
    private const int MaxSnapsPerPass = 2048;

    public static void Install(Transform mapRoot, bool debugLog = false)
    {
        if (mapRoot == null) return;

        Physics.SyncTransforms();

        int groundMask = BuildGroundMask();
        List<Renderer> groundRenderers = CollectGroundRenderers(mapRoot);
        Renderer[] allRenderers = mapRoot.GetComponentsInChildren<Renderer>(true);

        int totalSnapped = 0;
        for (int pass = 0; pass < MaxPasses; pass++)
        {
            int passSnapped = RunSnapPass(mapRoot, allRenderers, groundRenderers, groundMask);
            totalSnapped += passSnapped;
            Physics.SyncTransforms();
            if (passSnapped == 0)
                break;
        }

        if (debugLog)
            Debug.Log($"[EnvironmentGroundAnchor] Passes complete on '{mapRoot.name}': snapped={totalSnapped}, groundTiles={groundRenderers.Count}");
    }

    public static bool TrySampleGroundAt(Bounds bounds, Transform ignoreSubtree, out float groundY)
    {
        return TryResolveGroundY(bounds, ignoreSubtree, BuildGroundMask(), null, out groundY);
    }

    public static int BuildGroundMask()
    {
        int mask = 0;
        TryAddLayer(ref mask, "Ground");
        TryAddLayer(ref mask, "Terrain");
        TryAddLayer(ref mask, "Environment");
        TryAddLayer(ref mask, "Map");
        TryAddLayer(ref mask, "Default");
        if (mask == 0)
            mask = Physics.DefaultRaycastLayers;
        return mask;
    }

    private static int RunSnapPass(Transform mapRoot, Renderer[] allRenderers, List<Renderer> groundRenderers, int groundMask)
    {
        var shifts = new Dictionary<Transform, float>(128);

        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer rend = allRenderers[i];
            if (!IsValidRenderer(rend)) continue;
            if (IsGroundLikeName(rend.gameObject.name)) continue;

            Bounds bounds = rend.bounds;
            if (bounds.size.sqrMagnitude < 0.01f) continue;
            if (IsUnderPreservedPropHierarchy(rend.transform, mapRoot)) continue;
            if (!ShouldAnchorRenderer(bounds, rend.gameObject.name)) continue;

            Transform snapTarget = ResolveSnapTransform(rend.transform, mapRoot);
            if (snapTarget == null) continue;

            if (!TryResolveGroundY(bounds, snapTarget, groundMask, groundRenderers, out float groundY))
                continue;

            float gap = bounds.min.y - groundY;
            if (gap < MinSnapGap) continue;

            if (!shifts.TryGetValue(snapTarget, out float existing) || gap > existing)
                shifts[snapTarget] = gap;
        }

        int snapped = 0;
        foreach (KeyValuePair<Transform, float> entry in shifts)
        {
            if (entry.Key == null || entry.Value < MinSnapGap) continue;
            if (snapped >= MaxSnapsPerPass) break;

            entry.Key.position += new Vector3(0f, -entry.Value, 0f);
            MarkLocked(entry.Key);
            snapped++;
        }

        return snapped;
    }

    private static void MarkLocked(Transform t)
    {
        if (t == null) return;
        EnvironmentAnchorLock existing = t.GetComponent<EnvironmentAnchorLock>();
        if (existing == null)
            t.gameObject.AddComponent<EnvironmentAnchorLock>();
    }

    private static List<Renderer> CollectGroundRenderers(Transform mapRoot)
    {
        var list = new List<Renderer>(256);
        Renderer[] all = mapRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Renderer rend = all[i];
            if (!IsValidRenderer(rend)) continue;
            if (!IsGroundLikeName(rend.gameObject.name)) continue;
            list.Add(rend);
        }

        return list;
    }

    private static bool TryResolveGroundY(Bounds bounds, Transform ignoreSubtree, int groundMask,
        List<Renderer> groundRenderers, out float groundY)
    {
        if (groundRenderers != null && groundRenderers.Count > 0
            && TryGroundYFromRendererTiles(bounds, groundRenderers, out groundY))
            return true;

        return TryGroundYFromRaycast(bounds, ignoreSubtree, groundMask, out groundY);
    }

    private static bool TryGroundYFromRendererTiles(Bounds footprint, List<Renderer> groundRenderers, out float groundY)
    {
        groundY = 0f;
        bool found = false;
        float bestSurface = float.MinValue;

        for (int i = 0; i < groundRenderers.Count; i++)
        {
            Renderer rend = groundRenderers[i];
            if (rend == null) continue;

            Bounds tile = rend.bounds;
            if (!OverlapsXZ(footprint, tile)) continue;

            float surface = tile.max.y;
            if (surface > bestSurface)
            {
                bestSurface = surface;
                found = true;
            }
        }

        if (!found) return false;
        groundY = bestSurface;
        return true;
    }

    private static bool TryGroundYFromRaycast(Bounds bounds, Transform ignoreSubtree, int groundMask, out float groundY)
    {
        groundY = 0f;
        float castHeight = Mathf.Max(8f, bounds.size.y + 12f);
        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + castHeight, bounds.center.z);

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, MaxRayDistance, groundMask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (!IsValidGroundHit(hits[i], ignoreSubtree)) continue;
                groundY = hits[i].point.y;
                return true;
            }
        }

        origin.y = 600f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit fallback, MaxRayDistance, groundMask, QueryTriggerInteraction.Ignore)
            && IsValidGroundHit(fallback, ignoreSubtree))
        {
            groundY = fallback.point.y;
            return true;
        }

        return false;
    }

    private static bool IsValidGroundHit(RaycastHit hit, Transform ignoreSubtree)
    {
        if (hit.collider == null) return false;
        if (ignoreSubtree != null && hit.transform.IsChildOf(ignoreSubtree)) return false;

        if (IsGroundLikeName(hit.collider.gameObject.name))
            return true;

        Transform walk = hit.transform;
        for (int i = 0; i < 6 && walk != null; i++)
        {
            if (IsGroundLikeName(walk.gameObject.name))
                return true;
            walk = walk.parent;
        }

        return false;
    }

    private static bool IsUnderPreservedPropHierarchy(Transform t, Transform mapRoot)
    {
        while (t != null && t != mapRoot)
        {
            if (MapAttachedPropsPreserver.IsPreservedPropName(t.name))
                return true;
            t = t.parent;
        }

        return false;
    }

    private static bool ShouldAnchorRenderer(Bounds bounds, string objectName)
    {
        if (IsGroundLikeName(objectName)) return false;
        if (IsExcludedName(objectName)) return false;
        if (MapAttachedPropsPreserver.IsPreservedPropName(objectName)) return false;
        if (IsStructureName(objectName)) return true;
        return IsLargeFloatingVolume(bounds);
    }

    private static bool OverlapsXZ(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.z <= b.max.z && a.max.z >= b.min.z;
    }

    private static void TryAddLayer(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            mask |= 1 << layer;
    }

    private static bool IsValidRenderer(Renderer rend)
    {
        if (rend == null || !rend.enabled) return false;
        if (rend is ParticleSystemRenderer) return false;
        return rend.bounds.size.sqrMagnitude > 0.001f;
    }

    private static Transform ResolveSnapTransform(Transform leaf, Transform mapRoot)
    {
        if (leaf == null || mapRoot == null) return null;

        Transform current = leaf;
        Transform best = leaf;
        int depth = 0;

        while (current != null && current != mapRoot && depth < 8)
        {
            if (current.GetComponent<Renderer>() != null)
                best = current;

            if (IsStructureName(current.name))
                best = current;

            current = current.parent;
            depth++;
        }

        return best;
    }

    private static bool IsStructureName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        if (IsGroundLikeName(n) || IsExcludedName(n)) return false;

        return n.Contains("building") || n.Contains("hangar") || n.Contains("warehouse")
            || n.Contains("silo") || n.Contains("tank") || n.Contains("storage")
            || n.Contains("container") || n.Contains("cargo") || n.Contains("office")
            || n.Contains("structure") || n.Contains("hall") || n.Contains("shed")
            || n.Contains("tower") || n.Contains("chimney") || n.Contains("factory")
            || n.Contains("plant") || n.Contains("dome")
            || n.Contains("refinery") || n.Contains("boiler") || n.Contains("garage")
            || n.Contains("depot") || n.Contains("industrial");
    }

    private static bool IsGroundLikeName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        n = n.ToLowerInvariant();
        if (n.Contains("road") || n.Contains("asphalt") || n.Contains("ground")
            || n.Contains("floor") || n.Contains("pavement") || n.Contains("street")
            || n.Contains("sidewalk") || n.Contains("walkway") || n.Contains("path")
            || n.Contains("terrain") || n.Contains("tarmac") || n.Contains("lane")
            || n.Contains("way") || n.Contains("arena") || n.Contains("platform"))
            return true;

        return n.Contains("seamless_asphalt") || n.Contains("asphalt_v")
            || n.Contains("road_v") || n.Contains("pavement_v");
    }

    private static bool IsLargeFloatingVolume(Bounds bounds)
    {
        if (bounds.min.y < 0.2f) return false;
        if (bounds.size.y > 1.8f) return true;
        return bounds.size.x > 3.5f && bounds.size.z > 3.5f;
    }

    private static bool IsExcludedName(string n)
    {
        string lower = n.ToLowerInvariant();
        return lower.Contains("skybox") || lower.Contains("reflection") || lower.Contains("probe")
            || lower.Contains("invisible") || lower.Contains("volume") || lower.Contains("trigger")
            || lower.Contains("fence") || lower.Contains("wire") || lower.Contains("lamp")
            || lower.Contains("light") || lower.Contains("sign") || lower.Contains("car")
            || lower.Contains("vehicle") || lower.Contains("pipes_set") || lower.Contains("pipe_set")
            || lower.Contains("h_set") || lower.Contains("v_set") || lower.Contains("vent_")
            || lower.Contains("ladder") || lower.Contains("cable");
    }
}

public sealed class EnvironmentAnchorLock : MonoBehaviour
{
    private Vector3 _lockedWorldPosition;
    private bool _armed;

    private void Awake()
    {
        _lockedWorldPosition = transform.position;
        _armed = true;
    }

    private void LateUpdate()
    {
        if (!_armed) return;
        Vector3 p = transform.position;
        if (p.y > _lockedWorldPosition.y + 0.02f)
            transform.position = new Vector3(p.x, _lockedWorldPosition.y, p.z);
        else
            _lockedWorldPosition = p;
    }
}
