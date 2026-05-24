public sealed class AIStuckRecoveryState : AIStateBase
{
    public override AIStateId Id => AIStateId.StuckRecovery;

    public override void Tick(EnemyController host)
    {
        host.TickStuckRecoveryState();
    }
}
