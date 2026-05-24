using UnityEngine;

public sealed class AIApproachState : AIStateBase
{
    public override AIStateId Id => AIStateId.Approach;

    public override void Enter(EnemyController host)
    {
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

        if (host.GetCombatDistanceToTarget() > host.meleeAttackRange)
        {
            host.TransitionTo(AIStateId.Chase);
            return;
        }

        if (host.CanBeginMeleeAttack() && !host.AttackInProgress)
        {
            host.TransitionTo(AIStateId.Attack);
            return;
        }

        Vector3 toTarget = host.CurrentTarget.position - host.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.02f)
            host.FaceDirection(toTarget);

        AIMotor.EnforceSprintPursuit(host);
        host.CheckAndJumpIfStuck();
        host.CheckNoPathLedgeDrop();
    }
}
