using UnityEngine;
using UnityEngine.AI;

public static class AIMotor
{
    private static PlayerController _cachedPlayer;
    private static float _playerCacheTime = -1f;

    private static PlayerController GetCachedPlayer()
    {
        if (_cachedPlayer != null && Time.time - _playerCacheTime < 2f)
            return _cachedPlayer;
        _cachedPlayer = EnemyController.ResolveScenePlayer();
        _playerCacheTime = Time.time;
        return _cachedPlayer;
    }

    public static void ApplyBlackPlayerLocomotion(NavMeshAgent agent, EnemyController host)
    {
        if (agent == null || host == null)
            return;

        PlayerController player = GetCachedPlayer();
        if (player != null)
        {
            // Mirror the black player's locomotion 1:1 — the previous tuning
            // multiplied walk by 0.82 and sprint by 0.80, plus acceleration by
            // 0.78, which made enemies feel like they were moving in slow
            // motion compared to the player. At close range we hold the
            // player's full walk speed; at far range we go above the player's
            // sprint so a chase can actually close the gap on a sprinting
            // target instead of drifting behind it forever.
            // Mirror the black player's locomotion 1:1 — no boost, no
            // dampening. At close range the enemy moves at the player's
            // WALK pace; once they're chasing across the map they ramp up
            // to the player's SPRINT pace. This is the "normal, natural,
            // not faster, not slower" feel the user asked for. Designer
            // multipliers (chaseSpeedMultiplier, farTargetBurstMultiplier)
            // are intentionally NOT compounded here so the locomotion
            // never feels accelerated beyond the player's own envelope.
            float playerWalk = Mathf.Max(0.1f, player.moveSpeed);
            float playerSprint = playerWalk * Mathf.Max(1f, player.sprintMultiplier);

            float gap = host.GetCombatDistanceToTarget();
            float closeRange = Mathf.Max(2f, host.meleeAttackRange * 1.6f);
            float farRange = Mathf.Max(closeRange + 4f, host.farTargetBurstDistance);
            float t = Mathf.InverseLerp(closeRange, farRange, gap);

            // Floor = player walk, ceiling = player sprint. Linear ramp in
            // between based on engagement distance.
            float balanced = Mathf.Lerp(playerWalk, playerSprint, t);

            agent.speed = balanced;
            // Acceleration matches the player's exactly so the enemy
            // reaches cruise speed with the same snappiness — not slower
            // (lazy chase), not faster (rocket dash).
            agent.acceleration = Mathf.Max(0.1f, player.acceleration);
            agent.angularSpeed = Mathf.Max(30f, player.turnSpeed);
            host.rotationSpeed = Mathf.Max(8f, player.turnSpeed / 90f);
            return;
        }

        agent.speed = Mathf.Max(0.1f, host.chaseSpeed);
        agent.acceleration = Mathf.Max(0.1f, host.agentAcceleration);
        agent.angularSpeed = Mathf.Max(30f, host.agentAngularSpeed);
    }

    public static void SuppressRootMotion(EnemyController host)
    {
        if (host == null)
            return;

        Animator anim = host.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.applyRootMotion = false;
    }

    public static void EnforceSprintPursuit(EnemyController host)
    {
        if (host == null)
            return;

        if (host.IsLockedForAttack())
            return;

        Transform target = host.CurrentTarget;
        if (target == null)
            return;

        if (!host.PrepareAgentForCombatLocomotion())
        {
            host.DriveDirectChase(target);
            return;
        }

        NavMeshAgent agent = host.Agent;
        agent.isStopped = false;
        agent.updatePosition = true;
        agent.updateRotation = false;
        agent.autoBraking = false;
        agent.stoppingDistance = EnemyController.CombatStoppingDistance;
        ApplyBlackPlayerLocomotion(agent, host);
        SuppressRootMotion(host);
        host.TrySetChaseDestinationValidated();
    }

    public static void DrivePursuit(EnemyController host, float stoppingDistance)
    {
        EnforceSprintPursuit(host);
        if (host.IsAgentReady())
            host.Agent.stoppingDistance = stoppingDistance;
    }

    public static void ForceAggressivePath(EnemyController host)
    {
        EnforceSprintPursuit(host);
    }
}
