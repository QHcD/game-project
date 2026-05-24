public sealed class AIChaseState : AIStateBase
{
    public override AIStateId Id => AIStateId.Chase;

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

        if (host.GetCombatDistanceToTarget() <= host.meleeAttackRange && host.CanBeginMeleeAttack() && !host.AttackInProgress)
        {
            host.TransitionTo(AIStateId.Attack);
            return;
        }

        AIMotor.EnforceSprintPursuit(host);
        host.CheckAndJumpIfStuck();
        host.CheckNoPathLedgeDrop();
        host.TickCombatManeuver();
    }
}
