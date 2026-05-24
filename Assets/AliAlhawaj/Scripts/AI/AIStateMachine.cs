using System.Collections.Generic;

public sealed class AIStateMachine
{
    private readonly Dictionary<AIStateId, AIStateBase> _states = new Dictionary<AIStateId, AIStateBase>();
    private AIStateBase _current;
    private EnemyController _host;

    public AIStateId CurrentId => _current != null ? _current.Id : AIStateId.Idle;

    public void Bind(EnemyController host)
    {
        _host = host;
        Register(new AIIdleState());
        Register(new AIPatrolState());
        Register(new AIChaseState());
        Register(new AIApproachState());
        Register(new AIAttackState());
        Register(new AIEvadeState());
        Register(new AIStuckRecoveryState());
        Register(new AIFlinchState());
    }

    private void Register(AIStateBase state)
    {
        _states[state.Id] = state;
    }

    public void SetState(AIStateId id, bool force = false)
    {
        if (_host == null)
            return;

        if (!force && _current != null && _current.Id == id)
            return;

        AIStateId previous = _current != null ? _current.Id : AIStateId.Idle;

        if (!_states.TryGetValue(id, out AIStateBase next))
        {
            _current?.Exit(_host);
            _host.OnAIStateExit(previous, id);
            _current = null;
            _host.SyncAIStateId(id);
            _host.OnAIStateEnter(previous, id);
            return;
        }

        _current?.Exit(_host);
        _host.OnAIStateExit(previous, id);
        _current = next;
        _host.SyncAIStateId(id);
        _host.OnAIStateEnter(previous, id);
        _current.Enter(_host);
    }

    public void Tick()
    {
        if (_host == null || _current == null)
            return;

        _current.Tick(_host);
    }
}
