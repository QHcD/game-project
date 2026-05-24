using UnityEngine;

public sealed class AIIdleState : AIStateBase
{
    public override AIStateId Id => AIStateId.Idle;

    public override void Tick(EnemyController host)
    {
        if (host.CurrentTarget != null && host.IsHostileAlive(host.CurrentTarget))
        {
            if (host.IsAgentReady())
                host.Agent.isStopped = false;
            host.TransitionTo(AIStateId.Chase);
            return;
        }

        if (host.IsAgentReady())
            host.Agent.isStopped = true;

        host.IdleTimer += Time.deltaTime;
        if (host.IdleTimer >= host.IdleToPatrolDelay)
            host.TransitionTo(AIStateId.Patrol);
    }
}
