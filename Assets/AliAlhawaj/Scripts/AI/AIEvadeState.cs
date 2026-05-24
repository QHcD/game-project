public sealed class AIEvadeState : AIStateBase
{
    public override AIStateId Id => AIStateId.Evade;

    public override void Tick(EnemyController host)
    {
        host.TickEvadeState();
    }
}
