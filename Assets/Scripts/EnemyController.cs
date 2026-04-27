using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Enemy AI with NavMeshAgent pathfinding, jump-over-obstacles, natural animations,
/// immediate ragdoll death, and proper weapon support.
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
    private const string PrefKeySicklePos = "Grip.Player.L13.Sickle.Pos";
    private const string PrefKeySickleEuler = "Grip.Player.L13.Sickle.Euler";
    private const string PrefKeySawPos = "Grip.Player.L12.Saw.Pos";
    private const string PrefKeySawEuler = "Grip.Player.L12.Saw.Euler";
    // ── Tuning ──────────────────────────────────────────────────────────────
    [Header("Combat")]
    public float detectionRadius  = 80f;
    public float attackRadius     = 1.8f;
    public float attackDamage     = 10f;
    public float attackCooldown   = 0.65f;
    public int   maxHealth        = 60;
    [Tooltip("Hits with damage at or above this value trigger death-by-ragdoll even if health remains.")]
    public int   ragdollForceThreshold = 50;

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

    [Header("Flow Field Navigation")]
    [Tooltip("When enabled, chase movement follows the shared flow-field vectors instead of constantly pathing to target positions.")]
    public bool useFlowFieldNavigation = false;
    [Tooltip("Distance ahead (in metres) used as a local steering destination from the current flow vector.")]
    public float flowFieldLookAhead = 1.35f;
    [Tooltip("How often to refresh local steering destination from flow field.")]
    public float flowFieldSteerInterval = 0.12f;

    [Header("Jump (Obstacle Avoidance)")]
    [Tooltip("Height the enemy jumps when blocked. Tuned to a natural human-scale hop matching the Player.")]
    public float jumpHeight       = 1.05f;
    [Tooltip("Gravity value (negative). Mirrors PlayerController.gravity for matching arcs.")]
    public float gravity          = -22f;
    [Tooltip("Horizontal distance ahead to check for obstacles.")]
    public float obstacleCheckDist = 1.6f;
    [Tooltip("How long the enemy must be stuck before attempting a jump.")]
    public float stuckTime        = 1.2f;
    [Tooltip("Velocity below this is considered 'stuck' even when path exists.")]
    public float stuckVelocityThreshold = 0.25f;

    [Header("Black Ops 3 Maneuvers (Enemies)")]
    [Tooltip("How often (seconds) the enemy may roll a combat maneuver (sprint/jump/slide/flip) while chasing.")]
    public float maneuverRollInterval = 2.4f;
    [Tooltip("Chance per roll that the enemy fires off a maneuver during chase (0..1).")]
    [Range(0f, 1f)] public float maneuverChance = 0.32f;
    [Tooltip("Forward boost applied during a closing slide.")]
    public float maneuverSlideBoost = 6.5f;
    [Tooltip("Forward boost applied during an evasive flip.")]
    public float maneuverFlipBoost = 7.0f;

    [Header("Weapon")]
    public WeaponGripSystem weaponGripSystem;
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
    [Header("Runtime Grip Persistence")]
    [Tooltip("When enabled, enemy sickle/saw grip uses the latest saved runtime tune values.")]
    public bool useSavedRuntimeGripValues = true;

    [HideInInspector] public GameObject equippedWeaponObject;

    [Header("FFA Target Detection")]
    [Tooltip("Layer mask for valid targets (set to 'Character' layer).")]
    public LayerMask detectionMask = ~0;
    [Tooltip("Extended emergency scan radius so enemies stay aggressive and do not idle.")]
    public float aggressiveScanRadius = 120f;

    [Tooltip("Seconds between target scans (coroutine-based for performance).")]
    public float detectionInterval = 0.08f;

    [Header("Target Locking & LoS")]
    [Tooltip("How long (seconds) the AI commits to a target before considering switching.")]
    public float targetLockDuration = 0.15f;
    [Tooltip("A new candidate target must be this many metres closer than the current one to steal focus.")]
    public float targetSwitchHysteresis = 0f;
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
    public float separationRadius   = 1.0f;
    public float separationStrength = 1.0f;

    [Header("Hit Reaction")]
    public float flinchDuration = 0.25f;
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public AudioClip hitSound;
    public AudioClip deathSound;
    [Tooltip("Damage-window start after attack trigger (seconds).")]
    public float attackHitboxWindup = 0.12f;
    [Tooltip("How long the weapon hitbox remains active during attack (seconds).")]
    public float attackHitboxActiveTime = 0.3f;

    [Header("Death")]
    [Tooltip("Seconds the death animation plays before ragdoll activates.")]
    public float deathAnimDuration = 1.5f;
    [Tooltip("Seconds before corpse is cleaned up (0 = never).")]
    public float corpseLifetime    = 15f;
    [Tooltip("Upward force added when ragdolling.")]
    public float deathPopForce     = 2f;
    [Tooltip("How long ragdoll remains before this enemy object is destroyed.")]
    public float ragdollVisibleDuration = 5f;

    [Header("Patrol")]
    [Tooltip("When no target is found, enemy roams inside this radius.")]
    public float patrolRadius = 12f;
    [Tooltip("How often to pick a new random patrol point.")]
    public float patrolRetargetInterval = 2.25f;

    [Header("OffMesh Link Jump")]
    [Tooltip("Duration used to traverse jump links manually.")]
    public float offMeshJumpDuration = 0.45f;
    [Tooltip("Extra arc height while crossing OffMeshLinks.")]
    public float offMeshJumpHeight = 1.2f;

    // ── State ────────────────────────────────────────────────────────────────
  // ── State ────────────────────────────────────────────────────────────────
    private enum State { Idle, Patrol, Chase, Attack, Flinch, Jumping, Dead }
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
    private bool         _playerDamagedThisLife;
    private bool         _killedByPlayer;
    private RagdollController _ragdoll;
    private Vector3      _lastHitDirection;
    private Rigidbody[]  _ragdollBodies;
    private CapsuleCollider _mainCapsuleCollider;

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

    // Heartbeat (anti-freeze): if we don't displace meaningfully for
    // HeartbeatNoMoveThreshold seconds while in a movement state, force a
    // NavMesh path recompute to the nearest target.
    private const float HeartbeatNoMoveThreshold = 1.5f;
    private const float HeartbeatMoveEpsilon     = 0.05f; // metres
    private float _heartbeatTimer;
    private Vector3 _heartbeatLastSampledPos;
    private HashSet<int> _animParameterHashes;
    private Transform _activeWeaponSocket;
    private Transform _activeWeaponHandBone;
    private GameObject _equippedWeaponPrefab;
    private int _equippedWeaponLevel = -1;
    private bool _weaponAttachInProgress;
    private float _patrolTimer;
    private bool _isTraversingOffMeshLink;
    private float _flowFieldSteerTimer;
    private Coroutine _attackHitboxRoutine;
    private WeaponHitbox _equippedWeaponHitbox;
    private float _destinationRefreshTimer;

    // Position-delta velocity (same technique the PlayerController uses)
    private Vector3 _lastFramePosition;

    // Animator hashes
    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    private static readonly int HashVelX      = Animator.StringToHash("VelocityX");
    private static readonly int HashVelZ      = Animator.StringToHash("VelocityZ");
    private static readonly int HashAttack    = Animator.StringToHash("Attack");
    private static readonly int HashHit       = Animator.StringToHash("Hit");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");
    private const string WeaponSocketName     = "__EnemyWeaponSocket";
    private static readonly Color[] EnemyTintPalette =
    {
        new Color(0.75f, 0.22f, 0.20f, 1f),
        new Color(0.20f, 0.58f, 0.28f, 1f),
        new Color(0.24f, 0.34f, 0.78f, 1f),
        new Color(0.82f, 0.56f, 0.18f, 1f),
        new Color(0.55f, 0.24f, 0.72f, 1f),
        new Color(0.18f, 0.62f, 0.68f, 1f),
    };

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    // ── Global one-time setup ─────────────────────────────────────────────────
    // Layer collision matrix is a project-wide setting. Doing this once from
    // any character's Awake guarantees enemies / players collide regardless of
    // whatever the scene was authored with. Calling IgnoreLayerCollision(...,
    // false) for kinematic-vs-kinematic is technically a no-op (kinematic
    // bodies don't push each other apart), but the flag still affects trigger
    // events and OverlapSphere queries we rely on for the manual separation
    // push below.
    private static bool _characterLayerCollisionEnsured;

    internal static void EnsureCharacterLayerCollision()
    {
        if (_characterLayerCollisionEnsured) return;
        _characterLayerCollisionEnsured = true;

        int enemiesLayer  = LayerMask.NameToLayer("Enemies");
        int playerLayer   = LayerMask.NameToLayer("Player");
        int characterLayer = LayerMask.NameToLayer("Character");
        int hittableLayer = LayerMask.NameToLayer("Hittable");

        if (enemiesLayer >= 0)
            Physics.IgnoreLayerCollision(enemiesLayer, enemiesLayer, false);
        if (characterLayer >= 0)
            Physics.IgnoreLayerCollision(characterLayer, characterLayer, false);
        if (playerLayer >= 0 && enemiesLayer >= 0)
            Physics.IgnoreLayerCollision(playerLayer, enemiesLayer, false);
        if (hittableLayer >= 0)
            Physics.IgnoreLayerCollision(hittableLayer, hittableLayer, false);
    }

    private void Awake()
    {
        EnsureCharacterLayerCollision();

        // Hittable is the single deterministic melee query layer.
        int targetLayer = LayerMask.NameToLayer("Hittable");
        if (targetLayer < 0) targetLayer = LayerMask.NameToLayer("Character");
        if (targetLayer >= 0) SetLayerRecursive(gameObject, targetLayer);

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
        _rb.constraints       = RigidbodyConstraints.FreezeRotationX
                               | RigidbodyConstraints.FreezeRotationZ;
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
        LoadSavedRuntimeGripValues();
    }

    private void Start()
    {
        detectionInterval = Mathf.Clamp(detectionInterval, 0.05f, 0.2f);
        detectionRadius = Mathf.Max(detectionRadius, 80f);
        aggressiveScanRadius = Mathf.Max(aggressiveScanRadius, 120f);
        attackCooldown = Mathf.Min(attackCooldown, 0.65f);
        targetLockDuration = Mathf.Min(targetLockDuration, 0.15f);
        targetSwitchHysteresis = 0f;

        // LevelBuilder assigns maxHealth/chaseSpeed after AddComponent(), so
        // re-apply runtime state here once the spawner has finished tuning us.
        // Veteran/Hard custom matches scale enemy HP to make survivability
        // match the elevated damage/speed.
        if (GameManager.Instance != null)
        {
            float healthMul = Mathf.Max(0.25f, GameManager.Instance.GetEnemyHealthMultiplier());
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * healthMul));
        }
        _currentHealth = maxHealth;
        ConfigureAgent();

        _lastFramePosition = transform.position;
        EnsureProperMaterial(gameObject);
        AssignMaterial();

        RegisterForMatchStats();

        _ragdoll = GetComponent<RagdollController>();
        _mainCapsuleCollider = GetComponent<CapsuleCollider>();

        // Cache all bone rigidbodies once and keep ragdoll disabled by default.
        _ragdollBodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < _ragdollBodies.Length; i++)
        {
            Rigidbody boneBody = _ragdollBodies[i];
            if (boneBody == null || boneBody == _rb)
                continue;

            boneBody.isKinematic = true;
            boneBody.useGravity = false;
            boneBody.detectCollisions = true;
        }

        // Start the coroutine-based target scanner
        if (isActiveAndEnabled)
            _scanCoroutine = StartCoroutine(TargetScanLoop());

        _patrolTimer = Random.Range(0f, patrolRetargetInterval);
    }

    private void OnDisable()
    {
        if (_scanCoroutine != null)
        {
            StopCoroutine(_scanCoroutine);
            _scanCoroutine = null;
        }
    }

    private void Update()
    {
        if (_state == State.Dead) return;

        // End-match cinematic — freeze AI: stop the agent in place, drop the
        // animator into idle, but keep the visible mesh updated so the orbit
        // camera sees a clean podium pose.
        if (EndMatchCinematic.GameplayLocked)
        {
            if (_agent != null && _agent.enabled && !_agent.isStopped) _agent.isStopped = true;
            SyncAnimator();
            return;
        }

        // Warp logic removed per user request

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
            case State.Patrol:  UpdatePatrol();  break;
            case State.Chase:   UpdateChase();   break;
            case State.Attack:  UpdateAttack();  break;
            case State.Flinch:  UpdateFlinch();  break;
            case State.Jumping: UpdateJumping(); break;
        }

        TickHeartbeat();
        // Warp logic removed per user request
        SyncAnimator();
    }

    /// <summary>
    /// Anti-freeze watchdog. While the enemy is in a state that should be
    /// closing distance (Idle / Patrol / Chase) but its world position has
    /// barely changed for 1.5 s, kick the NavMeshAgent: clear its current
    /// path and re-target the nearest valid combatant (or the locked target).
    /// Attack / Flinch / Jumping / Dead are excluded — they legitimately hold.
    /// </summary>
    private void TickHeartbeat()
    {
        if (_state == State.Attack || _state == State.Flinch ||
            _state == State.Jumping || _state == State.Dead)
        {
            _heartbeatTimer = 0f;
            _heartbeatLastSampledPos = transform.position;
            return;
        }

        Vector3 here = transform.position;
        if ((here - _heartbeatLastSampledPos).sqrMagnitude > HeartbeatMoveEpsilon * HeartbeatMoveEpsilon)
        {
            _heartbeatTimer = 0f;
            _heartbeatLastSampledPos = here;
            return;
        }

        _heartbeatTimer += Time.deltaTime;
        if (_heartbeatTimer < HeartbeatNoMoveThreshold) return;

        _heartbeatTimer = 0f;
        _heartbeatLastSampledPos = here;
        ForcePathRecomputeToNearestTarget();
    }

    private void ForcePathRecomputeToNearestTarget()
    {
        if (!IsAgentReady()) return;

        Transform target = IsTargetValid(_target)
            ? _target
            : FindFfaTarget(Mathf.Max(detectionRadius, aggressiveScanRadius));

        if (target == null)
        {
            // Nothing valid to chase — at least re-issue the patrol so the
            // agent doesn't keep idling against a stale internal path.
            _agent.ResetPath();
            TransitionTo(State.Patrol);
            return;
        }

        _target = target;
        _agent.ResetPath();
        Vector3 dest = target.position;
        if (NavMesh.SamplePosition(dest, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
            dest = navHit.position;

        _agent.isStopped = false;
        _agent.SetDestination(dest);

        if (_state != State.Chase && _state != State.Attack)
            TransitionTo(State.Chase);
    }

    private void FixedUpdate()
    {
        ApplyEnemySeparation();
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
        if (IsAgentReady())
            _agent.isStopped = true;

        // Immediately react to any valid target (no wait for scan tick).
        if (IsTargetValid(_target))
            TransitionTo(State.Chase);
        else
            TransitionTo(State.Patrol);
    }

    private void UpdatePatrol()
    {
        if (!IsAgentReady())
            return;

        if (IsTargetValid(_target))
        {
            TransitionTo(State.Chase);
            return;
        }

        _agent.isStopped = false;
        _agent.speed = moveSpeed;

        if (_isTraversingOffMeshLink || _agent.isOnOffMeshLink)
            return;

        _patrolTimer -= Time.deltaTime;
        bool needsDestination = !_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.35f;
        if (_patrolTimer <= 0f || needsDestination)
        {
            _patrolTimer = patrolRetargetInterval;
            SetRandomPatrolDestination();
        }
    }

    private float _stuckInChaseTimer;

    private void UpdateChase()
    {
        if (HandleOffMeshLinkTraversal())
            return;

        // ── Validation ───────────────────────────────────────────────────────
        if (!IsTargetValid(_target))
        {
            _target = null;
            TransitionTo(State.Patrol);
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
        if (IsAgentReady())
        {
            if (_agent.isStopped) _agent.isStopped = false;

            if (_agent.velocity.magnitude < 0.1f)
            {
                _stuckInChaseTimer += Time.deltaTime;
                if (_stuckInChaseTimer >= 2.0f)
                {
                    _stuckInChaseTimer = 0f;
                    _agent.ResetPath();
                    _agent.SetDestination(_target.position);
                }
            }
            else
            {
                _stuckInChaseTimer = 0f;
            }

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
            _destinationRefreshTimer -= Time.deltaTime;
            bool refreshDestination = _destinationRefreshTimer <= 0f;

            if (useFlowFieldNavigation && FlowFieldManager.Instance != null)
            {
                if (refreshDestination)
                    DriveChaseWithFlowField();
            }
            else if (refreshDestination)
            {
                bool setDirect = _agent.SetDestination(_target.position);
                if (!setDirect || _agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    TrySetDestinationNearTarget();
            }

            if (refreshDestination)
                _destinationRefreshTimer = 0.5f;
        }

        CheckAndJumpIfStuck();

        // Black Ops 3 manoeuvre tick — gives enemies a small chance every
        // <maneuverRollInterval> seconds to fire off a sprint, slide, jump or
        // flip while chasing. Reuses the same physics as the player.
        TickCombatManeuver();
    }

    private void UpdateAttack()
    {
        if (HandleOffMeshLinkTraversal())
            return;

        if (!IsTargetValid(_target))
        {
            _target = null;
            TransitionTo(State.Patrol);
            return;
        }

        if (IsAgentReady())
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

        if (IsAgentReady())
        {
            _agent.isStopped = true;
            _agent.velocity  = Vector3.zero;
        }

        if (_flinchTimer <= 0f)
            TransitionTo(IsTargetValid(_target) ? State.Chase : State.Patrol);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SEPARATION (Anti-Stacking) — kinematic Rigidbody backup
    //  NavMeshAgent.HQ avoidance is supposed to keep agents from overlapping,
    //  but two failure modes still produce the "magnet merge" reported by the
    //  player: (a) coincident spawn points, (b) a NavMesh that can't pull
    //  agents apart because the area between them is unwalkable. Because the
    //  Rigidbody is kinematic, the physics engine WILL NOT push these
    //  capsules apart no matter what the layer collision matrix says — this
    //  routine performs the push manually without teleporting the agent.
    // ════════════════════════════════════════════════════════════════════════

    private static readonly Collider[] _separationBuffer = new Collider[24];

    private void ApplyEnemySeparation()
    {
        if (_state == State.Dead) return;

        Vector3 origin = transform.position + Vector3.up * 0.9f;
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            Mathf.Max(0.1f, separationRadius),
            _separationBuffer,
            ResolveHittableMask(),
            QueryTriggerInteraction.Ignore);
        Vector3 push = Vector3.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider other = _separationBuffer[i];
            if (other == null) continue;
            if (other.transform == transform || other.transform.IsChildOf(transform)) continue;

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive || damageable.gameObject == gameObject)
                continue;

            Vector3 delta = transform.position - damageable.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (distance < 0.001f)
            {
                float jitter = GetInstanceID() < damageable.gameObject.GetInstanceID() ? 1f : -1f;
                delta = new Vector3(jitter, 0f, jitter * 0.35f);
                distance = delta.magnitude;
            }

            float overlap = Mathf.Max(0f, separationRadius - distance);
            if (overlap > 0f)
                push += delta.normalized * overlap * Mathf.Max(0.1f, separationStrength);
        }

        if (push.sqrMagnitude < 1e-6f) return;

        Vector3 destination = transform.position + Vector3.ClampMagnitude(push, separationRadius);
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.Warp(destination);
        else
            transform.position = destination;

        Physics.SyncTransforms();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  OUT-OF-MAP RECOVERY
    //  Routine teleport recovery is disabled. The only allowed agent warp is
    //  when an enemy has actually fallen below the world.
    // ════════════════════════════════════════════════════════════════════════

    private Vector3 _frozenLastPos;

    private void RecoverOnlyIfOutOfMap()
    {
        if (_state == State.Dead) return;
        if (_agent == null || !_agent.enabled) return;
        if (transform.position.y < -5f)
        {
            WarpToNearestNavMeshAfterFall();
            return;
        }

        _frozenLastPos = transform.position;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  JUMP — Issue #1
    //  Detects when the enemy is stuck against an obstacle and launches it
    //  upward using the same physics formula as the Player:
    //      verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity)
    // ════════════════════════════════════════════════════════════════════════

    private void CheckAndJumpIfStuck()
    {
        if (!IsAgentReady() || !_agent.hasPath) return;

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

    // ════════════════════════════════════════════════════════════════════════
    //  ENEMY BLACK OPS 3 MANOEUVRES
    //  Adds the same four-button move vocabulary players use (Sprint / Slide /
    //  Jump / Flip) to the AI. We can't actually press buttons on their behalf,
    //  so the chase loop rolls a small chance every <maneuverRollInterval>
    //  seconds and triggers one of these moves. They're cosmetic + tactical:
    //    • Sprint → already handled by the dynamic chase speed up top.
    //    • Slide  → short forward velocity boost when in mid-range distance.
    //    • Jump   → small combat hop (re-uses TryJump).
    //    • Flip   → evasive forward flip with vertical+forward impulse.
    // ════════════════════════════════════════════════════════════════════════

    private float _maneuverTimer;

    private void TickCombatManeuver()
    {
        if (_state != State.Chase) return;
        if (!_isGrounded) return;
        if (_target == null || _agent == null || !_agent.enabled) return;

        _maneuverTimer -= Time.deltaTime;
        if (_maneuverTimer > 0f) return;
        _maneuverTimer = Mathf.Max(0.5f, maneuverRollInterval);

        if (Random.value > Mathf.Clamp01(maneuverChance)) return;

        float dist = Vector3.Distance(transform.position, _target.position);

        // Pick a move that suits the current distance.
        if (dist < 4.5f && Random.value < 0.55f)
            DoManeuverFlip();   // Close range → evasive flip
        else if (dist < 9f && Random.value < 0.6f)
            DoManeuverSlide();  // Mid range → slide-close
        else
            TryJump();          // Long range → combat hop forward
    }

    private void DoManeuverSlide()
    {
        if (_target == null || _rb == null) return;
        if (!_isGrounded) return;

        Vector3 dir = (_target.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        // Use the off-mesh physics path so we don't fight the agent.
        _preJumpState = _state;
        TransitionTo(State.Jumping);
        if (_agent != null) _agent.enabled = false;
        if (_controller != null) _controller.enabled = false;
        _rb.isKinematic = false;

        _rb.linearVelocity = new Vector3(dir.x * maneuverSlideBoost, 0.5f, dir.z * maneuverSlideBoost);
        _isGrounded = false;
        SetAnimatorBool(HashGrounded, false);
    }

    private void DoManeuverFlip()
    {
        if (_rb == null) return;
        if (!_isGrounded) return;

        Vector3 dir = transform.forward;
        if (_target != null)
        {
            Vector3 to = _target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) dir = to.normalized;
        }

        _preJumpState = _state;
        TransitionTo(State.Jumping);
        if (_agent != null) _agent.enabled = false;
        if (_controller != null) _controller.enabled = false;
        _rb.isKinematic = false;

        float flipUp = Mathf.Sqrt(Mathf.Max(0.01f, jumpHeight) * -2f * gravity);
        _rb.linearVelocity = new Vector3(dir.x * maneuverFlipBoost, flipUp, dir.z * maneuverFlipBoost);
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
                _agent.enabled = true;
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

        if (_attackHitboxRoutine != null)
        {
            StopCoroutine(_attackHitboxRoutine);
            _attackHitboxRoutine = null;
        }
        if (_equippedWeaponHitbox != null)
            _equippedWeaponHitbox.DisableHitbox();

        // Weapon-tip OverlapSphere during the timed window (see AttackHitboxWindowRoutine).
        // Body-centered spheres missed slim player capsules; the coroutine was never started before.
        _attackHitboxRoutine = StartCoroutine(AttackHitboxWindowRoutine());
    }

    private void DriveChaseWithFlowField()
    {
        if (!IsAgentReady())
            return;

        _flowFieldSteerTimer -= Time.deltaTime;
        if (_flowFieldSteerTimer > 0f)
            return;

        _flowFieldSteerTimer = Mathf.Max(0.03f, flowFieldSteerInterval);
        Vector3 flowDir = FlowFieldManager.Instance.GetFlowDirection(transform.position);
        if (flowDir.sqrMagnitude < 0.001f)
        {
            if (_target != null)
                _agent.SetDestination(_target.position);
            return;
        }

        Vector3 steerTarget = transform.position + flowDir.normalized * Mathf.Max(0.5f, flowFieldLookAhead);
        if (NavMesh.SamplePosition(steerTarget, out NavMeshHit hit, 1.8f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
        else
            _agent.SetDestination(steerTarget);
    }

    private IEnumerator AttackHitboxWindowRoutine()
    {
        if (_equippedWeaponHitbox == null)
            yield break;

        _equippedWeaponHitbox.DisableHitbox();

        if (attackHitboxWindup > 0f)
            yield return new WaitForSeconds(attackHitboxWindup);

        Physics.SyncTransforms();
        _equippedWeaponHitbox.damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage));
        _equippedWeaponHitbox.EnableHitbox();

        if (attackHitboxActiveTime > 0f)
            yield return new WaitForSeconds(attackHitboxActiveTime);

        _equippedWeaponHitbox.DisableHitbox();
        _attackHitboxRoutine = null;
    }

    public void TakeDamage(int amount, bool byPlayer = false)
    {
        if (_state == State.Dead) return;
        if (byPlayer) _playerDamagedThisLife = true;

        _currentHealth -= amount;

        // Flinch
        if (_state != State.Flinch)
        {
            _flinchTimer = flinchDuration;
            TransitionTo(State.Flinch);
            if (IsAgentReady()) _agent.ResetPath();
        }

        ApplyHitFlash();
        SetAnimatorTrigger(HashHit);
        PlayHitSound();

        if (_currentHealth <= 0)
        {
            _killedByPlayer = byPlayer;
            DisableMovementAndCollisionForDeath();
            Die();
        }
    }

    // ── IDamageable ─────────────────────────────────────────────────────────
    public bool IsAlive => _state != State.Dead;

    public void ReceiveDamage(int amount, GameObject attackerRoot)
    {
        // Door / wall occlusion: a static prop on the Environment layer
        // between attacker and us cancels the hit (the player can no longer
        // be tagged through closed doors and the AI returns the courtesy).
        if (DamageOcclusion.IsBlocked(attackerRoot, gameObject))
            return;

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

        // Store hit direction for ragdoll impulse (attacker → enemy)
        if (attackerRoot != null)
            _lastHitDirection = (transform.position - attackerRoot.transform.position).normalized;

        bool fromPlayer = attackerRoot != null
                       && attackerRoot.GetComponentInParent<PlayerHealth>() != null;

        int appliedDamage = Mathf.Max(1, amount);
        if (fromPlayer && GameManager.Instance != null)
        {
            int hitsToKill = Mathf.Max(1, GameManager.Instance.GetEnemyHitsToKillByPlayer());
            appliedDamage = Mathf.Max(1, Mathf.CeilToInt((float)maxHealth / hitsToKill));
        }

        TakeDamage(appliedDamage, byPlayer: fromPlayer);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DEATH — Issue #4
    //  Switches to ragdoll physics immediately so the body falls naturally.
    //  The corpse persists for corpseLifetime seconds before Destroy().
    // ════════════════════════════════════════════════════════════════════════

    private void Die()
    {
        _state = State.Dead;
        DisableMovementAndCollisionForDeath();
        CancelInvoke(nameof(EnableRagdoll));
        CancelInvoke(nameof(FreezeRagdoll));
        if (_attackHitboxRoutine != null)
        {
            StopCoroutine(_attackHitboxRoutine);
            _attackHitboxRoutine = null;
        }
        if (_equippedWeaponHitbox != null)
            _equippedWeaponHitbox.DisableHitbox();

        // Stop target scan coroutine — no point scanning when dead
        if (_scanCoroutine != null)
        {
            StopCoroutine(_scanCoroutine);
            _scanCoroutine = null;
        }

        // Notify GameManager FIRST so enemy count updates correctly (Issue #5)
        if (GameManager.Instance != null)
            GameManager.Instance.EnemyKilled(
                byPlayer: _killedByPlayer,
                assistedByPlayer: !_killedByPlayer && _playerDamagedThisLife);

        if (MatchStatsManager.Instance != null)
        {
            string combatantId = MatchStatsManager.BuildCombatantId(this);
            MatchStatsManager.Instance.MarkEliminated(combatantId);

            if (_killedByPlayer)
            {
                PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
                string playerId = MatchStatsManager.BuildCombatantId(player);
                MatchStatsManager.Instance.RecordKill(playerId);

                // PRISM credits + lifetime kill counter for the persistent
                // session — pays the +10 bounty and ticks "Kill N Enemies"
                // challenges via SessionManager.EvaluateChallenges(). The
                // currently-equipped weapon is forwarded so Weapon Master
                // challenges (e.g. "5 kills with Nunchucks") can advance.
                if (SessionManager.Instance != null)
                {
                    string wid = SessionManager.Instance.EquippedWeaponId;
                    SessionManager.Instance.OnPlayerKilledEnemy(wid);
                }
            }
            else
            {
                EnemyController enemyKiller = _lastAttacker != null ? _lastAttacker.GetComponentInParent<EnemyController>() : null;
                if (enemyKiller != null && enemyKiller != this)
                    MatchStatsManager.Instance.RecordKill(MatchStatsManager.BuildCombatantId(enemyKiller));
            }
        }

        // Stop all navigation immediately
        if (_rb != null) _rb.isKinematic = true;

        // Disable main collider so living enemies don't trip over the corpse
        Collider mainCol = GetComponent<Collider>();
        if (mainCol != null) mainCol.enabled = false;

        // Play death sound
        if (_audio != null)
        {
            if (deathSound != null) _audio.PlayOneShot(deathSound, AudioSettingsRuntime.ScaledSfx(1f));
            else PlayProceduralSound(200f, 0.4f);
        }

        EnableRagdoll();

        // ── Corpse cleanup ────────────────────────────────────────────────────
        if (ragdollVisibleDuration > 0f)
            Destroy(gameObject, ragdollVisibleDuration);
    }

    private void DisableMovementAndCollisionForDeath()
    {
        if (_attackHitboxRoutine != null)
        {
            StopCoroutine(_attackHitboxRoutine);
            _attackHitboxRoutine = null;
        }

        if (_equippedWeaponHitbox != null)
            _equippedWeaponHitbox.DisableHitbox();

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.enabled = false;
        }

        if (_controller != null)
            _controller.enabled = false;

        Collider mainCol = GetComponent<Collider>();
        if (mainCol != null)
            mainCol.enabled = false;
    }

    private void RegisterForMatchStats()
    {
        if (MatchStatsManager.Instance == null)
            return;

        string combatantId = MatchStatsManager.BuildCombatantId(this);
        string displayName = gameObject.name.ToUpperInvariant();
        // Pass the transform so the end-match cinematic can orbit around
        // the actual enemy mesh (instead of guessing from a name match).
        MatchStatsManager.Instance.RegisterCombatant(combatantId, displayName, isPlayer: false, transform: transform);
    }

    /// <summary>
    /// Converts the enemy to a full ragdoll: disables the Animator and lets
    /// Rigidbody physics take over every bone.
    /// Delegates to RagdollController when present; falls back to inline logic.
    /// </summary>
    private void EnableRagdoll()
    {
        if (_anim != null) _anim.enabled = false;
        if (_agent != null) _agent.enabled = false;
        if (_controller != null) _controller.enabled = false;

        int defaultLayer = LayerMask.NameToLayer("Default");
        int environmentLayer = LayerMask.NameToLayer("Environment");
        if (defaultLayer >= 0)
            Physics.IgnoreLayerCollision(gameObject.layer, defaultLayer, false);
        if (environmentLayer >= 0)
            Physics.IgnoreLayerCollision(gameObject.layer, environmentLayer, false);

        if (_ragdoll == null)
            _ragdoll = GetComponent<RagdollController>();

        if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            _ragdollBodies = GetComponentsInChildren<Rigidbody>(true);

        var boneBodies = new List<Rigidbody>();
        for (int i = 0; i < _ragdollBodies.Length; i++)
        {
            Rigidbody rb = _ragdollBodies[i];
            if (rb == null || rb.gameObject == gameObject)
                continue;
            boneBodies.Add(rb);
        }

        Vector3 hitDir = _lastHitDirection.sqrMagnitude > 0.001f
            ? _lastHitDirection.normalized
            : -transform.forward;

        // 1) Attempt full bone ragdoll first.
        if (_ragdoll != null || boneBodies.Count > 0)
        {
            if (_mainCapsuleCollider != null)
                _mainCapsuleCollider.enabled = false;

            Collider rootCol = GetComponent<Collider>();
            if (rootCol != null && rootCol != _mainCapsuleCollider)
                rootCol.enabled = false;

            if (_ragdoll != null)
            {
                _ragdoll.EnableRagdoll(hitDir);
                return;
            }

            Rigidbody impulseTarget = null;
            for (int i = 0; i < boneBodies.Count; i++)
            {
                Rigidbody boneRb = boneBodies[i];
                boneRb.gameObject.SetActive(true);
                boneRb.isKinematic = false;
                boneRb.useGravity = true;
                boneRb.detectCollisions = true;
                boneRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                boneRb.interpolation = RigidbodyInterpolation.Interpolate;
                if (impulseTarget == null)
                    impulseTarget = boneRb;
            }

            Collider[] allCols = GetComponentsInChildren<Collider>(true);
            PhysicsMaterial highFriction = new PhysicsMaterial("EnemyRagdollFriction")
            {
                dynamicFriction = 1f,
                staticFriction = 1f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            for (int i = 0; i < allCols.Length; i++)
            {
                Collider c = allCols[i];
                if (c == null) continue;
                if (c.gameObject == gameObject) continue;
                c.enabled = true;
                c.material = highFriction;
            }

            if (impulseTarget != null)
            {
                Vector3 ragdollImpulse = hitDir * deathPopForce + Vector3.up * (deathPopForce * 0.75f);
                impulseTarget.AddForce(ragdollImpulse, ForceMode.VelocityChange);
                impulseTarget.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
                Invoke(nameof(FreezeRagdoll), 3f);
                return;
            }
        }

        // 2) Bulletproof fallback: make the ROOT body physically topple.
        if (_mainCapsuleCollider == null)
            _mainCapsuleCollider = GetComponent<CapsuleCollider>();
        if (_mainCapsuleCollider == null)
            _mainCapsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        _mainCapsuleCollider.enabled = true; // critical: keep root collider active

        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.constraints = RigidbodyConstraints.None;
        _rb.detectCollisions = true;
        _rb.mass = Mathf.Max(20f, _rb.mass);
        _rb.linearDamping = 0.15f;
        _rb.angularDamping = 0.05f;

        Vector3 launch = (-transform.forward * (deathPopForce * 1.25f)) + (Vector3.up * (deathPopForce * 1.5f));
        _rb.AddForce(launch, ForceMode.VelocityChange);

        Vector3 tumbleAxis = Vector3.Cross(Vector3.up, hitDir);
        if (tumbleAxis.sqrMagnitude < 0.001f)
            tumbleAxis = transform.right;
        _rb.AddTorque((tumbleAxis.normalized * deathPopForce * 6f) + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
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

        Color tint = EnemyTintPalette[ResolveEnemyPaletteIndex() % EnemyTintPalette.Length];
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null) continue;
            Material instance = new Material(material);
            SetMaterialBaseColor(instance, Color.Lerp(GetMaterialBaseColor(instance), tint, 0.55f));
            smr.material = instance;
        }

        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _renderers[i].material == null) continue;
            _originalColors[i] = GetMaterialBaseColor(_renderers[i].material);
        }
    }

    private int ResolveEnemyPaletteIndex()
    {
        string objectName = gameObject != null ? gameObject.name : string.Empty;
        int parsed = 0;
        bool foundDigit = false;

        for (int i = 0; i < objectName.Length; i++)
        {
            char c = objectName[i];
            if (c < '0' || c > '9')
                continue;

            foundDigit = true;
            parsed = (parsed * 10) + (c - '0');
        }

        return foundDigit ? Mathf.Max(0, parsed - 1) : Mathf.Abs(GetInstanceID());
    }

    private bool HasAnimatorParameter(int hash)
    {
        return _anim != null
            && _animParameterHashes != null
            && _animParameterHashes.Contains(hash);
    }

    private static int ResolveHittableMask()
    {
        int mask = 0;
        void Add(string n)
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) mask |= 1 << l;
        }
        Add("Player");
        Add("Hittable");
        Add("Character");
        return mask != 0 ? mask : ~0;
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null || layer < 0) return;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
                transforms[i].gameObject.layer = layer;
        }
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
        if (hitSound != null) _audio.PlayOneShot(hitSound, AudioSettingsRuntime.ScaledSfx(0.7f));
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
        _audio.PlayOneShot(clip, AudioSettingsRuntime.ScaledSfx(0.5f));
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
        _agent.autoTraverseOffMeshLink = false;
    }

    private void TransitionTo(State newState)
    {
        State prevState = _state;
        _state = newState;

        if (!IsAgentReady()) return;

        switch (newState)
        {
            case State.Patrol:
                _agent.isStopped = false;
                _agent.speed = moveSpeed;
                SetRandomPatrolDestination();
                break;

            case State.Chase:
                // Re-engage the agent and immediately seed a destination so
                // there is never a frame where the agent reports no path.
                _agent.isStopped = false;
                _destinationRefreshTimer = 0f;
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
        Transform candidate = FindFfaTarget(Mathf.Max(detectionRadius, aggressiveScanRadius));

        if (candidate == null)
            candidate = FindPlayerTarget();

        if (candidate == null)
        {
            _target = null;
            if (_state != State.Dead && _state != State.Flinch && _state != State.Jumping)
                TransitionTo(State.Patrol);
            return;
        }

        _target = candidate;
        _targetLockTimer = targetLockDuration;
        if (_state == State.Idle || _state == State.Patrol || _state == State.Attack)
            TransitionTo(State.Chase);
    }

    private void SetRandomPatrolDestination()
    {
        if (!IsAgentReady())
            return;

        Vector3 random = transform.position + Random.insideUnitSphere * patrolRadius;
        random.y = transform.position.y;

        if (NavMesh.SamplePosition(random, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    private bool HandleOffMeshLinkTraversal()
    {
        if (!IsAgentReady() || _isTraversingOffMeshLink)
            return false;

        if (!_agent.isOnOffMeshLink)
            return false;

        StartCoroutine(TraverseOffMeshLink());
        return true;
    }

    private IEnumerator TraverseOffMeshLink()
    {
        _isTraversingOffMeshLink = true;
        if (!IsAgentReady())
        {
            _isTraversingOffMeshLink = false;
            yield break;
        }

        OffMeshLinkData data = _agent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = data.endPos + Vector3.up * _agent.baseOffset;
        float duration = Mathf.Max(0.1f, offMeshJumpDuration);
        float t = 0f;

        SetAnimatorBool(HashGrounded, false);
        _agent.isStopped = true;
        _agent.updatePosition = false;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float arc = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * offMeshJumpHeight;
            Vector3 pos = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(t));
            pos.y += arc;
            transform.position = pos;
            yield return null;
        }

        transform.position = endPos;
        _agent.updatePosition = true;
        _agent.CompleteOffMeshLink();
        _agent.isStopped = false;
        SetAnimatorBool(HashGrounded, true);
        _isTraversingOffMeshLink = false;
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

            if (d2 < bestScore)
            {
                bestScore = d2;
                best      = t;
            }
        }

        return best;
    }

    private Transform FindPlayerTarget()
    {
        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player == null)
            return null;

        IDamageable dmg = player.GetComponent<IDamageable>();
        if (dmg == null || !dmg.IsAlive)
            return null;

        return player.transform;
    }

    private void TrySetDestinationNearTarget()
    {
        if (!IsAgentReady() || _target == null)
            return;

        Vector3 targetPos = _target.position;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 4.5f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    private bool IsAgentReady()
    {
        return _agent != null && _agent.enabled && _agent.isOnNavMesh;
    }

    private void WarpToNearestNavMeshAfterFall()
    {
        if (_agent == null)
            return;

        if (transform.position.y >= -5f)
            return;

        if (!_agent.enabled)
            _agent.enabled = true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 30f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            _agent.Warp(hit.position);
            _frozenLastPos = hit.position;
        }
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
        if (_weaponAttachInProgress) return;
        if (weaponPrefab == null) return;
        if (level < 0)
            level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        if (equippedWeaponObject != null && _equippedWeaponPrefab == weaponPrefab && _equippedWeaponLevel == level)
            return;

        _weaponAttachInProgress = true;
        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
            equippedWeaponObject = null;
        }
        _equippedWeaponHitbox = null;
        _activeWeaponSocket   = null;
        _activeWeaponHandBone = null;

        if (weaponGripSystem != null)
        {
            equippedWeaponObject = weaponGripSystem.AttachWeapon(
                characterRoot: gameObject,
                weaponPrefab: weaponPrefab,
                isPlayer: false,
                level: level,
                damage: Mathf.RoundToInt(attackDamage));

            if (equippedWeaponObject != null)
            {
                _equippedWeaponHitbox = equippedWeaponObject.GetComponent<WeaponHitbox>();
                _equippedWeaponPrefab = weaponPrefab;
                _equippedWeaponLevel = level;
                _weaponAttachInProgress = false;
                return;
            }
        }

        // ── Resolve level and pull ALL grip data from the catalog. ───────────
        // This makes AttachWeaponToHand the single source of truth for enemy
        // weapon socketing — LevelBuilder no longer needs to pre-fill inspector
        // fields for grip pose, socket euler, or stabilization.
        WeaponLoadout loadout            = WeaponLoadoutCatalog.Get(level);
        weaponGripLocalPosition          = loadout.EnemyLocalPosition;
        weaponGripLocalEulerAngles       = loadout.EnemyLocalEuler;
        weaponSocketLocalEulerAngles     = WeaponLoadoutCatalog.GetEnemySocketLocalEuler(level);
        stabilizeWeaponSocketAgainstHandPose = (level == 9 || WeaponLoadoutCatalog.IsChainsawLevel(level, weaponPrefab));
        ApplySavedRuntimeGripValuesForLevel(level);

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
        if (WeaponLoadoutCatalog.IsChainsawLevel(level, weaponPrefab))
            WeaponLoadoutCatalog.ApplyChainsawEnemyGripPose(equippedWeaponObject.transform);
        else if (!WeaponLoadoutCatalog.ApplyRuntimeGripPose(level, weaponPrefab, equippedWeaponObject.transform))
            ApplyWeaponGripPose();
        WeaponLoadoutCatalog.ApplyRuntimeOverrides(level, weaponPrefab, equippedWeaponObject);

        // Level-specific grip fixes by weapon name (player/AI rigs differ).
        if (weaponPrefab != null)
        {
            string wn = weaponPrefab.name.ToLowerInvariant();
            if (wn.Contains("sickle"))
            {
                equippedWeaponObject.transform.localRotation = Quaternion.Euler(weaponGripLocalEulerAngles);
                equippedWeaponObject.transform.localPosition = weaponGripLocalPosition;
            }
        }

        if (WeaponLoadoutCatalog.IsChainsawLevel(level, weaponPrefab))
            WeaponLoadoutCatalog.ApplyChainsawEnemyGripPose(equippedWeaponObject.transform);

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
        hitbox.DisableHitbox();
        _equippedWeaponHitbox = hitbox;

        // ── 9. Visibility fix (URP) ─────────────────────────────────────────
        if (equippedWeaponObject.GetComponent<WeaponVisibilityFix>() == null)
            equippedWeaponObject.AddComponent<WeaponVisibilityFix>();

        _equippedWeaponPrefab = weaponPrefab;
        _equippedWeaponLevel = level;
        _weaponAttachInProgress = false;
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

    private void LoadSavedRuntimeGripValues()
    {
        if (!useSavedRuntimeGripValues)
            return;

        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        ApplySavedRuntimeGripValuesForLevel(level);
    }

    private void ApplySavedRuntimeGripValuesForLevel(int level)
    {
        if (!useSavedRuntimeGripValues)
            return;

        if (level == 13)
        {
            weaponGripLocalPosition = LoadVector3Pref(PrefKeySicklePos, weaponGripLocalPosition);
            weaponGripLocalEulerAngles = LoadVector3Pref(PrefKeySickleEuler, weaponGripLocalEulerAngles);
        }
        else if (level == 12)
        {
            PlayerPrefs.DeleteKey(PrefKeySawPos + ".x");
            PlayerPrefs.DeleteKey(PrefKeySawPos + ".y");
            PlayerPrefs.DeleteKey(PrefKeySawPos + ".z");
            PlayerPrefs.DeleteKey(PrefKeySawEuler + ".x");
            PlayerPrefs.DeleteKey(PrefKeySawEuler + ".y");
            PlayerPrefs.DeleteKey(PrefKeySawEuler + ".z");
        }
    }

    private static Vector3 LoadVector3Pref(string key, Vector3 fallback)
    {
        if (!PlayerPrefs.HasKey(key + ".x")
            || !PlayerPrefs.HasKey(key + ".y")
            || !PlayerPrefs.HasKey(key + ".z"))
        {
            return fallback;
        }

        return new Vector3(
            PlayerPrefs.GetFloat(key + ".x", fallback.x),
            PlayerPrefs.GetFloat(key + ".y", fallback.y),
            PlayerPrefs.GetFloat(key + ".z", fallback.z));
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
