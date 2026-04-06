using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Overhauled enemy AI with proper separation, hit reactions, and death ragdoll.
///
/// Improvements over original:
///   - NavMeshAgent avoidance priority + obstacle avoidance prevents stacking
///   - TakeDamage triggers flinch (brief pause + visual flash + audio)
///   - Death triggers ragdoll (replace NavMeshAgent with Rigidbodies) and corpse stays
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    // ── Tuning ──────────────────────────────────────────────────────────────
    [Header("Combat")]
    public float detectionRadius  = 18f;
    public float attackRadius     = 1.8f;
    public float attackDamage     = 10f;
    public float attackCooldown   = 1.4f;
    public int   maxHealth        = 60;

    [Header("Movement")]
    public float moveSpeed        = 3.5f;
    public float chaseSpeed       = 4.8f;
    public float rotationSpeed    = 8f;

    [Header("Separation (Anti-Stacking)")]
    [Tooltip("Minimum distance between enemies before push kicks in.")]
    public float separationRadius   = 2.0f;
    [Tooltip("Force of the separation push.")]
    public float separationStrength = 5.0f;

    [Header("Hit Reaction")]
    [Tooltip("Duration of the hit-stun pause (seconds).")]
    public float flinchDuration = 0.25f;
    [Tooltip("Color flash on hit.")]
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("Hit sound effect (auto-generated if null).")]
    public AudioClip hitSound;
    [Tooltip("Death sound effect (auto-generated if null).")]
    public AudioClip deathSound;

    [Header("Death")]
    [Tooltip("Seconds before corpse is cleaned up (0 = never).")]
    public float corpseLifetime = 15f;
    [Tooltip("Upward force when ragdolling on death.")]
    public float deathPopForce = 2f;

    // ── State ────────────────────────────────────────────────────────────────
    private enum State { Idle, Chase, Attack, Flinch, Dead }
    private State _state = State.Idle;

    private NavMeshAgent _agent;
    private Animator     _anim;
    private AudioSource  _audio;
    private int          _currentHealth;
    private float        _attackTimer;
    private Transform    _target;
    private bool         _lastHitByPlayer;

    // Flinch
    private float _flinchTimer;

    // Re-targeting timer
    private float _retargetTimer;
    private const float RetargetInterval = 0.3f;

    // Visual feedback
    private Renderer[] _renderers;
    private Color[] _originalColors;
    private float _flashTimer;
    private const float FlashDuration = 0.15f;

    // Death
    private bool _ragdollActive;

    // Animator hashes
    private static readonly int HashSpeed    = Animator.StringToHash("Speed");
    private static readonly int HashAttack   = Animator.StringToHash("Attack");
    private static readonly int HashDead     = Animator.StringToHash("Dead");
    private static readonly int HashHit      = Animator.StringToHash("Hit");

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        _agent         = GetComponent<NavMeshAgent>();
        _anim          = GetComponentInChildren<Animator>();
        _currentHealth = maxHealth;

        // Audio source for hit/death sounds
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1f; // 3D sound
        _audio.playOnAwake = false;

        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.stoppingDistance = attackRadius * 0.85f;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 12f;

            // Avoidance: assign random priority so enemies don't all yield to each other
            _agent.avoidancePriority = Random.Range(30, 70);
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            _agent.radius = 0.5f;
        }

        // Cache renderers for hit flash
        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].material != null)
                _originalColors[i] = _renderers[i].material.color;
        }
    }

    private void Update()
    {
        if (_state == State.Dead) return;

        _attackTimer -= Time.deltaTime;

        // Hit flash recovery
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
                RestoreOriginalColors();
        }

        switch (_state)
        {
            case State.Idle:   UpdateIdle();   break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
            case State.Flinch: UpdateFlinch(); break;
        }

        // Animator sync
        if (_anim != null)
        {
            float speed = _agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f;
            _anim.SetFloat(HashSpeed, speed);
        }
    }

    // ── State handlers ────────────────────────────────────────────────────────
    private void UpdateIdle()
    {
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer > 0f) return;
        _retargetTimer = RetargetInterval;

        _target = FindNearestTarget(detectionRadius);
        if (_target != null)
            TransitionTo(State.Chase);
    }

    private void UpdateChase()
    {
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = RetargetInterval;
            _target = FindNearestTarget(detectionRadius * 1.5f);
        }

        if (_target == null) { TransitionTo(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist <= attackRadius)
        {
            if (_agent.enabled) _agent.ResetPath();
            TransitionTo(State.Attack);
            return;
        }

        if (_agent.enabled)
        {
            _agent.speed = chaseSpeed;
            _agent.SetDestination(_target.position);
        }
        FaceTarget();

        // Separation: prevent stacking with other enemies
        ApplySeparation();
    }

    private void UpdateAttack()
    {
        if (_target == null) { TransitionTo(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, _target.position);
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

    private void UpdateFlinch()
    {
        _flinchTimer -= Time.deltaTime;
        if (_flinchTimer <= 0f)
        {
            // Resume previous behavior
            if (_target != null)
                TransitionTo(State.Chase);
            else
                TransitionTo(State.Idle);
        }
    }

    // ── Combat ────────────────────────────────────────────────────────────────
    private void ExecuteAttack()
    {
        if (_anim != null) _anim.SetTrigger(HashAttack);

        if (_target != null)
        {
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

    /// <summary>
    /// Applies damage with hit reaction (flinch + flash + sound).
    /// </summary>
    public void TakeDamage(int amount, bool byPlayer = false)
    {
        if (_state == State.Dead) return;
        if (byPlayer) _lastHitByPlayer = true;

        _currentHealth -= amount;

        // ── Hit Reaction ──
        // 1. Flinch: brief stun pause
        if (_state != State.Flinch)
        {
            _flinchTimer = flinchDuration;
            TransitionTo(State.Flinch);

            // Pause NavMeshAgent during flinch
            if (_agent != null && _agent.enabled)
                _agent.ResetPath();
        }

        // 2. Visual flash: tint renderers red briefly
        ApplyHitFlash();

        // 3. Hit animation trigger
        if (_anim != null) _anim.SetTrigger(HashHit);

        // 4. Hit sound
        PlayHitSound();

        // Check death
        if (_currentHealth <= 0)
            Die();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DEATH — Ragdoll + Corpse
    // ════════════════════════════════════════════════════════════════════════

    private void Die()
    {
        _state = State.Dead;

        // Notify GameManager
        if (GameManager.Instance != null)
            GameManager.Instance.EnemyKilled(_lastHitByPlayer);

        // Play death sound
        if (_audio != null)
        {
            if (deathSound != null)
                _audio.PlayOneShot(deathSound);
            else
                PlayProceduralSound(200f, 0.4f); // low thud
        }

        // Death animation (if Animator exists and has Dead trigger)
        if (_anim != null)
        {
            _anim.SetTrigger(HashDead);
        }

        // Disable NavMeshAgent
        if (_agent != null) _agent.enabled = false;

        // Activate ragdoll: replace animated skeleton with physics
        ActivateRagdoll();

        // Disable main collider so living enemies don't bump into corpse
        Collider mainCol = GetComponent<Collider>();
        if (mainCol != null) mainCol.enabled = false;

        // Clean up corpse after lifetime (0 = stays forever)
        if (corpseLifetime > 0f)
            Destroy(gameObject, corpseLifetime);
    }

    /// <summary>
    /// Converts the enemy into a ragdoll by adding Rigidbodies to bones
    /// and disabling the Animator. Creates a believable death fall.
    /// </summary>
    private void ActivateRagdoll()
    {
        _ragdollActive = true;

        // Disable Animator so physics takes over
        if (_anim != null)
            _anim.enabled = false;

        // Add Rigidbody to root if not present (for capsule enemies)
        Rigidbody rootRb = GetComponent<Rigidbody>();
        if (rootRb == null)
        {
            rootRb = gameObject.AddComponent<Rigidbody>();
            rootRb.mass = 40f;
            rootRb.linearDamping = 0.5f;
            rootRb.angularDamping = 2f;
        }
        rootRb.isKinematic = false;
        rootRb.useGravity = true;

        // Apply a slight pop-up force + random sideways tumble
        Vector3 deathForce = Vector3.up * deathPopForce + Random.insideUnitSphere * 1.5f;
        rootRb.AddForce(deathForce, ForceMode.VelocityChange);
        rootRb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);

        // If the model has child bones with colliders, enable their Rigidbodies too
        Rigidbody[] childBodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in childBodies)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Freeze after a few seconds so corpse doesn't keep sliding
        Invoke(nameof(FreezeRagdoll), 3f);
    }

    private void FreezeRagdoll()
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in bodies)
        {
            rb.isKinematic = true;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HIT REACTION EFFECTS
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyHitFlash()
    {
        _flashTimer = FlashDuration;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].material != null)
                _renderers[i].material.color = hitFlashColor;
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].material != null && i < _originalColors.Length)
                _renderers[i].material.color = _originalColors[i];
        }
    }

    private void PlayHitSound()
    {
        if (_audio == null) return;

        if (hitSound != null)
        {
            _audio.PlayOneShot(hitSound, 0.7f);
        }
        else
        {
            // Procedural hit sound: short high-pitched blip
            PlayProceduralSound(800f, 0.08f);
        }
    }

    /// <summary>
    /// Creates a simple procedural beep/thud sound when no AudioClip is assigned.
    /// </summary>
    private void PlayProceduralSound(float frequency, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("ProceduralHit", sampleCount, 1, sampleRate, false);

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration); // Linear decay
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.3f;
        }
        clip.SetData(samples, 0);

        _audio.PlayOneShot(clip, 0.5f);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SEPARATION (Anti-Stacking)
    // ════════════════════════════════════════════════════════════════════════

    private void ApplySeparation()
    {
        if (_agent == null || !_agent.enabled) return;

        Collider[] neighbours = Physics.OverlapSphere(transform.position, separationRadius);
        Vector3 push = Vector3.zero;
        int pushCount = 0;

        foreach (Collider nb in neighbours)
        {
            if (nb.transform == transform) continue;
            if (nb.GetComponent<EnemyController>() == null) continue;

            Vector3 away = transform.position - nb.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < 0.01f)
            {
                // Overlapping: push in random direction
                away = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                dist = 0.01f;
            }

            float strength = 1f - Mathf.Clamp01(dist / separationRadius);
            push += away.normalized * strength;
            pushCount++;
        }

        if (pushCount > 0 && push.sqrMagnitude > 0.001f)
        {
            Vector3 offset = push.normalized * separationStrength * Time.deltaTime;
            Vector3 newDest = _agent.destination + offset;

            // Only adjust if agent has a valid path
            if (_agent.hasPath)
                _agent.SetDestination(newDest);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void TransitionTo(State newState)
    {
        _state = newState;
        if (newState == State.Idle && _agent != null && _agent.enabled)
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

    private Transform FindNearestTarget(float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;

            // Prioritize the player
            PlayerHealth ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                float d = Vector3.Distance(transform.position, ph.transform.position);
                // Heavy bias toward player: treat player distance as half actual
                if (d * 0.5f < bestDist) { bestDist = d * 0.5f; best = ph.transform; }
                continue;
            }

            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy != this && enemy._state != State.Dead)
            {
                float d = Vector3.Distance(transform.position, enemy.transform.position);
                if (d < bestDist) { bestDist = d; best = enemy.transform; }
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
}
