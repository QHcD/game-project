using UnityEngine;
using UnityEngine.AI;

public sealed class AIAttackState : AIStateBase
{
    public override AIStateId Id => AIStateId.Attack;

    public override void Enter(EnemyController host)
    {
        if (!host.IsAgentReady())
            return;

        NavMeshAgent agent = host.Agent;
        if (agent.hasPath)
            agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        agent.updateRotation = false;
        agent.autoBraking = true;
        agent.updatePosition = true;
    }

    public override void Exit(EnemyController host)
    {
        if (!host.IsAgentReady())
            return;

        NavMeshAgent agent = host.Agent;
        agent.isStopped = false;
        agent.autoBraking = false;
    }

    public override void Tick(EnemyController host)
    {
        if (host.HandleOffMeshLinkTraversal())
            return;

        if (!host.IsHostileAlive(host.CurrentTarget))
        {
            host.AbortActiveAttack();
            host.ClearTarget();
            host.TransitionTo(AIStateId.Patrol);
            return;
        }

        if (!host.IsWithinMeleeAttackRange() || !host.HasCombatVisionTo(host.CurrentTarget))
        {
            host.AbortActiveAttack();
            host.TransitionTo(AIStateId.Chase);
            return;
        }

        if (host.IsAgentReady() && !host.Agent.isStopped)
            host.Agent.isStopped = true;

        Vector3 toTarget = host.CurrentTarget.position - host.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.02f)
            host.FaceDirection(toTarget);

        if (!host.AttackInProgress && host.AttackTimer <= 0f && host.CanBeginMeleeAttack())
        {
            host.AttackTimer = host.attackCooldown;
            host.ExecuteAttack();
        }
    }
}
