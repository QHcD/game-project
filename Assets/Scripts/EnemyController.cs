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

    // Tracks whether the killing blow came from the player (not another enemy)
    private bool _lastHitByPlayer;

    // Re-acquire target every N seconds instead of every frame (performance + stability)
    private float _retargetTimer;
    private const float RetargetInterval = 0.3f;

    // Minimum gap before we push away from a neighbour (prevents stacking)
    private const float SeparationRadius  = 1.4f;
    private const float SeparationStrength = 3.5f;

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
        // Only scan for targets on a timer — avoids an OverlapSphere call every frame
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer > 0f) return;
        _retargetTimer = RetargetInterval;

        _target = FindNearestTarget(detectionRadius);
        if (_target != null)
            TransitionTo(State.Chase);
    }

    private void UpdateChase()
    {
        // Re-acquire target on a timer so enemies can swap targets without per-frame physics queries
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = RetargetInterval;
            _target = FindNearestTarget(detectionRadius * 1.5f);
        }

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

        // Separation: gently push away from overlapping enemies so they don't stack
        ApplySeparation();
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

    public void TakeDamage(int amount, bool byPlayer = false)
    {
        if (_state == State.Dead) return;
        if (byPlayer) _lastHitByPlayer = true;
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
            GameManager.Instance.EnemyKilled(_lastHitByPlayer);
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

    /// <summary>
    /// Nudges the NavMeshAgent destination away from overlapping enemies so they
    /// spread out naturally instead of stacking on top of each other.
    /// </summary>
    private void ApplySeparation()
    {
        if (!_agent.enabled) return;

        Collider[] neighbours = Physics.OverlapSphere(transform.position, SeparationRadius);
        Vector3 push = Vector3.zero;

        foreach (var nb in neighbours)
        {
            if (nb.transform == transform) continue;
            if (nb.GetComponent<EnemyController>() == null) continue;

            Vector3 away = transform.position - nb.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < 0.001f) continue;

            // Closer enemies push harder (linear falloff)
            push += away.normalized * (1f - dist / SeparationRadius);
        }

        if (push.sqrMagnitude > 0.001f)
        {
            // Offset the destination rather than teleporting — keeps NavMesh happy
            _agent.SetDestination(_agent.destination + push.normalized * SeparationStrength * Time.deltaTime);
        }
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
