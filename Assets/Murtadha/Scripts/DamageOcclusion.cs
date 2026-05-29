using UnityEngine;

/// <summary>
/// Shared occlusion gate for melee / ranged damage. Linecasts between attacker and victim
/// on solid-world layers only (Environment, walls, doors, etc.). Character layers are not
/// part of the mask so player/enemy/weapon/hittable volumes never register as “walls”.
/// </summary>
public static class DamageOcclusion
{
    private static int _solidMask;
    private static bool _solidMaskResolved;

    /// <summary>
    /// Layers that can block line-of-sight. Does not include Player, Hittable, Character,
    /// or Weapon — those must never occlude in the physics matrix for this check.
    /// </summary>
    private static int ResolveSolidOcclusionMask()
    {
        if (_solidMaskResolved)
            return _solidMask;

        int mask = 0;
        void Add(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                mask |= 1 << layer;
        }

        Add("Environment");
        Add("Building");
        Add("StaticObstacle");
        Add("Wall");
        Add("Door");
        Add("Map");
        Add("LevelContent");

        // Projects that only tag world geometry as Default still need occlusion.
        if (mask == 0)
            Add("Default");

        _solidMask = mask;
        _solidMaskResolved = true;
        return _solidMask;
    }

    public static bool IsBlocked(GameObject attackerRoot, GameObject victim)
    {
        if (attackerRoot == null || victim == null) return false;
        if (attackerRoot == victim) return false;

        float horizontalDist = Vector3.Distance(
            new Vector3(attackerRoot.transform.position.x, 0f, attackerRoot.transform.position.z),
            new Vector3(victim.transform.position.x, 0f, victim.transform.position.z));
        if (horizontalDist < 3f)
            return false;

        int mask = ResolveSolidOcclusionMask();
        if (mask == 0) return false;

        Vector3 from = attackerRoot.transform.position + Vector3.up * 1.6f;
        Vector3 to = victim.transform.position + Vector3.up * 1.3f;

        return EnvironmentSegmentBlocks(from, to, attackerRoot.transform, victim.transform, mask);
    }

    /// <summary>Line-of-sight from a world point (e.g. weapon tip) to the victim.</summary>
    public static bool IsBlockedFromPoint(GameObject attackerRoot, GameObject victim, Vector3 attackOriginWorld)
    {
        if (attackerRoot == null || victim == null) return false;
        if (attackerRoot == victim) return false;

        float horizontalDist = Vector3.Distance(
            new Vector3(attackOriginWorld.x, 0f, attackOriginWorld.z),
            new Vector3(victim.transform.position.x, 0f, victim.transform.position.z));
        if (horizontalDist < 3f)
            return false;

        int mask = ResolveSolidOcclusionMask();
        if (mask == 0) return false;

        Vector3 to = victim.transform.position + Vector3.up * 1.3f;

        return EnvironmentSegmentBlocks(attackOriginWorld, to, attackerRoot.transform, victim.transform, mask);
    }

    public static bool IsSegmentBlocked(GameObject attackerRoot, GameObject victim, Vector3 from, Vector3 to)
    {
        if (attackerRoot != null && victim != null)
        {
            float horizontalDist = Vector3.Distance(
                new Vector3(attackerRoot.transform.position.x, 0f, attackerRoot.transform.position.z),
                new Vector3(victim.transform.position.x, 0f, victim.transform.position.z));
            if (horizontalDist < 3f)
                return false;
        }

        int mask = ResolveSolidOcclusionMask();
        if (mask == 0) return false;

        Transform attacker = attackerRoot != null ? attackerRoot.transform : null;
        Transform target = victim != null ? victim.transform : null;
        return EnvironmentSegmentBlocks(from, to, attacker, target, mask);
    }

    private static bool EnvironmentSegmentBlocks(
        Vector3 from, Vector3 to,
        Transform attackerRoot, Transform victimRoot,
        int envMask)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.02f) return false;

        if (!Physics.Linecast(from, to, out RaycastHit hit, envMask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.distance >= dist - 0.08f)
            return false;

        Transform hitT = hit.collider.transform;
        if (attackerRoot != null && hitT.IsChildOf(attackerRoot)) return false;
        if (victimRoot != null && hitT.IsChildOf(victimRoot)) return false;
        return true;
    }
}
