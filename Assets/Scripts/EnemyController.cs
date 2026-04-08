using UnityEngine;
using UnityEngine.AI;
using System.Collections;
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
public class EnemyController : MonoBehaviour, IDamageable
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

    [Header("Weapon")]
    [Tooltip("Drag the enemy's right-hand bone here in the Inspector (optional). " +
             "If null, EquipmentManager auto-detects bip_hand_R / weapon_bone_R.")]
    public Transform weaponAttachPoint;

    [HideInInspector] public GameObject equippedWeaponObject;

    [Header("FFA Target Detection")]
    [Tooltip("Layer mask for valid targets (set to 'Character' layer).")]
    public LayerMask detectionMask = ~0;

    [Tooltip("Seconds between target scans (coroutine-based for performance).")]
    public float detectionInterval = 0.5f;

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

    // FFA: track whoever last damaged this enemy so we can retaliate
    private Transform _lastAttacker;

    // Coroutine-based target scanning
    private Coroutine _scanCoroutine;

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

        // ── Auto-resolve Character layer mask ────────────────────────────────
        int charLayer = LayerMask.NameToLayer("Character");
        if (charLayer >= 0)
            detectionMask = 1 << charLayer;
    }

    private void Start()
    {
        EnsureProperMaterial(gameObject);
        AssignMaterial();
        // Start the coroutine-based target scanner (runs every detectionInterval)
        _scanCoroutine = StartCoroutine(TargetScanLoop());
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
        // Target is assigned by the TargetScanLoop coroutine every 0.5s
        if (_target != null) TransitionTo(State.Chase);
    }

    private void UpdateChase()
    {
        // If the target died or was destroyed, go idle and wait for next scan
        if (_target == null)
        {
            TransitionTo(State.Idle);
            return;
        }

        // Check if target is still alive via IDamageable
        IDamageable targetDmg = _target.GetComponentInParent<IDamageable>();
        if (targetDmg != null && !targetDmg.IsAlive)
        {
            _target = null;
            TransitionTo(State.Idle);
            return;
        }

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

        // Use IDamageable so we can hit player OR enemy without type-checking
        IDamageable target = _target.GetComponentInParent<IDamageable>();
        if (target != null && target.IsAlive)
        {
            target.ReceiveDamage((int)attackDamage, gameObject);
            return;
        }

        // Legacy fallback for Actor-based entities not yet using IDamageable
        Actor actor = _target.GetComponentInParent<Actor>();
        if (actor != null) actor.TakeDamage((int)attackDamage);
    }

    public void TakeDamage(int amount, bool byPlayer = false)
    {
        if (_state == State.Dead) return;
        if (byPlayer) _lastHitByPlayer = true;

        _currentHealth -= amount;

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

    // ── IDamageable ─────────────────────────────────────────────────────────
    public bool IsAlive => _state != State.Dead;

    public void ReceiveDamage(int amount, GameObject attackerRoot)
    {
        // Record attacker so the FFA scan can retaliate on the next tick
        if (attackerRoot != null)
            _lastAttacker = attackerRoot.transform;

        bool fromPlayer = attackerRoot != null
                       && attackerRoot.GetComponentInParent<PlayerHealth>() != null;
        TakeDamage(amount, byPlayer: fromPlayer);
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

        // Stop target scan coroutine — no point scanning when dead
        if (_scanCoroutine != null)
        {
            StopCoroutine(_scanCoroutine);
            _scanCoroutine = null;
        }

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

    private void AssignMaterial()
    {
        // Try explicit paths first, then search all Materials in Resources as a fallback.
        Material material = Resources.Load<Material>("Materials/Enemy");
        if (material == null)
            material = Resources.Load<Material>("Materials/Ronin");
        if (material == null)
        {
            // Broad fallback: find any material whose name contains "Enemy" or "Ronin"
            foreach (Material m in Resources.LoadAll<Material>("Materials"))
            {
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                if (n.Contains("enemy") || n.Contains("ronin") || n.Contains("soldier"))
                {
                    material = m;
                    break;
                }
            }
        }

        if (material == null)
        {
            // Last resort: create a visible default so the enemy isn't a white statue
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = new Color(0.25f, 0.35f, 0.25f); // military green
        }

        // Ensure AnimationEventSink is present to prevent CS0246 event-method errors
        EnsureAnimationEventSink();

        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null) continue;
            smr.material = material;
        }
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

    // ════════════════════════════════════════════════════════════════════════
    //  FFA TARGET SCANNING — "All-Out War" logic
    //
    //  Priority system (evaluated each scan tick):
    //    1. ENEMY AGGRESSOR — any other enemy currently targeting this enemy.
    //    2. RETALIATE      — last enemy that damaged us, if still valid.
    //    3. NEAREST ENEMY  — any living enemy in detection range.
    //    4. ARENA FALLBACK — nearest living enemy anywhere in the arena.
    //    5. PLAYER         — only if no enemy target is available.
    //
    //  This stops all enemies from hard-locking onto the player and creates a
    //  proper arena-wide free-for-all.
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator TargetScanLoop()
    {
        // Stagger start so all 11 enemies don't scan on the same frame
        yield return new WaitForSeconds(Random.Range(0f, detectionInterval));

        while (true)
        {
            if (_state != State.Dead)
            {
                float radius = (_state == State.Chase)
                    ? detectionRadius * 1.5f
                    : detectionRadius;

                _target = FindFfaTarget(radius);
            }

            yield return new WaitForSeconds(detectionInterval);
        }
    }

    /// <summary>
    /// Free-for-All targeting with attacker retaliation.
    /// Enemies prioritise fighting OTHER ENEMIES over the player.
    /// </summary>
    private Transform FindFfaTarget(float radius)
    {
        // Use ~0 (all layers) so enemies detect EACH OTHER and the player equally.
        // The previous detectionMask (Character layer only) caused all enemies to
        // target only the player when enemies were on a different layer.
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);

        Transform nearest = null;
        float nearestDistSqr = float.MaxValue;
        var evaluated = new HashSet<int>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform == transform) continue;

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) continue;
            if (dmg.gameObject == gameObject) continue;

            int id = dmg.gameObject.GetInstanceID();
            if (evaluated.Contains(id)) continue;
            evaluated.Add(id);

            float d2 = (dmg.transform.position - transform.position).sqrMagnitude;
            if (d2 < nearestDistSqr)
            {
                nearestDistSqr = d2;
                nearest = dmg.transform;
            }
        }

        return nearest;
    }

    private Transform FindNearestLivingEnemy()
    {
        EnemyController[] allEnemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (EnemyController otherEnemy in allEnemies)
        {
            if (otherEnemy == null || otherEnemy == this || !otherEnemy.IsAlive)
                continue;

            float dist = Vector3.Distance(transform.position, otherEnemy.transform.position);
            if (dist < nearestDistance)
            {
                nearestDistance = dist;
                nearest = otherEnemy.transform;
            }
        }

        return nearest;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WEAPON ATTACHMENT — Ghost Melee Fix
    //  Searches for "bip_hand_R" by name first (Crosby rig), then falls back
    //  to other common bone names.  After parenting, localScale is ALWAYS
    //  forced to (0.1, 0.1, 0.1) so the weapon is visible regardless of
    //  the skeleton's import scale.
    // ════════════════════════════════════════════════════════════════════════

    public void AttachWeaponToHand(GameObject weaponPrefab, float targetSize = 0.5f)
    {
        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
            equippedWeaponObject = null;
        }

        if (weaponPrefab == null) return;

        // Strict hand socketing: Humanoid RightHand first.
        Transform handBone = weaponAttachPoint;
        if (handBone == null && _anim != null && _anim.isHuman)
            handBone = _anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (handBone == null)
            handBone = FindHandBone(gameObject);

        if (handBone == null)
        {
            handBone = transform;
            Debug.LogWarning($"[EnemyController] '{name}': bip_hand_R not found. " +
                             "Weapon attached to root. Drag the hand bone into 'Weapon Attach Point'.");
        }

        // ── 2. Instantiate WITHOUT parenting first (avoids inherited scale) ──
        equippedWeaponObject = Instantiate(weaponPrefab);
        equippedWeaponObject.name = "WeaponModel";

        // ── 3. Parent to hand bone with worldPositionStays=false ─────────────
        equippedWeaponObject.transform.SetParent(handBone, worldPositionStays: false);

        // STRICT: force weapon into the palm — overrides all rig/import scale inheritance.
        equippedWeaponObject.transform.localPosition = Vector3.zero;
        equippedWeaponObject.transform.localRotation = Quaternion.identity;
        equippedWeaponObject.transform.localScale    = Vector3.one * 0.1f;
        equippedWeaponObject.SetActive(true);

        // ── 6. Activate every child renderer ─────────────────────────────────
        foreach (Transform t in equippedWeaponObject.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);

        // Disable physics so weapon never flies/drifts away.
        foreach (Rigidbody rb in equippedWeaponObject.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable weapon animators so they don't apply their own motion on top of hand socketing.
        foreach (Animator weaponAnimator in equippedWeaponObject.GetComponentsInChildren<Animator>(true))
            weaponAnimator.enabled = false;

        // Disable colliders to prevent pushing/physics conflicts with enemy body.
        foreach (Collider col in equippedWeaponObject.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // ── 7. Wire WeaponBase ────────────────────────────────────────────────
        WeaponBase wb = equippedWeaponObject.GetComponent<WeaponBase>();
        if (wb == null) wb = equippedWeaponObject.AddComponent<WeaponBase>();
        wb.damage      = (int)attackDamage;
        wb.attackRange = attackRadius;
        wb.isRanged    = false;

        // ── 8. Ensure renderers are visible (URP shadow-caster fix) ──────────
        if (equippedWeaponObject.GetComponent<WeaponVisibilityFix>() == null)
            equippedWeaponObject.AddComponent<WeaponVisibilityFix>();
    }

    // ── Bone search: "bip_hand_R" first, then common fallback names ──────────
    private static readonly string[] HandBoneNames =
    {
        "bip_hand_R",           // Crosby (enemy) — must be first
        "weapon_bone_R",        // Crosby weapon socket
        "j_wrist_ri",           // Ronin (player)
        "mixamorig:RightHand",  // Mixamo
        "RightHand",
        "Hand_R", "hand_R", "hand_r",
        "Wrist_R", "wrist_R",
    };

    private static Transform FindHandBone(GameObject body)
    {
        // ── Priority 1: Humanoid avatar API (most reliable for ANY humanoid rig) ──
        Animator anim = body.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            Transform bone = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null)
            {
                Debug.Log($"[EnemyController] Hand bone found via Animator API: '{bone.name}'");
                return bone;
            }
        }

        // ── Priority 2: Name-based search (fallback for non-humanoid rigs) ──
        foreach (string boneName in HandBoneNames)
        {
            Transform found = FindBoneByName(body.transform, boneName);
            if (found != null)
            {
                Debug.Log($"[EnemyController] Hand bone found via name search: '{found.name}'");
                return found;
            }
        }
        return null;
    }

    private static Transform FindBoneByName(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneByName(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MATERIAL FIX — "White Statue" prevention
    //  Loads RoninTexture from Resources/Textures.  If the texture is found it
    //  is applied to every blank/missing material slot on the character mesh.
    //  Falls back to a warm skin-tone colour when the file is absent.
    // ════════════════════════════════════════════════════════════════════════

    private static void EnsureProperMaterial(GameObject body)
    {
        if (body == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Diffuse");
        if (shader == null) return;

        // Folders where Crosby/enemy textures live
        string[] enemyTextureFolders = { "Enemy/textures/" };

        // Fallback: load the best generic enemy texture
        Texture2D fallbackTex = LoadFirstAvailableTexture(
            "Enemy/textures/mtl_c_usa_mp_seal6_gear_wt",
            "Enemy/textures/mtl_c_usa_mp_seal6_ass_vest_green_wt",
            "Enemy/textures/mtl_c_usa_mp_seal6_pants_1_green_wt",
            "Enemy/textures/mtl_c_usa_mp_seal6_helmet_wt",
            "Enemy/textures/mtl_c_usa_mp_seal6_bala_wt");

        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!IsBlankMaterial(slots[i])) continue;

                // Try to match a texture to this material's name
                string slotName = (slots[i] != null) ? slots[i].name : "";
                Texture2D slotTex = TryLoadTextureForMaterial(slotName, enemyTextureFolders);
                if (slotTex == null) slotTex = fallbackTex;

                Material newMat = new Material(shader)
                {
                    name = string.IsNullOrEmpty(slotName) ? "EnemyMat_Runtime" : slotName + "_Fix"
                };
                if (slotTex != null)
                {
                    if (newMat.HasProperty("_BaseMap")) newMat.SetTexture("_BaseMap", slotTex);
                    if (newMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", slotTex);
                }
                else
                {
                    // Warm tan fallback so they don't look white
                    Color tan = new Color(0.68f, 0.52f, 0.38f);
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", tan);
                    if (newMat.HasProperty("_Color"))     newMat.SetColor("_Color",     tan);
                }
                slots[i] = newMat;
                changed = true;
            }
            if (changed) r.sharedMaterials = slots;
        }
    }

    /// <summary>
    /// Given a material name (e.g. "mtl_c_usa_mp_seal6_gear_wt"),
    /// try to find a matching texture in the enemy textures folder.
    /// </summary>
    private static Texture2D TryLoadTextureForMaterial(string matName, string[] folders)
    {
        if (string.IsNullOrEmpty(matName)) return null;
        string clean = matName.Replace(" (Instance)", "").Trim();
        if (string.IsNullOrEmpty(clean)) return null;

        foreach (string folder in folders)
        {
            Texture2D tex;
            tex = Resources.Load<Texture2D>(folder + clean);       if (tex != null) return tex;
            tex = Resources.Load<Texture2D>(folder + clean + "_c"); if (tex != null) return tex;
        }
        return null;
    }

    private static bool IsBlankMaterial(Material m)
    {
        if (m == null) return true;
        if (m.name.StartsWith("Default-")) return true;
        if (m.shader != null && m.shader.name == "Hidden/InternalErrorShader") return true;
        bool hasTexture = (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                       || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
        if (hasTexture) return false;
        Color c = Color.white;
        if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
        else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
        return c.r > 0.95f && c.g > 0.95f && c.b > 0.95f;
    }

    private static Texture2D LoadFirstAvailableTexture(params string[] resourcePaths)
    {
        for (int i = 0; i < resourcePaths.Length; i++)
        {
            Texture2D tex = Resources.Load<Texture2D>(resourcePaths[i]);
            if (tex != null) return tex;
        }
        return null;
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
