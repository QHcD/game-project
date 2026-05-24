using UnityEngine;

public sealed class AIAttackState : AIStateBase
{
    public override AIStateId Id => AIStateId.Attack;

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

        if (host.GetCombatDistanceToTarget() > host.meleeAttackRange)
        {
            host.AbortActiveAttack();
            host.TransitionTo(AIStateId.Chase);
            return;
        }

        if (host.IsAgentReady())
        {
            host.Agent.isStopped = true;
            host.Agent.velocity = Vector3.zero;
        }

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
