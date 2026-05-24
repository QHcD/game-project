public abstract class AIStateBase
{
    public abstract AIStateId Id { get; }

    public virtual void Enter(EnemyController host) { }

    public virtual void Exit(EnemyController host) { }

    public abstract void Tick(EnemyController host);
}
