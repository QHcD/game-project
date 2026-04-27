using UnityEngine;

/// <summary>
/// Shared occlusion gate for melee / ranged damage. If a static prop on the
/// Environment layer (closed doors, walls, crates that block sightlines) sits
/// between the attacker and the victim, the damage is suppressed at the
/// receiving end. Both PlayerHealth.ReceiveDamage and
/// EnemyController.ReceiveDamage call into this before applying any hit.
/// </summary>
public static class DamageOcclusion
{
    private const string EnvironmentLayerName = "Environment";

    private static int _envMask = -1;
    private static bool _envMaskResolved;

    public static bool IsBlocked(GameObject attackerRoot, GameObject victim)
    {
        if (attackerRoot == null || victim == null) return false;
        if (attackerRoot == victim) return false;

        int mask = ResolveEnvironmentMask();
        if (mask == 0) return false;

        Vector3 from = attackerRoot.transform.position + Vector3.up * 1.6f;
        Vector3 to   = victim.transform.position + Vector3.up * 1.3f;

        return EnvironmentSegmentBlocks(from, to, attackerRoot.transform, victim.transform, mask);
    }

    /// <summary>Line-of-sight from a world point (e.g. weapon tip) to the victim.</summary>
    public static bool IsBlockedFromPoint(GameObject attackerRoot, GameObject victim, Vector3 attackOriginWorld)
    {
        if (attackerRoot == null || victim == null) return false;
        if (attackerRoot == victim) return false;

        int mask = ResolveEnvironmentMask();
        if (mask == 0) return false;

        Vector3 to = victim.transform.position + Vector3.up * 1.3f;
        
        return EnvironmentSegmentBlocks(attackOriginWorld, to, attackerRoot.transform, victim.transform, mask);
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

    private static int ResolveEnvironmentMask()
    {
        if (_envMaskResolved) return _envMask;

        int layer = LayerMask.NameToLayer(EnvironmentLayerName);
        _envMask = layer >= 0 ? (1 << layer) : 0;
        _envMaskResolved = true;
        return _envMask;
    }
}
