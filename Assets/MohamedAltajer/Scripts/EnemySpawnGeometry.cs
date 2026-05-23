using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared outdoor spawn validation and runtime geometry checks for enemies.
/// Keeps spawns on walkable ground (not under the map) and away from wall faces.
/// </summary>
public static class EnemySpawnGeometry
{
    private const float MinWallClearance = 0.85f;
    private const float MaxFeetBelowGround = 0.12f;
    private const float MaxFeetAboveGround = 0.35f;
    private const float RequiredHeadroom = 1.95f;

    private static int _spawnPhysicsMask = -1;
    private static int _staticGeometryMask = -1;

    public static int SpawnPhysicsMask
    {
        get
        {
            if (_spawnPhysicsMask != -1)
                return _spawnPhysicsMask;

            int mask = Physics.DefaultRaycastLayers;
            ExcludeCharacterLayers(ref mask);
            _spawnPhysicsMask = mask;
            return _spawnPhysicsMask;
        }
    }

    public static int StaticGeometryMask
    {
        get
        {
            if (_staticGeometryMask != -1)
                return _staticGeometryMask;

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
            Add("Default");

            if (mask == 0)
                mask = Physics.DefaultRaycastLayers;

            ExcludeCharacterLayers(ref mask);
            _staticGeometryMask = mask;
            return _staticGeometryMask;
        }
    }

