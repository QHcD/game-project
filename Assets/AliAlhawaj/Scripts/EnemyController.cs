// test
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif


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
    private static readonly List<EnemyController> s_aliveEnemies = new List<EnemyController>(64);
    private static PlayerController s_scenePlayer;
    private static float s_scenePlayerRefreshTime = -999f;
    private const float ChaseGateRecheckInterval = 0.65f;

    private const string PrefKeySicklePos = "Grip.Player.L13.Sickle.Pos";
    private const string PrefKeySickleEuler = "Grip.Player.L13.Sickle.Euler";
    private const string PrefKeySawPos = "Grip.Player.L12.Saw.Pos";
    private const string PrefKeySawEuler = "Grip.Player.L12.Saw.Euler";
    // ── Tuning ──────────────────────────────────────────────────────────────
    [Header("Combat")]
    public float detectionRadius  = 12f;
    public float attackRadius     = 2f;
    [Tooltip("Hard distance gate applied at the moment of impact (after windup). " +
             "A hit is cancelled if the attacker root is farther than this from the target. " +
             "Keep at or below attackRadius so the enemy must be truly close to land a hit.")]
    public float meleeAttackRange = 2.0f;
    public float attackDamage     = 10f;
    public float attackCooldown   = 0.65f;
    public int   maxHealth        = 60;
    [Tooltip("Hits with damage at or above this value trigger death-by-ragdoll even if health remains.")]
    public int   ragdollForceThreshold = 50;

    [Header("Movement — mirrors Player feel")]
    [Tooltip("Base walking speed (patrol / evade).")]
    public float moveSpeed        = 3.2f;
    [Tooltip("Default chase speed toward targets.")]
    public float chaseSpeed       = 5.2f;
    [Tooltip("Upper cap when closing on a sprinting target — keeps aggression without rocket speeds.")]
    public float sprintChaseSpeed = 6.2f;
    [Tooltip("Manual rotation Slerp rate when smoothing toward movement / aim vectors.")]
    public float rotationSpeed    = 10f;
    [Tooltip("NavMeshAgent acceleration (units/s²).")]
    public float agentAcceleration = 14f;
    [Tooltip("NavMeshAgent angular speed (deg/sec). Visual turning uses LateUpdate Slerp when updateRotation is off.")]
    public float agentAngularSpeed = 540f;
    [Tooltip("Scales locomotion blend-tree Speed vs actual agent velocity so run cycles match chase cadence.")]
    public float runAnimationMultiplier = 1.15f;
    [Tooltip("Logs chase velocity, agent tuning, and animator sync.")]
    public bool debugEnemyMovement = false;

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
    [Tooltip("While jumping: ignore stuck-jump if horizontal agent speed exceeds this.")]
    public float obstacleBlockedSpeedThreshold = 0.25f;

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

    [Header("Level 2 Katana — Hand Grip")]
    [Tooltip("Offset FROM the hand bone TO the katana grip point, in the hand bone's local space.\n" +
             "Default (0,0,0) = weapon pivot sits exactly at the hand bone (wrist/palm).\n" +
             "Tweak in Play Mode: positive Y moves the grip toward the fingers, negative toward the wrist.")]
    public Vector3 katanaHandGripOffset = new Vector3(0f, 0.06f, 0.02f);

    [Tooltip("Extra euler offset applied ON TOP of the player-matched rotation during the carry/idle pose.\n" +
             "X negative = raise blade tip up.  Tweak in Play Mode to match the player's guard stance.\n" +
             "Has no effect during the attack animation.")]
    public Vector3 katanaCarryExtraEuler = new Vector3(-20f, 0f, 0f);
    [Header("Level 12 Saw — Carry Pose")]
    [Tooltip("Offset FROM bip_hand_R TO the saw handle grip point, in the hand bone's local space.\n" +
             "Positions the saw so the handle sits in the palm (not the blade tip).\n" +
             "Tune in Play Mode if the grip point drifts.")]
    public Vector3 sawHandGripOffset = new Vector3(-0.066f, -0.39f, 0.044f);

    [Tooltip("Extra euler offset applied on top of the player-matched rotation during idle/carry.\n" +
             "Has no effect during the attack animation.")]
    public Vector3 sawCarryExtraEuler = new Vector3(0f, 0f, 0f);

    [Header("Level 9 Axe — Attack Swing Correction")]
    [Tooltip("Extra euler rotation applied to the axe ONLY during the Attack state.\n" +
             "Compensates for the bip_hand_R vs j_wrist_ri axis difference that makes\n" +
             "the swing appear reversed compared to the black player.\n" +
             "Default (0,180,0) flips the swing around Y to match the player's direction.\n" +
             "Tune in Play Mode (Inspector) if the result still looks wrong.\n" +
             "Has NO effect on idle grip — only active while State == Attack.")]
    public Vector3 axeAttackSwingCorrection = new Vector3(0f, 180f, 0f);

    [Header("Level 12 Saw — Attack Swing Correction")]
    [Tooltip("Extra euler rotation applied to the saw ONLY during the Attack state.\n" +
             "Same bone-axis mismatch as the Level 9 axe (bip_hand_R vs j_wrist_ri).\n" +
             "Default (0,180,0) flips the swing around Y to match the player's direction.\n" +
             "Tune in Play Mode (Inspector) if the result still looks wrong.\n" +
             "Has NO effect on idle grip — only active while State == Attack.")]
    public Vector3 sawAttackSwingCorrection = new Vector3(0f, 180f, 0f);

    [Header("Runtime Grip Persistence")]
    [Tooltip("When enabled, enemy sickle/saw grip uses the latest saved runtime tune values.")]
    public bool useSavedRuntimeGripValues = true;

    [HideInInspector] public GameObject equippedWeaponObject;

    [Header("FFA Target Detection")]
    [Tooltip("Layer mask for valid targets (set to 'Character' layer).")]
    public LayerMask detectionMask = ~0;
    [Tooltip("Extended emergency scan radius so enemies stay aggressive and do not idle.")]
    public float aggressiveScanRadius = 120f;

    [Tooltip("Seconds between target scans. Lower = snappier target reacquisition.")]
    public float detectionInterval = 0.35f;

    [Header("Target Locking & LoS")]
    [Tooltip("How long (seconds) the AI commits to a target before considering switching.")]
    public float targetLockDuration = 1.6f;
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

    [Header("AI / FSM")]
    [Tooltip("Half-angle (degrees) of the horizontal vision cone for spotting threats.")]
    public float fieldOfViewAngle = 120f;
    [Tooltip("Below this fraction of max HP the enemy enters Evade.")]
    [Range(0.05f, 0.95f)]
    public float lowHealthPercent = 0.25f;
    [Tooltip("Nav velocity below this while chasing counts toward stuck recovery.")]
    public float stuckVelocityThreshold = 0.05f;
    [Tooltip("Seconds at low velocity with an active path before StuckRecovery.")]
    public float stuckTimeThreshold = 1.5f;
    [Tooltip("How often to revalidate NavMesh path and refresh chase destination.")]
    public float repathInterval = 0.45f;
    [Tooltip("Seconds in Idle before starting Patrol.")]
    public float idleToPatrolDelay = 0.55f;
    [Tooltip("Max degrees off-forward allowed before swinging at the target.")]
    public float attackFacingAngle = 50f;
    public bool debugAI = false;
    public bool debugAICombat = false;

    [Header("Flocking (group steering on NavMesh)")]
    [Tooltip("Steer away from other enemies / player when closer than this (horizontal).")]
    public float separationRadius = 1.45f;
    [Tooltip("Neighbour enemies within this radius contribute to alignment.")]
    public float alignmentRadius = 3f;
    [Tooltip("Neighbour enemies within this radius contribute to cohesion centroid.")]
    public float cohesionRadius = 4f;
    [Tooltip("Weight for separation steering.")]
    public float separationWeight = 1.6f;
    [Tooltip("Weight for alignment steering (match neighbour velocity).")]
    public float alignmentWeight = 0.4f;
    [Tooltip("Weight for cohesion steering toward group centroid.")]
    public float cohesionWeight = 0.5f;
    [Tooltip("Cap on combined flock steering magnitude (world units per second scale).")]
    public float maxFlockSteering = 1.2f;

    [Header("Spawn Protection")]
    [Tooltip("Seconds after spawn during which the enemy ignores incoming damage. " +
             "Prevents enemies dying instantly to AoE / spawn-overlap stomps. " +
             "0 = disabled. Keep small (1.0–2.0s).")]
    public float spawnProtectionDuration = 1.25f;
    private float _spawnTime = -1f;
    private float _spawnY = float.NaN; // captured in Start for rooftop watchdog

    [Header("Aggression Tuning (2026-05-21)")]
    [Tooltip("Multiplier applied to chaseSpeed at runtime. 1.0 = unchanged, 1.2 = 20% snappier closing.")]
    [Range(0.5f, 2.0f)] public float chaseSpeedMultiplier = 1.10f;
    [Tooltip("Extra multiplier applied when target distance > farTargetBurstDistance, so enemies don't drag at long range.")]
    [Range(1.0f, 2.0f)] public float farTargetBurstMultiplier = 1.25f;
    [Tooltip("Distance (m) beyond which the burst multiplier kicks in.")]
    public float farTargetBurstDistance = 12f;
    [Tooltip("Multiplier on NavMeshAgent.angularSpeed so enemies turn toward the player without spinning.")]
    [Range(0.5f, 3.0f)] public float turnResponsiveness = 1.30f;
    [Tooltip("Multiplier on the Animator.speed so locomotion cycles read snappier without changing transitions.")]
    [Range(0.5f, 1.5f)] public float animatorSpeedMultiplier = 1.08f;

    [Header("Rooftop / Stuck Watchdog (2026-05-21)")]
    [Tooltip("If enemy.Y rises this far above its spawn Y, it is treated as 'on a rooftop' and warped back down.")]
    public float watchdogMaxYAboveSpawn = 3.5f;
    [Tooltip("Seconds the enemy may remain in an invalid high position before being warped back.")]
    public float watchdogInvalidHighSeconds = 2.5f;
    [Tooltip("Seconds with no path progress while a target is present before forcing a repath / relocate.")]
    public float watchdogStuckPathSeconds = 3.5f;
    [Tooltip("Logs watchdog actions (rooftop warps, stuck recoveries). Off in shipped builds.")]
    public bool debugWatchdog = false;
    [Header("Stability Recovery")]
    [Tooltip("Minimum seconds between automatic recovery warps (prevents warp spam).")]
    public float recoveryWarpCooldown = 2.5f;
    [Tooltip("Seconds allowed on a partial NavMesh path before forcing repath/reposition.")]
    public float partialPathMaxSeconds = 4f;
    [Tooltip("Max vertical metres above spawn Y for chase destinations (rooftop guard).")]
    public float maxChaseVerticalDelta = 3.5f;
    [Tooltip("Seconds without meaningful motion before forcing target reacquisition.")]
    public float idleMotionTimeout = 2.2f;
    private float _highSinceTime = -1f;
    private float _lowProgressSinceTime = -1f;
    private float _partialPathSinceTime = -1f;
    private float _idleMotionSinceTime = -1f;
    private float _lastRecoveryWarpTime = -999f;
    private float _navValidateTimer;
    private Vector3 _watchdogLastPosition;

    [Header("Hit Reaction")]
    public float flinchDuration = 0.25f;
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Combat Voice SFX")]
    public AudioClip[] hurtSounds;
    public AudioClip[] deathSounds;
    private float _lastVoiceTime = -100f;

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
    private enum State { Idle, Patrol, Chase, Attack, Evade, StuckRecovery, Flinch, Jumping, Dead }
    private State _state = State.Idle;

    private NavMeshAgent _agent;
    private Animator     _anim;
    private AudioSource  _audio;
    private Rigidbody    _rb;
    private int          _currentHealth;

    /// <summary>Read-only health for UI / combat diagnostics.</summary>
    public int CurrentHealth => _currentHealth;

    /// <summary>Returns the level of the currently equipped weapon, falling back to current game level.</summary>
    public int GetEquippedWeaponLevel() => _equippedWeaponLevel > 0 ? _equippedWeaponLevel : (GameManager.Instance != null ? GameManager.Instance.currentLevel : 1);

    // ── Public hooks for the tactical brain (EnemyTacticalBrain) ────────────
    // Surgical surface only — the brain *reads* these to make decisions and
    // calls SuggestTarget(...) to nudge target selection. It never reaches
    // into the state machine. OnDamaged fires from ReceiveDamage so the brain
    // can react to incoming hits without polling every frame.
    public UnityEngine.AI.NavMeshAgent Agent => _agent;
    public Transform CurrentTarget => _target;
    public event System.Action<GameObject> OnDamaged;
    public void SuggestTarget(Transform t)
    {
        if (t == null || t == transform) return;
        IDamageable d = t.GetComponentInParent<IDamageable>();
        if (d == null || !d.IsAlive) return;
        _target = t;
        _targetLockTimer = Time.time + targetLockDuration;
    }

    // CharacterController is intentionally disabled at runtime — kept as a
    // reference only so the jump system can toggle it during the arc.
    private CharacterController _controller;
    private float        _attackTimer;
    private Transform    _target;
    private bool         _playerDamagedThisLife;
    private float        _noTargetTimer;

    // Multiplayer Co-op: when set, bots prioritise these human players over other bots.
    private System.Collections.Generic.List<Transform> _mpHumanTargets;
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

    private HashSet<int> _animParameterHashes;
    private Transform _activeWeaponSocket;
    private Transform _activeWeaponHandBone;
    private GameObject _equippedWeaponPrefab;
    private int _equippedWeaponLevel = -1;
    private Quaternion _weaponBaseLocalRot = Quaternion.identity;
    private PlayerController _cachedPlayer;
    private bool _weaponAttachInProgress;
    private float      _combatReadyBlend;
    private Quaternion _weaponAttachLocalRot;
    private GameObject _weaponAttachCachedObj;
    private float _patrolTimer;
    private bool _isTraversingOffMeshLink;
    private float _flowFieldSteerTimer;
    private Coroutine _attackHitboxRoutine;
    private WeaponHitbox _equippedWeaponHitbox;
    private float _repathTimer;
    private float _chaseGateTimer;
    private Transform _chaseGateTarget;
    private bool _chaseGateResult;
    private float _idleTimer;
    private float _navStuckTimer;
    private State _resumeAfterStuck = State.Patrol;
    private NavMeshPath _pathScratch;
    private Transform _cachedPlayerTransform;

    /// <summary>Duplicate root filtering for OverlapSphere hits (multi-collider rigs).</summary>
    private readonly HashSet<int> _flockOverlapSeenRoots = new HashSet<int>();

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
        // Agent owns position; yaw is smoothed in LateUpdate toward velocity / aim.
        // With autoBraking DISABLED the enemy holds closing speed until melee hysteresis.
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

        // Freeze ALL bone rigidbodies before any FixedUpdate fires to prevent ragdoll explosion at spawn.
        foreach (Rigidbody bone in GetComponentsInChildren<Rigidbody>(true))
        {
            if (bone == null || bone == _rb) continue;
            bone.isKinematic = true;
            bone.useGravity = false;
        }

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

        _pathScratch = new NavMeshPath();
    }

    private void Start()
    {
        CombatVoiceSfx voice = CombatVoiceSfx.GetOrAdd(gameObject);
        voice.ApplyInspectorClips(hurtSounds, deathSounds, hitSound, deathSound);

        detectionInterval = Mathf.Clamp(detectionInterval, 0.1f, 2.0f);
        detectionRadius = Mathf.Clamp(detectionRadius, 4f, 500f);
        aggressiveScanRadius = Mathf.Max(aggressiveScanRadius, Mathf.Max(detectionRadius * 2f, 24f));
        attackCooldown = Mathf.Min(attackCooldown, 0.65f);
        // Keep a real commitment window: too short (the old 0.15s cap) and the
        // AI re-snaps to whoever is marginally nearest every scan, which makes
        // every enemy dogpile the player. Clamp to a sane FFA range instead.
        targetLockDuration = Mathf.Clamp(targetLockDuration, 0.6f, 3f);
        targetSwitchHysteresis = Mathf.Max(0f, targetSwitchHysteresis);

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
        _spawnTime = Time.time;
        _spawnY = transform.position.y;
        _watchdogLastPosition = transform.position;
        ConfigureAgent();
        ApplyAggressionTuning();

        // Minimal runtime safety: ensure agent is enabled and actually sits on the NavMesh.
        EnsureAgentOnNavMesh();

        // Minimal runtime safety: try to acquire a target immediately (don’t wait a full scan tick).
        EvaluateTargets();

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
        _repathTimer = Random.Range(0f, repathInterval);
        _idleTimer = 0f;
        CachePlayerTransform();
    }

    private void CachePlayerTransform()
    {
        PlayerHealth ph = Object.FindFirstObjectByType<PlayerHealth>();
        _cachedPlayerTransform = ph != null ? ph.transform : null;
    }

    private Transform GetCachedPlayerTransform()
    {
        if (_cachedPlayerTransform == null)
            CachePlayerTransform();
        return _cachedPlayerTransform;
    }

    private bool _navMeshErrorLogged;
    private float _nextForceRetargetTime;

    private void EnsureAgentOnNavMesh()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_agent == null) return;

        if (!_agent.enabled)
            _agent.enabled = true;

        if (_agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            _agent.Warp(hit.position);
        }

        if (!_agent.isOnNavMesh && !_navMeshErrorLogged)
        {
            _navMeshErrorLogged = true;
            Debug.LogError($"[EnemyController] {name} is not on NavMesh after SamplePosition+Warp. pos={transform.position}", this);
        }
    }

    private void OnEnable()
    {
        if (!s_aliveEnemies.Contains(this))
            s_aliveEnemies.Add(this);
    }

    private void OnDisable()
    {
        s_aliveEnemies.Remove(this);

        if (_scanCoroutine != null)
        {
            StopCoroutine(_scanCoroutine);
            _scanCoroutine = null;
        }
    }

    public static void CopyAliveEnemies(List<EnemyController> buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        for (int i = 0; i < s_aliveEnemies.Count; i++)
        {
            EnemyController e = s_aliveEnemies[i];
            if (e != null && e.IsAlive)
                buffer.Add(e);
        }
    }

    private static PlayerController ResolveScenePlayer()
    {
        float t = Time.unscaledTime;
        if (s_scenePlayer == null || t - s_scenePlayerRefreshTime > 1.5f)
        {
            s_scenePlayer = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
            s_scenePlayerRefreshTime = t;
        }

        return s_scenePlayer;
    }

    private static Transform ResolveScenePlayerTransform()
    {
        PlayerController pc = ResolveScenePlayer();
        return pc != null ? pc.transform : null;
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

        // Rooftop / stuck / NavMesh watchdog — surgical, runs once per frame, no allocations.
        if (ShouldRunWatchdogThisFrame())
        {
            TickWatchdog();
            RecoverOnlyIfOutOfMap();
        }

        TickCombatDrive();

        // Fail-Safe Timer:
        if (_target == null)
        {
            _noTargetTimer += Time.deltaTime;
            if (_noTargetTimer >= 0.85f)
            {
                _noTargetTimer = 0f;
                EvaluateTargets();
                if (_target != null && (_state == State.Idle || _state == State.Patrol) && CanEngageChase(_target))
                    TransitionTo(State.Chase);
            }
        }
        else
        {
            _noTargetTimer = 0f;
        }

        TryGlobalAiTransitions();

        _attackTimer -= Time.deltaTime;

        // Hit flash recovery
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f) RestoreOriginalColors();
        }

        switch (_state)
        {
            case State.Idle:           UpdateIdle();           break;
            case State.Patrol:         UpdatePatrol();         break;
            case State.Chase:          UpdateChase();          break;
            case State.Attack:         UpdateAttack();         break;
            case State.Evade:          UpdateEvade();          break;
            case State.StuckRecovery:  UpdateStuckRecovery(); break;
            case State.Flinch:         UpdateFlinch();         break;
            case State.Jumping:        UpdateJumping();        break;
        }

        if (ShouldSyncAnimatorThisFrame())
            SyncAnimator();
    }

    private bool ShouldRunWatchdogThisFrame()
    {
        if ((Time.frameCount + GetInstanceID()) % 2 == 0)
            return true;

        return _state == State.Chase || _state == State.Attack || _target != null;
    }

    private bool ShouldSyncAnimatorThisFrame()
    {
        if (_state == State.Chase || _state == State.Attack || _state == State.Evade)
            return true;

        if ((Time.frameCount + GetInstanceID()) % 2 == 0)
            return true;

        Transform player = ResolveScenePlayerTransform();
        if (player == null)
            return true;

        return (player.position - transform.position).sqrMagnitude < 900f;
    }

    private void TryGlobalAiTransitions()
    {
        if (_state == State.Dead || EndMatchCinematic.GameplayLocked)
            return;

        if (ShouldEnterEvade())
        {
            TransitionTo(State.Evade);
            return;
        }

        if (ShouldEnterStuckRecovery())
            TransitionTo(State.StuckRecovery);
    }

    private bool ShouldEnterEvade()
    {
        if (_state == State.Dead || _state == State.Evade || _state == State.StuckRecovery ||
            _state == State.Jumping)
            return false;

        if (maxHealth <= 0)
            return false;

        return (float)_currentHealth / maxHealth <= lowHealthPercent;
    }

    private bool ShouldEnterStuckRecovery()
    {
        if (_state == State.Dead || _state == State.StuckRecovery || _state == State.Jumping ||
            _state == State.Attack || _state == State.Flinch || _state == State.Idle)
            return false;

        if (!IsAgentReady())
            return false;

        if (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.08f)
        {
            _navStuckTimer = 0f;
            return false;
        }

        if (_agent.velocity.magnitude > stuckVelocityThreshold)
        {
            _navStuckTimer = 0f;
            return false;
        }

        _navStuckTimer += Time.deltaTime;
        return _navStuckTimer >= stuckTimeThreshold;
    }

    private void FixedUpdate()
    {
        ApplyFlockingSteering();
    }

    private void LateUpdate()
    {
        ApplyMovementAlignedRotation();

        if (stabilizeWeaponSocketAgainstHandPose &&
            _activeWeaponSocket != null && _activeWeaponHandBone != null)
        {
            _activeWeaponSocket.localRotation =
                Quaternion.Inverse(_activeWeaponHandBone.localRotation) *
                Quaternion.Euler(weaponSocketLocalEulerAngles);
        }

        // ── Level 2 katana grip — carry pose only (not during attack) ───────
        //
        // During the ATTACK state the weapon follows the animated hand bone
        // naturally through the parent chain so the swing plays correctly.
        //
        // During every OTHER state (idle / walk / chase / flinch) we lock the
        // carry pose to match the player's blade angle exactly:
        //
        //   playerCharSpaceRot  = Inverse(player.root) * player.weapon.rotation
        //   enemy.weapon.rotation = enemy.root * playerCharSpaceRot * extraOffset
        //
        // katanaCarryExtraEuler lets designers tweak the raise angle in Play Mode.
        if (_equippedWeaponLevel == 2 &&
            equippedWeaponObject  != null &&
            _activeWeaponHandBone != null &&
            _state != State.Attack)          // ← let the swing anim run freely
        {
            if (_cachedPlayer == null)
                _cachedPlayer = ResolveScenePlayer();

            // ── POSITION: pin the weapon pivot to the hand bone + grip offset ──
            // This ensures the handle is physically inside the palm regardless
            // of which socket or local-position offset was set at attach-time.
            equippedWeaponObject.transform.position =
                _activeWeaponHandBone.position
                + _activeWeaponHandBone.rotation * katanaHandGripOffset;

            // ── ROTATION: match the player's blade angle in character space ───
            if (_cachedPlayer != null && _cachedPlayer.equippedWeaponObject != null)
            {
                Quaternion playerCharSpaceRot =
                    Quaternion.Inverse(_cachedPlayer.transform.rotation)
                    * _cachedPlayer.equippedWeaponObject.transform.rotation;

                Quaternion extra = Quaternion.Euler(katanaCarryExtraEuler);

                equippedWeaponObject.transform.rotation =
                    transform.rotation * playerCharSpaceRot * extra;
            }
            else
            {
                // Fallback — no player in scene (multiplayer bot-only match).
                equippedWeaponObject.transform.rotation =
                    _activeWeaponHandBone.rotation * Quaternion.Euler(0f, 0f, 160f);
            }
        }

        // ── Level 12 saw — carry pose (idle/walk/chase, NOT during attack) ──────
        //
        // Goal: same orientation as the black player's saw, handle in the palm.
        //
        // Problem: player socket is under j_wrist_ri (Ronin); enemy socket is under
        // bip_hand_R (Crosby). Different skeletons → different hand bone positions
        // in character space, so a plain character-space position copy puts the saw
        // at the correct body-relative position for the PLAYER but not for Crosby's
        // proportions (hand ends up in the wrong place relative to bip_hand_R).
        //
        // Fix (two steps):
        //   1. ROTATION — character-space copy from the player (bone-axis-agnostic).
        //   2. POSITION — anchor the SAW'S HANDLE point to bip_hand_R in world space.
        //
        // The handle's position in the saw's own local space is derived from the
        // player's grip setup (localPosition / localRotation on j_wrist_ri):
        //
        //   Parent-child relation: saw.world = parent.world + parent.rot * localPos
        //   → parent.world = saw.world - parent.rot * localPos
        //   → handleInSawLocal = -(Inverse(localRot) * localPos)
        //
        // This is purely the saw's local geometry — independent of which skeleton
        // holds it — so the same value works for both Ronin and Crosby.
        if (_equippedWeaponLevel == 12 &&
            equippedWeaponObject  != null &&
            _activeWeaponHandBone != null &&
            _state                != State.Attack)
        {
            if (_cachedPlayer == null)
                _cachedPlayer = ResolveScenePlayer();

            if (_cachedPlayer != null && _cachedPlayer.equippedWeaponObject != null)
            {
                // Step 1 — rotation: character-space copy from player (bone-independent).
                Quaternion playerCharSpaceRot =
                    Quaternion.Inverse(_cachedPlayer.transform.rotation)
                    * _cachedPlayer.equippedWeaponObject.transform.rotation;

                Quaternion sawWorldRot =
                    transform.rotation * playerCharSpaceRot * Quaternion.Euler(sawCarryExtraEuler);

                equippedWeaponObject.transform.rotation = sawWorldRot;

                // Step 2 — position: anchor handle to bip_hand_R.
                // Player setup: localPos=(-0.066,-0.39,0.044), localRot=Euler(-177.177,-175.886,88.481)
                // handleInSawLocal = -(Inverse(localRot) * localPos)
                Vector3 handleInSawLocal =
                    -(Quaternion.Inverse(Quaternion.Euler(-177.177f, -175.886f, 88.481f))
                      * new Vector3(-0.066f, -0.39f, 0.044f));

                equippedWeaponObject.transform.position =
                    _activeWeaponHandBone.position - sawWorldRot * handleInSawLocal;
            }
            else
            {
                // Fallback: no player in scene — anchor to hand bone with catalogue euler.
                equippedWeaponObject.transform.rotation =
                    _activeWeaponHandBone.rotation * Quaternion.Euler(-177.177f, -175.886f, 88.481f);
            }
        }

        // ── Level 9 axe — attack swing direction correction ──────────────────
        //
        // Crosby's bip_hand_R has different local axes than the player's j_wrist_ri.
        // The attack animation rotates the hand bone; on the player this produces a
        // correct forward swing, but on Crosby the same rotation produces a reversed
        // swing because the bone's local forward is mirrored.
        //
        // Fix: after the animator updates the hand bone each frame, multiply the
        // weapon's current localRotation by axeAttackSwingCorrection (default 180° Y)
        // to flip the swing back to the correct direction.
        //
        // This block only runs during the Attack state — idle grip is untouched.
        // Tune axeAttackSwingCorrection in the Inspector during Play Mode if needed.
        if (_equippedWeaponLevel == 9 &&
            equippedWeaponObject  != null &&
            _state                == State.Attack)
        {
            // SET to a fixed value each frame — never compound/multiply current
            // localRotation or it oscillates every frame (base → corrected → base...).
            // _weaponBaseLocalRot is the idle grip (0,180,90); axeAttackSwingCorrection
            // is applied on top once per frame so the result is always stable.
            equippedWeaponObject.transform.localRotation =
                _weaponBaseLocalRot * Quaternion.Euler(axeAttackSwingCorrection);
        }

        // ── Level 12 saw — attack swing direction correction ─────────────────
        //
        // Same bone-axis mismatch as Level 9 (bip_hand_R vs j_wrist_ri). The attack
        // animation rotates bip_hand_R so the swing appears reversed on Crosby.
        // Apply a stable fixed correction each frame during Attack only.
        //
        // We also correct localPosition so the handle stays in bip_hand_R's palm
        // (the position set at attach-time is tuned for j_wrist_ri and causes the
        // saw to "fly" during the swing if left uncorrected).
        //
        // Derivation — we want: saw.world.pos + saw.world.rot * handleInSawLocal = bip_hand_R
        //   → localPos = attackLocalRot * Inverse(playerLocalRot) * playerLocalPos
        //   where handleInSawLocal = -(Inverse(playerLocalRot) * playerLocalPos)
        if (_equippedWeaponLevel == 12 &&
            equippedWeaponObject  != null &&
            _state                == State.Attack)
        {
            Quaternion attackLocalRot = _weaponBaseLocalRot * Quaternion.Euler(sawAttackSwingCorrection);
            equippedWeaponObject.transform.localRotation = attackLocalRot;

            // Anchor the handle to bip_hand_R in local space.
            equippedWeaponObject.transform.localPosition =
                attackLocalRot
                * Quaternion.Inverse(Quaternion.Euler(-177.177f, -175.886f, 88.481f))
                * new Vector3(-0.066f, -0.39f, 0.044f);
        }
    }


    /// <summary>
    /// Smoothly aligns the body with horizontal NavMesh velocity so feet/locomotion match travel direction (no moonwalking).
    /// NavMeshAgent.updateRotation stays false — rotation is driven here via Slerp.
    /// </summary>
    private void ApplyMovementAlignedRotation()
    {
        if (_state == State.Dead || EndMatchCinematic.GameplayLocked)
            return;

        if (_state == State.Attack || _state == State.Flinch || _state == State.Jumping)
            return;

        if (_agent == null || !_agent.enabled)
            return;

        Vector3 flatVel = _agent.velocity;
        flatVel.y = 0f;

        if (flatVel.sqrMagnitude > 0.07f)
        {
            FaceDirection(flatVel);
            return;
        }

        if (_state == State.Chase && _target != null)
        {
            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.02f)
                FaceDirection(toTarget);
        }
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

        if (IsTargetValid(_target) && CanEngageChase(_target))
        {
            TransitionTo(State.Chase);
            return;
        }

        _idleTimer += Time.deltaTime;
        if (_idleTimer >= idleToPatrolDelay)
            TransitionTo(State.Patrol);
    }

    private void UpdatePatrol()
    {
        if (!IsAgentReady())
            return;

        if (IsTargetValid(_target) && CanEngageChase(_target))
        {
            TransitionTo(State.Chase);
            return;
        }

        _agent.isStopped = false;
        _agent.speed = moveSpeed;
        _agent.stoppingDistance = Mathf.Max(0.25f, attackRadius * 0.35f);

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

    private void UpdateChase()
    {
        if (HandleOffMeshLinkTraversal())
            return;

        if (!IsTargetValid(_target))
        {
            _target = null;
            TransitionTo(State.Patrol);
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);
        float engageDist = attackRadius + attackRangePadding;

        if (dist <= engageDist && HasCombatVisionTo(_target) &&
            IsFacingTarget(_target, attackFacingAngle))
        {
            TransitionTo(State.Attack);
            return;
        }

        if (IsAgentReady())
        {
            if (_agent.isStopped)
                _agent.isStopped = false;

            float targetSpeed = chaseSpeed;
            PlayerController pc = _target != null
                ? _target.GetComponentInParent<PlayerController>()
                : null;
            if (pc != null)
            {
                float playerTopSpeed = pc.moveSpeed * pc.sprintMultiplier;
                float boosted = Mathf.Max(chaseSpeed, playerTopSpeed + 0.35f);
                targetSpeed = Mathf.Min(sprintChaseSpeed, boosted);
            }

            _agent.speed = targetSpeed;
            _agent.acceleration = agentAcceleration;
            _agent.angularSpeed = agentAngularSpeed;
            _agent.autoBraking = false;
            _agent.stoppingDistance = Mathf.Max(0.08f, attackRadius * 0.85f);

            TryRefreshChaseDestination();
        }

        CheckAndJumpIfStuck();
        TickCombatManeuver();
    }

    /// <summary>
    /// Throttled NavMesh validation / destination refresh (see <see cref="repathInterval"/>).
    /// </summary>
    private void TryRefreshChaseDestination()
    {
        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f)
            return;

        _repathTimer = repathInterval;

        if (_target == null || !IsAgentReady())
            return;

        Vector3 dest = ClampChaseDestination(_target.position);
        if (!_agent.pathPending && _agent.hasPath && _agent.pathStatus != NavMeshPathStatus.PathInvalid)
        {
            float destDelta = (_agent.destination - dest).sqrMagnitude;
            if (destDelta < 2.25f && (_agent.remainingDistance > 0.35f || _agent.velocity.sqrMagnitude > 0.04f))
                return;
        }

        if (useFlowFieldNavigation && FlowFieldManager.Instance != null)
        {
            _flowFieldSteerTimer = 0f;
            DriveChaseWithFlowField();
        }
        else
        {
            NavMesh.CalculatePath(transform.position, _target.position, NavMesh.AllAreas, _pathScratch);

            if (_pathScratch.status != NavMeshPathStatus.PathInvalid)
                _agent.SetDestination(dest);
            else
            {
                RepositionOnInvalidPath();
                if (debugAI)
                    Debug.Log($"[EnemyAI] {name} chase path {_pathScratch.status}, repositioning.", this);
            }

            return;
        }

        NavMesh.CalculatePath(transform.position, _target.position, NavMesh.AllAreas, _pathScratch);
        if (_pathScratch.status == NavMeshPathStatus.PathInvalid)
        {
            RepositionOnInvalidPath();
            if (debugAI)
                Debug.Log($"[EnemyAI] {name} flow chase goal unreachable ({_pathScratch.status}).", this);
        }
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
            _agent.velocity = Vector3.zero;
        }

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        float breakDist = attackRadius + attackRangePadding + breakAttackPadding;
        if (dist > breakDist || !HasCombatVisionTo(_target))
        {
            TransitionTo(State.Chase);
            return;
        }

        FaceDirection(toTarget);

        float engageDist = attackRadius + attackRangePadding;
        if (dist <= engageDist && IsFacingTarget(_target, attackFacingAngle) && HasCombatVisionTo(_target))
        {
            if (_attackTimer <= 0f)
            {
                _attackTimer = attackCooldown;
                ExecuteAttack();
            }
        }
    }

    private void UpdateEvade()
    {
        if (HandleOffMeshLinkTraversal())
            return;

        float hpPct = (float)_currentHealth / Mathf.Max(1, maxHealth);
        if (hpPct > lowHealthPercent + 0.02f)
        {
            TransitionTo(IsTargetValid(_target) && CanEngageChase(_target) ? State.Chase : State.Patrol);
            return;
        }

        if (!IsAgentReady())
            return;

        _agent.isStopped = false;
        _agent.speed = moveSpeed;
        _agent.stoppingDistance = Mathf.Max(0.3f, attackRadius * 0.4f);

        Transform player = GetCachedPlayerTransform();
        if (player != null)
        {
            Vector3 away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
            {
                Vector3 fleeGoal = transform.position + away.normalized * 12f;
                if (NavMesh.SamplePosition(fleeGoal, out NavMeshHit hit, 14f, NavMesh.AllAreas))
                    _agent.SetDestination(hit.position);
            }
        }
        else
        {
            SetRandomPatrolDestination();
        }
    }

    private void UpdateStuckRecovery()
    {
        if (!IsAgentReady())
        {
            TransitionTo(_resumeAfterStuck);
            return;
        }

        _agent.ResetPath();
        _navStuckTimer = 0f;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit near, 3f, NavMesh.AllAreas))
        {
            if ((near.position - transform.position).sqrMagnitude > 0.04f)
                _agent.Warp(near.position);
        }

        Vector3 lateral = transform.position + Random.insideUnitSphere * 4f;
        lateral.y = transform.position.y;
        if (NavMesh.SamplePosition(lateral, out NavMeshHit dest, 8f, NavMesh.AllAreas))
            _agent.SetDestination(dest.position);

        if (debugAI)
            Debug.Log($"[EnemyAI] {name} stuck recovery → {_resumeAfterStuck}", this);

        TransitionTo(_resumeAfterStuck);
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
    //  FLOCKING — separation / alignment / cohesion as NavMeshAgent.Move offsets
    //  Single NonAlloc overlap per enemy per physics step (radius = max rule).
    //  Does not replace pathfinding; Chase/Patrol still own SetDestination.
    // ════════════════════════════════════════════════════════════════════════

    private static readonly Collider[] _flockColliderBuffer = new Collider[32];

    private void ApplyFlockingSteering()
    {
        if (_state == State.Dead || _state == State.Attack || _state == State.Flinch ||
            _state == State.Jumping || _state == State.StuckRecovery)
            return;

        if (EndMatchCinematic.GameplayLocked)
            return;

        Transform player = ResolveScenePlayerTransform();
        if (player != null)
        {
            float distSqr = (player.position - transform.position).sqrMagnitude;
            int phase = Time.frameCount + GetInstanceID();
            if (distSqr > 900f && phase % 4 != 0)
                return;
            if (distSqr > 2500f && phase % 2 != 0)
                return;
        }

        if (!IsAgentReady() || !_agent.isOnNavMesh || _agent.isStopped)
            return;

        float queryRadius = Mathf.Max(0.15f, Mathf.Max(separationRadius, Mathf.Max(alignmentRadius, cohesionRadius)));
        Vector3 origin = transform.position + Vector3.up * 0.9f;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            queryRadius,
            _flockColliderBuffer,
            ResolveHittableMask(),
            QueryTriggerInteraction.Ignore);

        Vector3 myFlat = new Vector3(transform.position.x, 0f, transform.position.z);

        Vector3 separation = Vector3.zero;
        Vector3 alignVelSum = Vector3.zero;
        int alignCount = 0;
        Vector3 cohesionPosSum = Vector3.zero;
        int cohesionCount = 0;

        _flockOverlapSeenRoots.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _flockColliderBuffer[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            EnemyController otherEc = col.GetComponentInParent<EnemyController>();
            if (otherEc != null)
            {
                if (otherEc == this || !otherEc.IsAlive)
                    continue;

                int rootId = otherEc.gameObject.GetInstanceID();
                if (!_flockOverlapSeenRoots.Add(rootId))
                    continue;

                Vector3 otherFlat = new Vector3(otherEc.transform.position.x, 0f, otherEc.transform.position.z);
                Vector3 offset = myFlat - otherFlat;
                float dist = offset.magnitude;

                if (dist > 0.001f && dist < separationRadius)
                {
                    float t = (separationRadius - dist) / Mathf.Max(0.001f, separationRadius);
                    separation += offset.normalized * t;
                }
                else if (dist <= 0.001f)
                {
                    float jitter = GetInstanceID() < rootId ? 1f : -1f;
                    separation += new Vector3(jitter, 0f, jitter * 0.35f).normalized;
                }

                if (dist < alignmentRadius && otherEc._agent != null)
                {
                    Vector3 ov = otherEc._agent.velocity;
                    ov.y = 0f;
                    alignVelSum += ov;
                    alignCount++;
                }

                if (dist < cohesionRadius)
                {
                    cohesionPosSum += otherEc.transform.position;
                    cohesionCount++;
                }

                continue;
            }

            PlayerHealth playerHealth = col.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                int rootId = playerHealth.gameObject.GetInstanceID();
                if (!_flockOverlapSeenRoots.Add(rootId))
                    continue;

                Vector3 pFlat = new Vector3(playerHealth.transform.position.x, 0f, playerHealth.transform.position.z);
                Vector3 offset = myFlat - pFlat;
                float dist = offset.magnitude;
                if (dist > 0.001f && dist < separationRadius)
                {
                    float t = (separationRadius - dist) / Mathf.Max(0.001f, separationRadius);
                    separation += offset.normalized * t;
                }
            }
        }

        Vector3 alignment = Vector3.zero;
        if (alignCount > 0)
        {
            Vector3 avgVel = alignVelSum / alignCount;
            Vector3 myVel = _agent.velocity;
            myVel.y = 0f;
            alignment = avgVel - myVel;
        }

        Vector3 cohesion = Vector3.zero;
        if (cohesionCount > 0)
        {
            Vector3 center = cohesionPosSum / cohesionCount;
            Vector3 toCenter = new Vector3(center.x, 0f, center.z) - myFlat;
            if (toCenter.sqrMagnitude > 0.0001f)
                cohesion = toCenter.normalized;
        }

        Vector3 flock = separation * separationWeight + alignment * alignmentWeight + cohesion * cohesionWeight;
        flock.y = 0f;
        flock = Vector3.ClampMagnitude(flock, maxFlockSteering);

        if (flock.sqrMagnitude < 1e-10f)
            return;

        _agent.Move(flock * Time.fixedDeltaTime);
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
            if (WorldArenaStabilizer.Instance != null &&
                WorldArenaStabilizer.Instance.TryRecoverTransform(transform, "fall_below_world"))
            {
                EvaluateTargets();
                return;
            }

            WarpToNearestNavMeshAfterFall();
            EvaluateTargets();
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
        if (speed > obstacleBlockedSpeedThreshold)
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
        float distToTarget = _target != null ? Vector3.Distance(transform.position, _target.position) : -1f;
        Debug.Log($"[L9Combat] ATTACK_STARTED attacker={name} target={(_target != null ? _target.name : "null")} dist={distToTarget:F2} attackRadius={attackRadius:F2}");

        if (CombatDebug.Enabled)
            CombatDebug.Log($"EnemyAttack started enemy={name}");

        // KatanaCombatHandler owns the full attack pipeline for the katana level.
        // It fires the animator trigger, casts the hit-box on the animation event,
        // and routes damage through Photon RPC in multiplayer — so we return early
        // and skip the WeaponHitbox coroutine entirely.
        KatanaCombatHandler katanaHandler = GetComponent<KatanaCombatHandler>();
        if (katanaHandler != null)
        {
            katanaHandler.TriggerAttack();
            return;
        }

        SetAnimatorTrigger(HashAttack);

        if (_attackHitboxRoutine != null)
        {
            StopCoroutine(_attackHitboxRoutine);
            _attackHitboxRoutine = null;
        }
        if (_equippedWeaponHitbox != null)
            _equippedWeaponHitbox.DisableHitbox();

        // Damage fires only through the timed hitbox window so it aligns with
        // the swing animation — the immediate strike was removed because it applied
        // damage at animation start (before the weapon reaches the player), making
        // enemies appear to hit from a distance.
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
        {
            Debug.LogWarning($"[L9Combat] HITBOX_NULL — attacker={name} has no WeaponHitbox, attack will deal no damage.");
            yield break;
        }

        _equippedWeaponHitbox.DisableHitbox();
        Debug.Log($"[L9Combat] HITBOX_DISABLED (pre-windup) attacker={name} windup={attackHitboxWindup:F2}s");

        if (attackHitboxWindup > 0f)
            yield return new WaitForSeconds(attackHitboxWindup);

        // Post-windup distance gate: if the target moved out of melee range during
        // the windup animation, cancel the swing before the hitbox becomes active.
        if (_target != null)
        {
            float sqrDist  = (_target.position - transform.position).sqrMagnitude;
            float sqrRange = meleeAttackRange * meleeAttackRange;
            if (sqrDist > sqrRange)
            {
                float actualDist = Mathf.Sqrt(sqrDist);
                Debug.Log($"[MeleeGate] SWING_CANCELLED_POST_WINDUP — attacker={name} target={_target.name} " +
                          $"dist={actualDist:F2} meleeAttackRange={meleeAttackRange:F2} (target left range during windup)");
                _equippedWeaponHitbox.DisableHitbox();
                _attackHitboxRoutine = null;
                yield break;
            }
        }

        Physics.SyncTransforms();
        _equippedWeaponHitbox.meleeAttackRange = meleeAttackRange;
        _equippedWeaponHitbox.damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage));
        _equippedWeaponHitbox.EnableHitbox();
        Debug.Log($"[L9Combat] HITBOX_ENABLED attacker={name} damage={_equippedWeaponHitbox.damage} activeWindow={attackHitboxActiveTime:F2}s meleeAttackRange={meleeAttackRange:F2}");

        if (attackHitboxActiveTime > 0f)
            yield return new WaitForSeconds(attackHitboxActiveTime);

        _equippedWeaponHitbox.DisableHitbox();
        Debug.Log($"[L9Combat] HITBOX_DISABLED (end of window) attacker={name}");
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
        CombatVoiceSfx.GetOrAdd(gameObject).PlayHurt();

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
        if (MultiplayerMode.IsMultiplayer && MultiplayerMode.ActiveMode == MpGameMode.CoopSurvival)
        {
            if (attackerRoot != null && attackerRoot.GetComponentInParent<EnemyController>() != null)
                return;
        }

        // Brief spawn-protection grace window: avoids enemies being killed in
        // the first frames before the AI has a chance to react / spread out.
        // Not invincibility — duration is short and tunable in Inspector.
        if (spawnProtectionDuration > 0f
            && _spawnTime >= 0f
            && Time.time - _spawnTime < spawnProtectionDuration)
        {
            return;
        }

        // Occlusion is already checked by every caller before ReceiveDamage is
        // invoked (WeaponHitbox.TryDamageCollider, PlayerController.TryDamageTargetFromPoint,
        // etc.). A second linecast here doubles the false-block surface area: if the
        // first check passes but an in-between geometry grazes the second origin
        // (attacker.position+1.6m vs the caller's origin), damage is silently eaten.
        // We keep the log for CombatDebug diagnostics but skip the early return.
        if (CombatDebug.Enabled)
        {
            bool blocked = DamageOcclusion.IsBlocked(attackerRoot, gameObject);
            CombatDebug.Log($"[ReceiveDamage] blockedByWall={blocked} (occlusion not re-enforced here)");
        }

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
                // Lock onto the recent attacker so retaliation actually sticks.
                _targetLockTimer = Time.time + targetLockDuration;
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

        int healthBefore = _currentHealth;
        if (CombatDebug.Enabled)
            CombatDebug.Log($"applyingDamage amount={appliedDamage} target={gameObject.name}");

        TakeDamage(appliedDamage, byPlayer: fromPlayer);

        if (CombatDebug.Enabled)
            CombatDebug.Log($"healthBefore={healthBefore} healthAfter={_currentHealth}");

        // Notify the tactical brain (subscriber-only; null-safe).
        OnDamaged?.Invoke(attackerRoot);
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
                PlayerHealth player = null;
                if (_lastAttacker != null)
                {
                    player = _lastAttacker.GetComponentInParent<PlayerHealth>();
                }
                if (player == null)
                {
                    player = FindFirstObjectByType<PlayerHealth>(); // Fallback
                }
                
                string playerId = MatchStatsManager.BuildCombatantId(player);
                MatchStatsManager.Instance.RecordKill(playerId);

                // Enforce that single-player challenges and bounties are only advanced
                // if the local player was the one who actually dealt the final blow.
                #if PUN_2_OR_NEWER
                bool isLocalKill = (player != null && player.GetComponent<PhotonView>() != null && player.GetComponent<PhotonView>().IsMine);
                #else
                bool isLocalKill = true;
                #endif

                if (isLocalKill && SessionManager.Instance != null)
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

        CombatVoiceSfx.GetOrAdd(gameObject).PlayDeath();

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

        // Normalise by sprint cap so full-speed chases register ~1.0 on the blend tree; optional runAnimationMultiplier
        // nudges foot speed to match visible stride when the agent is at top chase velocity.
        float refSpeed = Mathf.Max(0.01f, sprintChaseSpeed);
        float normalizedSpeed = Mathf.Clamp01((actualSpeed * runAnimationMultiplier) / refSpeed);

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
        if (debugEnemyMovement && _state == State.Chase && _agent != null && (Time.frameCount % 15) == 0)
        {
            Debug.Log(
                $"[EnemyMovement] enemy={name} velMag={actualSpeed:F2} agentSpeed={_agent.speed:F2} " +
                $"animNorm={normalizedSpeed:F2} state={_state}",
                this);
        }

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
        if (dmg == null || !dmg.IsAlive)
            return false;

        if (!IsInsidePlayableBounds(t.position))
            return false;

        if (!float.IsNaN(_spawnY))
        {
            float vertical = t.position.y - _spawnY;
            if (vertical > maxChaseVerticalDelta + 1.5f || vertical < -6f)
                return false;
        }

        return true;
    }

    private bool IsInsidePlayableBounds(Vector3 worldPos)
    {
        if (WorldArenaStabilizer.Instance != null)
            return WorldArenaStabilizer.Instance.IsInsidePlayableBounds(worldPos);

        if (LevelBuilder.Instance != null)
        {
            float half = LevelBuilder.Instance.arenaHalfSize * 0.98f;
            return Mathf.Abs(worldPos.x) <= half && Mathf.Abs(worldPos.z) <= half;
        }

        return true;
    }

    private bool TryRecoveryWarp(Vector3 desired, float sampleRadius, string reason)
    {
        if (_agent == null || !_agent.enabled)
            return false;

        float now = Time.time;
        if (now - _lastRecoveryWarpTime < recoveryWarpCooldown)
            return false;

        if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return false;

        if (!float.IsNaN(_spawnY) && (hit.position.y - _spawnY) > watchdogMaxYAboveSpawn + 0.5f)
            return false;

        _agent.Warp(hit.position);
        _agent.ResetPath();
        _lastRecoveryWarpTime = now;
        _highSinceTime = -1f;
        _lowProgressSinceTime = -1f;
        _partialPathSinceTime = -1f;

        if (debugWatchdog || debugAI)
            Debug.Log($"[EnemyWatchdog] {name} recovery warp ({reason}) -> {hit.position}");

        return true;
    }

    private static int _aiVisionBlockMask;
    private static bool _aiVisionBlockMaskCached;

    /// <summary>
    /// Solid environment layers only — excludes characters so walls/doors block vision.
    /// </summary>
    private static int ResolveAiVisionBlockMask()
    {
        if (_aiVisionBlockMaskCached)
            return _aiVisionBlockMask;

        int mask = 0;
        void Add(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                mask |= 1 << layer;
        }

        Add("Environment");
        Add("Building");
        Add("StaticObstacle");
        Add("Wall");
        Add("Door");

        _aiVisionBlockMask = mask;
        _aiVisionBlockMaskCached = true;
        return _aiVisionBlockMask;
    }

    /// <summary>Chest-to-chest line test using environment-only layers.</summary>
    private bool HasCombatVisionTo(Transform t)
    {
        if (t == null)
            return true;

        int mask = ResolveAiVisionBlockMask();
        if (mask == 0)
            return true;

        Vector3 from = transform.position + Vector3.up * lineOfSightEyeHeight;
        Vector3 to = t.position + Vector3.up * lineOfSightEyeHeight;
        return !Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore);
    }

    private bool IsInCombatFieldOfView(Transform t)
    {
        Vector3 to = t.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(transform.forward, to) <= fieldOfViewAngle * 0.5f;
    }

    private bool IsFacingTarget(Transform t, float maxDegrees)
    {
        Vector3 to = t.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(transform.forward, to) <= maxDegrees;
    }

    /// <summary>
    /// Patrol→Chase gate: range, FOV cone, AI vision, full NavMesh path.
    /// </summary>
    private bool CanEngageChase(Transform t)
    {
        if (!IsTargetValid(t))
            return false;

        float d = Vector3.Distance(transform.position, t.position);
        bool isPlayer = t.GetComponentInParent<PlayerHealth>() != null;
        float maxRange = isPlayer
            ? Mathf.Max(detectionRadius, aggressiveScanRadius)
            : detectionRadius * 1.05f;
        if (d > maxRange)
            return false;

        if (_chaseGateTarget == t && Time.time < _chaseGateTimer)
            return _chaseGateResult;

        NavMesh.CalculatePath(transform.position, t.position, NavMesh.AllAreas, _pathScratch);
        _chaseGateTarget = t;
        _chaseGateTimer = Time.time + ChaseGateRecheckInterval;
        _chaseGateResult = _pathScratch.status == NavMeshPathStatus.PathComplete
            || _pathScratch.status == NavMeshPathStatus.PathPartial;
        return _chaseGateResult;
    }

    /// <summary>
    /// Keeps agents chasing: re-enable movement, refresh stale paths, transition idle→chase when a target exists.
    /// </summary>
    private void TickCombatDrive()
    {
        if (_state == State.Dead || EndMatchCinematic.GameplayLocked)
            return;

        if (_agent != null && _agent.enabled && !_agent.isOnNavMesh)
            EnsureAgentOnNavMesh();

        if (_target == null)
            return;

        if (!IsTargetValid(_target))
        {
            _target = null;
            EvaluateTargets();
            if (_target == null)
                return;
        }

        if ((_state == State.Idle || _state == State.Patrol) && CanEngageChase(_target))
        {
            TransitionTo(State.Chase);
            return;
        }

        if (_state != State.Chase && _state != State.Attack && _state != State.Evade
            && _state != State.Jumping && _state != State.Flinch && _state != State.StuckRecovery)
            return;

        if (!IsAgentReady())
            return;

        _agent.isStopped = false;

        bool needsDestination = !_agent.hasPath || _agent.pathPending
            || (_agent.remainingDistance <= _agent.stoppingDistance + 0.15f && _agent.velocity.sqrMagnitude < 0.04f);
        bool stalePartial = _agent.pathStatus == NavMeshPathStatus.PathPartial
            && _agent.remainingDistance > 6f
            && _agent.velocity.sqrMagnitude < stuckVelocityThreshold;

        if (needsDestination || stalePartial || Time.time >= _nextForceRetargetTime)
        {
            _nextForceRetargetTime = Time.time + Mathf.Max(0.25f, repathInterval);
            TrySetChaseDestinationValidated();
        }
    }

    private void TrySetChaseDestinationValidated()
    {
        if (_target == null || !IsAgentReady())
            return;

        Vector3 dest = ClampChaseDestination(_target.position);
        if (!_agent.pathPending && _agent.hasPath && _agent.pathStatus != NavMeshPathStatus.PathInvalid)
        {
            float destDelta = (_agent.destination - dest).sqrMagnitude;
            if (destDelta < 2.25f)
                return;
        }

        NavMesh.CalculatePath(transform.position, _target.position, NavMesh.AllAreas, _pathScratch);

        if (_pathScratch.status != NavMeshPathStatus.PathInvalid)
        {
            _agent.SetDestination(dest);
            return;
        }

        RepositionOnInvalidPath();
        if (debugAI)
            Debug.Log($"[EnemyAI] {name} initial chase path {_pathScratch.status}, repositioned.", this);
    }

    private Vector3 ClampChaseDestination(Vector3 raw)
    {
        if (float.IsNaN(_spawnY))
            return raw;

        float maxY = _spawnY + maxChaseVerticalDelta;
        if (raw.y <= maxY)
            return raw;

        Vector3 clamped = raw;
        clamped.y = maxY;
        if (NavMesh.SamplePosition(clamped, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            return hit.position;

        return clamped;
    }

    private void RepositionOnInvalidPath()
    {
        if (!IsAgentReady())
            return;

        Vector3 probe = transform.position + Random.insideUnitSphere * 5f;
        probe.y = float.IsNaN(_spawnY) ? transform.position.y : _spawnY;

        if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            _agent.ResetPath();
            _agent.SetDestination(hit.position);
            return;
        }

        TryRecoveryWarp(transform.position, 4f, "invalid_path");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void ConfigureAgent()
    {
        if (_agent == null) return;

        _agent.speed                 = chaseSpeed;
        _agent.stoppingDistance      = Mathf.Max(0.08f, attackRadius * 0.85f);
        _agent.acceleration          = agentAcceleration;
        _agent.angularSpeed          = agentAngularSpeed;
        _agent.avoidancePriority     = Random.Range(30, 70);
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _agent.radius                = 0.45f;
        _agent.baseOffset            = 0f;   // FIX: zero so enemy root sits on NavMesh, not above it
        _agent.autoBraking           = false;
        _agent.autoRepath            = true;
        _agent.updatePosition        = true;
        // Manual rotation in LateUpdate follows NavMesh velocity — avoids instant agent snap + moonwalk.
        _agent.updateRotation        = false;
        _agent.autoTraverseOffMeshLink = false;
    }

    private void TransitionTo(State newState)
    {
        State prevState = _state;
        if (newState == State.StuckRecovery && prevState != State.StuckRecovery)
            _resumeAfterStuck = MapResumeAfterStuck(prevState);

        _state = newState;

        if (debugAICombat)
        {
            Debug.Log($"[EnemyAI] {name} transitioned from {prevState} to {newState}. Target={(_target != null ? _target.name : "null")}");
        }

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
                _idleTimer = 0f;
                _repathTimer = 0f;
                if (IsTargetValid(_target))
                    TrySetChaseDestinationValidated();
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

            case State.Evade:
                _agent.isStopped = false;
                _agent.speed = moveSpeed;
                break;

            case State.StuckRecovery:
                _agent.isStopped = false;
                break;
        }
    }

    private static State MapResumeAfterStuck(State previous)
    {
        switch (previous)
        {
            case State.Attack:
            case State.Flinch:
                return State.Chase;
            default:
                return previous;
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

    /// <summary>
    /// Called by MpBotDirector on master client to give bots a priority
    /// human-player target list (Co-op Survival mode).
    /// Pass null or empty list to revert to default FFA targeting.
    /// </summary>
    public void SetMultiplayerTargets(System.Collections.Generic.List<Transform> humanTargets)
    {
        _mpHumanTargets = humanTargets;
    }

    private void EvaluateTargets()
    {
        // Co-op Survival: prefer the nearest living human player from the MP list.
        if (_mpHumanTargets != null && _mpHumanTargets.Count > 0)
        {
            Transform nearest  = null;
            float     nearestD = float.MaxValue;
            foreach (Transform t in _mpHumanTargets)
            {
                if (t == null) continue;
                IDamageable dmg = t.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                float d = Vector3.Distance(transform.position, t.position);
                if (d < nearestD) { nearestD = d; nearest = t; }
            }
            if (nearest != null && nearestD <= Mathf.Max(detectionRadius, aggressiveScanRadius))
            {
                if (nearest != _target) { _target = nearest; _targetLockTimer = Time.time + targetLockDuration; }
                if ((_state == State.Idle || _state == State.Patrol || _state == State.Attack) && CanEngageChase(_target))
                    TransitionTo(State.Chase);
                return;
            }
        }

        Transform candidate = FindFfaTarget(Mathf.Max(detectionRadius, aggressiveScanRadius));

        // Minimal freeze fix: fall back to nearest living enemy/player (no hard player priority).
        if (candidate == null)
            candidate = FindFallbackTarget();

        if (candidate == null)
        {
            _target = null;
            if (_state != State.Dead && _state != State.Flinch && _state != State.Jumping &&
                _state != State.Evade)
                TransitionTo(State.Patrol);
            return;
        }

        // Target-commitment gate. While the lock timer is still running, keep
        // the current valid target unless a new candidate is meaningfully
        // closer (targetSwitchHysteresis metres). Previously _targetLockTimer
        // was set but never read, so EvaluateTargets re-picked the nearest
        // combatant every scan — that is what caused the per-scan target
        // thrash and the all-enemies-swarm-the-player behaviour. With the gate
        // active, an enemy that has locked onto another enemy keeps fighting
        // it instead of re-snapping to the player.
        if (IsTargetValid(_target) && _target != candidate && Time.time < _targetLockTimer)
        {
            float currentDist   = Vector3.Distance(transform.position, _target.position);
            float candidateDist = Vector3.Distance(transform.position, candidate.position);
            if (candidateDist > currentDist - Mathf.Max(0f, targetSwitchHysteresis))
                candidate = _target;
        }

        if (candidate != _target)
        {
            _target = candidate;
            _targetLockTimer = Time.time + targetLockDuration;
        }

        if ((_state == State.Idle || _state == State.Patrol || _state == State.Attack) &&
            CanEngageChase(_target))
            TransitionTo(State.Chase);
    }

    private Transform FindFallbackTarget()
    {
        Transform player = FindPlayerTarget();
        if (player != null && IsTargetValid(player))
            return player;

        Transform nearestEnemy = FindNearestLivingEnemy();
        if (nearestEnemy != null && IsTargetValid(nearestEnemy))
            return nearestEnemy;

        return player;
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
    private static readonly Collider[] _targetScanBuffer = new Collider[32];
    private static readonly HashSet<int> _ffaEvaluatedRoots = new HashSet<int>();

    private Transform FindFfaTarget(float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            _targetScanBuffer,
            detectionMask,
            QueryTriggerInteraction.Ignore);

        Transform best      = null;
        float     bestScore = float.MaxValue;
        _ffaEvaluatedRoots.Clear();

        for (int h = 0; h < hitCount; h++)
        {
            Collider hit = _targetScanBuffer[h];
            if (hit == null || hit.transform == transform) continue;

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) continue;
            if (dmg.gameObject == gameObject) continue;

            int id = dmg.gameObject.GetInstanceID();
            if (!_ffaEvaluatedRoots.Add(id)) continue;

            Transform t  = dmg.transform;
            float     d2 = (t.position - transform.position).sqrMagnitude;
            bool isPlayer = t.GetComponentInParent<PlayerHealth>() != null;
            float score = d2 * (isPlayer ? 0.72f : 1f);

            if (score < bestScore)
            {
                bestScore = score;
                best      = t;
            }
        }

        return best;
    }

    private Transform FindPlayerTarget()
    {
        PlayerController pc = ResolveScenePlayer();
        if (pc == null)
            return null;

        PlayerHealth player = pc.GetComponent<PlayerHealth>();
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
        Transform nearest = null;
        float nearestDistanceSqr = float.MaxValue;
        Vector3 pos = transform.position;

        for (int i = 0; i < s_aliveEnemies.Count; i++)
        {
            EnemyController otherEnemy = s_aliveEnemies[i];
            if (otherEnemy == null || otherEnemy == this || !otherEnemy.IsAlive)
                continue;

            float distSqr = (otherEnemy.transform.position - pos).sqrMagnitude;
            if (distSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distSqr;
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

        // Level 12 saw always uses the main attach path (bip_hand_R + stabilization).
        // WeaponGripSystem uses its own bone resolution which may pick weapon_bone_R
        // (a different bone with a baked orientation), producing an inconsistent grip
        // across enemies depending on their Inspector setup.
        if (weaponGripSystem != null && level != 12)
        {
            equippedWeaponObject = weaponGripSystem.AttachWeapon(
                characterRoot: gameObject,
                weaponPrefab: weaponPrefab,
                isPlayer: false,
                level: level,
                damage: Mathf.RoundToInt(attackDamage));

            if (equippedWeaponObject != null)
            {
                WeaponLoadout catalogLoadout = WeaponLoadoutCatalog.Get(level);
                equippedWeaponObject.transform.localPosition    = catalogLoadout.EnemyLocalPosition;
                equippedWeaponObject.transform.localEulerAngles = catalogLoadout.EnemyLocalEuler;
                if (level == 2) ApplyKatanaHumanoidGrip(equippedWeaponObject, null);

                // Expose the hand bone so LateUpdate's world-space rotation fix
                // can run for this path too.  WeaponGripSystem parents the weapon
                // directly to the hand bone (no intermediate socket), so the
                // weapon's immediate parent IS the hand bone.
                _activeWeaponHandBone = equippedWeaponObject.transform.parent;

                WeaponHitbox wh = equippedWeaponObject.GetComponent<WeaponHitbox>();
                if (wh == null) wh = equippedWeaponObject.AddComponent<WeaponHitbox>();
                wh.damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage));
                wh.meleeAttackRange = meleeAttackRange;
                if (level == 12)
                {
                    wh.overlapRadius = 0.45f;
                    float engageDist12 = attackRadius + attackRangePadding;
                    float tipDist12    = Mathf.Clamp(engageDist12 * 0.55f, 0.6f, 2.5f);
                    wh.maxAttackRange  = tipDist12 + wh.overlapRadius + 0.1f;
                }
                else
                {
                    wh.maxAttackRange = attackRadius + 1.0f;
                }
                _equippedWeaponHitbox = wh;
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
        // Level 2 katana / Level 9 axe: use identity socket (mirrors PlayerController)
        // so the grip euler lands in the same humanoid hand-space on every rig.
        // Both are single-handed weapons whose catalog euler already matches the
        // player's grip exactly — socket stabilization via weapon_bone_R would
        // cancel out the hand-bone rotation and produce a flipped/inverted result.
        stabilizeWeaponSocketAgainstHandPose = (level != 2 && level != 9 && level != 12);
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

        // Level 2 katana / Level 9 axe / Level 12 saw: bypass weapon_bone_R (Crosby's
        // weapon socket which has a baked orientation that varies per-prefab) in favour
        // of bip_hand_R so every enemy uses the exact same bone as the stabilization base.
        // Without this, enemies whose Inspector weaponAttachPoint differs get different
        // stabilized socket frames and the same euler produces different visual results.
        // Priority 1 — Unity humanoid API (normalises world rotation across rigs).
        // Priority 2 — bip_hand_R by name (works even when Avatar is Generic).
        if (level == 2 || level == 9 || level == 12)
        {
            Transform overrideHand = null;

            if (_anim != null && _anim.isHuman)
                overrideHand = _anim.GetBoneTransform(HumanBodyBones.RightHand);

            if (overrideHand == null)
                overrideHand = FindBoneByName(transform, "bip_hand_R");

            if (overrideHand != null)
            {
                handBone = overrideHand;
                Debug.Log($"[EnemyController] '{name}' lvl={level}: handBone overridden to '{handBone.name}' (isHuman={(_anim != null && _anim.isHuman)})");
            }
            else
            {
                Debug.LogWarning($"[EnemyController] '{name}' lvl={level}: could not find humanoid RightHand or bip_hand_R — grip may look wrong.");
            }
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

        if (level == 2) ApplyKatanaHumanoidGrip(equippedWeaponObject, _activeWeaponSocket);

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
        if (level == 12)
        {
            // Derive max range from the same formula WeaponHitbox uses to place
            // the tip sphere (GetWeaponTipWorldPosition: tipDist = engageDist * 0.55,
            // clamped 0.6–2.5). A hit is only valid if the target is within actual
            // physical reach: tipDist + overlapRadius + a 0.1 m tolerance.
            hitbox.overlapRadius = 0.45f;
            float engageDist = attackRadius + attackRangePadding;
            float tipDist    = Mathf.Clamp(engageDist * 0.55f, 0.6f, 2.5f);
            hitbox.maxAttackRange = tipDist + hitbox.overlapRadius + 0.1f;
        }
        else
        {
            hitbox.maxAttackRange = attackRadius + 1.0f;
        }
        hitbox.meleeAttackRange = meleeAttackRange;
        hitbox.DisableHitbox();
        _equippedWeaponHitbox = hitbox;

        // ── 9. Visibility fix (URP) ─────────────────────────────────────────
        if (equippedWeaponObject.GetComponent<WeaponVisibilityFix>() == null)
            equippedWeaponObject.AddComponent<WeaponVisibilityFix>();

        _equippedWeaponPrefab  = weaponPrefab;
        _equippedWeaponLevel   = level;
        _weaponAttachInProgress = false;
        // Cache the idle grip rotation so LateUpdate can use it as a stable
        // base for the attack-swing correction without compounding each frame.
        if (equippedWeaponObject != null)
            _weaponBaseLocalRot = equippedWeaponObject.transform.localRotation;

        // Bind KatanaCombatHandler — it enforces the final grip pose via
        // WeaponGripOffset.Enforce() and routes hit detection on anim events.
        KatanaCombatHandler katanaHandler = GetComponent<KatanaCombatHandler>();
        if (katanaHandler != null)
        {
            WeaponGripOffset grip = equippedWeaponObject.GetComponent<WeaponGripOffset>();
            if (grip != null)
                katanaHandler.BindKatana(grip);
        }
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

    /// <summary>
    /// Attaches the katana so its euler (0,0,160) is applied in humanoid
    /// RightHand space — identical to PlayerController's approach.
    /// If weaponSocket is provided and non-null, the socket localRotation is
    /// reset to identity so no Crosby-basis compensation interferes.
    /// </summary>
    private void ApplyKatanaHumanoidGrip(GameObject weaponObj, Transform weaponSocket)
    {
        if (weaponObj == null) return;

        // Reset socket to identity — mirrors PlayerController.GetOrCreateWeaponSocket
        // which always sets localRotation = Quaternion.identity.
        if (weaponSocket != null)
            weaponSocket.localRotation = Quaternion.identity;

        // Apply the same grip euler the player uses.
        weaponObj.transform.localPosition    = WeaponLoadoutCatalog.Get(2).EnemyLocalPosition;
        weaponObj.transform.localEulerAngles = new Vector3(0f, 0f, 160f);
    }

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
        Gizmos.color = new Color(0.35f, 0.55f, 1f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, alignmentRadius);
        Gizmos.color = new Color(0.35f, 0.85f, 0.45f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, cohesionRadius);

        // Show obstacle-check ray
        Gizmos.color = Color.magenta;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        Gizmos.DrawRay(origin, transform.forward * obstacleCheckDist);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  AGGRESSION TUNING (added 2026-05-21)
    //  Pure tuning shim: bakes the new multipliers into the same agent fields
    //  ConfigureAgent already drives. Called once at Start, and live each tick
    //  via the existing chase-update path because UpdateChaseMovement writes
    //  _agent.speed/acceleration/angularSpeed itself — we only need to update
    //  the source-of-truth `chaseSpeed`, `agentAcceleration`, `agentAngularSpeed`
    //  fields so those writes pick up the multipliers.
    // ════════════════════════════════════════════════════════════════════════
    private bool _aggressionApplied;
    private float _baseChaseSpeed;
    private float _baseAgentAccel;
    private float _baseAgentAngular;
    private float _baseAnimatorSpeed = 1f;
    private void ApplyAggressionTuning()
    {
        if (_aggressionApplied) return;
        _aggressionApplied = true;

        _baseChaseSpeed   = chaseSpeed;
        _baseAgentAccel   = agentAcceleration;
        _baseAgentAngular = agentAngularSpeed;

        chaseSpeed         = _baseChaseSpeed   * chaseSpeedMultiplier;
        agentAcceleration  = _baseAgentAccel   * Mathf.Max(1f, chaseSpeedMultiplier);
        agentAngularSpeed  = _baseAgentAngular * Mathf.Max(0.5f, turnResponsiveness);

        // Animator speed is read by the existing SyncAnimator path; bumping the
        // root Animator speed is the cleanest one-shot to make locomotion read
        // less robotic without changing any transition graph.
        Animator a = GetComponentInChildren<Animator>();
        if (a != null)
        {
            _baseAnimatorSpeed = a.speed > 0.01f ? a.speed : 1f;
            a.speed = _baseAnimatorSpeed * Mathf.Max(0.5f, animatorSpeedMultiplier);
        }

        if (_agent != null && _agent.enabled)
        {
            _agent.acceleration = agentAcceleration;
            _agent.angularSpeed = agentAngularSpeed;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WATCHDOG (added 2026-05-21)
    //  Two cheap checks per frame; no allocations, no FindObject calls.
    //   1. Y-above-spawn rooftop trap — warps the agent back down to the
    //      nearest NavMesh sample near the original spawn Y.
    //   2. No-path-progress timeout — resets the path and re-acquires a target
    //      when the enemy is stuck rotating in place without closing distance.
    // ════════════════════════════════════════════════════════════════════════
    private void TickWatchdog()
    {
        if (_state == State.Dead) return;
        if (_agent == null || !_agent.enabled) return;

        float now = Time.time;

        _navValidateTimer -= Time.deltaTime;
        if (_navValidateTimer <= 0f)
        {
            _navValidateTimer = 0.45f;
            if (!_agent.isOnNavMesh)
                EnsureAgentOnNavMesh();
        }

        if (!_agent.isOnNavMesh)
            return;

        if (!float.IsNaN(_spawnY))
        {
            float yDelta = transform.position.y - _spawnY;

            // ── 1. Rooftop trap ──────────────────────────────────────────────
            if (yDelta > watchdogMaxYAboveSpawn)
            {
                if (_highSinceTime < 0f) _highSinceTime = now;
                else if (now - _highSinceTime >= watchdogInvalidHighSeconds)
                {
                    Vector3 anchor = _target != null
                        ? new Vector3(_target.position.x, _spawnY, _target.position.z)
                        : new Vector3(transform.position.x, _spawnY, transform.position.z);
                    TryRecoveryWarp(anchor, 14f, "rooftop");
                    _highSinceTime = -1f;
                    _lowProgressSinceTime = -1f;
                }
            }
            else
            {
                _highSinceTime = -1f;
            }
        }

        // ── 2. No path progress while chasing ────────────────────────────────
        if (_target != null && _agent.hasPath && !_agent.pathPending)
        {
            float moved = (transform.position - _watchdogLastPosition).sqrMagnitude;
            if (moved < 0.04f)
            {
                if (_lowProgressSinceTime < 0f) _lowProgressSinceTime = now;
                else if (now - _lowProgressSinceTime >= watchdogStuckPathSeconds)
                {
                    _agent.ResetPath();
                    EvaluateTargets();
                    if (_target != null && IsTargetValid(_target))
                        TrySetChaseDestinationValidated();
                    else
                        RepositionOnInvalidPath();

                    if (debugWatchdog)
                        Debug.Log($"[EnemyWatchdog] {name} stuck path reset; reacquiring target.");
                    _lowProgressSinceTime = -1f;
                }
            }
            else
            {
                _lowProgressSinceTime = -1f;
                _watchdogLastPosition = transform.position;
            }

            if (_agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                if (_partialPathSinceTime < 0f) _partialPathSinceTime = now;
                else if (now - _partialPathSinceTime >= partialPathMaxSeconds)
                {
                    RepositionOnInvalidPath();
                    _partialPathSinceTime = -1f;
                }
            }
            else
            {
                _partialPathSinceTime = -1f;
            }
        }
        else
        {
            _lowProgressSinceTime = -1f;
            _partialPathSinceTime = -1f;
            _watchdogLastPosition = transform.position;
        }

        // ── 3. Out-of-bounds horizontal recovery ─────────────────────────────
        if (!IsInsidePlayableBounds(transform.position))
        {
            float halfSize = LevelBuilder.Instance != null
                ? LevelBuilder.Instance.arenaHalfSize
                : 80f;
            Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
            if (flat.sqrMagnitude > 0.01f)
            {
                Vector3 warpTarget = flat.normalized * (halfSize * 0.9f);
                warpTarget.y = float.IsNaN(_spawnY) ? transform.position.y : _spawnY;
                TryRecoveryWarp(warpTarget, 15f, "out_of_bounds");
            }
        }

        // ── 4. Invalid / unreachable target ────────────────────────────────
        if (_target != null && !IsTargetValid(_target))
        {
            _target = null;
            EvaluateTargets();
        }
        else if (_target != null && _state == State.Chase && !CanEngageChase(_target))
        {
            if (_idleMotionSinceTime < 0f) _idleMotionSinceTime = now;
            else if (now - _idleMotionSinceTime >= idleMotionTimeout)
            {
                _target = null;
                EvaluateTargets();
                _idleMotionSinceTime = -1f;
            }
        }
        else
        {
            _idleMotionSinceTime = -1f;
        }
    }
}
