using System.Collections.Generic;
using UnityEngine;
public static class AISensing
{
    private static readonly Collider[] ScanBuffer = new Collider[64];
    private static readonly HashSet<int> EvaluatedRoots = new HashSet<int>();

    public static Transform FindClosestHostile(EnemyController self, float radius)
    {
        return FindClosestHostile(self, radius, requireLineOfSight: true);
    }

    public static Transform FindClosestHostile(EnemyController self, float radius, bool requireLineOfSight)
    {
        if (self == null || radius <= 0f)
            return null;

        Transform overlapBest = ScanOverlapSphere(self, radius, requireLineOfSight);
        Transform registryBest = ScanAliveEnemyRegistry(self, radius, requireLineOfSight);
        Transform playerBest = ScanPlayer(self, radius, requireLineOfSight);

        Transform best = null;
        float bestDistSqr = float.MaxValue;

        Consider(overlapBest, self.transform.position, ref best, ref bestDistSqr);
        Consider(registryBest, self.transform.position, ref best, ref bestDistSqr);
        Consider(playerBest, self.transform.position, ref best, ref bestDistSqr);

        return best;
    }

    private static void Consider(Transform candidate, Vector3 origin, ref Transform best, ref float bestDistSqr)
    {
        if (candidate == null)
            return;

        float d2 = (candidate.position - origin).sqrMagnitude;
        if (d2 < bestDistSqr)
        {
            bestDistSqr = d2;
            best = candidate;
        }
    }

    private static Transform ScanOverlapSphere(EnemyController self, float radius, bool requireLineOfSight)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            self.transform.position,
            radius,
            ScanBuffer,
            self.detectionMask,
            QueryTriggerInteraction.Collide);

        Transform best = null;
        float bestDistSqr = float.MaxValue;
        EvaluatedRoots.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = ScanBuffer[i];
            if (hit == null)
                continue;

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive)
                continue;

            GameObject root = dmg.gameObject;
            if (root == self.gameObject)
                continue;

            int id = root.GetInstanceID();
            if (!EvaluatedRoots.Add(id))
                continue;

            Transform t = dmg.transform;
            if (!self.IsHostileFaction(t))
                continue;

            if (requireLineOfSight && !self.HasCombatVisionTo(t))
                continue;

            float d2 = (t.position - self.transform.position).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                best = t;
            }
        }

        return best;
    }

    private static Transform ScanAliveEnemyRegistry(EnemyController self, float radius, bool requireLineOfSight)
    {
        Transform best = null;
        float bestDistSqr = float.MaxValue;
        float radiusSqr = radius * radius;
        IReadOnlyList<EnemyController> enemies = EnemyController.AliveEnemies;
        Vector3 origin = self.transform.position;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController other = enemies[i];
            if (other == null || other == self || !other.IsAlive)
                continue;

            if (!self.IsHostileFaction(other.transform))
                continue;

            if (requireLineOfSight && !self.HasCombatVisionTo(other.transform))
                continue;

            float d2 = (other.transform.position - origin).sqrMagnitude;
            if (d2 <= radiusSqr && d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                best = other.transform;
            }
        }

        return best;
    }

    private static Transform ScanPlayer(EnemyController self, float radius, bool requireLineOfSight)
    {
        PlayerController pc = EnemyController.ResolveScenePlayer();
        if (pc == null)
            return null;

        PlayerHealth health = pc.GetComponent<PlayerHealth>();
        if (health == null)
            return null;

        IDamageable dmg = health.GetComponent<IDamageable>();
        if (dmg == null || !dmg.IsAlive)
            return null;

        float d2 = (health.transform.position - self.transform.position).sqrMagnitude;
        if (d2 > radius * radius)
            return null;

        if (requireLineOfSight && !self.HasCombatVisionTo(health.transform))
            return null;

        return health.transform;
    }

    public static bool ShouldSwitchTarget(EnemyController self, Transform current, Transform candidate, float hysteresisMeters)
    {
        if (candidate == null)
            return false;

        if (current == null || !self.IsHostileAlive(current))
            return true;

        if (candidate == current)
            return false;

        float currentDist = Vector3.Distance(self.transform.position, current.position);
        float candidateDist = Vector3.Distance(self.transform.position, candidate.position);
        return candidateDist + Mathf.Max(0f, hysteresisMeters) < currentDist;
    }
}
