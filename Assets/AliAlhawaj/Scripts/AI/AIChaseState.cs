using UnityEngine;
using UnityEngine.AI;

public sealed class AIChaseState : AIStateBase
{
    public override AIStateId Id => AIStateId.Chase;

    private const float DoorwayProbeRadius = 1.15f;
    private const float DoorwayYieldDuration = 0.35f;
    private const float DoorwayBlockedSpeedThreshold = 0.40f;
    private const float DoorwayBlockedHoldTime = 0.22f;
    private const float DoorwaySideStepDistance = 1.4f;
    private const float DoorwayWaitJitter = 0.18f;

    private float _stuckAtObstacleTimer;
    private float _noProgressTimer;
    private float _replanCooldown;
    private float _doorwayBlockedTimer;
    private float _doorwayYieldUntil;
    private Vector3 _doorwayYieldDestination;
    private bool _doorwayYieldActive;
    private float _propBypassUntil;
    private Vector3 _propBypassDestination;
    private bool _propBypassActive;
    private float _propStuckTimer;
    private int _propBypassSideSign = 1;
    private float _propBypassCooldownUntil;
    private float _forwardClearanceCooldown;
    private int _forwardClearanceSideSign = 1;

    private static readonly RaycastHit[] s_clearanceHits = new RaycastHit[4];

    private static readonly Collider[] s_doorwayProbeBuffer = new Collider[12];

    public override void Enter(EnemyController host)
    {
        _stuckAtObstacleTimer = 0f;
        _noProgressTimer = 0f;
        _replanCooldown = 0f;
        _doorwayBlockedTimer = 0f;
        _doorwayYieldUntil = 0f;
        _doorwayYieldActive = false;
        _propBypassActive = false;
        _propBypassUntil = 0f;
        _propStuckTimer = 0f;
        _propBypassCooldownUntil = 0f;
        _forwardClearanceCooldown = 0f;
        AIMotor.ForceAggressivePath(host);
    }

    public override void Tick(EnemyController host)
    {
        if (host.HandleOffMeshLinkTraversal())
            return;

        if (!host.IsHostileAlive(host.CurrentTarget))
        {
            host.ClearTarget();
            host.TransitionTo(AIStateId.Patrol);
            return;
        }

        if (host.GetCombatDistanceToTarget() <= host.meleeAttackRange && host.CanBeginMeleeAttack() && !host.AttackInProgress)
        {
            host.TransitionTo(AIStateId.Attack);
            return;
        }

        if (TickDoorwayYield(host))
            return;

        if (TickPropBypass(host))
            return;

        AIMotor.EnforceSprintPursuit(host);
        ApplyProactiveForwardClearance(host);
        DetectDoorwayDeadlock(host);
        DetectPropHangAndBypass(host);
        DetectAndRecoverFromObstacleHang(host);
        host.CheckAndJumpIfStuck();
        host.CheckNoPathLedgeDrop();
    }

