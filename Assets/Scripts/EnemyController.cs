using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Enemy AI with NavMeshAgent pathfinding, jump-over-obstacles, natural animations,
/// death animation before ragdoll, and proper weapon support.
///
/// Fix list (2026):
///   1. Jump — uses Mathf.Sqrt(jumpHeight * -2f * gravity) matching the Player formula.
///              Triggers when agent gets stuck with an obstacle in front.
///   2. Weapon attachment handled by LevelBuilder; EnemyController exposes weaponAttachPoint.
///   3. Animations — Speed parameter is normalised (0-1) and VelocityX/Z are set for blendtrees.
///   4. Death — plays "Dead" animation for deathAnimDuration seconds BEFORE ragdoll activates.
///   5. Victory — notifies GameManager via EnemyKilled() which now tracks totals correctly.
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

    [Header("Jump (Obstacle Avoidance)")]
    [Tooltip("Height the enemy jumps when blocked (matches Player formula).")]
    public float jumpHeight       = 1.8f;
    [Tooltip("Gravity value (negative). Must match Player gravity for same arc.")]
    public float gravity          = -25f;
    [Tooltip("Horizontal distance ahead to check for obstacles.")]
    public float obstacleCheckDist = 1.6f;
    [Tooltip("How long the enemy must be stuck before attempting a jump.")]
    public float stuckTime        = 1.2f;
    [Tooltip("Velocity below this is considered 'stuck' even when path exists.")]
    public float stuckVelocityThreshold = 0.25f;

    [Header("Separation (Anti-Stacking)")]
    public float separationRadius   = 2.0f;
    public float separationStrength = 5.0f;

    [Header("Hit Reaction")]
    public float flinchDuration = 0.25f;
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Death")]
    [Tooltip("Seconds the death animation plays before ragdoll activates.")]
    public float deathAnimDuration = 1.5f;
    [Tooltip("Seconds before corpse is cleaned up (0 = never).")]
    public float corpseLifetime    = 15f;
    [Tooltip("Upward force added when ragdolling.")]
    public float deathPopForce     = 2f;

    // ── State ────────────────────────────────────────────────────────────────
    private enum State { Idle, Chase, Attack, Flinch, Jumping, Dead }
    private State _state = State.Idle;

    private NavMeshAgent _agent;
    private Animator     _anim;
    private AudioSource  _audio;
    private Rigidbody    _rb;
    private int          _currentHealth;
    private float        _attackTimer;
    private Transform    _target;
    private bool         _lastHitByPlayer;

    // Flinch
    private float _flinchTimer;

    // Re-targeting
    private float _retargetTimer;
    private const float RetargetInterval = 0.3f;

    // Visual feedback
    private Renderer[] _renderers;
    private Color[]    _originalColors;
    private float      _flashTimer;
    private const float FlashDuration = 0.15f;

    // Jump state
    private float _stuckTimer;
    private bool  _isGrounded = true;
    private State _preJumpState;
    private HashSet<int> _animParameterHashes;

    // Animator hashes
    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    private static readonly int HashVelX      = Animator.StringToHash("VelocityX");
    private static readonly int HashVelZ      = Animator.StringToHash("VelocityZ");
    private static readonly int HashAttack    = Animator.StringToHash("Attack");
    private static readonly int HashDead      = Animator.StringToHash("Dead");
    private static readonly int HashHit       = Animator.StringToHash("Hit");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        _agent         = GetComponent<NavMeshAgent>();
        _anim          = GetComponentInChildren<Animator>();
        _currentHealth = maxHealth;
        EnsureAnimationEventSink();
        CacheAnimatorParameters();

        // Audio
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1f;
        _audio.playOnAwake  = false;

        // ── NavMeshAgent ──────────────────────────────────────────────────────
        if (_agent != null)
        {
            _agent.speed                  = moveSpeed;
            _agent.stoppingDistance       = attackRadius * 0.85f;
            _agent.angularSpeed           = 360f;
            _agent.acceleration           = 12f;
            _agent.avoidancePriority      = Random.Range(30, 70);
            _agent.obstacleAvoidanceType  = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            _agent.radius                 = 0.5f;
        }

        // ── Rigidbody for jump physics ────────────────────────────────────────
        // Kinematic by default so NavMeshAgent controls movement.
        // Becomes non-kinematic only during the jump arc.
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass              = 70f;
        _rb.linearDamping     = 0.5f;
        _rb.angularDamping    = 4f;
        _rb.interpolation     = RigidbodyInterpolation.Interpolate;
        _rb.constraints       = RigidbodyConstraints.FreezeRotation;
        _rb.isKinematic       = true;  // NavMeshAgent owns movement normally

        // Cache renderers for hit flash (URP-compatible: prefer _BaseColor over _Color)
        _renderers      = GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _renderers[i].material == null) continue;
            _originalColors[i] = GetMaterialBaseColor(_renderers[i].material);
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
            if (_flashTimer <= 0f) RestoreOriginalColors();
        }

        switch (_state)
        {
            case State.Idle:    UpdateIdle();    break;
            case State.Chase:   UpdateChase();   break;
            case State.Attack:  UpdateAttack();  break;
            case State.Flinch:  UpdateFlinch();  break;
            case State.Jumping: UpdateJumping(); break;
        }

        SyncAnimator();
    }

    // ── State handlers ────────────────────────────────────────────────────────
    private void UpdateIdle()
    {
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer > 0f) return;
        _retargetTimer = RetargetInterval;

        _target = FindNearestTarget(detectionRadius);
        if (_target != null) TransitionTo(State.Chase);
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
        ApplySeparation();

        // ── Stuck / Jump detection ──
        CheckAndJumpIfStuck();
    }

    private void UpdateAttack()
    {
        if (_target == null) { TransitionTo(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, _target.position);
        if (dist > attackRadius * 1.4f) { TransitionTo(State.Chase); return; }

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
            TransitionTo(_target != null ? State.Chase : State.Idle);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  JUMP — Issue #1
    //  Detects when the enemy is stuck against an obstacle and launches it
    //  upward using the same physics formula as the Player:
    //      verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity)
    // ════════════════════════════════════════════════════════════════════════

    private void CheckAndJumpIfStuck()
    {
        if (_agent == null || !_agent.enabled || !_agent.hasPath) return;

        float speed = _agent.velocity.magnitude;
        if (speed > stuckVelocityThreshold)
        {
            _stuckTimer = 0f; // moving fine — reset timer
            return;
        }

        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < stuckTime) return;
        _stuckTimer = 0f;

        // Is there actually an obstacle in front?
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        if (!Physics.Raycast(origin, transform.forward, obstacleCheckDist)) return;

        TryJump();
    }

    private void TryJump()
    {
        if (!_isGrounded) return;

        _preJumpState = _state;
        TransitionTo(State.Jumping);

        // Hand movement over to physics
        if (_agent != null) _agent.enabled = false;
        _rb.isKinematic = false;

        // Same formula the Player uses in Jump():
        //   verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity)
        float jumpVel = Mathf.Sqrt(jumpHeight * -2f * gravity); // gravity is negative

        // Carry current horizontal momentum forward so the enemy clears the obstacle
        Vector3 horizDir = _target != null
            ? ((_target.position - transform.position).normalized)
            : transform.forward;
        horizDir.y = 0f;
        horizDir.Normalize();

        _rb.linearVelocity = new Vector3(
            horizDir.x * chaseSpeed * 0.6f,
            jumpVel,
            horizDir.z * chaseSpeed * 0.6f);

        _isGrounded = false;

        SetAnimatorBool(HashGrounded, false);
    }

    private void UpdateJumping()
    {
        // Apply manual gravity to the Rigidbody while airborne so the arc
        // matches the Player's CharacterController behaviour.
        if (_rb != null && !_rb.isKinematic)
            _rb.linearVelocity += Vector3.up * gravity * Time.deltaTime;

        // Landing check: raycast downward from the character's feet
        bool hitGround = Physics.Raycast(
            transform.position + Vector3.up * 0.2f,
            Vector3.down,
            0.35f,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (hitGround && _rb != null && _rb.linearVelocity.y <= 0.1f)
        {
            _isGrounded = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic    = true;

            // Re-hand control back to NavMeshAgent
            if (_agent != null)
            {
                _agent.enabled = true;
                _agent.Warp(transform.position);
            }

            SetAnimatorBool(HashGrounded, true);

            // Resume what we were doing before the jump
            TransitionTo(_target != null ? _preJumpState : State.Idle);
        }
    }

    // ── Combat ────────────────────────────────────────────────────────────────
    private void ExecuteAttack()
    {
        SetAnimatorTrigger(HashAttack);

        if (_target == null) return;

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

    public void TakeDamage(int amount, bool byPlayer = false)
    {
        if (_state == State.Dead) return;
        if (byPlayer) _lastHitByPlayer = true;

        _currentHealth -= amount;
        Debug.Log($"[EnemyController] {name} hit for {amount} dmg — HP now {_currentHealth}/{maxHealth} (byPlayer={byPlayer})");

        // Flinch
        if (_state != State.Flinch)
        {
            _flinchTimer = flinchDuration;
            TransitionTo(State.Flinch);
            if (_agent != null && _agent.enabled) _agent.ResetPath();
        }

        ApplyHitFlash();
        SetAnimatorTrigger(HashHit);
        PlayHitSound();

        if (_currentHealth <= 0) Die();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DEATH — Issue #4
    //  Plays the "Dead" animation for deathAnimDuration seconds, THEN
    //  switches to ragdoll physics so the body falls naturally.
    //  The corpse persists for corpseLifetime seconds before Destroy().
    // ════════════════════════════════════════════════════════════════════════

    private void Die()
    {
        _state = State.Dead;

        // Notify GameManager FIRST so enemy count updates correctly (Issue #5)
        if (GameManager.Instance != null)
            GameManager.Instance.EnemyKilled(_lastHitByPlayer);

        // Stop all navigation immediately
        if (_agent != null) _agent.enabled = false;
        if (_rb != null)    _rb.isKinematic = true;

        // Disable main collider so living enemies don't trip over the corpse
        Collider mainCol = GetComponent<Collider>();
        if (mainCol != null) mainCol.enabled = false;

        // Play death sound
        if (_audio != null)
        {
            if (deathSound != null) _audio.PlayOneShot(deathSound);
            else PlayProceduralSound(200f, 0.4f);
        }

        // ── Phase 1: Death animation (animator stays ON) ──────────────────────
        if (_anim != null)
        {
            _anim.applyRootMotion = false; // prevent the anim sliding the corpse
            SetAnimatorTrigger(HashDead);
        }

        // ── Phase 2: Ragdoll fires after the animation plays (Issue #4 fix) ──
        Invoke(nameof(ActivateRagdoll), deathAnimDuration);

        // ── Corpse cleanup ────────────────────────────────────────────────────
        if (corpseLifetime > 0f)
            Destroy(gameObject, deathAnimDuration + corpseLifetime);
    }

    /// <summary>
    /// Converts the enemy to a full ragdoll: disables the Animator and lets
    /// Rigidbody physics take over every bone.
    /// </summary>
    private void ActivateRagdoll()
    {
        // Disable Animator now that the death clip has played
        if (_anim != null) _anim.enabled = false;

        // Activate root rigidbody
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass           = 50f;
        _rb.linearDamping  = 0.5f;
        _rb.angularDamping = 2f;
        _rb.isKinematic    = false;
        _rb.useGravity     = true;

        // Small random pop + tumble for visual variety
        Vector3 popForce = Vector3.up * deathPopForce + Random.insideUnitSphere * 1.5f;
        _rb.AddForce(popForce, ForceMode.VelocityChange);
        _rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);

        // If model has pre-configured bone Rigidbodies, wake them all up
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
        }

        // Freeze the ragdoll after a few seconds so it stops sliding
        Invoke(nameof(FreezeRagdoll), 3f);
    }

    private void FreezeRagdoll()
    {
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = true;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ANIMATIONS — Issue #3
    //  Speed is normalised to 0–1 so it maps cleanly onto animator blend
    //  trees without depending on raw world-speed units.
    //  VelocityX and VelocityZ drive left/right and forward/back separately
    //  so strafe animations play correctly on both legs.
    // ════════════════════════════════════════════════════════════════════════

    private void SyncAnimator()
    {
        if (_anim == null) return;

        // Determine actual velocity source
        Vector3 worldVelocity = Vector3.zero;
        if (_state == State.Jumping && _rb != null && !_rb.isKinematic)
            worldVelocity = _rb.linearVelocity;
        else if (_agent != null && _agent.enabled)
            worldVelocity = _agent.velocity;

        // Normalise speed to [0,1] using chaseSpeed as the reference maximum
        float normSpeed = Mathf.Clamp01(worldVelocity.magnitude / Mathf.Max(0.01f, chaseSpeed));

        // Local-space velocity for directional blend trees (left/right legs)
        Vector3 localVel = transform.InverseTransformDirection(worldVelocity);
        float normX = Mathf.Clamp(localVel.x / Mathf.Max(0.01f, chaseSpeed), -1f, 1f);
        float normZ = Mathf.Clamp(localVel.z / Mathf.Max(0.01f, chaseSpeed), -1f, 1f);

        SetAnimatorFloat(HashSpeed, normSpeed, 0.1f);
        SetAnimatorFloat(HashVelX, normX, 0.1f);
        SetAnimatorFloat(HashVelZ, normZ, 0.1f);
        SetAnimatorBool(HashGrounded, _isGrounded);
    }

    private void CacheAnimatorParameters()
    {
        _animParameterHashes = new HashSet<int>();

        if (_anim == null)
            return;

        foreach (AnimatorControllerParameter parameter in _anim.parameters)
            _animParameterHashes.Add(parameter.nameHash);
    }

    private void EnsureAnimationEventSink()
    {
        if (_anim == null)
            return;

        GameObject animatorHost = _anim.gameObject;
        if (animatorHost.GetComponent<AnimationEventSink>() == null)
            animatorHost.AddComponent<AnimationEventSink>();
    }

    private bool HasAnimatorParameter(int hash)
    {
        return _anim != null
            && _animParameterHashes != null
            && _animParameterHashes.Contains(hash);
    }

    private void SetAnimatorFloat(int hash, float value, float dampTime = 0f)
    {
        if (!HasAnimatorParameter(hash))
            return;

        if (dampTime > 0f)
            _anim.SetFloat(hash, value, dampTime, Time.deltaTime);
        else
            _anim.SetFloat(hash, value);
    }

    private void SetAnimatorBool(int hash, bool value)
    {
        if (HasAnimatorParameter(hash))
            _anim.SetBool(hash, value);
    }

    private void SetAnimatorTrigger(int hash)
    {
        if (HasAnimatorParameter(hash))
            _anim.SetTrigger(hash);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HIT REACTION EFFECTS
    // ════════════════════════════════════════════════════════════════════════

    // ── URP-safe color helpers ────────────────────────────────────────────────
    /// <summary>
    /// Returns the "main" colour of a material, checking URP's _BaseColor
    /// first and falling back to Standard's _Color.
    /// </summary>
    private static Color GetMaterialBaseColor(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color"))     return mat.GetColor("_Color");
        return Color.white;
    }

    /// <summary>Sets the main colour on a material (URP + Standard safe).</summary>
    private static void SetMaterialBaseColor(Material mat, Color c)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
    }

    private void ApplyHitFlash()
    {
        _flashTimer = FlashDuration;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _renderers[i].material == null) continue;
            SetMaterialBaseColor(_renderers[i].material, hitFlashColor);
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _renderers[i].material == null) continue;
            if (i < _originalColors.Length)
                SetMaterialBaseColor(_renderers[i].material, _originalColors[i]);
        }
    }

    private void PlayHitSound()
    {
        if (_audio == null) return;
        if (hitSound != null) _audio.PlayOneShot(hitSound, 0.7f);
        else PlayProceduralSound(800f, 0.08f);
    }

    private void PlayProceduralSound(float frequency, float duration)
    {
        int sampleRate  = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        AudioClip clip  = AudioClip.Create("ProceduralHit", sampleCount, 1, sampleRate, false);

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t        = (float)i / sampleRate;
            float envelope = 1f - (t / duration);
            samples[i]     = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.3f;
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
                away = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                dist = 0.01f;
            }

            float strength = 1f - Mathf.Clamp01(dist / separationRadius);
            push += away.normalized * strength;
            pushCount++;
        }

        if (pushCount > 0 && push.sqrMagnitude > 0.001f)
        {
            Vector3 offset  = push.normalized * separationStrength * Time.deltaTime;
            Vector3 newDest = _agent.destination + offset;
            if (_agent.hasPath) _agent.SetDestination(newDest);
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
        Collider[] hits    = Physics.OverlapSphere(transform.position, radius);
        Transform  best    = null;
        float      bestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;

            // Prioritise the player (treat distance as half so player is always preferred)
            PlayerHealth ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                float d = Vector3.Distance(transform.position, ph.transform.position) * 0.5f;
                if (d < bestDist) { bestDist = d; best = ph.transform; }
                continue;
            }

            // Enemies do NOT target other enemies — only the player is a valid target.
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

        // Show obstacle-check ray
        Gizmos.color = Color.magenta;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        Gizmos.DrawRay(origin, transform.forward * obstacleCheckDist);
    }
}