    private static void ExcludeCharacterLayers(ref int mask)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int characterLayer = LayerMask.NameToLayer("Character");
        int hittableLayer = LayerMask.NameToLayer("Hittable");
        int enemiesLayer = LayerMask.NameToLayer("Enemies");
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);
        if (characterLayer >= 0) mask &= ~(1 << characterLayer);
        if (hittableLayer >= 0) mask &= ~(1 << hittableLayer);
        if (enemiesLayer >= 0) mask &= ~(1 << enemiesLayer);
    }

    /// <summary>
    /// Outdoor spawn: grounded feet, headroom, not inside geometry, wall clearance, optional rooftop guard.
    /// </summary>
    public static bool IsValidOutdoorSpawn(Vector3 candidate, Vector3 playerNavPos, bool rejectRooftops)
    {
        if (rejectRooftops && playerNavPos != default &&
            (candidate.y - playerNavPos.y) > LevelBuilder.MaxSpawnYAbovePlayer)
            return false;

        if (!TryAlignFeetToGround(candidate, out Vector3 feet))
            return false;

        if (!HasSpawnGroundAndClearance(feet, RequiredHeadroom))
            return false;

        if (!HasHorizontalWallClearance(feet))
            return false;

        return true;
    }

    public static bool TryAlignFeetToGround(Vector3 candidate, out Vector3 feet)
    {
        feet = candidate;
        int mask = SpawnPhysicsMask;

        Vector3 probe = candidate + Vector3.up * 0.35f;
        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 2.75f, mask, QueryTriggerInteraction.Ignore))
            return false;

        float deltaY = candidate.y - ground.point.y;
        if (deltaY < -MaxFeetBelowGround || deltaY > MaxFeetAboveGround + 0.5f)
            return false;

        feet = ground.point + Vector3.up * 0.08f;

        if (Physics.Raycast(feet + Vector3.up * 0.12f, Vector3.up, RequiredHeadroom + 0.25f, mask,
                QueryTriggerInteraction.Ignore))
            return false;

        float bodyRadius = 0.45f;
        Vector3 bodyCenter = feet + Vector3.up * (RequiredHeadroom * 0.5f);
        if (Physics.CheckSphere(bodyCenter, bodyRadius, mask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    public static bool HasHorizontalWallClearance(Vector3 feet)
    {
        int mask = StaticGeometryMask;
        Vector3 chest = feet + Vector3.up * 1f;

        for (int i = 0; i < 8; i++)
        {
            float ang = i * (Mathf.PI * 2f / 8f);
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            if (Physics.Raycast(chest, dir, MinWallClearance, mask, QueryTriggerInteraction.Ignore))
                return false;
        }

        if (Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.up, 4f, mask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    /// <summary>
    /// Spiral search for a valid outdoor point near <paramref name="seed"/>.
    /// </summary>
    public static bool TryFindValidOutdoorSpawnNear(
        Vector3 seed,
        Vector3 playerNavPos,
        float maxHorizontalDrift,
        out Vector3 validPosition)
    {
        validPosition = default;
        float maxDriftSq = maxHorizontalDrift * maxHorizontalDrift;
        Vector2 seedXZ = new Vector2(seed.x, seed.z);

        float[] sampleRadii = { 1.5f, 4f, 8f, 14f, 22f };
        Vector2[] offsets =
        {
            Vector2.zero,
            new Vector2( 2f,  0f), new Vector2(-2f,  0f),
            new Vector2( 0f,  2f), new Vector2( 0f, -2f),
            new Vector2( 3f,  3f), new Vector2(-3f, -3f),
            new Vector2( 3f, -3f), new Vector2(-3f,  3f),
        };

        for (int r = 0; r < sampleRadii.Length; r++)
        {
            for (int o = 0; o < offsets.Length; o++)
            {
                Vector3 probe = seed + new Vector3(offsets[o].x, 0.5f, offsets[o].y);
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, sampleRadii[r], NavMesh.AllAreas))
                    continue;

                Vector2 hitXZ = new Vector2(hit.position.x, hit.position.z);
                if ((hitXZ - seedXZ).sqrMagnitude > maxDriftSq)
                    continue;

                if (!IsValidOutdoorSpawn(hit.position, playerNavPos, rejectRooftops: true))
                    continue;

                validPosition = hit.position;
                return true;
            }
        }

        return false;
    }

    public static bool TryPlaceAgentOnValidNavMesh(
        NavMeshAgent agent,
        Transform target,
        Vector3 preferred,
        Vector3 playerNavPos)
    {
        if (target == null)
            return false;

        if (agent != null)
            agent.enabled = false;

        const float maxDrift = 14f;
        if (TryFindValidOutdoorSpawnNear(preferred, playerNavPos, maxDrift, out Vector3 valid))
        {
            target.position = valid;
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                    agent.Warp(valid);
            }
            return true;
        }

        target.position = preferred;
        if (agent != null)
            agent.enabled = true;
        return false;
    }

    /// <summary>
    /// Capsule overlap against static geometry — used to stop NavMeshAgent/flocking clipping through walls.
    /// </summary>
    public static bool IsCapsuleInsideStaticGeometry(Vector3 worldPosition, float radius, float height)
    {
        float half = Mathf.Max(radius, height * 0.5f - radius);
        Vector3 bottom = worldPosition + Vector3.up * radius;
        Vector3 top = worldPosition + Vector3.up * (height - radius);
        return Physics.CheckCapsule(bottom, top, radius * 0.92f, StaticGeometryMask, QueryTriggerInteraction.Ignore);
    }

    private static bool HasSpawnGroundAndClearance(Vector3 candidate, float requiredHeight)
    {
        int mask = SpawnPhysicsMask;

        Vector3 feetProbe = candidate + Vector3.up * 0.35f;
        if (!Physics.Raycast(feetProbe, Vector3.down, out RaycastHit ground, 2.5f, mask, QueryTriggerInteraction.Ignore))
            return false;

        if (candidate.y < ground.point.y - MaxFeetBelowGround)
            return false;

        if (Physics.Raycast(candidate + Vector3.up * 0.12f, Vector3.up, requiredHeight + 0.25f, mask,
                QueryTriggerInteraction.Ignore))
            return false;

        float bodyRadius = 0.45f;
        Vector3 bodyCenter = candidate + Vector3.up * (requiredHeight * 0.5f);
        return !Physics.CheckSphere(bodyCenter, bodyRadius, mask, QueryTriggerInteraction.Ignore);
    }
}
