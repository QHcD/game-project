public sealed class AIFlinchState : AIStateBase
{
    public override AIStateId Id => AIStateId.Flinch;

    public override void Tick(EnemyController host)
    {
        host.TickFlinchState();
    }
}
