using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Grunt enemy AI — chases and attacks the nearest target (player or other enemies).
/// Requires a NavMeshAgent and a Collider on the same GameObject.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    // ── Tuning ──────────────────────────────────────────────────────────────
    [Header("Combat")]
    public float detectionRadius  = 18f;   // range at which the enemy starts chasing
    public float attackRadius     = 1.8f;  // range at which the enemy swings
    public float attackDamage     = 10f;
    public float attackCooldown   = 1.4f;  // seconds between attacks
    public int   maxHealth        = 60;

    [Header("Movement")]
    public float moveSpeed        = 3.5f;
    public float chaseSpeed       = 4.8f;
    public float rotationSpeed    = 8f;

    // ── State ────────────────────────────────────────────────────────────────
    private enum State { Idle, Chase, Attack, Dead }
    private State _state = State.Idle;

    private NavMeshAgent _agent;
    private Animator     _anim;
    private int          _currentHealth;
    private float        _attackTimer;
    private Transform    _target;

    // ── Animation parameter hashes (faster than string lookup) ───────────────
    private static readonly int HashSpeed    = Animator.StringToHash("Speed");
    private static readonly int HashAttack   = Animator.StringToHash("Attack");
    private static readonly int HashDead     = Animator.StringToHash("Dead");

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        _agent         = GetComponent<NavMeshAgent>();
        _anim          = GetComponentInChildren<Animator>();
        _currentHealth = maxHealth;

        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.stoppingDistance = attackRadius * 0.85f;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 12f;
        }
    }

    private void Update()
    {
        if (_state == State.Dead) return;

        _attackTimer -= Time.deltaTime;

        switch (_state)
        {
            case State.Idle:   UpdateIdle();   break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
        }

        // Keep animator in sync
        if (_anim != null)
        {
            float speed = _agent.enabled ? _agent.velocity.magnitude : 0f;
            _anim.SetFloat(HashSpeed, speed);
        }
    }

    // ── State handlers ────────────────────────────────────────────────────────
    private void UpdateIdle()
    {
        _target = FindNearestTarget(detectionRadius);
        if (_target != null)
            TransitionTo(State.Chase);
    }

    private void UpdateChase()
    {
        // Re-acquire target every 0.5 s so enemies can swap targets
        _target = FindNearestTarget(detectionRadius * 1.5f);

        if (_target == null)
        {
            TransitionTo(State.Idle);
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist <= attackRadius)
        {
            _agent.ResetPath();
            TransitionTo(State.Attack);
            return;
        }

        _agent.speed = chaseSpeed;
        _agent.SetDestination(_target.position);
        FaceTarget();
    }

    private void UpdateAttack()
    {
        if (_target == null) { TransitionTo(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, _target.position);

        // If target moved away, chase again
        if (dist > attackRadius * 1.4f)
        {
            TransitionTo(State.Chase);
            return;
        }

        FaceTarget();

        if (_attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            ExecuteAttack();
        }
    }

    // ── Combat ────────────────────────────────────────────────────────────────
    private void ExecuteAttack()
    {
        if (_anim != null) _anim.SetTrigger(HashAttack);

        // Deal damage to the target
        if (_target != null)
        {
            // Try PlayerHealth first (for the player)
            PlayerHealth ph = _target.GetComponentInParent<PlayerHealth>();
            if (ph != null) { ph.TakeDamage((int)attackDamage); return; }

            EnemyController otherEnemy = _target.GetComponentInParent<EnemyController>();
            if (otherEnemy != null && otherEnemy != this)
            {
                otherEnemy.TakeDamage((int)attackDamage);
                return;
            }

            Actor actor = _target.GetComponentInParent<Actor>();
            if (actor != null) actor.TakeDamage((int)attackDamage);
        }
    }

    public void TakeDamage(int amount)
    {
        if (_state == State.Dead) return;
        _currentHealth -= amount;
        if (_currentHealth <= 0) Die();
    }

    private void Die()
    {
        _state = State.Dead;
        if (_anim != null) _anim.SetTrigger(HashDead);
        if (_agent != null) _agent.enabled = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnemyKilled();
        }

        // Remove collider so enemies don't stack on corpse
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, 4f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void TransitionTo(State newState)
    {
        _state = newState;
        if (newState == State.Idle && _agent.enabled)
            _agent.ResetPath();
    }

    private void FaceTarget()
    {
        if (_target == null) return;
        Vector3 dir = (_target.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Finds the nearest valid target (Player or other EnemyController) within range.
    /// This makes enemies fight both the player AND each other.
    /// </summary>
    private Transform FindNearestTarget(float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        Transform best    = null;
        float     bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;

            bool isPlayer = hit.CompareTag("Player");
            bool isEnemy  = hit.GetComponent<EnemyController>() != null && hit.transform != transform;

            if (!isPlayer && !isEnemy) continue;

            float d = Vector3.Distance(transform.position, hit.transform.position);
            if (d < bestDist) { bestDist = d; best = hit.transform; }
        }

        return best;
    }

    // ── Editor Gizmos ─────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
