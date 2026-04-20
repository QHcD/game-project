using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
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

    [Header("Movement — mirrors Player feel")]
    [Tooltip("Base walking speed (matches PlayerController.moveSpeed = 3.5).")]
    public float moveSpeed        = 3.5f;
    [Tooltip("Chase/sprint speed — slightly faster than the player's sprint (4.8) for aggression.")]
    public float chaseSpeed       = 5.8f;
    [Tooltip("Manual rotation Slerp rate in Attack state (player body uses 8f — we bump slightly for responsiveness).")]
    public float rotationSpeed    = 12f;
    [Tooltip("NavMeshAgent acceleration. Raised to 40 so the agent reaches full speed in ~0.15 s — matches the player's snappy feel.")]
    public float agentAcceleration = 40f;
    [Tooltip("NavMeshAgent angular speed (deg/sec). 1080 = full turn in ~0.33 s, matching the player body Slerp.")]
    public float agentAngularSpeed = 1080f;

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
    [Tooltip("Local grip position offset applied after the weapon is parented to the right hand socket.")]
    public Vector3 weaponGripLocalPosition = new Vector3(-0.01f, -0.0025f, 0f);
    [Tooltip("Local grip rotation offset in degrees so the blade/head extends forward from the hand.")]
    [FormerlySerializedAs("weaponGripLocalEuler")]
    public Vector3 weaponGripLocalEulerAngles = new Vector3(0f, 0f, 90f);
    [Tooltip("Optional local socket rotation normalization applied before per-weapon grip offsets.")]
    public Vector3 weaponSocketLocalEulerAngles = Vector3.zero;
    [Tooltip("When enabled, continuously removes the animated hand bone basis so the weapon can keep a player-matched pose on Crosby.")]
    public bool stabilizeWeaponSocketAgainstHandPose = false;

    [HideInInspector] public GameObject equippedWeaponObject;

    [Header("FFA Target Detection")]
    [Tooltip("Layer mask for valid targets (set to 'Character' layer).")]
    public LayerMask detectionMask = ~0;

    [Tooltip("Seconds between target scans (coroutine-based for performance).")]
    public float detectionInterval = 0.2f;

    [Header("Target Locking & LoS")]
    [Tooltip("How long (seconds) the AI commits to a target before considering switching.")]
    public float targetLockDuration = 3.5f;
    [Tooltip("A new candidate target must be this many metres closer than the current one to steal focus.")]
    public float targetSwitchHysteresis = 2.0f;
    [Tooltip("Extra metres added to attackRadius when deciding it is time to swing.")]
    public float attackRangePadding = 0.35f;
    [Tooltip("Extra metres beyond attack radius before the attacker breaks off to chase again (hysteresis).")]
    public float breakAttackPadding = 0.9f;
    [Tooltip("Eye height used for line-of-sight raycasts.")]
    public float lineOfSightEyeHeight = 1.2f;
    [Tooltip("Layers that block line of sight. Leave empty to disable LoS checks (always visible).")]
    public LayerMask lineOfSightBlockers = 0;
    [Tooltip("Max seconds the 'last attacker' retaliation priority remains hot.")]
    public float retaliationMemory = 6f;

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
  // ── State ────────────────────────────────────────────────────────────────
    private enum State { Idle, Chase, Attack, Flinch, Jumping, Dead }
    private State _state = State.Idle;

    private NavMeshAgent _agent;
    private Animator     _anim;
    private AudioSource  _audio;
    private Rigidbody    _rb;
    private int          _currentHealth;

    // CharacterController is intentionally disabled at runtime — kept as a
    // reference only so the jump system can toggle it during the arc.
    private CharacterController _controller;
    private float        _attackTimer;
    private Transform    _target;
    private bool         _lastHitByPlayer;

    // Flinch
    private float _flinchTimer;

    // FFA: track whoever last damaged this enemy so we can retaliate
    private Transform _lastAttacker;
    private float     _lastAttackerTime = -999f;

    // Target lock / commitment timer
    private float _targetLockTimer;

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
    private Transform _activeWeaponSocket;
    private Transform _activeWeaponHandBone;

    // Position-delta velocity (same technique the PlayerController uses)
    private Vector3 _lastFramePosition;

    // Animator hashes
    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    private static readonly int HashVelX      = Animator.StringToHash("VelocityX");
    private static readonly int HashVelZ      = Animator.StringToHash("VelocityZ");
    private static readonly int HashAttack    = Animator.StringToHash("Attack");
    private static readonly int HashDead      = Animator.StringToHash("Dead");
    private static readonly int HashHit       = Animator.StringToHash("Hit");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");
    private const string WeaponSocketName     = "__EnemyWeaponSocket";

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
        // The agent FULLY owns position AND rotation. Tuning below is chosen
        // to mirror the PlayerController feel (MoveTowards with acceleration
        // = 12, body rotation Slerp at 8f). With autoBraking DISABLED the
        // enemy charges into melee range at full speed instead of ramping
        // down — this is the single biggest "sluggish near target" fix.
        if (_agent != null)
            ConfigureAgent();

        // ── Disable any CharacterController so it never fights the agent ─────
        _controller = GetComponent<CharacterController>();
        if (_controller != null) _controller.enabled = false;

        // ── Animator: disable root motion so NavMeshAgent drives all movement ─
        if (_anim != null)
            _anim.applyRootMotion = false;

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

        // ── Detection mask ────────────────────────────────────────────────────
        // Use ALL physics layers so the player is always detectable regardless
        // of which Unity layer they are assigned to.  The IDamageable check in
        // FindFfaTarget() already filters out non-damageable objects, so it is
        // safe to cast against everything.  Restricting to a single named layer
        // was the primary cause of the "enemy stands still / brain-dead" bug —
        // if the player is on Default (layer 0) instead of "Character", the
        // OverlapSphere returned zero hits and the enemy never acquired a target.
        detectionMask = ~0;
    }

    private void Start()
    {
        // LevelBuilder assigns maxHealth/chaseSpeed after AddComponent(), so
        // re-apply runtime state here once the spawner has finished tuning us.
        _currentHealth = maxHealth;
        ConfigureAgent();

        _lastFramePosition = transform.position;
        EnsureProperMaterial(gameObject);
        AssignMaterial();

        // ── Warp the agent onto the NavMesh ──────────────────────────────────
        // Enemies that spawn even a centimetre off-mesh will refuse to move.
        // Sample the nearest valid NavMesh point and warp the agent to it.
        if (_agent != null && _agent.enabled)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            else
                _agent.Warp(transform.position);
        }

        // Start the coroutine-based target scanner
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

    private void LateUpdate()
    {
        if (!stabilizeWeaponSocketAgainstHandPose)
            return;

        if (_activeWeaponSocket == null || _activeWeaponHandBone == null)
            return;

        _activeWeaponSocket.localRotation =
            Quaternion.Inverse(_activeWeaponHandBone.localRotation) *
            Quaternion.Euler(weaponSocketLocalEulerAngles);
    }

    // ── State handlers ────────────────────────────────────────────────────────
    //
    //  Philosophy: the NavMeshAgent owns EVERYTHING movement-related.
    //  State handlers are tiny — they only set destinations, toggle
    //  isStopped, and pick targets. No manual gravity, no CharacterController,
    //  no velocity smoothing. The agent's internal acceleration curve drives
    //  the feel (and we tune it aggressively in Awake for snappy reactions).
    //
    private void UpdateIdle()
    {
        if (_agent != null && _agent.enabled)
            _agent.isStopped = true;

        // Immediately react to any valid target (no wait for scan tick).
        if (IsTargetValid(_target))
            TransitionTo(State.Chase);
    }

    private void UpdateChase()
    {
        // ── Validation ───────────────────────────────────────────────────────
        if (!IsTargetValid(_target))
        {
            _target = null;
            TransitionTo(State.Idle);
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        // Enter attack range. Padding so we commit rather than hover at the edge.
        float engageDist = attackRadius + attackRangePadding;
        if (dist <= engageDist)
        {
            TransitionTo(State.Attack);
            return;
        }

        // ── Drive the agent directly ────────────────────────────────────────
        if (_agent != null && _agent.enabled)
        {
            if (_agent.isStopped) _agent.isStopped = false;

            // ── Dynamic speed: exceed the player's actual sprint speed ───────
            // Read the player's current max speed at runtime so a future tweak
            // to PlayerController.moveSpeed / sprintMultiplier is automatically
            // reflected here — no manual sync needed.
            float targetSpeed = chaseSpeed;
            PlayerController pc = _target != null
                ? _target.GetComponentInParent<PlayerController>() : null;
            if (pc != null)
            {
                // Player sprint = moveSpeed * sprintMultiplier ≈ 4.8 u/s.
                // We add a 1.5 u/s margin so the enemy always closes the gap.
                float playerTopSpeed = pc.moveSpeed * pc.sprintMultiplier;
                targetSpeed = Mathf.Max(chaseSpeed, playerTopSpeed + 1.5f);
            }

            _agent.speed            = targetSpeed;
            _agent.acceleration     = agentAcceleration;
            _agent.angularSpeed     = agentAngularSpeed;
            _agent.autoBraking      = false;
            _agent.stoppingDistance = Mathf.Max(0.1f, attackRadius * 0.5f);

            // ── High-frequency destination refresh ───────────────────────────
            // Old threshold was 0.6 sqrMag (≈ 0.77 m) guarded by !pathPending.
            // Problem: if a path is always computing (pathPending == true) the
            // destination was NEVER updated, making the enemy chase a stale
            // position while the player sprinted away.
            //
            // Fix:
            //   • Threshold → 0.04 sqrMag (≈ 0.2 m) so fast players are tracked
            //     almost immediately.
            //   • pathPending guard removed: re-issuing SetDestination while a
            //     path is computing is safe on Unity 6 and overwrites the pending
            //     request with the fresher target position.
            bool needsPath =
                !_agent.hasPath ||
                _agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                (_agent.destination - _target.position).sqrMagnitude > 0.04f;

            if (needsPath)
                _agent.SetDestination(_target.position);
        }

        CheckAndJumpIfStuck();
    }

    private void UpdateAttack()
    {
        if (!IsTargetValid(_target))
        {
            _target = null;
            TransitionTo(State.Idle);
            return;
        }

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            // Zero velocity so the agent stops IMMEDIATELY — no drift-slide
            // past the target while the swing animation plays.
            _agent.velocity  = Vector3.zero;
        }

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // Hysteresis: only leave Attack when the target has moved noticeably
        // outside the engagement ring — prevents Chase↔Attack thrash.
        float breakDist = attackRadius + attackRangePadding + breakAttackPadding;
        if (dist > breakDist)
        {
            TransitionTo(State.Chase);
            return;
        }

        // Manual snappy rotation (agent is stopped, so it won't rotate itself).
        FaceDirection(toTarget);

        if (_attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            ExecuteAttack();
        }
    }

    private void UpdateFlinch()
    {
        _flinchTimer -= Time.deltaTime;

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity  = Vector3.zero;
        }

        if (_flinchTimer <= 0f)
            TransitionTo(IsTargetValid(_target) ? State.Chase : State.Idle);
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
        if (_controller != null) _controller.enabled = false;
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

            // Re-hand control back to NavMeshAgent (which owns movement)
            if (_agent != null)
            {
                _agent.enabled = true;
                _agent.Warp(transform.position);
            }
            // NOTE: do NOT re-enable _controller — it would fight the agent
            // (the same hybrid that caused the original sluggish/jitter bug).
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
        // ── Record attacker & retaliate immediately ─────────────────────────
        if (attackerRoot != null && attackerRoot != gameObject)
        {
            Transform attackerT = attackerRoot.transform;

            // Walk up to the IDamageable root so we lock onto the correct
            // Transform (not a weapon hitbox child).
            IDamageable atkDmg = attackerRoot.GetComponentInParent<IDamageable>();
            if (atkDmg != null) attackerT = atkDmg.transform;

            _lastAttacker     = attackerT;
            _lastAttackerTime = Time.time;

            // Immediate retaliation: if we were idle, chasing someone far, or
            // our current target is dead/invalid, snap onto the attacker.
            if (IsTargetValid(attackerT) &&
                (!IsTargetValid(_target) || _target == null || _target == attackerT ||
                 Vector3.Distance(transform.position, attackerT.position) <
                 Vector3.Distance(transform.position, _target.position) - 0.5f))
            {
                _target          = attackerT;
                _targetLockTimer = targetLockDuration;
            }
        }

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

        // ══════════════════════════════════════════════════════════════════════
        //  NAVMESHAGENT VELOCITY SYNC
        //  ──────────────────────────
        //  Now that the NavMeshAgent owns position AND rotation, its
        //  `velocity` field is the single source of truth for locomotion
        //  animation. This eliminates the desync between the walk-cycle
        //  foot-speed and the visible displacement that the old position-delta
        //  method suffered when the agent was fighting a CharacterController.
        // ══════════════════════════════════════════════════════════════════════

        Vector3 agentVelocity = (_agent != null && _agent.enabled)
            ? _agent.velocity
            : Vector3.zero;
        agentVelocity.y = 0f;

        float actualSpeed = agentVelocity.magnitude;

        // Normalise against the current movement ceiling.
        float maxSpeed        = Mathf.Max(0.01f, chaseSpeed);
        float normalizedSpeed = Mathf.Clamp01(actualSpeed / maxSpeed);

        // Keep _lastFramePosition updated for any legacy callers.
        _lastFramePosition = transform.position;

        // ── Drive the "Speed" parameter (Float → blend tree) ─────────────
        // Tight damp (0.05) keeps the locomotion tree snappy — matches the
        // player's position-delta responsiveness without visible jitter.
        bool droveSpeed = SetAnimatorFloatChecked(HashSpeed, normalizedSpeed, 0.05f);

        // Fallback: if the animator has no "Speed" parameter, force-crossfade
        // into the correct state directly (same fallback PlayerController uses)
        if (!droveSpeed)
            ForceLocomotionState(normalizedSpeed);

        // ── Directional blend-tree params (VelocityX / VelocityZ) ────────
        if (actualSpeed > 0.05f)
        {
            Vector3 localVel = transform.InverseTransformDirection(
                agentVelocity.normalized * normalizedSpeed);
            SetAnimatorFloat(HashVelX, Mathf.Clamp(localVel.x, -1f, 1f), 0.05f);
            SetAnimatorFloat(HashVelZ, Mathf.Clamp(localVel.z, -1f, 1f), 0.05f);
        }
        else
        {
            SetAnimatorFloat(HashVelX, 0f, 0.05f);
            SetAnimatorFloat(HashVelZ, 0f, 0.05f);
        }

        SetAnimatorBool(HashGrounded, _isGrounded);
    }

    /// <summary>
    /// Fallback for animator controllers that lack a "Speed" float parameter.
    /// Directly CrossFades into "Walk" or "Idle" states — same technique the
    /// PlayerController.ForceLocomotionState() uses.
    /// </summary>
    private void ForceLocomotionState(float normalizedSpeed)
    {
        if (_anim == null || _anim.runtimeAnimatorController == null) return;

        string stateName     = normalizedSpeed > 0.05f ? "Walk" : "Idle";
        string fullStateName = "Base Layer." + stateName;
        int    stateHash     = Animator.StringToHash(fullStateName);

        if (!_anim.HasState(0, stateHash)) return;

        AnimatorStateInfo current = _anim.GetCurrentAnimatorStateInfo(0);
        if (current.fullPathHash == stateHash) return;

        _anim.CrossFadeInFixedTime(fullStateName, 0.15f, 0);
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

        if (animatorHost.GetComponent<MeleeAnimationEventSink>() == null)
            animatorHost.AddComponent<MeleeAnimationEventSink>();
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

    /// <summary>Same as SetAnimatorFloat but returns true if the parameter existed.</summary>
    private bool SetAnimatorFloatChecked(int hash, float value, float dampTime = 0f)
    {
        if (!HasAnimatorParameter(hash))
            return false;

        if (dampTime > 0f)
            _anim.SetFloat(hash, value, dampTime, Time.deltaTime);
        else
            _anim.SetFloat(hash, value);
        return true;
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
    //  NavMeshAgent.obstacleAvoidanceType = HighQualityObstacleAvoidance
    //  + randomized avoidancePriority (set in Awake) handles enemy-vs-enemy
    //  separation natively now that the agent owns movement. No manual
    //  push-vector math required.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>True if the transform points at something still alive we can damage.</summary>
    private bool IsTargetValid(Transform t)
    {
        if (t == null) return false;
        IDamageable dmg = t.GetComponentInParent<IDamageable>();
        return dmg != null && dmg.IsAlive;
    }

    /// <summary>Raycast-based LoS. Returns true if no blocker layer is set.</summary>
    private bool HasLineOfSight(Transform t)
    {
        if (t == null) return false;
        if (lineOfSightBlockers.value == 0) return true;

        Vector3 from = transform.position + Vector3.up * lineOfSightEyeHeight;
        Vector3 to   = t.position + Vector3.up * lineOfSightEyeHeight;
        Vector3 dir  = to - from;
        float   d    = dir.magnitude;
        if (d < 0.05f) return true;

        return !Physics.Raycast(
            from, dir / d, d, lineOfSightBlockers, QueryTriggerInteraction.Ignore);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void ConfigureAgent()
    {
        if (_agent == null) return;

        _agent.speed                 = chaseSpeed;
        _agent.stoppingDistance      = Mathf.Max(0.1f, attackRadius * 0.5f);
        _agent.acceleration          = agentAcceleration;
        _agent.angularSpeed          = agentAngularSpeed;
        _agent.avoidancePriority     = Random.Range(30, 70);
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _agent.radius                = 0.45f;
        _agent.autoBraking           = false;
        _agent.autoRepath            = true;
        _agent.updatePosition        = true;
        _agent.updateRotation        = true;
    }

    private void TransitionTo(State newState)
    {
        State prevState = _state;
        _state = newState;

        if (_agent == null || !_agent.enabled) return;

        switch (newState)
        {
            case State.Chase:
                // Re-engage the agent and immediately seed a destination so
                // there is never a frame where the agent reports no path.
                _agent.isStopped = false;
                if (IsTargetValid(_target))
                    _agent.SetDestination(_target.position);
                break;

            case State.Attack:
                // Stop in place BUT keep the last path so we can resume
                // chasing instantly on hysteresis break without a re-path delay.
                _agent.isStopped = true;
                _agent.velocity  = Vector3.zero;
                break;

            case State.Idle:
            case State.Flinch:
                _agent.isStopped = true;
                _agent.velocity  = Vector3.zero;
                if (prevState != State.Flinch) _agent.ResetPath();
                break;

            case State.Jumping:
                // Jump handler takes over the agent entirely.
                break;
        }
    }

    private void FaceTarget()
    {
        if (_target == null) return;
        FaceDirection(_target.position - transform.position);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        // ★ Exact port of PlayerController's body-rotation Slerp:
        //     rot = Slerp(rot, Euler(0, targetY, 0), rate * dt)
        // Using only the Y-axis of the target rotation keeps the enemy
        // upright even if the target is on uneven ground.
        float targetY = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y;
        Quaternion targetRot = Quaternion.Euler(0f, targetY, 0f);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FFA TARGET SCANNING — "All-Out War" logic with Target Locking
    //
    //  Each scan tick (detectionInterval seconds):
    //    1. Decrement the lock timer.
    //    2. If our current target is still valid AND lock > 0 AND still in a
    //       reasonable range → KEEP IT. (No more flip-flopping every tick.)
    //    3. If recent attacker memory is hot and the attacker is valid → that
    //       overrides lock and becomes our new target.
    //    4. Otherwise scan for the closest valid threat (enemy OR player) and
    //       only switch if the new candidate is meaningfully closer (hysteresis).
    //
    //  This creates stable engagements — enemies commit to a fight instead of
    //  constantly re-evaluating targets every frame.
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator TargetScanLoop()
    {
        // Stagger scan start so all spawned enemies don't all scan on the
        // same frame (tiny perf win + prevents synchronized destination thrash).
        yield return new WaitForSeconds(Random.Range(0f, detectionInterval));

        var wait = new WaitForSeconds(detectionInterval);
        while (true)
        {
            if (_state != State.Dead)
                EvaluateTargets();
            yield return wait;
        }
    }

    private void EvaluateTargets()
    {
        _targetLockTimer -= detectionInterval;

        bool currentValid = IsTargetValid(_target);

        // ── 1. Retaliation priority ─────────────────────────────────────────
        // If we were hit recently and the attacker is still a legal target,
        // prefer them over the current focus (but still respect range).
        bool attackerHot = (Time.time - _lastAttackerTime) <= retaliationMemory;
        if (attackerHot && IsTargetValid(_lastAttacker) && _lastAttacker != _target)
        {
            float atkDist = Vector3.Distance(transform.position, _lastAttacker.position);
            if (atkDist <= detectionRadius * 1.4f)
            {
                _target          = _lastAttacker;
                _targetLockTimer = targetLockDuration;
                return;
            }
        }

        // ── 2. Commit to the existing target while the lock is still hot ──
        if (currentValid && _targetLockTimer > 0f)
        {
            float d = Vector3.Distance(transform.position, _target.position);
            // Only drop the lock if the target has run miles away.
            if (d <= detectionRadius * 2.2f) return;
        }

        // ── 3. Scan for a new candidate ─────────────────────────────────────
        float scanRadius = currentValid ? detectionRadius * 1.5f : detectionRadius;
        Transform candidate = FindFfaTarget(scanRadius);

        if (candidate == null)
        {
            if (!currentValid) _target = null;
            return;
        }

        // ── 4. Hysteresis — don't switch unless newcomer is clearly closer ─
        if (currentValid && candidate != _target)
        {
            float curD = Vector3.Distance(transform.position, _target.position);
            float newD = Vector3.Distance(transform.position, candidate.position);
            if (newD >= curD - targetSwitchHysteresis) return; // keep current
        }

        _target          = candidate;
        _targetLockTimer = targetLockDuration;
    }

    /// <summary>
    /// Free-for-All targeting. Returns the closest living IDamageable in
    /// range, preferring visible targets (LoS) and excluding self.
    /// Both the player and other enemies are fair game.
    /// </summary>
    private Transform FindFfaTarget(float radius)
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, radius, detectionMask, QueryTriggerInteraction.Ignore);

        Transform best      = null;
        float     bestScore = float.MaxValue;
        var       evaluated = new HashSet<int>();

        foreach (Collider hit in hits)
        {
            if (hit == null || hit.transform == transform) continue;

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) continue;
            if (dmg.gameObject == gameObject) continue;

            int id = dmg.gameObject.GetInstanceID();
            if (!evaluated.Add(id)) continue;

            Transform t  = dmg.transform;
            float     d2 = (t.position - transform.position).sqrMagnitude;

            // Penalise targets we can't see so visible threats win ties.
            if (!HasLineOfSight(t)) d2 *= 2.25f;

            if (d2 < bestScore)
            {
                bestScore = d2;
                best      = t;
            }
        }

        return best;
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
    //  to other common bone names. A neutral socket is inserted under the
    //  hand so imported bone compensation scales do not collapse the weapon
    //  transform or distort its final world size.
    // ════════════════════════════════════════════════════════════════════════

    public void AttachWeaponToHand(GameObject weaponPrefab, float targetSize = 0.5f, int level = -1)
    {
        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
            equippedWeaponObject = null;
        }
        _activeWeaponSocket   = null;
        _activeWeaponHandBone = null;

        if (weaponPrefab == null) return;

        // ── Resolve level and pull ALL grip data from the catalog. ───────────
        // This makes AttachWeaponToHand the single source of truth for enemy
        // weapon socketing — LevelBuilder no longer needs to pre-fill inspector
        // fields for grip pose, socket euler, or stabilization.
        if (level < 0)
            level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        WeaponLoadout loadout            = WeaponLoadoutCatalog.Get(level);
        weaponGripLocalPosition          = loadout.EnemyLocalPosition;
        weaponGripLocalEulerAngles       = loadout.EnemyLocalEuler;
        weaponSocketLocalEulerAngles     = WeaponLoadoutCatalog.GetEnemySocketLocalEuler(level);
        stabilizeWeaponSocketAgainstHandPose = (level == 9);

        // ── 1. Find right-hand bone ─────────────────────────────────────────
        Transform handBone = weaponAttachPoint;
        if (handBone == null)
            handBone = FindHandBone(gameObject);
        if (handBone == null && _anim != null && _anim.isHuman)
            handBone = _anim.GetBoneTransform(HumanBodyBones.RightHand);

        if (handBone == null)
        {
            handBone = transform;
            Debug.LogWarning($"[EnemyController] '{name}': Right hand bone not found. " +
                             "Weapon attached to root. Drag the hand bone into 'Weapon Attach Point'.");
        }

        // ── 2. Instantiate unparented (clean world-space bounds) ────────────
        equippedWeaponObject = Instantiate(weaponPrefab);
        equippedWeaponObject.name = "WeaponModel";
        equippedWeaponObject.SetActive(true);

        foreach (Transform t in equippedWeaponObject.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);

        // ── 3. Measure bounds at unit scale ─────────────────────────────────
        equippedWeaponObject.transform.localScale = Vector3.one;
        float weaponExtent = GetMaxRendererExtent(equippedWeaponObject);
        if (weaponExtent < 0.001f) weaponExtent = 1f;

        // ── 4. Parent to a neutral socket so imported bone scales do not
        // shrink the weapon transform down to near-zero local values. ───────
        Transform weaponSocket = GetOrCreateWeaponSocket(handBone);
        Quaternion socketRotation = Quaternion.Euler(weaponSocketLocalEulerAngles);
        if (stabilizeWeaponSocketAgainstHandPose)
            socketRotation = Quaternion.Inverse(handBone.localRotation) * socketRotation;
        weaponSocket.localRotation = socketRotation;
        _activeWeaponSocket   = weaponSocket;
        _activeWeaponHandBone = handBone;
        equippedWeaponObject.transform.SetParent(weaponSocket, worldPositionStays: false);
        equippedWeaponObject.transform.localPosition = Vector3.zero;
        equippedWeaponObject.transform.localRotation = Quaternion.identity;
        Transform runtimeGripParent = WeaponLoadoutCatalog.GetOrCreateRuntimeGripAnchor(
            level, weaponPrefab, weaponSocket);
        if (runtimeGripParent != weaponSocket)
        {
            equippedWeaponObject.transform.SetParent(runtimeGripParent, worldPositionStays: false);
            equippedWeaponObject.transform.localPosition = Vector3.zero;
            equippedWeaponObject.transform.localRotation = Quaternion.identity;
        }

        // ── 5. Compute localScale from the ACTUAL inherited world size ─────
        float desiredWorldSize = Mathf.Max(0.01f, targetSize);
        equippedWeaponObject.transform.localScale = Vector3.one;
        float inheritedExtent = GetMaxRendererExtent(equippedWeaponObject);
        if (inheritedExtent < 0.001f) inheritedExtent = weaponExtent;
        float uniformScale = desiredWorldSize / inheritedExtent;
        equippedWeaponObject.transform.localScale = Vector3.one * uniformScale;

        // ── 6. Apply grip pose — catalog EnemyLocalPosition/Euler is the only
        // source of truth; inspector fields were already overwritten above. ──
        if (!WeaponLoadoutCatalog.ApplyRuntimeGripPose(level, weaponPrefab, equippedWeaponObject.transform))
            ApplyWeaponGripPose();
        WeaponLoadoutCatalog.ApplyRuntimeOverrides(level, weaponPrefab, equippedWeaponObject);

        // Saw (level 12): same top-handle correction as the player.
        // Crosby's hand basis differs from the Ronin wrist, so the enemy uses
        // the same rotation/position starting point — tune independently if the
        // enemy rig needs a different y offset.
        if (level == 12
            && weaponPrefab.name.IndexOf("saw", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            equippedWeaponObject.transform.localRotation = Quaternion.Euler(8f, 0f, -90f);
            equippedWeaponObject.transform.localPosition = new Vector3(0f, -0.25f, -0.05f);
        }

        if (level == 14
            && weaponPrefab.name.IndexOf("morgenstern", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            ApplyMorgensternEnemyGripPose(equippedWeaponObject.transform);
        }

        Debug.Log($"[EnemyController] '{name}' lvl={level} weapon → hand '{handBone.name}' " +
                  $"targetSize={desiredWorldSize} extent={weaponExtent} " +
                  $"localPosition={equippedWeaponObject.transform.localPosition} " +
                  $"localEuler={equippedWeaponObject.transform.localEulerAngles} " +
                  $"localScale={equippedWeaponObject.transform.localScale} " +
                  $"lossyScale={equippedWeaponObject.transform.lossyScale}");

        // ── 6. Disable physics, embedded animators, colliders ───────────────
        foreach (Rigidbody rb in equippedWeaponObject.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic     = true;
            rb.useGravity      = false;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        foreach (Animator weaponAnimator in equippedWeaponObject.GetComponentsInChildren<Animator>(true))
            weaponAnimator.enabled = false;

        foreach (Collider col in equippedWeaponObject.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // ── 7. Wire WeaponBase ──────────────────────────────────────────────
        WeaponBase wb = equippedWeaponObject.GetComponent<WeaponBase>();
        if (wb == null) wb = equippedWeaponObject.AddComponent<WeaponBase>();
        wb.damage      = (int)attackDamage;
        wb.attackRange = attackRadius;
        wb.isRanged    = false;

        // ── 8. Wire WeaponHitbox ────────────────────────────────────────────
        WeaponHitbox hitbox = equippedWeaponObject.GetComponent<WeaponHitbox>();
        if (hitbox == null) hitbox = equippedWeaponObject.AddComponent<WeaponHitbox>();
        hitbox.damage = (int)attackDamage;

        // ── 9. Visibility fix (URP) ─────────────────────────────────────────
        if (equippedWeaponObject.GetComponent<WeaponVisibilityFix>() == null)
            equippedWeaponObject.AddComponent<WeaponVisibilityFix>();
    }

    private static Transform GetOrCreateWeaponSocket(Transform handBone)
    {
        Transform socket = handBone.Find(WeaponSocketName);
        if (socket == null)
        {
            GameObject socketObject = new GameObject(WeaponSocketName);
            socket = socketObject.transform;
            socket.SetParent(handBone, worldPositionStays: false);
        }

        socket.localPosition = Vector3.zero;
        socket.localRotation = Quaternion.identity;

        Vector3 handLossy = handBone.lossyScale;
        socket.localScale = new Vector3(
            1f / Mathf.Max(Mathf.Abs(handLossy.x), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(handLossy.y), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(handLossy.z), 0.0001f));

        return socket;
    }

    private void ApplyWeaponGripPose()
    {
        if (equippedWeaponObject == null) return;

        equippedWeaponObject.transform.localRotation = Quaternion.Euler(weaponGripLocalEulerAngles);
        equippedWeaponObject.transform.localPosition = weaponGripLocalPosition;
    }

    private static readonly Vector3 MorgensternEnemyGripBasePosition = new Vector3(-0.025f, -0.0025f, 0f);
    private const float MorgensternEnemyHandleGripInsetNormalized = 0.82f;
    private const bool MorgensternEnemyHandleAtDominantMax = true;

    private static void ApplyMorgensternEnemyGripPose(Transform weaponRoot)
    {
        if (weaponRoot == null)
            return;

        if (!TryGetCombinedLocalBounds(weaponRoot, out Bounds localBounds))
        {
            weaponRoot.localRotation = Quaternion.Euler(0f, 0f, 90f);
            weaponRoot.localPosition = MorgensternEnemyGripBasePosition;
            return;
        }

        weaponRoot.localRotation = GetMorgensternEnemyLocalRotation(localBounds);
        Vector3 gripPoint = GetMorgensternEnemyGripPoint(localBounds);
        Vector3 scaledGripPoint = Vector3.Scale(gripPoint, weaponRoot.localScale);
        weaponRoot.localPosition = MorgensternEnemyGripBasePosition - (weaponRoot.localRotation * scaledGripPoint);
    }

    private static Quaternion GetMorgensternEnemyLocalRotation(Bounds localBounds)
    {
        int dominantAxis = GetDominantBoundsAxis(localBounds);

        switch (dominantAxis)
        {
            case 0:
                return Quaternion.Euler(0f, MorgensternEnemyHandleAtDominantMax ? 180f : 0f, 0f);
            case 1:
                return Quaternion.Euler(0f, 0f, MorgensternEnemyHandleAtDominantMax ? 90f : -90f);
            default:
                return Quaternion.Euler(0f, MorgensternEnemyHandleAtDominantMax ? -90f : 90f, 0f);
        }
    }

    private static Vector3 GetMorgensternEnemyGripPoint(Bounds localBounds)
    {
        int dominantAxis = GetDominantBoundsAxis(localBounds);
        float min = localBounds.min[dominantAxis];
        float max = localBounds.max[dominantAxis];
        float gripNormalized = MorgensternEnemyHandleAtDominantMax
            ? MorgensternEnemyHandleGripInsetNormalized
            : 1f - MorgensternEnemyHandleGripInsetNormalized;

        Vector3 gripPoint = localBounds.center;
        gripPoint[dominantAxis] = Mathf.Lerp(min, max, gripNormalized);
        return gripPoint;
    }

    private static int GetDominantBoundsAxis(Bounds localBounds)
    {
        int dominantAxis = 0;
        float dominantSize = localBounds.size.x;
        if (localBounds.size.y > dominantSize)
        {
            dominantAxis = 1;
            dominantSize = localBounds.size.y;
        }
        if (localBounds.size.z > dominantSize)
            dominantAxis = 2;

        return dominantAxis;
    }

    private static bool TryGetCombinedLocalBounds(Transform root, out Bounds combinedBounds)
    {
        combinedBounds = new Bounds();
        if (root == null)
            return false;

        bool hasBounds = false;
        Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            EncapsulateTransformedBounds(
                ref combinedBounds,
                ref hasBounds,
                meshFilter.sharedMesh.bounds,
                rootWorldToLocal * meshFilter.transform.localToWorldMatrix);
        }

        SkinnedMeshRenderer[] skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedMeshes.Length; i++)
        {
            SkinnedMeshRenderer skinnedMesh = skinnedMeshes[i];
            if (skinnedMesh == null)
                continue;

            Bounds sourceBounds = skinnedMesh.localBounds;
            if (sourceBounds.size.sqrMagnitude <= 0f && skinnedMesh.sharedMesh != null)
                sourceBounds = skinnedMesh.sharedMesh.bounds;
            if (sourceBounds.size.sqrMagnitude <= 0f)
                continue;

            EncapsulateTransformedBounds(
                ref combinedBounds,
                ref hasBounds,
                sourceBounds,
                rootWorldToLocal * skinnedMesh.transform.localToWorldMatrix);
        }

        return hasBounds;
    }

    private static void EncapsulateTransformedBounds(
        ref Bounds combinedBounds,
        ref bool hasBounds,
        Bounds sourceBounds,
        Matrix4x4 sourceToRootMatrix)
    {
        Vector3 center = sourceBounds.center;
        Vector3 extents = sourceBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 transformedCorner = sourceToRootMatrix.MultiplyPoint3x4(corner);
                    if (!hasBounds)
                    {
                        combinedBounds = new Bounds(transformedCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(transformedCorner);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns the largest axis of the combined renderer bounds for a weapon GameObject.
    /// Must be called BEFORE parenting (at world scale 1,1,1) for accurate results.
    /// </summary>
    private static float GetMaxRendererExtent(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        return Mathf.Max(combined.size.x, combined.size.y, combined.size.z);
    }

    // ── Bone search: authored weapon sockets first, then common hand bones ───
    private static readonly string[] HandBoneNames =
    {
        "weapon_bone_R",        // Crosby weapon socket
        "bip_hand_R",           // Crosby primary right hand
        "j_wrist_ri",           // Ronin (player)
        "mixamorig:RightHand",  // Mixamo
        "RightHand",
        "Hand_R", "hand_R", "hand_r",
        "Wrist_R", "wrist_R",
    };

    private static Transform FindHandBone(GameObject body)
    {
        // ── Priority 1: explicit weapon/hand sockets authored on the rig ────
        foreach (string boneName in HandBoneNames)
        {
            Transform found = FindBoneByName(body.transform, boneName);
            if (found != null)
            {
                Debug.Log($"[EnemyController] Hand bone found via name search: '{found.name}'");
                return found;
            }
        }

        // ── Priority 2: Humanoid avatar API fallback ───────────────────────
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