    private void ApplyProactiveForwardClearance(EnemyController host)
    {
        if (_forwardClearanceCooldown > 0f)
        {
            _forwardClearanceCooldown -= Time.deltaTime;
            return;
        }

        if (!host.IsAgentReady())
            return;

        NavMeshAgent agent = host.Agent;
        if (agent.pathPending)
            return;

        Vector3 vel = agent.desiredVelocity;
        vel.y = 0f;
        if (vel.sqrMagnitude < 0.16f)
            return;

        Vector3 dir = vel.normalized;
        float radius = Mathf.Max(0.3f, agent.radius * 0.9f);
        float height = Mathf.Max(1.2f, agent.height * 0.85f);
        Vector3 origin = host.transform.position;
        Vector3 point1 = origin + Vector3.up * radius;
        Vector3 point2 = origin + Vector3.up * (height - radius);
        float probeDistance = Mathf.Max(0.9f, agent.speed * 0.25f);

        int mask = EnemySpawnGeometry.StaticGeometryMask;
        int hitCount = Physics.CapsuleCastNonAlloc(point1, point2, radius, dir,
            s_clearanceHits, probeDistance, mask, QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
            return;

        Vector3 hitNormal = Vector3.zero;
        float closest = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = s_clearanceHits[i];
            if (h.collider == null)
                continue;
            if (h.collider.transform.IsChildOf(host.transform))
                continue;
            if (h.distance < closest)
            {
                closest = h.distance;
                hitNormal = h.normal;
            }
        }

        if (closest >= float.MaxValue)
            return;

        hitNormal.y = 0f;
        if (hitNormal.sqrMagnitude < 0.001f)
            return;
        hitNormal.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        float sideDot = Vector3.Dot(hitNormal, right);
        if (Mathf.Abs(sideDot) < 0.05f)
        {
            _forwardClearanceSideSign = -_forwardClearanceSideSign;
            sideDot = _forwardClearanceSideSign;
        }
        else
        {
            _forwardClearanceSideSign = sideDot >= 0f ? 1 : -1;
        }

        float sideMag = 1.4f;
        float forwardMag = Mathf.Max(0.4f, closest * 0.6f);
        Vector3 nudge = origin
            + right * (_forwardClearanceSideSign * sideMag)
            + dir * forwardMag;

        if (NavMesh.SamplePosition(nudge, out NavMeshHit nav, 1.4f, NavMesh.AllAreas))
        {
            if (!NavMesh.Raycast(origin, nav.position, out NavMeshHit _, NavMesh.AllAreas))
            {
                agent.SetDestination(nav.position);
                _forwardClearanceCooldown = 0.45f;
                return;
            }
        }

        _forwardClearanceSideSign = -_forwardClearanceSideSign;
        nudge = origin
            + right * (_forwardClearanceSideSign * sideMag)
            + dir * forwardMag;

        if (NavMesh.SamplePosition(nudge, out nav, 1.4f, NavMesh.AllAreas))
        {
            if (!NavMesh.Raycast(origin, nav.position, out NavMeshHit _, NavMesh.AllAreas))
            {
                agent.SetDestination(nav.position);
                _forwardClearanceCooldown = 0.45f;
            }
        }
    }

    private bool TickDoorwayYield(EnemyController host)
    {
        if (!_doorwayYieldActive)
            return false;

        if (!host.IsAgentReady())
        {
            _doorwayYieldActive = false;
            return false;
        }

        NavMeshAgent agent = host.Agent;
        if (Time.time >= _doorwayYieldUntil
            || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f))
        {
            _doorwayYieldActive = false;
            _doorwayBlockedTimer = 0f;
            return false;
        }

