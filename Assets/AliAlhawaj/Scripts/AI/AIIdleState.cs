using UnityEngine;
using UnityEngine.AI;

public sealed class AIIdleState : AIStateBase
{
    private float _activeScanCooldown;

    public override AIStateId Id => AIStateId.Idle;

    public override void Enter(EnemyController host)
    {
        if (host.IsAgentReady())
        {
            NavMeshAgent agent = host.Agent;
            if (agent.hasPath)
                agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.updateRotation = false;
        }

        host.IdleTimer = 0f;
        _activeScanCooldown = 0f;
    }

    public override void Exit(EnemyController host)
    {
        if (host.IsAgentReady())
            host.Agent.isStopped = false;
    }

    public override void Tick(EnemyController host)
    {
        if (TryEngageImmediately(host))
            return;

        host.IdleTimer += Time.deltaTime;
        if (host.IdleTimer >= host.IdleToPatrolDelay)
            host.TransitionTo(AIStateId.Patrol);
    }

    private bool TryEngageImmediately(EnemyController host)
    {
        Transform current = host.CurrentTarget;
        if (current != null && host.IsHostileAlive(current))
        {
            host.TransitionTo(AIStateId.Chase);
            return true;
        }

        _activeScanCooldown -= Time.deltaTime;
        if (_activeScanCooldown > 0f)
            return false;

        _activeScanCooldown = Mathf.Max(0.1f, host.detectionInterval * 0.5f);

        float scanRadius = Mathf.Max(host.detectionRadius, host.aggressiveScanRadius);
        Transform candidate = AISensing.FindClosestHostile(host, host.detectionRadius, requireLineOfSight: true);
        if (candidate == null)
            candidate = AISensing.FindClosestHostile(host, host.detectionRadius, requireLineOfSight: false);
        if (candidate == null)
            candidate = AISensing.FindClosestHostile(host, scanRadius, requireLineOfSight: false);

        if (candidate == null || !host.IsHostileAlive(candidate))
            return false;

        host.SuggestTarget(candidate);
        if (host.CurrentTarget == candidate)
        {
            host.TransitionTo(AIStateId.Chase);
            return true;
        }

        return false;
    }
}
