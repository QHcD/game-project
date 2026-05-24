using UnityEngine;

public sealed class AIPatrolState : AIStateBase
{
    public override AIStateId Id => AIStateId.Patrol;

    public override void Tick(EnemyController host)
    {
        if (host.CurrentTarget != null && host.IsHostileAlive(host.CurrentTarget))
        {
            if (host.IsAgentReady())
                host.Agent.isStopped = false;
            host.TransitionTo(AIStateId.Chase);
            return;
        }

        if (!host.IsAgentReady())
            return;

        host.Agent.isStopped = false;
        host.Agent.speed = host.moveSpeed;
        host.Agent.stoppingDistance = Mathf.Max(0.25f, host.attackRadius * 0.35f);

        if (host.IsTraversingOffMeshLink || host.Agent.isOnOffMeshLink)
            return;

        host.PatrolTimer -= Time.deltaTime;
        bool needsDestination = !host.Agent.hasPath
            || host.Agent.remainingDistance <= host.Agent.stoppingDistance + 0.35f;
        if (host.PatrolTimer <= 0f || needsDestination)
        {
            host.PatrolTimer = host.patrolRetargetInterval;
            host.SetRandomPatrolDestination();
        }
    }
}