        Transform target = host.CurrentTarget;
        if (target != null)
        {
            Vector3 face = target.position - host.transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.02f)
                host.FaceDirection(face);
        }

        if (agent.isStopped)
            agent.isStopped = false;
        agent.updateRotation = false;

        return true;
    }

    private void DetectDoorwayDeadlock(EnemyController host)
    {
        if (!host.IsAgentReady())
            return;

        NavMeshAgent agent = host.Agent;
        if (agent.pathPending)
            return;

        Transform target = host.CurrentTarget;
        if (target == null)
            return;

        float dt = Time.deltaTime;
        float speed = agent.velocity.magnitude;
        float gapToTarget = host.GetCombatDistanceToTarget();
        bool farFromTarget = gapToTarget > Mathf.Max(host.meleeAttackRange * 1.5f, 1.5f);

        if (!farFromTarget || speed >= DoorwayBlockedSpeedThreshold)
        {
            _doorwayBlockedTimer = 0f;
            return;
        }

        if (!IsBlockedByAnotherEnemy(host, target, out EnemyController blocker))
        {
            _doorwayBlockedTimer = 0f;
            return;
        }

        _doorwayBlockedTimer += dt;
        if (_doorwayBlockedTimer < DoorwayBlockedHoldTime)
            return;

        _doorwayBlockedTimer = 0f;

        if (ShouldYieldToBlocker(host, blocker))
        {
            TryStartDoorwayYield(host, blocker);
            return;
        }

        TryStartDoorwayYield(host, blocker);
    }

    private static bool IsBlockedByAnotherEnemy(EnemyController host, Transform target, out EnemyController blocker)
    {
        blocker = null;

        Vector3 origin = host.transform.position;
        Vector3 toTarget = target.position - origin;
        toTarget.y = 0f;
        float distToTarget = toTarget.magnitude;
        if (distToTarget < 0.001f)
            return false;

        Vector3 dir = toTarget / distToTarget;
        Vector3 probeCenter = origin + dir * DoorwayProbeRadius + Vector3.up * 0.9f;

        int count = Physics.OverlapSphereNonAlloc(probeCenter, DoorwayProbeRadius,
            s_doorwayProbeBuffer, ~0, QueryTriggerInteraction.Ignore);

        float bestProj = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider col = s_doorwayProbeBuffer[i];
            if (col == null)
                continue;

            EnemyController other = col.GetComponentInParent<EnemyController>();
            if (other == null || other == host || !other.IsAlive)
                continue;

            Vector3 offset = other.transform.position - origin;
            offset.y = 0f;
            float proj = Vector3.Dot(offset, dir);
            if (proj <= 0.05f || proj >= distToTarget)
                continue;

            Vector3 perp = offset - dir * proj;
            float side = perp.magnitude;
            if (side > DoorwayProbeRadius * 0.95f)
                continue;

            if (proj < bestProj)
            {
                bestProj = proj;
                blocker = other;
            }
        }

        return blocker != null;
    }

    private static bool ShouldYieldToBlocker(EnemyController host, EnemyController blocker)
    {
        if (blocker == null)
            return false;

        int hostId = host.GetInstanceID();
        int blockerId = blocker.GetInstanceID();
        return hostId > blockerId;
    }

    private void TryStartDoorwayYield(EnemyController host, EnemyController blocker)
    {
        NavMeshAgent agent = host.Agent;
        Transform target = host.CurrentTarget;
        if (agent == null || target == null)
            return;

        Vector3 origin = host.transform.position;
        Vector3 toTarget = target.position - origin;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector3 forward = toTarget.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        Vector3 blockerOffset = blocker.transform.position - origin;
        blockerOffset.y = 0f;
        float sideSign = Vector3.Dot(blockerOffset, right) >= 0f ? -1f : 1f;

        Vector3[] candidates =
        {
            origin + right * (sideSign * DoorwaySideStepDistance) + forward * 0.35f,
            origin + right * (-sideSign * DoorwaySideStepDistance) + forward * 0.35f,
            origin - forward * (DoorwaySideStepDistance * 0.7f) + right * (sideSign * 0.6f),
            origin - forward * (DoorwaySideStepDistance * 0.7f),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!NavMesh.SamplePosition(candidates[i], out NavMeshHit hit, 1.6f, NavMesh.AllAreas))
                continue;

            if (NavMesh.Raycast(origin, hit.position, out NavMeshHit _, NavMesh.AllAreas))
                continue;

            _doorwayYieldDestination = hit.position;
            _doorwayYieldActive = true;
            _doorwayYieldUntil = Time.time + DoorwayYieldDuration + Random.value * DoorwayWaitJitter;

            agent.ResetPath();
            agent.isStopped = false;
            agent.autoBraking = true;
            agent.stoppingDistance = 0.05f;
            agent.SetDestination(_doorwayYieldDestination);
            return;
        }

        _doorwayYieldUntil = Time.time + DoorwayYieldDuration + Random.value * DoorwayWaitJitter;
        _doorwayYieldActive = true;
        _doorwayYieldDestination = origin;
        if (agent.hasPath)
            agent.ResetPath();
    }

    private bool TickPropBypass(EnemyController host)
    {
        if (!_propBypassActive)
            return false;

        if (!host.IsAgentReady())
        {
            _propBypassActive = false;
            return false;
        }

        NavMeshAgent agent = host.Agent;
        Transform target = host.CurrentTarget;

        if (target != null)
        {
            Vector3 face = target.position - host.transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.02f)
                host.FaceDirection(face);
        }

        Vector3 toDest = _propBypassDestination - host.transform.position;
        toDest.y = 0f;
        bool reached = toDest.sqrMagnitude <= 0.36f
                    || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f);

        if (Time.time >= _propBypassUntil || reached)
        {
            _propBypassActive = false;
            _propBypassCooldownUntil = Time.time + 1.0f;
            agent.ResetPath();
            host.ResetChaseRepathClock();
            host.TrySetChaseDestinationValidated();
            return false;
        }

        if (agent.isStopped)
            agent.isStopped = false;

        return true;
    }

    private void DetectPropHangAndBypass(EnemyController host)
    {
        if (Time.time < _propBypassCooldownUntil)
            return;

        if (!host.IsAgentReady())
            return;

        NavMeshAgent agent = host.Agent;
        if (agent.pathPending)
            return;

        Transform target = host.CurrentTarget;
        if (target == null)
            return;

        float dt = Time.deltaTime;
        float speed = agent.velocity.magnitude;
        float gapToTarget = host.GetCombatDistanceToTarget();
        bool farFromTarget = gapToTarget > Mathf.Max(host.meleeAttackRange * 1.4f, 1.5f);
        bool wantsToMove = agent.desiredVelocity.sqrMagnitude > 0.25f
                        || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.15f);
        bool notMoving = speed < 0.35f;

        if (!farFromTarget || !wantsToMove || !notMoving)
        {
            _propStuckTimer = 0f;
            return;
        }

        _propStuckTimer += dt;
        if (_propStuckTimer < 0.35f)
            return;

        if (IsBlockedByAnotherEnemy(host, target, out _))
        {
            _propStuckTimer = 0f;
            return;
        }

        if (TryStartPropBypass(host))
            _propStuckTimer = 0f;
    }

    private bool TryStartPropBypass(EnemyController host)
    {
        NavMeshAgent agent = host.Agent;
        Transform target = host.CurrentTarget;
        if (agent == null || target == null)
            return false;

        Vector3 origin = host.transform.position;
        Vector3 toTarget = target.position - origin;
        toTarget.y = 0f;
        float distToTarget = toTarget.magnitude;
        if (distToTarget < 0.001f)
            return false;

        Vector3 forward = toTarget / distToTarget;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        _propBypassSideSign = -_propBypassSideSign;
        float primary = _propBypassSideSign;
        float alt = -_propBypassSideSign;

        float[] yawAngles =
        {
            45f * primary,
            45f * alt,
            70f * primary,
            70f * alt,
            95f * primary,
            95f * alt,
            130f * primary,
            130f * alt,
            180f,
        };
        float[] distances = { 1.6f, 2.4f, 3.2f };

        for (int d = 0; d < distances.Length; d++)
        {
            for (int a = 0; a < yawAngles.Length; a++)
            {
                Quaternion rot = Quaternion.AngleAxis(yawAngles[a], Vector3.up);
                Vector3 dir = rot * forward;
                Vector3 candidate = origin + dir * distances[d];

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.4f, NavMesh.AllAreas))
                    continue;

                Vector3 sampled = hit.position;
                Vector3 toSampled = sampled - origin;
                toSampled.y = 0f;
                if (toSampled.sqrMagnitude < 0.36f)
                    continue;

                if (NavMesh.Raycast(origin, sampled, out NavMeshHit _, NavMesh.AllAreas))
                    continue;

                _propBypassDestination = sampled;
                _propBypassActive = true;
                _propBypassUntil = Time.time + 0.85f;

                agent.ResetPath();
                agent.isStopped = false;
                agent.autoBraking = false;
                agent.stoppingDistance = 0.1f;
                agent.SetDestination(_propBypassDestination);
                return true;
            }
        }

        return false;
    }

    private void DetectAndRecoverFromObstacleHang(EnemyController host)
    {
        if (!host.IsAgentReady())
            return;

        NavMeshAgent agent = host.Agent;
        if (agent.pathPending)
            return;

        Transform target = host.CurrentTarget;
        if (target == null)
            return;

        float dt = Time.deltaTime;
        if (_replanCooldown > 0f)
            _replanCooldown -= dt;

        float speed = agent.velocity.magnitude;
        bool partialPath = agent.pathStatus == NavMeshPathStatus.PathPartial
                        || agent.pathStatus == NavMeshPathStatus.PathInvalid;
        float gapToTarget = host.GetCombatDistanceToTarget();
        bool farFromTarget = gapToTarget > Mathf.Max(host.meleeAttackRange * 1.5f, 1.5f);

        if (speed < 0.35f && farFromTarget)
            _stuckAtObstacleTimer += dt;
        else
            _stuckAtObstacleTimer = 0f;

        Vector3 destToTarget = agent.destination - target.position;
        destToTarget.y = 0f;
        float destDistSqr = destToTarget.sqrMagnitude;
        if (destDistSqr > 1.2f && farFromTarget)
            _noProgressTimer += dt;
        else
            _noProgressTimer = 0f;

        bool needsReplan = _stuckAtObstacleTimer >= 0.45f
                        || _noProgressTimer >= 0.6f
                        || partialPath;

        if (!needsReplan || _replanCooldown > 0f)
            return;

        agent.ResetPath();
        host.ResetChaseRepathClock();
        host.TrySetChaseDestinationValidated();

        if (partialPath || _noProgressTimer >= 0.6f)
            host.RepositionAroundUnreachableTarget();

        _stuckAtObstacleTimer = 0f;
        _noProgressTimer = 0f;
        _replanCooldown = 0.35f;
    }
}
