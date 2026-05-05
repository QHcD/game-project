using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller — melee-only.
/// Handles movement, camera, combat, weapon management, and animation.
/// Uses CharacterController for precise, non-physics movement.
///
/// Animation is driven entirely by a standard Animator Controller.
/// Parameters fed each frame: Speed, IsGrounded, IsAttacking, IsSprinting.
/// The Animator Controller is responsible for all blending and leg motion.
/// </summary>
public class PlayerController : MonoBehaviour
{
    private const string WeaponSocketName = "__PlayerWeaponSocket";
    private const string ChainsawSocketName = "__PlayerChainsawSocket";
    private const string RuntimeThirdPersonCameraName = "RuntimeThirdPersonCamera";
    private const float ThirdPersonMinPitch = -12f;
    private const float ThirdPersonMaxPitch = 45f;
    private const float ThirdPersonInitialPitch = 8f;
    private const float FirstPersonMinPitch = -80f;
    private const float FirstPersonMaxPitch = 80f;
    private const float ForcedSeparationDistance = 1.0f;
    private const float MeleeImpactRadius = 0.78f;
    private const string PrefKeySicklePos = "Grip.Player.L13.Sickle.Pos";
    private const string PrefKeySickleEuler = "Grip.Player.L13.Sickle.Euler";
    private const string PrefKeySawPos = "Grip.Player.L12.Saw.Pos";
    private const string PrefKeySawEuler = "Grip.Player.L12.Saw.Euler";
    private static readonly Vector3 SafeFallbackSpawn = new Vector3(0f, 1f, 0f);
    private static readonly Vector3 ChainsawSocketLocalPosition = Vector3.zero;
    private static readonly Vector3 ChainsawSocketLocalEuler = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 DefaultLevel13SickleGripLocalPosition = new Vector3(0.006f, 0.002f, 0.026f);
    private static readonly Vector3 DefaultLevel13SickleGripLocalEuler = new Vector3(86f, 5f, 98f);
    private static readonly Vector3 DefaultLevel12SawGripLocalPosition = WeaponLoadoutCatalog.ChainsawPlayerLocalPosition;
    private static readonly Vector3 DefaultLevel12SawGripLocalEuler = WeaponLoadoutCatalog.ChainsawPlayerLocalEuler;
    private static readonly Vector3 PlayerChainsawGripLocalPosition = new Vector3(-0.066f, -0.39f, 0.044f);
    private static readonly Vector3 PlayerChainsawGripLocalEuler = new Vector3(-177.177f, -175.886f, 88.481f);
    // Katana grip pose applied to the third-person RightHand bone.
    // (0.3, -0.4, 0.6) in hand-local space seats the hilt in the palm;
    // Euler (0, 90, 0) points the blade along the hand's forward axis.
    // These match the FP viewmodel offset so the weapon reads consistently
    // in both perspectives.
    private static readonly Vector3 PlayerKatanaGripLocalPosition = new Vector3(0.3f, -0.4f, 0.6f);
    private static readonly Vector3 PlayerKatanaGripLocalEuler    = new Vector3(0f, 90f, 0f);

    // ════════════════════════════════════════════════════════════════════════
    //  INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

    [Header("Movement")]
    [Tooltip("Base walking speed in units per second.")]
    public float moveSpeed = 8.5f;

    [Tooltip("Multiplier applied when sprint key is held.")]
    public float sprintMultiplier = 2f;

    [Tooltip("Gravity acceleration (negative value). Stronger gravity = snappier, less floaty landings.")]
    public float gravity = -28f;

    [Tooltip("Maximum jump height in units. Tuned to a natural human-scale hop.")]
    public float jumpHeight = 1.2f;

    [Header("Movement Input Debug")]
    [Tooltip("Left stick input below this magnitude is ignored so controller drift cannot bias movement.")]
    [Range(0f, 1f)] public float gamepadMoveDeadzone = 0.2f;

    [Tooltip("Temporary: logs raw and camera-relative movement when W-only is pressed.")]
    public bool debugWOnlyMovement = true;

    [Header("Animation")]
    [Tooltip("Animator controller for the spawned third-person body.")]
    public RuntimeAnimatorController playerAnimatorController;

    [Tooltip("Optional humanoid avatar override for the spawned third-person body.")]
    public Avatar playerAvatar;

    [Header("Movement Tuning")]
    [Tooltip("How fast the character reaches full speed (units/s²). High value removes the 'wading through mud' feel.")]
    public float acceleration = 26f;

    [Tooltip("How fast the character brakes to zero (units/s²). High value stops the player from sliding on key release.")]
    public float deceleration = 32f;

    [Header("Tactical Maneuvers")]
    public float crouchHeight = 1.3f;
    public float proneHeight = 0.7f;
    public float stanceTransitionSpeed = 8f;
    public float crouchSpeedMultiplier = 0.72f;
    public float proneSpeedMultiplier = 0.45f;
    public float slideSpeedMultiplier = 1.6f;
    public float slideDuration = 0.45f;
    public float powerSlideBoost = 6.0f;
    public float powerSlideCooldown = 0.4f;
    public float mantleUpHeight = 1.1f;
    public float mantleForwardDistance = 1.0f;
    public float mantleDuration = 0.25f;
    public float thrusterJumpHeight = 2.8f;
    public float thrusterForwardBoost = 5.8f;
    public float thrusterCooldown = 0.8f;
    public float crouchBodyYOffset = -0.28f;
    // Crawl/prone uses a tiny lift (model rotated 90° so it lies flat) — the
    // big -0.62 offset used to bury the legs in the floor; we keep it here for
    // any legacy callers but the live code now uses proneCrawlBodyYOffset.
    public float proneBodyYOffset = -0.62f;

    [Header("Black Ops 3 Maneuvers")]
    [Tooltip("Peak hop height in meters at the start of a flip (Z key). Was 6.5 — that lifted the player ~6.5 m, well above any room ceiling, and was the root cause of the post-flip ground clipping.")]
    public float flipVerticalImpulse = 1.4f;
    [Tooltip("Forward boost applied at the start of a flip (Z key). Tuned for a controllable dash, not a launch.")]
    public float flipForwardBoost = 5.5f;
    [Tooltip("How long the flip rotation animation plays (seconds). Shorter = faster recovery of player control.")]
    public float flipDuration = 0.42f;
    [Tooltip("Cooldown between flips (seconds).")]
    public float flipCooldown = 0.9f;
    [Tooltip("Body Y offset while crawling — tiny because the body lies flat.")]
    public float proneCrawlBodyYOffset = -0.05f;

    [Header("Camera")]
    [Tooltip("First-person camera reference.")]
    public Camera firstPersonCam;

    [Tooltip("Third-person camera reference (auto-created if null).")]
    public Camera thirdPersonCam;

    [Tooltip("Mouse look sensitivity.")]
    public float sensitivity = 100f;

    [Header("Mouse Look Smoothing")]
    [Tooltip("Higher = snappier; lower = smoother. 18–28 is a good FPS range.")]
    public float lookSmoothing = 80f;

    [Tooltip("Optional cap on per-frame mouse delta to prevent spikes on low FPS or focus changes.")]
    public float maxMouseDeltaPerFrame = 60f;

    [Header("Combat")]
    [Tooltip("Maximum distance for melee attacks.")]
    public float attackDistance = 3f;

    [Tooltip("Delay before damage is applied after attack input.")]
    public float attackDelay = 0.05f;

    [Tooltip("Speed multiplier for attack animations.")]
    public float attackSpeed = 1f;

    [Tooltip("Base damage per hit.")]
    public int attackDamage = 1;

    [Tooltip("Physics layer mask for attack raycasts.")]
    public LayerMask attackLayer;

    [Tooltip("Radius for melee overlap detection.")]
    public float attackRadius = 1.25f;

    [Tooltip("Forward lunge distance on attack.")]
    public float attackLungeDistance = 0.45f;

    [Tooltip("Knockback force applied to hit targets.")]
    public float hitKnockbackForce = 4.5f;

    [Header("Weapon References")]
    [Tooltip("Current weapon display name (read by HUD).")]
    public string equippedWeaponName = "Combat Knife";

    [Tooltip("Optional hit-effect prefab spawned at impact point.")]
    public GameObject hitEffect;

    [Tooltip("Sound played on attack swing.")]
    public AudioClip swordSwing;

    [Tooltip("Sound played on successful hit.")]
    public AudioClip hitSound;

    [Header("Weapon Grip Fine Tuning (Player)")]
    [Tooltip("Level 13 sickle grip local position (player).")]
    public Vector3 level13SickleGripLocalPosition = DefaultLevel13SickleGripLocalPosition;
    [Tooltip("Level 13 sickle grip local euler (player).")]
    public Vector3 level13SickleGripLocalEuler = DefaultLevel13SickleGripLocalEuler;
    [Tooltip("Level 12 saw grip local position (player).")]
    public Vector3 level12SawGripLocalPosition = DefaultLevel12SawGripLocalPosition;
    [Tooltip("Level 12 saw grip local euler (player).")]
    public Vector3 level12SawGripLocalEuler = DefaultLevel12SawGripLocalEuler;

    [Header("Level 1 — Combat knife (FBX)")]
    [Tooltip("Drag your imported knife prefab/model here, or leave empty to load from Resources.")]
    public GameObject combatKnifePrefabOverride;

    [Tooltip("Resources path with no extension (file must live under a Resources folder).")]
    public string combatKnifeResourcePath = "Weapons/TacticalKnife/TacticalKnife";

    [Tooltip("Local position on the third-person right hand.")]
    public Vector3 combatKnifeThirdPersonLocalPos = Vector3.zero;

    [Tooltip("Local rotation on the third-person right hand (degrees).")]
    public Vector3 combatKnifeThirdPersonLocalEuler = Vector3.zero;

    [Tooltip("Local scale on the third-person weapon socket.")]
    public Vector3 combatKnifeThirdPersonLocalScale = new Vector3(0.02f, 0.02f, 0.02f);

    [Tooltip("Local position on the first-person Weapon socket.")]
    public Vector3 combatKnifeFirstPersonLocalPos;

    [Tooltip("Local rotation on the first-person Weapon socket (degrees).")]
    public Vector3 combatKnifeFirstPersonLocalEuler;

    [Tooltip("Local scale for the knife in first person.")]
    public Vector3 combatKnifeFirstPersonLocalScale = Vector3.one;

    [Header("Arena Boundaries")]
    public float arenaBoundaryRadius = 22.8f;
    public float arenaBoundaryPadding = 0.35f;
    public float arenaFloorHeight = 0.1f;
    public float floorSnapSpeed = 14f;
    public float maxFloorSnapDistance = 0.45f;

    [Header("Wall / Static collision (movement)")]
    [Tooltip("Walls, props, containers — used to pre-clamp horizontal motion before CharacterController.Move. Exclude Character/Hittable/Player.")]
    public LayerMask staticObstacleMask;

    [Tooltip("Extra clearance beyond skinWidth so we do not ram the capsule into mesh every frame (reduces jitter).")]
    public float wallCollisionPadding = 0.045f;

    [Tooltip("Minimum gap to leave when a CapsuleCast hits static geometry.")]
    public float minMoveClearance = 0.025f;

    [Tooltip("Max separation push per frame (meters) vs other characters — prevents oscillation with walls.")]
    public float maxSeparationPushPerFrame = 0.08f;

    [Header("Jump")]
    [Tooltip("Extra downward ray distance beyond step/skin width so Space still registers as grounded when the CharacterController flag flickers.")]
    public float jumpGroundProbeExtra = 0.14f;

    // ════════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    // Components
    private CharacterController controller;
    private Animator animator;
    private AudioSource audioSource;

    // Movement
    private Vector3 verticalVelocity;
    private Vector3 horizontalVelocity;
    private Vector3 actualHorizontalVelocity;
    private Vector2 moveInputRaw;
    private Vector2 moveInputSmoothed;
    private Vector2 lookInput;
    private bool isGrounded;
    private bool isSprinting;
    private bool wasMoving;
    private bool jumpRequested;
    private bool isCrouching;
    private bool isProne;
    private bool isSliding;
    private bool isMantling;
    private bool isFlipping;
    private bool sprintToggled; // Latched by tapping C — like CoD/BO3 sprint hold-to-toggle.
    private float slideTimer;
    private float slideCooldownTimer;
    private float thrusterCooldownTimer;
    private float flipCooldownTimer;
    private float targetControllerHeight = 1.8f;
    private Vector3 targetControllerCenter = new Vector3(0f, 0.92f, 0f);
    private Coroutine mantleCoroutine;
    private Coroutine flipCoroutine;
    private Vector3 thirdPersonBodyBaseLocalPosition;
    // Cached so prone-rotation and flip-rotation can return the body to its
    // original facing exactly without drifting after each maneuver.
    private Quaternion thirdPersonBodyBaseLocalRotation = Quaternion.identity;
    private const float InputSmoothing = 10f;

    // Camera
    private float cameraPitch;
    private float cameraYaw;
    private Vector2 lookInputSmoothed;
    private Vector3 firstPersonLocalPos;
    private Quaternion firstPersonLocalRot;
    private Camera runtimeThirdPersonCamera;
    private bool isThirdPersonActive;
    private float nextWOnlyMoveDebugLogTime;

    // Head bob
    private float headBobTimer;
    private float headBobVelocity;
    private const float HeadBobFrequency = 10f;
    private const float HeadBobAmplitude = 0.038f;

    // Camera kick (recoil feedback)
    private float cameraKickTarget;
    private float cameraKickCurrent;

    // Combat
    private bool isAttacking;
    private float attackCooldownTimer;
    private float attackResetTimer;
    private float attackFailsafeTimer;
    private const float AttackCooldown = 0.15f;
    private const float AttackResetTime = 0.7f;
    private const float AttackFailsafeDuration = 1.0f;
    private const int MaxMeleeOverlapHits = 32;
    private LayerMask resolvedAttackMask;
    private readonly Collider[] meleeOverlapHits = new Collider[MaxMeleeOverlapHits];
    private readonly RaycastHit[] meleeRaycastHits = new RaycastHit[MaxMeleeOverlapHits];

    // Third-person body
    private GameObject thirdPersonBody;
    private Renderer[] firstPersonRenderers;
    [HideInInspector] public GameObject equippedWeaponObject;
    private int equippedWeaponLevel = -1;
    private bool weaponAttachInProgress;
    private WeaponHitbox equippedWeaponHitbox;
    private Coroutine weaponHitboxRoutine;
    private bool gripTuningMode;

    private Transform firstPersonWeaponSlot;
    private MeshRenderer firstPersonWeaponMeshRenderer;
    private GameObject firstPersonKnifeInstance;

    // ════════════════════════════════════════════════════════════════════════
    //  ANIMATOR
    // ════════════════════════════════════════════════════════════════════════

    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    private static readonly int HashAttack    = Animator.StringToHash("Attack");
    private static readonly int HashDead      = Animator.StringToHash("Dead");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");
    private static readonly int HashAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int HashSprinting = Animator.StringToHash("IsSprinting");

    private HashSet<int> _animParameterHashes;

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════════════════════════════════

    public Camera ActiveCamera => isThirdPersonActive ? runtimeThirdPersonCamera : firstPersonCam;
    public GameObject GetThirdPersonBody() => thirdPersonBody;
    public bool IsMeleeWeapon => true;

    // ════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // ── Near clip — set before any camera becomes active so the weapon
        // viewmodel never disappears on the very first frame.
        if (Camera.main != null)
            Camera.main.nearClipPlane = 0.1f;
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam != null && cam.nearClipPlane < 0.1f)
                cam.nearClipPlane = 0.1f;

        // Force the project-wide layer collision matrix to allow Player ↔
        // Enemies overlap so the manual separation push below sees them. The
        // helper handles missing layers gracefully (layers are project-level
        // settings and cannot be created at runtime).
        EnemyController.EnsureCharacterLayerCollision();

        // Prefer dedicated Player layer for enemy melee masks; fall back for older scenes.
        int targetLayer = LayerMask.NameToLayer("Player");
        if (targetLayer < 0) targetLayer = LayerMask.NameToLayer("Hittable");
        if (targetLayer < 0) targetLayer = LayerMask.NameToLayer("Character");
        if (targetLayer >= 0) SetLayerRecursive(gameObject, targetLayer);

        controller  = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        if (controller != null)
        {
            controller.height          = 1.8f;
            controller.radius          = 0.4f;
            controller.center          = new Vector3(0f, 0.92f, 0f);
            controller.skinWidth       = 0.04f;
            controller.stepOffset      = 0.5f;
            controller.slopeLimit      = 45f;
            controller.minMoveDistance  = 0f;
            targetControllerHeight = controller.height;
            targetControllerCenter = controller.center;
        }

        // Cache first-person camera transform for restoring when toggling.
        if (firstPersonCam == null)
            firstPersonCam = GetComponentInChildren<Camera>();
        if (firstPersonCam != null)
        {
            firstPersonLocalPos = firstPersonCam.transform.localPosition;
            firstPersonLocalRot = firstPersonCam.transform.localRotation;
            firstPersonCam.gameObject.SetActive(false);
        }

        CacheFirstPersonWeaponSlot();

        firstPersonRenderers = GetComponentsInChildren<Renderer>(true);
        resolvedAttackMask   = ResolveHittableMask();
        if (staticObstacleMask.value == 0)
            staticObstacleMask = BuildDefaultStaticObstacleMask();

        foreach (Animator childAnimator in GetComponentsInChildren<Animator>(true))
            childAnimator.applyRootMotion = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        LoadSavedGripOverrides();

        // Refresh the runtime mouse sensitivity multiplier from PlayerPrefs
        // each time a player spawns. Guarantees the Options-menu slider value
        // applies to the very first frame of mouse-look in GameScene.
        LookSensitivityRuntime.LoadFromPrefs();

        // Any Rigidbody on the player root (legacy prefabs / imported rigs) must
        // not tip the capsule — CharacterController owns translation.
        Rigidbody rootBody = GetComponent<Rigidbody>();
        if (rootBody != null)
        {
            rootBody.isKinematic = true;
            rootBody.useGravity = false;
            rootBody.interpolation = RigidbodyInterpolation.Interpolate;
            rootBody.constraints = RigidbodyConstraints.FreezeRotationX
                                  | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    private void Start()
    {
        EnsureCriticalComponents();
        ResetLevelOneWeaponOffsets();

        // ── 1. Snap player above the floor ──
        if (controller != null) controller.enabled = false;
        Vector3 startPos = IsUnsafeSpawn(transform.position)
            ? SafeFallbackSpawn
            : transform.position;
        startPos.y = Mathf.Max(startPos.y, SafeFallbackSpawn.y);
        transform.position = startPos;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        cameraYaw = transform.eulerAngles.y;
        cameraPitch = ThirdPersonInitialPitch;
        if (controller != null) controller.enabled = true;
        verticalVelocity.y = -2f;

        // ── 2. Spawn the third-person body ──
        ApplyPerspectivePreference();
        EnsureThirdPersonBody();
        if (thirdPersonBody != null)
        {
            SetLayerRecursive(thirdPersonBody, gameObject.layer);
            LockAvatarRigidbodies(thirdPersonBody);
        }

        // ── 3. Acquire the body animator and bind the intended player controller ──
        animator = null;
        if (thirdPersonBody != null)
            animator = thirdPersonBody.GetComponentInChildren<Animator>(true);

        // Fallback: any non-Arms animator on the player hierarchy
        if (animator == null)
        {
            foreach (Animator a in GetComponentsInChildren<Animator>(true))
            {
                if (a.gameObject.name.IndexOf("Arms", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                animator = a;
                break;
            }
        }

        ConfigureAnimatorBinding(animator, forceControllerAssignment: false);

        foreach (Animator childAnimator in GetComponentsInChildren<Animator>(true))
            ConfigureAnimatorBinding(childAnimator, forceControllerAssignment: false);

        // Do NOT call Rebind() here — it resets the state machine before the
        // controller has finished initialising, which can freeze it permanently.
        // The Entry → default-state transition fires automatically on the first Update.
        CacheAnimatorParameters();

        // ── 4. Patch any missing/blank materials so the body isn't a white statue ──
        if (thirdPersonBody != null)
            EnsureProperMaterial(thirdPersonBody);
        AssignMaterial();

        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        EquipWeaponForLevel(level);
    }

    private void EnsureCriticalComponents()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.92f, 0f);
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (firstPersonCam == null)
            firstPersonCam = GetComponentInChildren<Camera>(true);

        EnsureThirdPersonCamera();
        if (runtimeThirdPersonCamera == null)
            Debug.LogError("[PlayerController] No gameplay camera could be created. Movement is still enabled.");
    }

    private void ResetLevelOneWeaponOffsets()
    {
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        if (level != 1)
            return;

        equippedWeaponName = "Tactical Knife";
        combatKnifeThirdPersonLocalPos = Vector3.zero;
        combatKnifeThirdPersonLocalEuler = Vector3.zero;
        combatKnifeThirdPersonLocalScale = Vector3.one;
        combatKnifeFirstPersonLocalPos = Vector3.zero;
        combatKnifeFirstPersonLocalEuler = Vector3.zero;
        combatKnifeFirstPersonLocalScale = Vector3.one;
    }

    private static bool IsUnsafeSpawn(Vector3 position)
    {
        return float.IsNaN(position.x)
            || float.IsNaN(position.y)
            || float.IsNaN(position.z)
            || position.y < -0.5f;
    }

    private void Update()
    {
        if (controller == null)
            return;
        if (IsUnsafeSpawn(transform.position))
            TeleportTo(SafeFallbackSpawn);

        // Hard anti-flip clamp — unconditional. If anything tilted the root
        // (animator, ragdoll, external script) reset to yaw-only this frame.
        if (!EndMatchCinematic.GameplayLocked)
        {
            Vector3 e0 = transform.eulerAngles;
            transform.eulerAngles = new Vector3(0f, e0.y, 0f);

            // Also kill any root-motion on all body animators — belt-and-suspenders
            // so an asset import that ships with root motion can't cause flips.
            if (thirdPersonBody != null)
            {
                foreach (Animator a in thirdPersonBody.GetComponentsInChildren<Animator>(true))
                    if (a != null && a.applyRootMotion) a.applyRootMotion = false;
            }
        }

        isGrounded = controller.isGrounded;

        // End-match cinematic freezes player input — the Winners-Circle
        // sequence runs entirely on its own camera, and any input we sample
        // here would also fight the slow-motion timeScale.
        if (EndMatchCinematic.GameplayLocked)
        {
            UpdateAnimatorParameters();
            return;
        }

        ReadInput();
        HandleActionInput();
        ApplyMovement();
        ApplyCharacterSeparationPush();
        ApplyLook();
        // UpdateCameraZoom removed — zoom is permanently disabled.
        UpdateHeadBob();
        UpdateCameraKick();
        EnforcePerspectiveVisibility();
        UpdateCombatState();
        UpdateAnimatorParameters();
        HandleWeaponGripDebugTuning();

        // Cursor policy:
        // - During gameplay: locked + hidden.
        // - When timeScale is 0 (pause/menus): unlocked + visible (PauseMenuController handles this too).
        if (Time.timeScale <= 0.0001f)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    private void LateUpdate()
    {
        // Winners Circle / end-match cinematic drives the root transform via
        // VictoryPoseDriver — do not fight it with yaw lock or ground snap.
        if (EndMatchCinematic.GameplayLocked)
            return;

        // Strictly lock ROOT rotation to yaw only
        transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);

        // Guard the body mesh: if an animator or physics bone drifted it off-vertical
        // outside of a deliberate flip/prone pose, snap it back to its cached base.
        if (!isFlipping && thirdPersonBody != null && !isProne)
        {
            NormalizeThirdPersonBodyLocalRotation();
        }

        SnapCharacterToGroundNavMesh();
        SnapToArenaFloor();
        EnforceGroundYLock();
    }

    private void NormalizeThirdPersonBodyLocalRotation()
    {
        if (thirdPersonBody == null)
            return;

        if (!wasMoving)
        {
            thirdPersonBody.transform.localRotation = Quaternion.Slerp(
                thirdPersonBody.transform.localRotation,
                thirdPersonBodyBaseLocalRotation,
                18f * Time.deltaTime);
            return;
        }

        Vector3 currentLocalEuler = thirdPersonBody.transform.localEulerAngles;
        Vector3 baseLocalEuler = thirdPersonBodyBaseLocalRotation.eulerAngles;
        thirdPersonBody.transform.localRotation = Quaternion.Euler(
            baseLocalEuler.x,
            currentLocalEuler.y,
            baseLocalEuler.z);
    }

    // ── Anti-Sink + Anti-Flip enforcement (runs every physics tick) ────────
    //
    // FixedUpdate is the deterministic place to enforce two absolute rules
    // that must hold regardless of what any animator, coroutine, or external
    // script tries to do to the transform:
    //
    //   1. The capsule bottom is NEVER below the floor surface.
    //   2. The character is NEVER tilted around X (pitch) or Z (roll).
    //
    // These run in addition to the smoother per-frame logic in Update /
    // LateUpdate.  This is the hard backstop — zero tolerance, no smoothing.
    private void FixedUpdate()
    {
        if (controller == null) return;
        if (EndMatchCinematic.GameplayLocked) return;

        // ── Rule 2: hard yaw-only rotation lock ─────────────────────────────
        // Unconditional — no threshold. The root MUST stay Y-only every physics
        // tick. Flip visuals live on the body's local rotation, not here.
        Vector3 e = transform.eulerAngles;
        transform.eulerAngles = new Vector3(0f, e.y, 0f);

        // ── Rule 1: hard anti-sink floor clamp ──────────────────────────────
        EnforceFloorClamp();
    }

    private void EnforceFloorClamp()
    {
        if (isMantling) return; // Mantle owns transform during the lerp.
        // NOTE: intentionally runs during flips — the flip arc goes UP, not down,
        // so hitting the sink condition (capsule bottom below floor) while flipping
        // means something else pushed us underground. Fix it immediately.

        // Probe from inside the capsule downward to find the real floor.
        Vector3 castOrigin = transform.position
                           + Vector3.up * (controller.center.y + 0.05f);
        int groundMask = BuildGroundCheckMask();

        // Distance covers the full capsule height plus 1 m so a knockback or
        // slope dive that briefly sinks us is still recoverable on one tick.
        float castDist = controller.height + 1f;
        if (!Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit,
                             castDist, groundMask, QueryTriggerInteraction.Ignore))
            return;

        if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            return;

        float capsuleBottomY = transform.position.y
                             + controller.center.y
                             - controller.height * 0.5f;
        float floorY         = hit.point.y;

        // Allow exactly skinWidth of penetration (the CC's natural contact
        // tolerance).  Anything past that is a sink — fix it now.  Toggling
        // controller.enabled bypasses CC depenetration, which can fail on
        // thin floor meshes for a single tick.
        if (capsuleBottomY >= floorY - controller.skinWidth) return;

        float lift = floorY - capsuleBottomY;
        controller.enabled = false;
        transform.position = new Vector3(transform.position.x,
                                         transform.position.y + lift,
                                         transform.position.z);
        controller.enabled = true;
        if (verticalVelocity.y < 0f) verticalVelocity.y = -2f;
    }

    /// <summary>
    /// Keeps the capsule bottom from sitting under the baked NavMesh surface (arena floors).
    /// </summary>
    private void SnapCharacterToGroundNavMesh()
    {
        if (controller == null || !controller.enabled || isFlipping) return;
        if (EndMatchCinematic.GameplayLocked) return;

        Vector3 sampleOrigin = transform.position + Vector3.up * 0.25f;
        if (!NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            return;

        float capsuleBottomY = transform.position.y + controller.center.y - controller.height * 0.5f;
        float groundY = hit.position.y;
        const float sinkAllowance = 0.08f;
        if (capsuleBottomY >= groundY - sinkAllowance)
            return;

        float lift = (groundY - sinkAllowance) - capsuleBottomY;
        lift = Mathf.Clamp(lift, 0f, 0.4f);
        if (lift > 1e-4f)
            controller.Move(Vector3.up * lift);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════════════

    private void ReadInput()
    {
        moveInputRaw = ReadMovementInput();
        moveInputRaw = Vector2.ClampMagnitude(moveInputRaw, 1f);
        moveInputSmoothed = Vector2.Lerp(moveInputSmoothed, moveInputRaw, InputSmoothing * Time.deltaTime);

        // Read raw mouse delta (Input System gives pixels-per-frame-ish deltas).
        // We clamp it to avoid giant spikes (alt-tab, focus changes, or 10 FPS stutters).
        lookInput = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        lookInput = Vector2.ClampMagnitude(lookInput, Mathf.Max(1f, maxMouseDeltaPerFrame));

        // Smooth mouse input to remove jitter while keeping responsiveness.
        // Exponential smoothing is frame-rate independent.
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float k = 1f - Mathf.Exp(-lookSmoothing * dt);
        lookInputSmoothed = Vector2.Lerp(lookInputSmoothed, lookInput, k);
    }

    private void HandleActionInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                TryThrusterJumpOrQueueNormalJump();

            // ─── Black Ops 3 maneuver layout ─────────────────────────────────
            //   Z = FLIP   (combat flip — short hop + forward boost + 360° pitch)
            //   X = SLIDE  (power slide along the ground while moving)
            //   C = SPRINT (toggle a permanent sprint until you release W)
            //   V = CRAWL  (toggle prone — body lies flat instead of digging in)
            if (Keyboard.current.zKey.wasPressedThisFrame)
                TryStartFlip();

            if (Keyboard.current.xKey.wasPressedThisFrame)
                TryStartSlide();

            if (Keyboard.current.cKey.wasPressedThisFrame)
                ToggleSprintLock();

            if (Keyboard.current.vKey.wasPressedThisFrame)
                ToggleCrawl();

            // Optional auxiliaries kept on Left Control / Left Alt so existing
            // muscle memory still resolves to the right action.
            if (Keyboard.current.leftCtrlKey.wasPressedThisFrame)
                ToggleCrouch();

            if (Keyboard.current.leftAltKey.wasPressedThisFrame)
                TryMantle();
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            TryThrusterJumpOrQueueNormalJump();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Attack();
    }

    /// <summary>
    /// Keyboard (WASD + arrows + numpad) takes priority when any movement key is held.
    /// Otherwise a drifting gamepad stick can dominate with X-only input and remove forward/back.
    /// </summary>
    private Vector2 ReadMovementInput()
    {
        Vector2 keyboard = ReadKeyboardMovementVector();
        if (keyboard.sqrMagnitude > 0.0001f)
            return Vector2.ClampMagnitude(keyboard, 1f);

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            float deadzone = Mathf.Clamp01(gamepadMoveDeadzone);
            if (stick.magnitude >= deadzone)
                return Vector2.ClampMagnitude(stick, 1f);
        }

        return Vector2.zero;
    }

    private static Vector2 ReadKeyboardMovementVector()
    {
        if (Keyboard.current == null) return Vector2.zero;

        Keyboard k = Keyboard.current;
        Vector2 m = Vector2.zero;

        if (k.wKey.isPressed || k.upArrowKey.isPressed || k.numpad8Key.isPressed) m.y += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed || k.numpad2Key.isPressed) m.y -= 1f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed || k.numpad4Key.isPressed) m.x -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed || k.numpad6Key.isPressed) m.x += 1f;

        return m;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MOVEMENT
    // ════════════════════════════════════════════════════════════════════════

    // ── Separation (Anti-Stacking) ───────────────────────────────────────────
    // Push the player out of any overlapping IDamageable actor so the
    // CharacterController never merges into a kinematic enemy capsule after
    // spawn overlap, knockback, or simultaneous melee lunges. Walls / props
    // are ignored — we only push against other characters.

    private static readonly Collider[] _separationBuffer = new Collider[24];

    /// <summary>
    /// Soft separation from other damageable actors only. Runs in Update (not FixedUpdate)
    /// so it never fights the main CharacterController move on a mismatched timestep.
    /// </summary>
    private void ApplyCharacterSeparationPush()
    {
        if (controller == null || !controller.enabled) return;
        if (isMantling || isFlipping) return;

        Vector3 worldCenter = transform.TransformPoint(controller.center);
        float   probe       = ForcedSeparationDistance;

        int hitCount = Physics.OverlapSphereNonAlloc(
            worldCenter, probe, _separationBuffer, ResolveHittableMask(), QueryTriggerInteraction.Ignore);
        if (hitCount == 0) return;

        Vector3 push = Vector3.zero;
        for (int i = 0; i < hitCount; i++)
        {
            Collider other = _separationBuffer[i];
            if (other == null) continue;
            if (other.transform == transform) continue;
            if (other.transform.IsChildOf(transform)) continue;

            IDamageable dmg = other.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            Vector3 delta = transform.position - other.transform.position;
            delta.y = 0f;
            float d = delta.magnitude;

            if (d < 0.001f)
            {
                delta = new Vector3(transform.position.x - other.transform.position.x + 0.01f,
                                    0f,
                                    transform.position.z - other.transform.position.z + 0.01f);
                delta = delta.sqrMagnitude > 0f ? delta.normalized : Vector3.right;
                d = 0.01f;
            }

            float overlap = Mathf.Max(0f, ForcedSeparationDistance - d);
            if (overlap > 0f)
                push += (delta / d) * overlap;
        }

        if (push.sqrMagnitude > 1e-8f)
        {
            float cap = Mathf.Min(ForcedSeparationDistance, maxSeparationPushPerFrame);
            controller.Move(Vector3.ClampMagnitude(push, cap));
        }
    }

    /// <summary>
    /// Imported humanoid rigs sometimes ship with loose Rigidbodies on bones — they can tug
    /// the mesh and read as "flips". Kinematic + frozen tilt keeps visuals aligned with the capsule.
    /// </summary>
    private static void LockAvatarRigidbodies(GameObject body)
    {
        if (body == null) return;

        foreach (Rigidbody rb in body.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null) continue;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    private static LayerMask BuildDefaultStaticObstacleMask()
    {
        int mask = 0;
        void Add(string layerName)
        {
            int l = LayerMask.NameToLayer(layerName);
            if (l >= 0) mask |= 1 << l;
        }
        Add("Environment");
        Add("Default");
        Add("Map");
        Add("LevelContent");
        return mask == 0 ? (LayerMask)0 : (LayerMask)mask;
    }

    private void GetCapsuleWorldEndpoints(out Vector3 bottom, out Vector3 top)
    {
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        float halfHeight = Mathf.Max(controller.radius, controller.height * 0.5f - controller.radius);
        bottom = worldCenter - Vector3.up * halfHeight;
        top    = worldCenter + Vector3.up * halfHeight;
    }

    /// <summary>
    /// True if we should allow a jump: CharacterController ground flag and/or a short foot ray.
    /// Prevents Space from being consumed while airborne and fixes flicker when CC briefly reports not grounded.
    /// </summary>
    private bool IsGroundedForJump()
    {
        if (controller == null) return false;
        if (controller.isGrounded) return true;

        float reach = Mathf.Max(controller.stepOffset, controller.skinWidth) + jumpGroundProbeExtra;
        GetCapsuleWorldEndpoints(out Vector3 bottom, out Vector3 _);
        Vector3 origin = bottom + Vector3.up * 0.06f;
        return Physics.Raycast(origin, Vector3.down, out _, reach + 0.06f,
            BuildGroundCheckMask(), QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Forward + slightly-down ray: if we're driving velocity into a wall, damp horizontal speed
    /// (extra layer on top of CapsuleCast clamp) to reduce "frozen" feel on box colliders.
    /// </summary>
    private Vector3 DampHorizontalVelocityIntoWalls(Vector3 worldHorizontalVelocity)
    {
        if (controller == null || staticObstacleMask.value == 0) return worldHorizontalVelocity;
        worldHorizontalVelocity.y = 0f;
        float mag = worldHorizontalVelocity.magnitude;
        if (mag < 0.01f) return worldHorizontalVelocity;

        Vector3 dir = worldHorizontalVelocity / mag;
        GetCapsuleWorldEndpoints(out Vector3 bottom, out Vector3 _);
        Vector3 origin = bottom + Vector3.up * Mathf.Max(0.05f, controller.radius * 0.6f);
        Vector3 probeDir = (dir + Vector3.down * 0.15f).normalized;
        float dist = Mathf.Clamp(mag * Time.deltaTime + controller.radius * 0.35f, 0.08f, 0.55f);

        if (Physics.Raycast(origin, probeDir, out RaycastHit hit, dist, staticObstacleMask, QueryTriggerInteraction.Ignore)
            && hit.collider != null
            && !hit.collider.transform.IsChildOf(transform))
        {
            float damp = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, dist));
            return worldHorizontalVelocity * damp * damp;
        }

        return worldHorizontalVelocity;
    }

    /// <summary>
    /// CapsuleCast + one slide iteration so we shed velocity into walls instead of
    /// hammering CharacterController internal depenetration (stutter near crates).
    /// </summary>
    private Vector3 ClampHorizontalMoveAgainstStatics(Vector3 horizontalDelta)
    {
        if (horizontalDelta.sqrMagnitude < 1e-12f || controller == null)
            return horizontalDelta;

        horizontalDelta.y = 0f;
        GetCapsuleWorldEndpoints(out Vector3 p0, out Vector3 p1);
        float r = controller.radius;
        float pad = Mathf.Max(controller.skinWidth, wallCollisionPadding) + minMoveClearance;
        float dist = horizontalDelta.magnitude;
        Vector3 dir = horizontalDelta / dist;

        if (!Physics.CapsuleCast(
                p0, p1,
                r * 0.98f,
                dir,
                out RaycastHit hit,
                dist + pad,
                staticObstacleMask,
                QueryTriggerInteraction.Ignore))
            return horizontalDelta;

        if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            return horizontalDelta;

        float allowed = Mathf.Max(0f, hit.distance - pad);
        Vector3 move1 = dir * Mathf.Min(dist, allowed);
        Vector3 leftover = horizontalDelta - move1;
        if (leftover.sqrMagnitude < 1e-10f)
            return move1;

        Vector3 n = hit.normal;
        n.y = 0f;
        if (n.sqrMagnitude < 0.001f)
            return move1;
        n.Normalize();

        Vector3 slide = Vector3.ProjectOnPlane(leftover, n);
        slide.y = 0f;
        float slideDist = slide.magnitude;
        if (slideDist < 1e-6f)
            return move1;

        Vector3 sdir = slide / slideDist;
        Vector3 p0b = p0 + move1;
        Vector3 p1b = p1 + move1;
        if (!Physics.CapsuleCast(
                p0b, p1b,
                r * 0.98f,
                sdir,
                out RaycastHit hit2,
                slideDist + pad,
                staticObstacleMask,
                QueryTriggerInteraction.Ignore))
            return move1 + slide;

        if (hit2.collider != null && hit2.collider.transform.IsChildOf(transform))
            return move1 + slide;

        float allowed2 = Mathf.Max(0f, hit2.distance - pad);
        return move1 + sdir * Mathf.Min(slideDist, allowed2);
    }

    private void ApplyMovement()
    {
        if (controller == null) return;
        if (isMantling) return;

        Vector3 frameStartPosition = transform.position;

        // CoD/BO3-style sprint resolution:
        //   • Hold LeftShift → sprint (legacy behaviour).
        //   • Tap C         → latch sprint until W is released or C is tapped again.
        // Either way you must be moving forward (y > 0.1) for sprint to apply.
        bool shiftHeld   = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        bool wantSprint  = (shiftHeld || sprintToggled) && moveInputSmoothed.y > 0.1f;
        if (sprintToggled && moveInputSmoothed.y <= 0.05f)
            sprintToggled = false; // Auto-cancel toggle when you stop moving forward.

        // Sliding / prone / crouch all force sprint off so you can't sprint
        // through a slide or while lying flat.
        if (isSliding || isProne)
            wantSprint = false;

        isSprinting = wantSprint;

        float targetSpeed    = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        if (isSliding) targetSpeed *= slideSpeedMultiplier;
        else if (isProne) targetSpeed *= proneSpeedMultiplier;
        else if (isCrouching) targetSpeed *= crouchSpeedMultiplier;

        // Flip preserves the boost velocity assigned by TryStartFlip — we
        // don't let WASD accelerate it (so the spin reads cleanly), but
        // gravity below still ticks normally so the player lands.
        if (!isFlipping)
        {
            Vector3 moveDirection = GetCameraRelativeMoveDirection(moveInputSmoothed);
            DebugWOnlyMovement(moveDirection);
            Vector3 targetVelocity = moveDirection * targetSpeed;

            float rate = moveInputSmoothed.sqrMagnitude > 0.01f ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);
            horizontalVelocity = DampHorizontalVelocityIntoWalls(horizontalVelocity);
        }

        Vector3 horizontalDelta = horizontalVelocity * Time.deltaTime;
        horizontalDelta.y = 0f;
        if (staticObstacleMask.value != 0)
            horizontalDelta = ClampHorizontalMoveAgainstStatics(horizontalDelta);

        bool jumpFeetGrounded = IsGroundedForJump();
        // Guard: while the flip coroutine owns the vertical impulse, do NOT
        // reset it to -2f. isGrounded is captured at the very top of Update
        // (before HandleActionInput), so it is still true on the same frame
        // that TryStartFlip sets verticalVelocity.y to the launch impulse.
        // Without this guard the -2f overwrite fires immediately, the player
        // never leaves the ground, and the high horizontal boost drives them
        // into the floor geometry instead of arcing cleanly over it.
        if (!isFlipping && (isGrounded || (jumpFeetGrounded && verticalVelocity.y <= 0f)))
        {
            verticalVelocity.y = -2f;

            if (jumpRequested && !isProne && !isSliding && jumpFeetGrounded)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isGrounded = false;
            }
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
            verticalVelocity.y  = Mathf.Max(verticalVelocity.y, -40f);
        }

        jumpRequested = false;
        Vector3 verticalDelta = new Vector3(0f, verticalVelocity.y * Time.deltaTime, 0f);
        controller.Move(horizontalDelta + verticalDelta);

        UpdateTacticalManeuverState();
        UpdateStanceCollider();

        ClampInsideArena();

        Vector3 frameDisplacement = transform.position - frameStartPosition;
        frameDisplacement.y = 0f;
        actualHorizontalVelocity = Time.deltaTime > 0f
            ? frameDisplacement / Time.deltaTime
            : Vector3.zero;

        wasMoving = actualHorizontalVelocity.sqrMagnitude > 0.01f;

        // While flipping, the FlipRoutine owns the body's local rotation —
        // skip the world-yaw snap so the spin isn't immediately overwritten.
        if (isThirdPersonActive && thirdPersonBody != null && !isFlipping)
        {
            Vector3 moveFlat = actualHorizontalVelocity;
            if (moveFlat.sqrMagnitude > 0.01f)
            {
                float targetY = Quaternion.LookRotation(moveFlat).eulerAngles.y;
                thirdPersonBody.transform.rotation = Quaternion.Slerp(
                    thirdPersonBody.transform.rotation,
                    Quaternion.Euler(0f, targetY, 0f),
                    40f * Time.deltaTime);
            }
        }
    }

    private Vector3 GetCameraRelativeMoveDirection(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);
        if (input.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Quaternion yawOnlyBasis = Quaternion.Euler(0f, cameraYaw, 0f);
        Vector3 forward = yawOnlyBasis * Vector3.forward;
        Vector3 right = yawOnlyBasis * Vector3.right;

        Vector3 moveDirection = (right * input.x) + (forward * input.y);
        moveDirection.y = 0f;
        return moveDirection.sqrMagnitude > 1f
            ? moveDirection.normalized
            : moveDirection;
    }

    private void DebugWOnlyMovement(Vector3 moveDirection)
    {
        if (!debugWOnlyMovement || Time.time < nextWOnlyMoveDebugLogTime)
            return;

        if (!IsKeyboardWOnlyMovement())
            return;

        nextWOnlyMoveDebugLogTime = Time.time + 0.35f;
        Debug.Log($"[PlayerController] W-only moveInputRaw={moveInputRaw} finalMoveDirection={moveDirection}");
    }

    private static bool IsKeyboardWOnlyMovement()
    {
        if (Keyboard.current == null) return false;

        Keyboard k = Keyboard.current;
        bool forward = k.wKey.isPressed || k.upArrowKey.isPressed || k.numpad8Key.isPressed;
        bool back = k.sKey.isPressed || k.downArrowKey.isPressed || k.numpad2Key.isPressed;
        bool left = k.aKey.isPressed || k.leftArrowKey.isPressed || k.numpad4Key.isPressed;
        bool right = k.dKey.isPressed || k.rightArrowKey.isPressed || k.numpad6Key.isPressed;
        return forward && !back && !left && !right;
    }

    private Vector3 GetActualHorizontalVelocity()
        => actualHorizontalVelocity;

    private Vector3 ResolveMoveWorldDirection(Vector2 input)
    {
        Vector2 clamped = Vector2.ClampMagnitude(input, 1f);
        if (clamped.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return new Vector3(clamped.x, 0f, clamped.y);
    }

    private void EnforcePerspectiveVisibility()
    {
        if (thirdPersonBody == null)
            return;

        if (isThirdPersonActive)
            SetThirdPersonRenderersVisible(true);
        else
            SetThirdPersonRenderersVisible(false);
    }

    // ✅ FIX: GetActiveAnimator always returns the live animator from thirdPersonBody
    //         so it works even if the body was spawned after Start()
    private Animator GetActiveAnimator()
    {
        if (thirdPersonBody != null)
        {
            Animator bodyAnim = thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (bodyAnim != null)
            {
                // Keep the cached field in sync so other code that reads animator directly works
                if (bodyAnim != animator)
                {
                    animator = bodyAnim;
                    ConfigureAnimatorBinding(animator, forceControllerAssignment: false);
                    CacheAnimatorParameters();
                }
                return bodyAnim;
            }
        }
        return animator;
    }

    public void TeleportTo(Vector3 position)
    {
        if (IsUnsafeSpawn(position))
            position = SafeFallbackSpawn;

        if (controller != null) controller.enabled = false;
        transform.position = position;
        if (controller != null) controller.enabled = true;
        verticalVelocity = Vector3.zero;
        cameraYaw = transform.eulerAngles.y;

        if (runtimeThirdPersonCamera != null)
        {
            CameraController camCtrl = runtimeThirdPersonCamera.GetComponent<CameraController>();
            if (camCtrl != null)
            {
                camCtrl.target = transform;
                camCtrl.SnapToTarget();
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CAMERA / LOOK
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyLook()
    {
        Camera cam = ActiveCamera;
        if (cam == null) return;

        // Mouse-look responsiveness is now driven by the camera's
        // RotationSpeed property, which combines the designer-tuned base
        // rotation speed with the live Options-menu sensitivity multiplier
        // (0–7 slider → 0.20×–2.20×). Falls back to the local sensitivity
        // value if the CameraController hasn't initialized yet.
        float rotSpeed = (CameraController.Instance != null)
            ? CameraController.Instance.RotationSpeed
            : sensitivity * 100f * LookSensitivityRuntime.LookMultiplier;
        // Convert deg/sec back into the per-mouse-unit input scale we used
        // before so existing input.lookInput numbers continue to feel right.
        float perUnit = rotSpeed * 0.01f;
        // Use the smoothed input so rotation doesn't jitter on high DPI mice.
        // Use unscaledDeltaTime so pause/menu timeScale changes don't distort sensitivity.
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float mouseX = lookInputSmoothed.x * perUnit * dt;
        float mouseY = lookInputSmoothed.y * perUnit * dt;

        cameraPitch -= mouseY;
        if (isThirdPersonActive)
        {
            // Keep the third-person camera in a grounded shooter range.
            cameraPitch = Mathf.Clamp(cameraPitch, ThirdPersonMinPitch, ThirdPersonMaxPitch);
        }
        else
        {
            // Call-of-duty style: wider pitch in first person.
            cameraPitch = Mathf.Clamp(cameraPitch, FirstPersonMinPitch, FirstPersonMaxPitch);
        }

        cameraYaw += mouseX;
        transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);

        if (isThirdPersonActive && runtimeThirdPersonCamera != null)
        {
            CameraController orbitCtrl = runtimeThirdPersonCamera.GetComponent<CameraController>();
            if (orbitCtrl != null)
                orbitCtrl.pitch = cameraPitch;
        }
        else if (!isThirdPersonActive && firstPersonCam != null)
        {
            // Apply pitch directly to the FPS camera, preserving its base orientation.
            firstPersonCam.transform.localRotation = firstPersonLocalRot * Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void UpdateHeadBob()
    {
        if (isThirdPersonActive) return;
        if (firstPersonCam == null) return;

        // Simple, clean FPS bob: tied to horizontal speed and grounded state.
        float speed = new Vector3(actualHorizontalVelocity.x, 0f, actualHorizontalVelocity.z).magnitude;
        float bobStrength = isGrounded ? Mathf.Clamp01(speed / 6f) : 0f;
        float bobFreq = Mathf.Lerp(0f, 10.5f, bobStrength);

        float y = Mathf.Sin(Time.time * bobFreq) * 0.03f * bobStrength;
        float x = Mathf.Cos(Time.time * bobFreq * 0.5f) * 0.02f * bobStrength;

        Vector3 basePos = firstPersonLocalPos;
        firstPersonCam.transform.localPosition = basePos + new Vector3(x, y, 0f);
    }

    private void UpdateCameraZoom()
    {
        // Zoom permanently disabled — camera stays at its default OTS offset.
    }

    private void UpdateCameraKick()
    {
        if (ActiveCamera == null) return;
        cameraKickCurrent = Mathf.Lerp(cameraKickCurrent, cameraKickTarget, 18f * Time.deltaTime);
        cameraKickTarget  = Mathf.Lerp(cameraKickTarget, 0f, 14f * Time.deltaTime);
        cameraPitch += cameraKickCurrent * Time.deltaTime;
        // Keep camera kick inside the same grounded shooter range.
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            isThirdPersonActive ? ThirdPersonMinPitch : FirstPersonMinPitch,
            isThirdPersonActive ? ThirdPersonMaxPitch : FirstPersonMaxPitch);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  COMBAT
    // ════════════════════════════════════════════════════════════════════════

    public void Attack()
    {
        if (attackCooldownTimer > 0f || isAttacking) return;

        isAttacking         = true;
        attackCooldownTimer = AttackCooldown;
        attackResetTimer    = AttackResetTime;
        attackFailsafeTimer = AttackFailsafeDuration;

        Animator activeAnimator = GetActiveAnimator();
        if (activeAnimator == null || activeAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                "Attack started without a valid RuntimeAnimatorController. " +
                "Combat failsafe will clear the attack lock automatically.",
                this);
        }

        FireAttack();
    }

    private void FireAttack()
    {
        ApplyAttackLunge();

        if (audioSource != null && swordSwing != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(swordSwing, AudioSettingsRuntime.ScaledSfx(1f));
        }

        if (weaponHitboxRoutine != null)
            StopCoroutine(weaponHitboxRoutine);

        if (equippedWeaponObject != null)
        {
            WeaponBase weapon = equippedWeaponObject.GetComponent<WeaponBase>();
            if (weapon != null) weapon.Attack();

            // Drive the per-weapon "alive" animation (e.g. Nunchucks open
            // + spin during the swing window). The animator handles its
            // own timing relative to attackSpeed/attackDelay.
            WeaponLiveAnimator live = equippedWeaponObject.GetComponent<WeaponLiveAnimator>();
            if (live != null) live.PlayAttack(attackSpeed, attackDelay);
        }

        bool immediateHit = TryImmediateMeleeStrike();

        if (!immediateHit && equippedWeaponHitbox != null)
        {
            if (weaponHitboxRoutine != null)
                StopCoroutine(weaponHitboxRoutine);
            weaponHitboxRoutine = StartCoroutine(PlayerWeaponHitboxWindowRoutine());
        }
    }

    private System.Collections.IEnumerator PlayerWeaponHitboxWindowRoutine()
    {
        equippedWeaponHitbox.DisableHitbox();

        if (attackDelay > 0f)
            yield return new WaitForSeconds(attackDelay);

        equippedWeaponHitbox.EnableHitbox();

        yield return new WaitForSeconds(0.12f);

        equippedWeaponHitbox.DisableHitbox();
        weaponHitboxRoutine = null;
    }

    private void UpdateCombatState()
    {
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (!isAttacking) return;

        if (attackResetTimer > 0f)
            attackResetTimer -= Time.deltaTime;

        if (attackFailsafeTimer > 0f)
            attackFailsafeTimer -= Time.deltaTime;

        bool normalAttackFinished = attackResetTimer <= 0f;
        bool failsafeExpired = attackFailsafeTimer <= 0f;

        if (normalAttackFinished || failsafeExpired)
            ResetAttackState();
    }

    private void ToggleCrouch()
    {
        if (isMantling) return;
        if (isProne)
        {
            isProne = false;
            isCrouching = true;
        }
        else
        {
            isCrouching = !isCrouching;
        }
    }

    /// <summary>
    /// Black Ops 3 style "Crawl" (prone). Toggles the lying-flat stance.
    /// Visually the body is rotated 90° forward so the legs no longer punch
    /// through the ground, and the controller height shrinks to <see cref="proneHeight"/>.
    /// </summary>
    private void ToggleCrawl()
    {
        if (isMantling || isFlipping) return;
        isProne = !isProne;
        if (isProne)
        {
            isCrouching   = false;
            isSliding     = false;
            sprintToggled = false; // Can't sprint while crawling.
        }
    }

    // Kept for backwards-compat with any callers that still hit the old name.
    private void ToggleProne() => ToggleCrawl();

    /// <summary>Toggle the sprint lock (CoD/BO3 style — tap C to keep sprinting).</summary>
    private void ToggleSprintLock()
    {
        if (isProne || isSliding || isMantling || isFlipping) return;
        sprintToggled = !sprintToggled;
    }

    /// <summary>
    /// Performs a Black Ops 3 style combat flip — a short upward hop with a
    /// forward boost and a full pitched 360° rotation on the body mesh. Cannot
    /// chain into another flip until <see cref="flipCooldown"/> elapses.
    /// </summary>
    private void TryStartFlip()
    {
        if (isMantling || isSliding || isFlipping) return;
        if (flipCooldownTimer > 0f) return;
        if (controller == null || !controller.enabled) return;

        // Cancel any conflicting stance — you can't flip while crawling.
        isProne     = false;
        isCrouching = false;

        // Forward direction in world space — falls back to the player's
        // facing if there's no input on the WASD axes.
        Vector3 inputDir = new Vector3(moveInputSmoothed.x, 0f, moveInputSmoothed.y);
        Vector3 forward  = inputDir.sqrMagnitude > 0.05f
            ? transform.TransformDirection(inputDir.normalized)
            : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward;

        verticalVelocity.y = Mathf.Sqrt(Mathf.Max(0.01f, flipVerticalImpulse) * -2f * gravity);
        horizontalVelocity = forward.normalized * flipForwardBoost;
        flipCooldownTimer  = flipCooldown;

        if (flipCoroutine != null) StopCoroutine(flipCoroutine);
        flipCoroutine = StartCoroutine(FlipRoutine(forward));
    }

    private System.Collections.IEnumerator FlipRoutine(Vector3 worldForward)
    {
        isFlipping = true;
        jumpRequested = false;

        // Reference axis to rotate around (always perpendicular to the
        // world-up + forward plane so the spin reads as a clean front flip).
        // We rotate the body's *local* X axis through 360° over the duration.
        Quaternion baseLocal = thirdPersonBodyBaseLocalRotation;

        float elapsed  = 0f;
        float duration = Mathf.Max(0.15f, flipDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            // Smooth ease so the spin feels weighty rather than linear.
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
            float angle = ease * 360f;
            if (thirdPersonBody != null)
                thirdPersonBody.transform.localRotation = baseLocal * Quaternion.AngleAxis(angle, Vector3.right);
            yield return null;
        }

        if (thirdPersonBody != null)
            thirdPersonBody.transform.localRotation = baseLocal;

        isFlipping = false;
        flipCoroutine = null;

        // Do NOT slam vertical velocity to -2: that discontinuity used to trigger
        // the SnapToArenaFloor downward-teleport bug. Keep whatever velocity the
        // arc finished with so gravity / the CharacterController land naturally.
        // Drop nearly all residual horizontal boost so WASD input is responsive
        // again the instant the flip ends (was 0.4 = 40 % carry-over → felt
        // uncontrollable).
        horizontalVelocity *= 0.15f;
        SnapRootAboveFloorImmediate();
    }

    private void SnapRootAboveFloorImmediate()
    {
        if (controller == null) return;

        Vector3 p = transform.position;
        // Cast distance must reach the floor even from the flip arc peak (~6.5 m).
        // Use 10 m so it works regardless of jump / flip height.
        Vector3 origin = p + Vector3.up * 0.35f;
        int groundMask = BuildGroundCheckMask();
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            return;

        // Place the capsule bottom exactly on the floor surface (+ skinWidth so
        // the CC's internal contact offset keeps the player flush, not buried).
        // Previous formula subtracted skinWidth, which put the capsule 4 cm underground.
        float restY = hit.point.y - controller.center.y + (controller.height * 0.5f) + controller.skinWidth;
        if (p.y < restY - 0.02f)
        {
            controller.enabled = false;
            transform.position = new Vector3(p.x, restY, p.z);
            controller.enabled = true;
        }
    }

    // Returns a layermask that covers solid ground but excludes the player's
    // own capsule so snap raycasts never self-intersect.
    private static int BuildGroundCheckMask()
    {
        int mask = Physics.DefaultRaycastLayers;
        int playerLayer    = LayerMask.NameToLayer("Player");
        int characterLayer = LayerMask.NameToLayer("Character");
        int hittableLayer  = LayerMask.NameToLayer("Hittable");
        if (playerLayer    >= 0) mask &= ~(1 << playerLayer);
        if (characterLayer >= 0) mask &= ~(1 << characterLayer);
        if (hittableLayer  >= 0) mask &= ~(1 << hittableLayer);
        return mask;
    }

    private void TryStartSlide()
    {
        if (isMantling || isProne || isSliding || isFlipping) return;
        if (!isGrounded) return;
        if (slideCooldownTimer > 0f) return;
        if (moveInputSmoothed.sqrMagnitude < 0.1f) return;

        isSliding = true;
        slideTimer = slideDuration;
        slideCooldownTimer = powerSlideCooldown;
        isCrouching = true;
        Vector3 inputDir = new Vector3(moveInputSmoothed.x, 0f, moveInputSmoothed.y);
        Vector3 slideDirection = inputDir.sqrMagnitude > 0.05f
            ? transform.TransformDirection(inputDir.normalized)
            : transform.forward;
        slideDirection.y = 0f;
        if (slideDirection.sqrMagnitude < 0.001f)
            slideDirection = transform.forward;
        horizontalVelocity = slideDirection.normalized * powerSlideBoost;
    }

    private void TryThrusterJumpOrQueueNormalJump()
    {
        if (isProne || isSliding || isMantling || isFlipping)
            return;

        // Thruster = Shift + movement (keyboard) or shoulder/trigger + stick (gamepad).
        // Do NOT treat "any stick movement" as thruster — that blocked normal Space jumps when a pad was connected.
        bool wantsThruster = Keyboard.current != null
            && Keyboard.current.leftShiftKey.isPressed
            && moveInputSmoothed.sqrMagnitude > 0.05f;

        if (Gamepad.current != null
            && (Gamepad.current.leftShoulder.isPressed || Gamepad.current.leftTrigger.ReadValue() > 0.45f)
            && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.05f)
            wantsThruster = true;

        if (wantsThruster && TryStartThrusterJump())
            return;

        if (!IsGroundedForJump())
            return;

        jumpRequested = true;
    }

    private bool TryStartThrusterJump()
    {
        if (!IsGroundedForJump() || thrusterCooldownTimer > 0f)
            return false;

        Vector3 inputDir = new Vector3(moveInputSmoothed.x, 0f, moveInputSmoothed.y);
        Vector3 thrustDirection = inputDir.sqrMagnitude > 0.05f
            ? transform.TransformDirection(inputDir.normalized)
            : transform.forward;
        thrustDirection.y = 0f;
        if (thrustDirection.sqrMagnitude < 0.001f)
            thrustDirection = transform.forward;

        verticalVelocity.y = Mathf.Sqrt(thrusterJumpHeight * -2f * gravity);
        horizontalVelocity += thrustDirection.normalized * thrusterForwardBoost;
        thrusterCooldownTimer = thrusterCooldown;
        isGrounded = false;
        jumpRequested = false;
        isCrouching = false;
        return true;
    }

    private void TryMantle()
    {
        if (isMantling || isSliding) return;
        if (mantleCoroutine != null) return;

        Vector3 origin = transform.position + Vector3.up * 1.0f;
        int groundMask = BuildGroundCheckMask();
        if (!Physics.Raycast(origin, transform.forward, out RaycastHit wallHit, 1.1f, groundMask, QueryTriggerInteraction.Ignore))
        {
            // Fallback mini-vault so V always produces a visible maneuver.
            Vector3 fallbackTarget = transform.position + transform.forward * 0.7f + Vector3.up * 0.25f;
            mantleCoroutine = StartCoroutine(MantleRoutine(fallbackTarget));
            return;
        }

        Vector3 topCheck = wallHit.point + Vector3.up * mantleUpHeight + transform.forward * 0.15f;
        if (Physics.Raycast(topCheck, Vector3.down, out RaycastHit topHit, mantleUpHeight + 1.2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            mantleCoroutine = StartCoroutine(MantleRoutine(topHit.point + transform.forward * mantleForwardDistance));
        }
    }

    private System.Collections.IEnumerator MantleRoutine(Vector3 mantleTarget)
    {
        isMantling = true;
        isSliding = false;
        jumpRequested = false;

        Vector3 start = transform.position;
        float t = 0f;
        float duration = Mathf.Max(0.05f, mantleDuration);
        if (controller != null) controller.enabled = false;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, mantleTarget, Mathf.Clamp01(t));
            yield return null;
        }

        if (controller != null) controller.enabled = true;
        verticalVelocity = Vector3.zero;
        isMantling = false;
        mantleCoroutine = null;
    }

    private void UpdateTacticalManeuverState()
    {
        if (slideCooldownTimer > 0f)
            slideCooldownTimer -= Time.deltaTime;

        if (thrusterCooldownTimer > 0f)
            thrusterCooldownTimer -= Time.deltaTime;

        if (flipCooldownTimer > 0f)
            flipCooldownTimer -= Time.deltaTime;

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f || !isGrounded)
            {
                isSliding = false;
                isCrouching = false;
            }
        }
    }

    private void UpdateStanceCollider()
    {
        if (controller == null) return;

        float standHeight = 1.8f;
        float desiredHeight = standHeight;
        if (isProne) desiredHeight = proneHeight;
        else if (isCrouching || isSliding) desiredHeight = crouchHeight;

        targetControllerHeight = Mathf.Clamp(desiredHeight, 0.6f, standHeight);
        targetControllerCenter = new Vector3(0f, targetControllerHeight * 0.5f + 0.02f, 0f);

        controller.height = Mathf.Lerp(controller.height, targetControllerHeight, stanceTransitionSpeed * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center, targetControllerCenter, stanceTransitionSpeed * Time.deltaTime);
        controller.stepOffset = controller.height > 1.6f ? 0.5f : 0.05f;

        // While the FlipRoutine is animating the body, leave its rotation alone
        // so we don't fight it for the local rotation slot.
        if (isFlipping) return;

        if (thirdPersonBody != null)
        {
            // Crawl uses a tiny lift (the legs no longer need to be buried in
            // the floor — we rotate the model 90° forward instead). Crouch /
            // slide keep their existing offset.
            float desiredOffsetY = 0f;
            if (isProne) desiredOffsetY = proneCrawlBodyYOffset;
            else if (isCrouching || isSliding) desiredOffsetY = crouchBodyYOffset;

            Vector3 desiredPos = thirdPersonBodyBaseLocalPosition + new Vector3(0f, desiredOffsetY, 0f);
            thirdPersonBody.transform.localPosition = Vector3.Lerp(
                thirdPersonBody.transform.localPosition,
                desiredPos,
                stanceTransitionSpeed * Time.deltaTime);

            // Rotate the model 90° forward when crawling so it actually lies
            // flat instead of standing inside a half-height capsule. All other
            // stances reset to the cached base rotation (set at body spawn).
            Quaternion desiredRot = isProne
                ? thirdPersonBodyBaseLocalRotation * Quaternion.Euler(-90f, 0f, 0f)
                : thirdPersonBodyBaseLocalRotation;
            thirdPersonBody.transform.localRotation = Quaternion.Slerp(
                thirdPersonBody.transform.localRotation,
                desiredRot,
                stanceTransitionSpeed * Time.deltaTime);
        }
    }

    private void ResetAttackState()
    {
        isAttacking = false;
        attackResetTimer = 0f;
        attackFailsafeTimer = 0f;
    }

    private void ApplyAttackLunge()
    {
        if (controller == null) return;
        Vector3 lunge = transform.forward * attackLungeDistance;
        lunge.y = 0f;
        if (staticObstacleMask.value != 0)
            lunge = ClampHorizontalMoveAgainstStatics(lunge);
        controller.Move(lunge);
        ClampInsideArena();
    }

    private void AttackRaycast()
    {
        AttackMelee();
        cameraKickTarget = -1.2f;
    }

    private const float MeleeHitAngle = 60f;   // strict forward-facing cone (half-angle)

    private bool TryImmediateMeleeStrike()
    {
        Physics.SyncTransforms();

        Camera cam = ActiveCamera != null ? ActiveCamera : Camera.main;
        Vector3 origin;
        Vector3 forward;

        if (cam != null && !isThirdPersonActive)
        {
            origin = cam.transform.position + cam.transform.forward * 0.12f;
            forward = cam.transform.forward;
        }
        else
        {
            origin = transform.position + Vector3.up * 1.15f + transform.forward * 0.25f;
            forward = transform.forward;
        }

        if (forward.sqrMagnitude < 0.0001f)
            return false;
        forward.Normalize();

        float radius = Mathf.Clamp(attackRadius * 0.42f, 0.25f, 0.65f);
        float reach = Mathf.Max(attackDistance + 0.65f, 2.8f);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            forward,
            meleeRaycastHits,
            reach,
            ~0,
            QueryTriggerInteraction.Collide);

        Transform bestTarget = null;
        Vector3 bestPoint = origin + forward * reach;
        float bestDistance = float.PositiveInfinity;
        float nearestBlockerDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = meleeRaycastHits[i];
            Collider col = hit.collider;
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            Transform damageRoot = GetDamageTargetTransform(col.transform);
            if (damageRoot != null)
            {
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestTarget = damageRoot;
                    bestPoint = hit.point.sqrMagnitude > 0.0001f ? hit.point : col.ClosestPoint(origin);
                }
                continue;
            }

            if (!col.isTrigger && IsMeleeBlocker(col))
                nearestBlockerDistance = Mathf.Min(nearestBlockerDistance, hit.distance);
        }

        if (bestTarget == null)
        {
            Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
            Vector3 forwardFlat = forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.0001f)
                forwardFlat = transform.forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.0001f)
                return false;
            forwardFlat.Normalize();

            if (TryFindBestMeleeTarget(playerCenter, forwardFlat, out bestTarget, out bestPoint))
                bestDistance = Vector3.Distance(origin, bestPoint);
        }

        if (bestTarget == null || bestDistance > nearestBlockerDistance + 0.05f)
            return false;

        if (!TryDamageTargetFromPoint(bestTarget, attackDamage, origin))
            return false;

        Vector3 hitDirection = bestTarget.position - transform.position;
        hitDirection.y = 0f;
        if (hitDirection.sqrMagnitude < 0.001f)
            hitDirection = forward;
        ApplyHitReaction(bestTarget, hitDirection.normalized);
        HitTarget(bestPoint);
        cameraKickTarget = -1.2f;
        return true;
    }

    private bool IsMeleeBlocker(Collider col)
    {
        if (col == null || staticObstacleMask.value == 0)
            return false;

        int layerMask = 1 << col.gameObject.layer;
        return (staticObstacleMask.value & layerMask) != 0;
    }

    private void AttackMelee()
    {
        // Deterministic melee: one non-alloc sphere at the weapon impact point,
        // restricted to the Hittable layer only.

        Vector3 playerCenter = transform.position + Vector3.up * 1.0f;

        // Flatten the player's forward vector to the horizontal plane so the
        // cone check ignores vertical aim and stays consistent on slopes.
        Vector3 forwardFlat = transform.forward;
        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude < 0.0001f) return;
        forwardFlat.Normalize();

        // CharacterController / NavMeshAgent movement can update transforms
        // outside the physics step. Sync once so the overlap query sees the
        // latest positions before resolving the attack.
        Physics.SyncTransforms();

        Vector3 impactPoint = GetMeleeImpactPoint(playerCenter, forwardFlat);
        int hitCount = Physics.OverlapSphereNonAlloc(
            impactPoint,
            MeleeImpactRadius,
            meleeOverlapHits,
            ResolveHittableMask(),
            QueryTriggerInteraction.Ignore);

        Transform bestTarget = null;
        Vector3 bestHitPoint = impactPoint;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = meleeOverlapHits[i];
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            Transform damageRoot = GetDamageTargetTransform(hit.transform);
            if (damageRoot == null) continue;

            Vector3 closest = hit.ClosestPoint(impactPoint);
            Vector3 toTarget = closest - playerCenter;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > attackDistance * attackDistance) continue;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(forwardFlat, toTarget.normalized) > MeleeHitAngle)
                continue;

            float d2 = (closest - impactPoint).sqrMagnitude;
            if (d2 < bestDistanceSqr)
            {
                bestDistanceSqr = d2;
                bestTarget = damageRoot;
                bestHitPoint = closest;
            }
        }

        if (bestTarget != null && TryDamageTarget(bestTarget, attackDamage))
        {
            ApplyHitReaction(bestTarget, forwardFlat);
            HitTarget(bestHitPoint);
        }
    }

    private Vector3 GetMeleeImpactPoint(Vector3 playerCenter, Vector3 forwardFlat)
    {
        if (equippedWeaponObject != null)
        {
            Renderer[] renderers = equippedWeaponObject.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(equippedWeaponObject.transform.position, Vector3.zero);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled) continue;
                if (!hasBounds) { bounds = renderers[i].bounds; hasBounds = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }

            if (hasBounds)
            {
                Vector3 weaponPoint = bounds.center + forwardFlat * Mathf.Min(attackDistance * 0.45f, bounds.extents.magnitude);
                weaponPoint.y = Mathf.Clamp(weaponPoint.y, playerCenter.y - 0.35f, playerCenter.y + 0.45f);
                return weaponPoint;
            }
        }

        return playerCenter + forwardFlat * Mathf.Clamp(attackDistance * 0.65f, 0.75f, attackDistance);
    }

    private bool TryFindBestMeleeTarget(Vector3 playerCenter, Vector3 forwardFlat,
        out Transform bestTarget, out Vector3 bestHitPoint)
    {
        bestTarget = null;
        bestHitPoint = playerCenter;

        float capsuleRadius = Mathf.Max(0.1f, attackRadius);
        float capsuleReach = Mathf.Max(0f, attackDistance - capsuleRadius);
        Vector3 capsuleEnd = playerCenter + forwardFlat * capsuleReach;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            playerCenter,
            capsuleEnd,
            capsuleRadius,
            meleeOverlapHits,
            resolvedAttackMask,
            QueryTriggerInteraction.Collide);

        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = meleeOverlapHits[i];
            if (hit == null) continue;

            Transform hitTransform = hit.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            Transform damageRoot = GetDamageTargetTransform(hitTransform);
            if (damageRoot == null) continue;

            Vector3 closestPoint = hit.ClosestPoint(playerCenter);
            Vector3 toTarget = closestPoint - playerCenter;
            toTarget.y = 0f;
            float horizontalDistanceSqr = toTarget.sqrMagnitude;

            if (horizontalDistanceSqr > attackDistance * attackDistance)
                continue;

            if (horizontalDistanceSqr >= 0.0001f)
            {
                float horizontalDistance = Mathf.Sqrt(horizontalDistanceSqr);

                // Let truly point-blank overlaps through even if the target's
                // root pivot is offset or partially beside the player.
                if (horizontalDistance > capsuleRadius * 0.5f)
                {
                    float angle = Vector3.Angle(forwardFlat, toTarget / horizontalDistance);
                    if (angle > MeleeHitAngle) continue;
                }
            }

            if (horizontalDistanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = horizontalDistanceSqr;
                bestTarget = damageRoot;
                bestHitPoint = closestPoint;
            }
        }

        return bestTarget != null;
    }

    // Single, single-target, single-hit precision fallback.
    // • Fires ONE forward sphere cast from the player's chest
    // • Starts just ahead of the player so self-overlap cannot block it
    // • Re-validates the forward-cone constraint
    // • Will damage at most ONE enemy and never chains
    private void TryPrecisionSweepFallback(Vector3 playerCenter, Vector3 forwardFlat)
    {
        float sweepRadius = Mathf.Clamp(attackRadius * 0.35f, 0.12f, 0.45f);
        float sweepDistance = Mathf.Max(0.05f, attackDistance - sweepRadius);
        Vector3 sweepOrigin = playerCenter + forwardFlat * sweepRadius;

        if (!Physics.SphereCast(
                sweepOrigin,
                sweepRadius,
                forwardFlat,
                out RaycastHit rayHit,
                sweepDistance,
                resolvedAttackMask,
                QueryTriggerInteraction.Collide))
            return;

        if (rayHit.transform == transform || rayHit.transform.IsChildOf(transform))
            return;

        Transform damageRoot = GetDamageTargetTransform(rayHit.transform);
        if (damageRoot == null) return;

        Vector3 targetPoint = rayHit.collider != null
            ? rayHit.collider.ClosestPoint(playerCenter)
            : rayHit.point;

        Vector3 toHit = targetPoint - playerCenter;
        toHit.y = 0f;
        if (toHit.sqrMagnitude >= 0.0001f)
        {
            if (Vector3.Angle(forwardFlat, toHit.normalized) > MeleeHitAngle) return;
        }

        if (toHit.sqrMagnitude > attackDistance * attackDistance) return;

        if (TryDamageTarget(damageRoot, attackDamage))
        {
            ApplyHitReaction(damageRoot, forwardFlat);
            HitTarget(rayHit.point);
        }
    }

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private Transform GetDamageTargetTransform(Transform target)
    {
        if (target == null) return null;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.gameObject != gameObject && damageable.IsAlive)
            return damageable.transform;

        Actor actor = target.GetComponentInParent<Actor>();
        if (actor != null && actor.gameObject != gameObject)
            return actor.transform;

        return null;
    }

    private bool TryDamageTarget(Transform target, int damage)
    {
        if (target == null) return false;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.gameObject != gameObject && damageable.IsAlive)
        {
            if (DamageOcclusion.IsBlocked(gameObject, damageable.gameObject))
                return false;
            damageable.ReceiveDamage(damage, gameObject);
            return true;
        }

        Actor actor = target.GetComponentInParent<Actor>();
        if (actor != null && actor.gameObject != gameObject)
        {
            actor.TakeDamage(damage);
            return true;
        }

        return false;
    }

    private bool TryDamageTargetFromPoint(Transform target, int damage, Vector3 attackOriginWorld)
    {
        if (target == null) return false;

        EnemyController enemy = target.GetComponentInParent<EnemyController>();
        if (enemy != null && enemy.gameObject != gameObject && enemy.IsAlive)
        {
            if (DamageOcclusion.IsBlockedFromPoint(gameObject, enemy.gameObject, attackOriginWorld))
                return false;

            int appliedDamage = Mathf.Max(1, damage);
            if (GameManager.Instance != null)
            {
                int hitsToKill = Mathf.Max(1, GameManager.Instance.GetEnemyHitsToKillByPlayer());
                appliedDamage = Mathf.Max(1, Mathf.CeilToInt((float)enemy.maxHealth / hitsToKill));
            }

            enemy.TakeDamage(appliedDamage, byPlayer: true);
            return true;
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.gameObject != gameObject && damageable.IsAlive)
        {
            if (DamageOcclusion.IsBlockedFromPoint(gameObject, damageable.gameObject, attackOriginWorld))
                return false;
            damageable.ReceiveDamage(damage, gameObject);
            return true;
        }

        Actor actor = target.GetComponentInParent<Actor>();
        if (actor != null && actor.gameObject != gameObject)
        {
            actor.TakeDamage(damage);
            return true;
        }

        return false;
    }

    private void ApplyHitReaction(Transform hitTransform, Vector3 hitDirection)
    {
        if (hitTransform == null) return;

        Vector3 push = new Vector3(hitDirection.x, 0f, hitDirection.z).normalized;
        if (push.sqrMagnitude < 0.001f)
        {
            push = (hitTransform.position - transform.position);
            push.y = 0f;
            push.Normalize();
        }

        Rigidbody rb = hitTransform.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(push * hitKnockbackForce, ForceMode.VelocityChange);
            return;
        }

        hitTransform.position += push * 0.18f;
    }

    private void HitTarget(Vector3 pos)
    {
        if (audioSource != null && hitSound != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(hitSound, AudioSettingsRuntime.ScaledSfx(1f));
        }

        if (hitEffect != null)
        {
            GameObject fx = Instantiate(hitEffect, pos, Quaternion.identity);
            Destroy(fx, 20f);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ANIMATION
    // ════════════════════════════════════════════════════════════════════════

    private void CacheAnimatorParameters()
    {
        _animParameterHashes = new HashSet<int>();

        // ✅ FIX: Use GetActiveAnimator() instead of the cached field, so we
        //         always cache against the live animator even if called early.
        Animator activeAnim = thirdPersonBody != null
            ? thirdPersonBody.GetComponentInChildren<Animator>(true)
            : animator;

        if (activeAnim == null) return;

        foreach (AnimatorControllerParameter p in activeAnim.parameters)
            _animParameterHashes.Add(p.nameHash);
    }

    private void UpdateAnimatorParameters()
    {
        Animator anim = GetActiveAnimator();
        if (anim == null) return;

        ConfigureAnimatorBinding(anim, forceControllerAssignment: false);
        if (anim.runtimeAnimatorController == null) return;

        float maxGroundSpeed = Mathf.Max(0.01f, moveSpeed * Mathf.Max(1f, sprintMultiplier));
        float normalizedSpeed = Mathf.Clamp01(GetActualHorizontalVelocity().magnitude / maxGroundSpeed);

        // ── Locomotion (Float) ──────────────────────────────────────────────
        bool droveSpeedParameter = AnimSetFloat(anim, "Speed", normalizedSpeed, 0.1f);
        if (!droveSpeedParameter)
            ForceLocomotionState(anim, normalizedSpeed);

        // ── Bool params (Player Controller.controller style) ────────────────
        AnimSetBool(anim, "IsAttacking", isAttacking);
        AnimSetBool(anim, "IsGrounded",  isGrounded);
        AnimSetBool(anim, "IsSprinting", isSprinting);

        // ── Trigger params (CrosbyAnimator style) ───────────────────────────
        // Fire Attack trigger on the frame isAttacking becomes true
        if (isAttacking && !_wasAttackingLastFrame)
            AnimFireTrigger(anim, "Attack");
        _wasAttackingLastFrame = isAttacking;
    }
    private bool _wasAttackingLastFrame;

    // ── Direct, timing-safe param helpers ────────────────────────────────────
    // Query animator.parameters every call — no HashSet that can be stale.

    private static bool AnimSetFloat(Animator anim, string name, float value, float damp = 0f)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.name != name || p.type != AnimatorControllerParameterType.Float) continue;
            if (damp > 0f) anim.SetFloat(name, value, damp, Time.deltaTime);
            else           anim.SetFloat(name, value);
            return true;
        }
        return false;
    }

    private static bool AnimSetBool(Animator anim, string name, bool value)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.name != name || p.type != AnimatorControllerParameterType.Bool) continue;
            anim.SetBool(name, value);
            return true;
        }
        return false;
    }

    private static bool AnimFireTrigger(Animator anim, string name)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.name != name || p.type != AnimatorControllerParameterType.Trigger) continue;
            anim.SetTrigger(name);
            return true;
        }
        return false;
    }

    private static void ForceLocomotionState(Animator anim, float normalizedSpeed)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;

        string stateName = normalizedSpeed > 0.05f ? "Walk" : "Idle";
        string fullStateName = $"Base Layer.{stateName}";
        int stateHash = Animator.StringToHash(fullStateName);

        if (!anim.HasState(0, stateHash)) return;

        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        if (currentState.fullPathHash == stateHash) return;

        anim.CrossFadeInFixedTime(fullStateName, 0.1f, 0);
    }

    // Keep old helpers so no compile errors in code that still calls them
    private bool HasAnimatorParameter(int hash)
        => _animParameterHashes != null && _animParameterHashes.Contains(hash);

    private void SetAnimatorFloat(int hash, float value, float dampTime = 0f)
    {
        Animator activeAnim = GetActiveAnimator();
        if (activeAnim == null || !HasAnimatorParameter(hash)) return;
        if (dampTime > 0f) activeAnim.SetFloat(hash, value, dampTime, Time.deltaTime);
        else               activeAnim.SetFloat(hash, value);
    }

    private void SetAnimatorBool(int hash, bool value)
    {
        Animator activeAnim = GetActiveAnimator();
        if (activeAnim == null || !HasAnimatorParameter(hash)) return;
        activeAnim.SetBool(hash, value);
    }

    private void ConfigureAnimatorBinding(Animator targetAnimator, bool forceControllerAssignment)
    {
        if (targetAnimator == null) return;

        targetAnimator.applyRootMotion = false;

        // ── Resolve controller ────────────────────────────────────────────────
        // Priority 1: Inspector-assigned playerAnimatorController
        // Priority 2: Auto-load CrosbyAnimator (same controller used by all enemies)
        RuntimeAnimatorController resolvedCtrl = playerAnimatorController;
        if (resolvedCtrl == null)
            resolvedCtrl = Resources.Load<RuntimeAnimatorController>("Enemy/CrosbyAnimator");

        if (resolvedCtrl != null &&
            (forceControllerAssignment || targetAnimator.runtimeAnimatorController == null))
        {
            targetAnimator.runtimeAnimatorController = resolvedCtrl;
        }

        // ── Resolve avatar ────────────────────────────────────────────────────
        if (playerAvatar != null &&
            (forceControllerAssignment || targetAnimator.avatar == null) &&
            targetAnimator.avatar != playerAvatar)
        {
            targetAnimator.avatar = playerAvatar;
        }

        // ── Final safety check (only warn once, not every frame) ─────────────
        if (targetAnimator.runtimeAnimatorController == null && !_animatorMissingWarned)
        {
            _animatorMissingWarned = true;
            Debug.LogWarning(
                "[PlayerController] No animator controller could be resolved. " +
                "Assign one to 'playerAnimatorController' in the Inspector, or ensure " +
                "Resources/Enemy/CrosbyAnimator exists.",
                targetAnimator);
        }
    }
    private bool _animatorMissingWarned;

    // ════════════════════════════════════════════════════════════════════════
    //  ARENA BOUNDARIES
    // ════════════════════════════════════════════════════════════════════════

    private void ClampInsideArena()
    {
        // Arena boundary disabled — the map is open for the player to explore
        // freely.  The CharacterController, NavMesh floor, and level geometry
        // are sufficient to keep the player in the world without an artificial
        // invisible circular wall.  The hardcoded arenaFloorHeight Y-clamp
        // that was here also mismatched real map geometry and contributed to
        // the player being snapped underground near the boundary.
    }

    private float GetGroundedRootY()
    {
        if (controller == null)
            return arenaFloorHeight + 1f;

        return arenaFloorHeight - controller.center.y + (controller.height * 0.5f);
    }

    private void SnapToArenaFloor()
    {
        if (controller == null) return;
        if (isFlipping) return;

        // Only fix the case where the capsule bottom has sunk BELOW the physics
        // surface — never pull an airborne player downward.  Doing so while
        // isFlipping just became false caused a 112 m/s force-teleport that
        // bypassed CharacterController physics and drove the player underground.
        // Normal falling is handled entirely by gravity in ApplyMovement.
        if (!isGrounded) return;
        // Also skip while the player has upward momentum (jumping / start of flip).
        if (verticalVelocity.y > 0.1f) return;

        int groundMask = BuildGroundCheckMask();
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                             maxFloorSnapDistance + 0.4f, groundMask, QueryTriggerInteraction.Ignore))
            return;

        float capsuleBottomY = transform.position.y + controller.center.y - controller.height * 0.5f;
        float floorY         = hit.point.y;

        // Capsule bottom is already at or above the floor — nothing to do.
        if (capsuleBottomY >= floorY - 0.02f) return;

        // Capsule has sunk below the floor surface: lift it back up smoothly.
        float lift      = floorY - capsuleBottomY;
        float snapSpeed = Mathf.Lerp(floorSnapSpeed, floorSnapSpeed * 4f, Mathf.Clamp01(lift / 0.5f));
        float liftDelta = Mathf.MoveTowards(0f, lift, snapSpeed * Time.deltaTime);

        controller.enabled = false;
        transform.position = new Vector3(transform.position.x,
                                         transform.position.y + liftDelta,
                                         transform.position.z);
        controller.enabled = true;
    }

    /// <summary>
    /// Strict Y-axis ground lock: if the CharacterController has confirmed the
    /// player is grounded, the capsule bottom must sit at exactly
    /// (detected floor Y + 1.0 m above the capsule centre offset) — preventing
    /// any frame where the feet visually sink below the surface.
    ///
    /// This runs AFTER all other snapping so it acts as the final safety net.
    /// Skipped during flips, mantles, and airborne frames so it never fights
    /// intentional vertical motion.
    /// </summary>
    private void EnforceGroundYLock()
    {
        if (controller == null || !controller.enabled) return;
        if (isFlipping || isMantling) return;
        if (!controller.isGrounded) return;
        if (verticalVelocity.y > 0.05f) return; // jumping — don't clamp

        int groundMask = BuildGroundCheckMask();
        Vector3 probeOrigin = transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit,
                             1.5f, groundMask, QueryTriggerInteraction.Ignore))
            return;

        // Target: capsule bottom exactly on the floor surface.
        // capsuleBottomY = transform.position.y + center.y - height*0.5
        // → transform.position.y = floorY - center.y + height*0.5
        float targetY = hit.point.y - controller.center.y + controller.height * 0.5f;

        // Only lift (never push down) — prevents fighting the physics step offset.
        if (transform.position.y < targetY - 0.005f)
        {
            controller.enabled = false;
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            controller.enabled = true;
            if (verticalVelocity.y < 0f) verticalVelocity.y = -2f;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PERSPECTIVE MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════

    private void TogglePerspective()
    {
        if (GameManager.Instance == null)
            return;
        GameManager.PerspectiveMode current = GameManager.Instance.GetPerspectiveMode();
        GameManager.PerspectiveMode next = current == GameManager.PerspectiveMode.FirstPerson
            ? GameManager.PerspectiveMode.ThirdPerson
            : GameManager.PerspectiveMode.FirstPerson;
        GameManager.Instance.SetPerspectiveMode(next);
        RefreshGameplayPreferences();
    }

    public void RefreshGameplayPreferences()
    {
        ApplyPerspectivePreference();
        if (thirdPersonBody != null && isThirdPersonActive)
            thirdPersonBody.SetActive(true);
    }

    private void ApplyPerspectivePreference()
    {
        GameManager.PerspectiveMode mode = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : (GameManager.PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", (int)GameManager.PerspectiveMode.ThirdPerson), 0, 1);

        if (mode == GameManager.PerspectiveMode.FirstPerson)
            EnableFirstPersonView();
        else
            EnableThirdPersonView();
    }

    private void EnableFirstPersonView()
    {
        isThirdPersonActive = false;
        cameraPitch = 0f;

        if (firstPersonCam == null)
            firstPersonCam = GetComponentInChildren<Camera>(true);

        if (firstPersonCam != null)
        {
            // Snap to a stable head height (prevents "inside floor/body" camera on some prefabs).
            float headY = controller != null ? Mathf.Clamp(controller.height - 0.18f, 1.35f, 1.85f) : 1.65f;
            firstPersonLocalPos = new Vector3(firstPersonLocalPos.x, headY, Mathf.Max(firstPersonLocalPos.z, 0.09f));
            firstPersonCam.transform.localPosition = firstPersonLocalPos;
            firstPersonCam.transform.localRotation = firstPersonLocalRot;
            // Keep a safe near clip to avoid seeing inside meshes.
            firstPersonCam.nearClipPlane = 0.1f;
            firstPersonCam.tag = "MainCamera";
            firstPersonCam.gameObject.SetActive(true);

            // If FPSCameraRig is present, let it take over anchor + culling-mask setup.
            FPSCameraRig rig = firstPersonCam.GetComponent<FPSCameraRig>();
            if (rig == null)
                rig = firstPersonCam.gameObject.AddComponent<FPSCameraRig>();
            rig.playerRoot = transform;
            // Forward the head bone if the body is already spawned.
            if (thirdPersonBody != null)
            {
                Transform headBone = FPSCameraRig_FindHeadBone(thirdPersonBody.transform);
                if (headBone != null) rig.SetHeadBone(headBone);
            }
        }

        if (runtimeThirdPersonCamera != null)
        {
            runtimeThirdPersonCamera.tag = "Untagged";
            runtimeThirdPersonCamera.gameObject.SetActive(false);
        }

        // Keep the third-person rig ACTIVE so weapon scripts/coroutines keep working,
        // but hide its renderers so FPS view stays clean.
        if (thirdPersonBody != null)
        {
            thirdPersonBody.SetActive(true);
            SetThirdPersonRenderersVisible(false);
        }

        SetFirstPersonRenderersVisible(true);
        RefreshFirstPersonWeaponModel(equippedWeaponLevel > 0 ? equippedWeaponLevel : (GameManager.Instance != null ? GameManager.Instance.currentLevel : 1));
    }

    private void EnableThirdPersonView()
    {
        isThirdPersonActive = true;
        EnsureThirdPersonCamera();
        EnsureThirdPersonBody();

        if (firstPersonCam != null)
        {
            firstPersonCam.tag = "Untagged";
            firstPersonCam.gameObject.SetActive(false);
        }

        if (runtimeThirdPersonCamera != null)
        {
            runtimeThirdPersonCamera.tag = "MainCamera";
            runtimeThirdPersonCamera.gameObject.SetActive(true);
        }

        SetFirstPersonRenderersVisible(false);
        if (thirdPersonBody != null)
        {
            thirdPersonBody.SetActive(true);
            SetThirdPersonRenderersVisible(true);
        }

        ClearFirstPersonKnifeVisual();
    }

    private void SetThirdPersonRenderersVisible(bool visible)
    {
        if (thirdPersonBody == null) return;
        Renderer[] rs = thirdPersonBody.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] != null)
                rs[i].enabled = visible;
        }
    }

    private void EnsureThirdPersonCamera()
    {
        if (runtimeThirdPersonCamera != null)
        {
            thirdPersonCam = runtimeThirdPersonCamera;
            ApplyThirdPersonCameraSettings(runtimeThirdPersonCamera.GetComponent<CameraController>(), runtimeThirdPersonCamera);
            return;
        }

        CameraController[] existingFollows = Object.FindObjectsByType<CameraController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);

        for (int i = 0; i < existingFollows.Length; i++)
        {
            CameraController existingFollow = existingFollows[i];
            if (existingFollow == null)
                continue;

            runtimeThirdPersonCamera = existingFollow.GetComponent<Camera>();
            if (runtimeThirdPersonCamera != null)
            {
                thirdPersonCam = runtimeThirdPersonCamera;
                existingFollow.target = transform;
                // Must be MainCamera so Camera.main / interaction rays match gameplay.
                runtimeThirdPersonCamera.tag = "MainCamera";
                ApplyThirdPersonCameraSettings(existingFollow, runtimeThirdPersonCamera);
                DestroyDuplicateThirdPersonCameras(existingFollows, i);
                return;
            }
        }

        GameObject camObj = new GameObject(RuntimeThirdPersonCameraName);
        runtimeThirdPersonCamera = camObj.AddComponent<Camera>();

        // Borrow settings from the first-person camera if it still exists,
        // otherwise use sensible defaults (first-person cam may be disabled).
        if (firstPersonCam != null)
        {
            runtimeThirdPersonCamera.fieldOfView     = Mathf.Max(firstPersonCam.fieldOfView, 70f);
            runtimeThirdPersonCamera.nearClipPlane   = firstPersonCam.nearClipPlane;
            runtimeThirdPersonCamera.farClipPlane    = firstPersonCam.farClipPlane;
            runtimeThirdPersonCamera.clearFlags      = firstPersonCam.clearFlags;
            runtimeThirdPersonCamera.backgroundColor = firstPersonCam.backgroundColor;
        }
        else
        {
            runtimeThirdPersonCamera.fieldOfView     = 70f;
            runtimeThirdPersonCamera.nearClipPlane   = 0.1f;
            runtimeThirdPersonCamera.farClipPlane    = 1000f;
            runtimeThirdPersonCamera.clearFlags      = CameraClearFlags.Skybox;
        }
        runtimeThirdPersonCamera.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();

        CameraController follow = camObj.AddComponent<CameraController>();
        if (follow != null)
        {
            ApplyThirdPersonCameraSettings(follow, runtimeThirdPersonCamera);
        }

        thirdPersonCam = runtimeThirdPersonCamera;
    }

    private void ApplyThirdPersonCameraSettings(CameraController follow, Camera cam)
    {
        if (follow == null) return;

        follow.target = transform;
        follow.offset = new Vector3(0.45f, 1.45f, -4.65f);
        follow.smoothSpeed = 60f;
        follow.lookHeight = 1.55f;
        follow.lookTargetLocalOffset = Vector3.zero;
        follow.lookTargetSmoothSpeed = 60f;
        follow.minPitch = ThirdPersonMinPitch;
        follow.maxPitch = ThirdPersonMaxPitch;
        follow.pitch = Mathf.Clamp(cameraPitch, ThirdPersonMinPitch, ThirdPersonMaxPitch);
        follow.enableCollision = true;
        follow.minDistance = 0.35f;
        follow.minHeightAboveGround = 0.55f;
        follow.collisionRadius = 0.2f;
        follow.wallPadding = 0.16f;
        follow.pullInSpeed = 0.035f;
        follow.recoverySmoothTime = 0.06f;
        follow.closeDistanceFailsafe = 0.5f;
        follow.closeSpaceHeightBoost = 0.35f;
        follow.closeSpaceFieldOfView = 76f;
        follow.defaultFieldOfView = 68f;

        if (cam != null)
        {
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.03f;
        }
    }

    private static void DestroyDuplicateThirdPersonCameras(CameraController[] controllers, int keepIndex)
    {
        for (int i = controllers.Length - 1; i >= 0; i--)
        {
            if (i == keepIndex || controllers[i] == null)
                continue;

            GameObject cameraObject = controllers[i].gameObject;
            if (cameraObject != null && cameraObject.name == RuntimeThirdPersonCameraName)
                Destroy(cameraObject);
        }
    }

    private void SetFirstPersonRenderersVisible(bool visible)
    {
        if (firstPersonRenderers == null) return;
        for (int i = 0; i < firstPersonRenderers.Length; i++)
        {
            if (firstPersonRenderers[i] != null)
                firstPersonRenderers[i].enabled = visible;
        }

        if (firstPersonWeaponMeshRenderer != null)
            firstPersonWeaponMeshRenderer.enabled = visible && firstPersonKnifeInstance == null;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  THIRD-PERSON BODY
    // ════════════════════════════════════════════════════════════════════════

    private void EnsureThirdPersonBody()
    {
        if (thirdPersonBody != null) return;

        GameObject roninBodyPrefab = Resources.Load<GameObject>("Player/Ronin/source/Ronin");
        if (roninBodyPrefab != null)
        {
            thirdPersonBody = Instantiate(roninBodyPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            thirdPersonBodyBaseLocalPosition = thirdPersonBody.transform.localPosition;
            thirdPersonBodyBaseLocalRotation = thirdPersonBody.transform.localRotation;
            NormalizeBodyScale(thirdPersonBody, 1.8f);

            Animator roninAnimator = thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (roninAnimator != null)
            {
                ConfigureAnimatorBinding(roninAnimator, forceControllerAssignment: true);
                EnsureAnimationEventSink(roninAnimator.gameObject);

                // ✅ FIX: Update cached animator field immediately after body spawns
                animator = roninAnimator;
            }

            CacheAnimatorParameters();

            // Notify FPSCameraRig (if present) of the new head bone.
            NotifyFPSCameraRigHeadBone(thirdPersonBody.transform);
            return;
        }

        GameObject dragonSoulsBodyPrefab = Resources.Load<GameObject>("Player/DragonSoulsThirdPersonBody");
        if (dragonSoulsBodyPrefab != null)
        {
            thirdPersonBody = Instantiate(dragonSoulsBodyPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.identity;
            thirdPersonBodyBaseLocalPosition = thirdPersonBody.transform.localPosition;
            thirdPersonBodyBaseLocalRotation = thirdPersonBody.transform.localRotation;
            NormalizeBodyScale(thirdPersonBody, 1.8f);

            Animator bodyAnimator = thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (bodyAnimator != null)
            {
                ConfigureAnimatorBinding(bodyAnimator, forceControllerAssignment: true);
                EnsureAnimationEventSink(bodyAnimator.gameObject);

                // ✅ FIX: Update cached animator field immediately after body spawns
                animator = bodyAnimator;
            }

            CacheAnimatorParameters();
            return;
        }

        GameObject knightPrefab = Resources.Load<GameObject>("ThirdPersonKnight/Paladin WProp J Nordstrom");
        if (knightPrefab != null)
        {
            thirdPersonBody = Instantiate(knightPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.identity;
            thirdPersonBodyBaseLocalPosition = thirdPersonBody.transform.localPosition;
            thirdPersonBodyBaseLocalRotation = thirdPersonBody.transform.localRotation;
            thirdPersonBody.transform.localScale    = new Vector3(0.92f, 0.92f, 0.92f);

            HideKnightWeaponProp(thirdPersonBody);

            Animator importedAnimator = thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (importedAnimator != null)
            {
                ConfigureAnimatorBinding(importedAnimator, forceControllerAssignment: true);
                EnsureAnimationEventSink(importedAnimator.gameObject);

                // ✅ FIX: Update cached animator field immediately after body spawns
                animator = importedAnimator;
            }

            CacheAnimatorParameters();
            return;
        }

        thirdPersonBody = new GameObject("ThirdPersonBody");
        thirdPersonBody.transform.SetParent(transform, false);
        thirdPersonBody.transform.localPosition = Vector3.zero;
        thirdPersonBodyBaseLocalPosition = thirdPersonBody.transform.localPosition;
        thirdPersonBodyBaseLocalRotation = thirdPersonBody.transform.localRotation;
    }

    private void ApplySkinMaterial(GameObject body)
    {
        Material skinMat = Resources.Load<Material>("Player/SkinMaterial");

        if (skinMat == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Standard");
            skinMat = new Material(lit);
            skinMat.color = new Color(0.86f, 0.76f, 0.66f);
            skinMat.SetFloat("_Smoothness", 0.3f);
        }

        foreach (SkinnedMeshRenderer smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] mats = smr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = skinMat;
            smr.sharedMaterials = mats;
        }

        foreach (MeshRenderer mr in body.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = skinMat;
            mr.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// Loads "RoninTexture" from Resources/Textures and applies it to any blank/
    /// missing material slot on the character body so the player is never a
    /// "white statue".  Slots that already have a real texture are left alone.
    /// Place your texture at:  Assets/_Shared/Imports/Resources/Player/Ronin/textures/RoninTexture.png
    /// </summary>
    private static void EnsureProperMaterial(GameObject body)
    {
        if (body == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Diffuse");
        if (shader == null) return;

        // ── Step 1: Try to load existing .mat files from Ronin source ────────
        Material prebuiltMat = LoadFirstAvailableMaterial(
            "Player/Ronin/source/mp_ronin_torso",
            "Player/Ronin/source/body_mp_western_ronin_4_1_lod1",
            "Player/Ronin/source/mp_western_ronin_arm_r");

        // ── Step 2: Build per-slot replacement ───────────────────────────────
        // For each blank material slot, try to find a matching texture by
        // the material's own name, then fall back to a generic body texture.
        string[] roninTextureFolders = {
            "Player/Ronin/textures/",
            "Player/Ronin/source/"
        };

        // Generic fallback texture (torso — most representative body part)
        Texture2D fallbackTex = LoadFirstAvailableTexture(
            "Player/Ronin/textures/body_mp_western_ronin_4_1_lod1_c.tga",
            "Player/Ronin/textures/body_mp_western_ronin_4_1_lod1_c",
            "Player/Ronin/textures/mp_ronin_torso_c.tga",
            "Player/Ronin/textures/mp_ronin_torso_c",
            "Player/Ronin/textures/mp_western_ronin_arm_r_c.tga",
            "Player/Ronin/textures/mp_western_ronin_arm_r_c",
            "Textures/RoninTexture");

        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!IsBlankMaterial(slots[i])) continue;

                // Try to find a per-slot texture matching this material's name
                string slotName = (slots[i] != null) ? slots[i].name : "";
                Texture2D slotTex = TryLoadTextureForMaterial(slotName, roninTextureFolders);

                if (slotTex == null) slotTex = fallbackTex;

                // If we have a prebuilt .mat and no specific texture, use the prebuilt
                if (slotTex == null && prebuiltMat != null)
                {
                    slots[i] = prebuiltMat;
                    changed = true;
                    continue;
                }

                // Create a new URP material with the texture
                Material newMat = new Material(shader)
                {
                    name = string.IsNullOrEmpty(slotName) ? "PlayerMat_Runtime" : slotName + "_Fix"
                };
                if (slotTex != null)
                {
                    if (newMat.HasProperty("_BaseMap")) newMat.SetTexture("_BaseMap", slotTex);
                    if (newMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", slotTex);
                }
                else
                {
                    // No texture at all — warm skin tone so at least it's not white
                    Color tan = new Color(0.72f, 0.58f, 0.44f);
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
    /// Given a material's name (e.g. "mp_ronin_torso"), tries to find a
    /// matching color texture in known folders (e.g. "mp_ronin_torso_c.tga").
    /// </summary>
    private static Texture2D TryLoadTextureForMaterial(string matName, string[] folders)
    {
        if (string.IsNullOrEmpty(matName)) return null;

        // Strip common Unity suffixes
        string clean = matName.Replace(" (Instance)", "").Replace("_Fix", "").Trim();
        if (string.IsNullOrEmpty(clean)) return null;

        // Try "{folder}{name}_c.tga", "{folder}{name}_c", "{folder}{name}"
        foreach (string folder in folders)
        {
            Texture2D tex;
            tex = Resources.Load<Texture2D>(folder + clean + "_c.tga");  if (tex != null) return tex;
            tex = Resources.Load<Texture2D>(folder + clean + "_c");       if (tex != null) return tex;
            tex = Resources.Load<Texture2D>(folder + clean);              if (tex != null) return tex;
        }
        return null;
    }

    private static Material LoadFirstAvailableMaterial(params string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            Material m = Resources.Load<Material>(paths[i]);
            if (m != null) return m;
        }
        return null;
    }

    private static bool IsBlankMaterial(Material m)
    {
        if (m == null) return true;
        if (m.name.StartsWith("Default-")) return true;
        // Check if shader is missing/pink (URP project with Standard-only mat)
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

    private static void NormalizeBodyScale(GameObject body, float targetHeight)
    {
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any || b.size.y < 0.01f) return;
        float scale = targetHeight / b.size.y;
        body.transform.localScale = Vector3.one * scale;
    }

    private void EnsureAnimationEventSink(GameObject root)
    {
        if (root == null) return;

        if (root.GetComponent<AnimationEventSink>() == null)
            root.AddComponent<AnimationEventSink>();

        if (root.GetComponent<MeleeAnimationEventSink>() == null)
            root.AddComponent<MeleeAnimationEventSink>();
    }

    private void AssignMaterial()
    {
        // Try explicit paths first, then search all Materials in Resources as a fallback.
        Material material = Resources.Load<Material>("Materials/Ronin");
        if (material == null)
            material = Resources.Load<Material>("Materials/Enemy");
        if (material == null)
        {
            // Broad fallback: find any material whose name contains "Ronin" or "Enemy"
            foreach (Material m in Resources.LoadAll<Material>("Materials"))
            {
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                if (n.Contains("ronin") || n.Contains("enemy") || n.Contains("player"))
                {
                    material = m;
                    break;
                }
            }
        }

        if (material == null)
        {
            // Last resort: create a visible default so the character isn't white
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = new Color(0.6f, 0.45f, 0.35f); // warm skin tone
        }

        // Ensure AnimationEventSink is present to prevent CS0246 event-method errors
        if (thirdPersonBody != null)
            EnsureAnimationEventSink(thirdPersonBody);

        // Solid jet-black operator silhouette — strong contrast against the
        // varied enemy roster, reads clean from every angle, and matches the
        // dark-side aesthetic the user requested. The minimap arrow stays
        // bright green so player identity is still obvious on the map even
        // though the character itself is black.
        Color playerStandoutColor = new Color(0.04f, 0.04f, 0.05f, 1f);
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null) continue;
            Material instance = new Material(material);
            ApplyReadableTint(instance, playerStandoutColor, 0.92f);
            smr.material = instance;
        }
    }

    private static void ApplyReadableTint(Material mat, Color tint, float strength)
    {
        if (mat == null) return;

        Color baseColor = Color.white;
        if (mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");
        else if (mat.HasProperty("_Color")) baseColor = mat.GetColor("_Color");

        Color mixed = Color.Lerp(baseColor, tint, Mathf.Clamp01(strength));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mixed);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", mixed);
    }

    private void HideKnightWeaponProp(GameObject knight)
    {
        Transform prop = FindBoneContaining(knight.transform, "Weapon");
        if (prop != null) prop.gameObject.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WEAPON SYSTEM
    // ════════════════════════════════════════════════════════════════════════

    public void EquipWeaponForLevel(int level)
    {
        int gameplayLevel = Mathf.Max(1, level);
        if (GameManager.Instance != null)
            gameplayLevel = Mathf.Clamp(gameplayLevel, 1, GameManager.TotalLevels);

        // Store buffs apply only when the equipped store weapon matches this
        // campaign level. Otherwise we always use the level's mesh + base
        // stats (fixes every level showing e.g. Baseball Bat after buying it).
        WeaponDefinition storeEquipped = null;
        if (SessionManager.Instance != null)
            storeEquipped = SessionManager.Instance.EquippedWeapon;
        bool storeBuffsThisLevel = storeEquipped != null && storeEquipped.Id != "default"
            && storeEquipped.LevelIndex == gameplayLevel;

        if (GameManager.Instance != null)
        {
            equippedWeaponName = GameManager.Instance.GetWeaponNameForLevel(gameplayLevel);
            attackDamage       = Mathf.RoundToInt(GameManager.Instance.GetWeaponDamageForLevel(gameplayLevel));
            attackDistance     = GameManager.Instance.GetWeaponRangeForLevel(gameplayLevel);
            attackSpeed = 1.0f; attackDelay = 0.4f; attackRadius = 1.25f;
        }
        else
        {
            equippedWeaponName = "Combat Knife";
        }

        if (storeBuffsThisLevel)
        {
            equippedWeaponName = storeEquipped.Name;
            attackDamage       = Mathf.Max(1, Mathf.RoundToInt(attackDamage * storeEquipped.DamageMul));
            attackSpeed       *= storeEquipped.AttackSpeedMul;
            attackDelay        = Mathf.Clamp(attackDelay / storeEquipped.AttackSpeedMul, 0.12f, 1.0f);
        }

        if (thirdPersonBody != null)
        {
            if (isThirdPersonActive)
                thirdPersonBody.SetActive(true);

            if (equippedWeaponObject == null || equippedWeaponLevel != gameplayLevel)
                AttachWeaponToHand(thirdPersonBody, gameplayLevel);
            SetupWeaponIK();
        }

        // Tint the freshly-attached weapon with whatever skin the player has
        // equipped in the PRISM Store. Idempotent — re-applying with the same
        // colour is a no-op.
        ApplyEquippedKatanaSkin();

        WeaponDefinition liveAnimDef = storeBuffsThisLevel
            ? storeEquipped
            : (SessionManager.Instance != null
                ? SessionManager.Instance.FindWeaponForCampaignLevel(gameplayLevel)
                : null);
        ApplyWeaponLiveAnimation(liveAnimDef);

        // Keep the FPS viewmodel in sync when the player swaps weapons.
        if (!isThirdPersonActive)
            RefreshFirstPersonWeaponModel(gameplayLevel);
    }

    /// <summary>
    /// Attaches a <see cref="WeaponLiveAnimator"/> to the equipped weapon so it
    /// animates in sync with attacks. Different store weapons get different
    /// presets — Nunchucks chain-spin, Hammer gets a heavy windup, etc.
    /// </summary>
    private void ApplyWeaponLiveAnimation(WeaponDefinition def)
    {
        if (equippedWeaponObject == null) return;
        WeaponLiveAnimator live = equippedWeaponObject.GetComponent<WeaponLiveAnimator>();
        if (live == null) live = equippedWeaponObject.AddComponent<WeaponLiveAnimator>();
        live.Configure(def);
    }

    /// <summary>
    /// Re-tints every renderer on the equipped weapon object with the colour
    /// of the PRISM-store skin currently equipped on the player profile.
    /// </summary>
    public void ApplyEquippedKatanaSkin()
    {
        if (equippedWeaponObject == null) return;
        if (SessionManager.Instance == null) return;

        Color tint = SessionManager.Instance.EquippedSkinColor;
        Renderer[] renderers = equippedWeaponObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r.sharedMaterial == null) continue;
            // Use the per-renderer instance so we don't bleed the colour into
            // every weapon ever spawned from the same prefab.
            Material instance = new Material(r.sharedMaterial);
            ApplyReadableTint(instance, tint, 0.85f);
            r.material = instance;
        }
    }

    private void SetupWeaponIK()
    {
        if (thirdPersonBody == null) return;

        WeaponIKHandler ikHandler = thirdPersonBody.GetComponent<WeaponIKHandler>();
        if (ikHandler == null)
            ikHandler = thirdPersonBody.AddComponent<WeaponIKHandler>();

        ikHandler.DisableIK();
    }

    // ── Hand bone priority list — melee grip sockets first, firearm tag last ─
    private static readonly string[] PlayerHandBoneNames =
    {
        "Hand.R",
        "Wrist.R",
        "Palm.R",
        "tag_accessory_right",  // Ronin fallback socket on right wrist
        "j_wrist_ri",           // Ronin wrist bone
        "weapon_bone_R",        // Crosby weapon socket
        "bip_hand_R",           // Crosby rig (enemies use same bones as player on some levels)
        "tag_weapon_right",     // Ronin firearm socket (kept as last-resort fallback)
        "mixamorig:RightHand",  // Mixamo
        "RightHand",
        "Hand_R", "hand_R", "hand_r",
        "Wrist_R", "wrist_R",
    };

    private static Transform FindPlayerHandBone(GameObject body)
    {
        // ── Priority 1: explicit weapon/hand sockets authored on the rig ────
        foreach (string boneName in PlayerHandBoneNames)
        {
            Transform found = FindBoneExact(body.transform, boneName);
            if (found != null)
            {
                Debug.Log($"[PlayerController] Hand bone found via name search: '{found.name}'");
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
                Debug.Log($"[PlayerController] Hand bone found via Animator API: '{bone.name}'");
                return bone;
            }
        }

        return null;
    }

    private static Transform FindBoneExact(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneExact(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindPlayerChainsawAttachBone(GameObject body)
    {
        if (body == null) return null;

        // Level 12 should attach to the animated wrist itself, not to any
        // firearm/body carry tags, so the rear handle stays in the palm.
        Transform accessoryBone = FindBoneExact(body.transform, "tag_accessory_right");
        if (accessoryBone != null)
            return accessoryBone;

        Transform wristBone = FindBoneExact(body.transform, "j_wrist_ri");
        if (wristBone != null)
            return wristBone;

        Animator anim = body.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
            return anim.GetBoneTransform(HumanBodyBones.RightHand);

        return FindPlayerHandBone(body);
    }

    private void AttachWeaponToHand(GameObject body, int weaponLevel = -1)
    {
        if (body == null) return;
        if (weaponAttachInProgress) return;

        int level = weaponLevel >= 1
            ? weaponLevel
            : (GameManager.Instance != null ? GameManager.Instance.currentLevel : 1);
        if (equippedWeaponObject != null && equippedWeaponLevel == level)
            return;

        weaponAttachInProgress = true;
        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
            equippedWeaponObject = null;
            equippedWeaponLevel = -1;
        }
        equippedWeaponHitbox = null;

        WeaponLoadout loadout = WeaponLoadoutCatalog.Get(level);

        // ── 1. Load prefab with guaranteed fallback chain ───────────────────
        float finalTargetSize;
        GameObject prefab = WeaponLoadoutCatalog.LoadPrefabWithFallback(level, out finalTargetSize);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerController] All weapon sources exhausted for level {level}, using primitive.");
            Transform fallbackBone = ResolveHandBone(body, level);
            Transform fallbackSocket = GetOrCreateWeaponSocket(fallbackBone);
            equippedWeaponObject = BuildPrimitiveWeapon(level, fallbackSocket, loadout);
            equippedWeaponLevel = level;
            equippedWeaponHitbox = equippedWeaponObject != null ? equippedWeaponObject.GetComponent<WeaponHitbox>() : null;
            weaponAttachInProgress = false;
            return;
        }

        // ── 2. Bone verification — mirrors EnemyController exactly ──────────
        // Priority 1: known rig bone names
        // Priority 2: Humanoid avatar API (the #1 floating-weapon fix)
        Transform handBone = ResolveHandBone(body, level);
        Debug.Log($"[PlayerController] Hand bone resolved: '{handBone.name}' (level={level})");

        // ── 3. Attach parent: katana / level-2 → RightHand bone directly (palm alignment).
        //     Other weapons keep the scale-normalising socket under the hand bone.
        bool katanaStyle = level == 2
            || (prefab != null && prefab.name.ToLowerInvariant().Contains("katana"));
        Transform weaponParent = GetOrCreateWeaponSocket(handBone);
        if (katanaStyle)
        {
            Animator anim = body.GetComponentInChildren<Animator>(true);
            if (anim != null && anim.isHuman)
            {
                Transform rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (rh != null) weaponParent = rh;
            }
        }

        // ── 4. Instantiate unparented to measure clean world-space bounds ────
        GameObject weapon = Instantiate(prefab);
        weapon.name = "WeaponModel";
        weapon.SetActive(true);
        SetLayerRecursive(weapon, gameObject.layer);

        foreach (Transform t in weapon.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);

        // ── 5. Measure bounds at unit scale ─────────────────────────────────
        weapon.transform.localScale = Vector3.one;
        float weaponExtent = GetMaxRendererExtent(weapon);
        if (weaponExtent < 0.001f) weaponExtent = 1f;

        // ── 6. Parent to hand / socket — MIRROR ENEMY LOGIC ─────────────────
        weapon.transform.SetParent(weaponParent, worldPositionStays: false);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale    = Vector3.one;

        // ── 7. Scale to target world size ────────────────────────────────────
        float uniformScale = finalTargetSize / weaponExtent;
        Vector3 parentLossy = weaponParent.lossyScale;
        weapon.transform.localScale = new Vector3(
            uniformScale / Mathf.Max(Mathf.Abs(parentLossy.x), 0.0001f),
            uniformScale / Mathf.Max(Mathf.Abs(parentLossy.y), 0.0001f),
            uniformScale / Mathf.Max(Mathf.Abs(parentLossy.z), 0.0001f));

        // ── 8. Grip pose ─────────────────────────────────────────────────────
        if (prefab != null)
        {
            string n = prefab.name.ToLowerInvariant();
            if (n.Contains("sickle"))
            {
                weapon.transform.localPosition = DefaultLevel13SickleGripLocalPosition;
                weapon.transform.localRotation = Quaternion.Euler(DefaultLevel13SickleGripLocalEuler);
                ForceWeaponRenderable(weapon);
                ApplySickleHandPose(handBone, weapon.transform);
            }
            else if (ShouldUsePlayerChainsawGrip(level, prefab))
            {
                ApplyPlayerChainsawGrip(weapon.transform);
                ForceWeaponRenderable(weapon);
            }
            else
            {
                if (!WeaponLoadoutCatalog.ApplyPlayerRuntimeGripPose(level, prefab, weapon.transform))
                    ApplyWeaponGripPose(weapon.transform, loadout.PlayerLocalPosition, loadout.PlayerLocalEuler);
            }
        }
        else
        {
            ApplyWeaponGripPose(weapon.transform, loadout.PlayerLocalPosition, loadout.PlayerLocalEuler);
        }

        WeaponLoadoutCatalog.ApplyRuntimeOverrides(level, prefab, weapon);
        ApplyKatanaGripFix(level, prefab, weapon.transform);

        Debug.Log($"[PlayerController] Weapon '{weapon.name}' → '{weaponParent.name}' " +
                  $"targetSize={finalTargetSize} extent={weaponExtent} " +
                  $"localPosition={weapon.transform.localPosition} " +
                  $"localEuler={weapon.transform.localEulerAngles} " +
                  $"localScale={weapon.transform.localScale} lossyScale={weapon.transform.lossyScale}");

        // ── 9. Disable physics, embedded animators, colliders ────────────────
        foreach (Animator weaponAnimator in weapon.GetComponentsInChildren<Animator>(true))
            weaponAnimator.enabled = false;

        foreach (Rigidbody rb in weapon.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        foreach (Collider col in weapon.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // ── 10. Wire WeaponBase ──────────────────────────────────────────────
        WeaponBase wb = weapon.GetComponent<WeaponBase>();
        if (wb == null) wb = weapon.AddComponent<WeaponBase>();
        wb.weaponName  = equippedWeaponName;
        wb.damage      = attackDamage;
        wb.attackRange = attackDistance;
        wb.isRanged    = false;

        // ── 11. Wire WeaponHitbox ────────────────────────────────────────────
        WeaponHitbox hitbox = weapon.GetComponent<WeaponHitbox>();
        if (hitbox == null) hitbox = weapon.AddComponent<WeaponHitbox>();
        hitbox.damage = attackDamage;
        hitbox.DisableHitbox();
        equippedWeaponHitbox = hitbox;

        // ── 12. Visibility fix (URP) ─────────────────────────────────────────
        if (weapon.GetComponent<WeaponVisibilityFix>() == null)
            weapon.AddComponent<WeaponVisibilityFix>();

        MeleeWeaponWallPullback wallPull = weapon.GetComponent<MeleeWeaponWallPullback>();
        if (wallPull == null) wallPull = weapon.AddComponent<MeleeWeaponWallPullback>();
        wallPull.Configure(transform, staticObstacleMask);

        // ── 13. Refresh melee animation event sink cache ─────────────────────
        if (thirdPersonBody != null)
        {
            MeleeAnimationEventSink sink = thirdPersonBody.GetComponentInChildren<MeleeAnimationEventSink>(true);
            if (sink != null) sink.ClearCache();
        }

        weapon.SetActive(true);
        equippedWeaponObject = weapon;
        equippedWeaponLevel = level;
        weaponAttachInProgress = false;
    }

    private static bool ShouldUsePlayerChainsawGrip(int level, GameObject prefab)
    {
        if (level != 12)
            return false;

        if (prefab != null)
        {
            string prefabName = prefab.name.ToLowerInvariant();
            if (prefabName.Contains("chainsaw") || prefabName.Contains("chain") || prefabName.Contains("saw"))
                return true;
        }

        return WeaponLoadoutCatalog.IsChainsawLevel(level, prefab);
    }

    private static void ApplyPlayerChainsawGrip(Transform weaponRoot)
    {
        if (weaponRoot == null)
            return;

        weaponRoot.localPosition = PlayerChainsawGripLocalPosition;
        weaponRoot.localRotation = Quaternion.Euler(PlayerChainsawGripLocalEuler);
    }

    private static void ApplyKatanaGripFix(int level, GameObject prefab, Transform weaponRoot)
    {
        if (weaponRoot == null) return;
        string prefabName = prefab != null ? prefab.name.ToLowerInvariant() : string.Empty;
        if (level != 2 && !prefabName.Contains("katana"))
            return;

        // Pin the hilt into the palm with a wrist-aligned rotation so the
        // blade extends forward of the fingertips instead of riding the
        // forearm.
        weaponRoot.localPosition = PlayerKatanaGripLocalPosition;
        weaponRoot.localRotation = Quaternion.Euler(PlayerKatanaGripLocalEuler);
    }

    private static int ResolveHittableMask()
    {
        int hittable = LayerMask.NameToLayer("Hittable");
        if (hittable >= 0) return 1 << hittable;

        int character = LayerMask.NameToLayer("Character");
        return character >= 0 ? 1 << character : ~0;
    }

    // Resolves the player right-hand bone using explicit names first, then the
    // Humanoid avatar API as the guaranteed fallback (fixes floating weapons).
    private static Transform ResolveHandBone(GameObject body, int weaponLevel = -1)
    {
        if (body == null) return null;

        if (weaponLevel == 13)
        {
            string[] meleeNames =
            {
                "Hand.R",
                "Wrist.R",
                "Palm.R",
                "j_wrist_ri",
                "weapon_bone_R",
                "bip_hand_R",
                "RightHand",
                "Hand_R",
                "hand_R",
                "hand_r",
                "Wrist_R",
                "wrist_R",
                "tag_accessory_right",
            };

            foreach (string n in meleeNames)
            {
                Transform found = FindBoneExact(body.transform, n);
                if (found != null)
                {
                    Debug.Log($"[PlayerController] ResolveHandBone: found '{found.name}' by Level 13 melee priority");
                    return found;
                }
            }

            Animator meleeAnim = body.GetComponentInChildren<Animator>(true);
            if (meleeAnim != null && meleeAnim.isHuman)
            {
                Transform meleeBone = meleeAnim.GetBoneTransform(HumanBodyBones.RightHand);
                if (meleeBone != null)
                {
                    Debug.Log($"[PlayerController] ResolveHandBone: found '{meleeBone.name}' via HumanBodyBones for Level 13");
                    return meleeBone;
                }
            }
        }

        // Explicit bone name search — same priority list as FindPlayerHandBone
        string[] names = {
            "Hand.R", "Wrist.R", "Palm.R",
            "RightHand", "Hand_R",
            "tag_accessory_right", "j_wrist_ri", "weapon_bone_R",
            "bip_hand_R", "tag_weapon_right",
            "mixamorig:RightHand",
            "hand_R", "hand_r", "Wrist_R", "wrist_R",
        };
        foreach (string n in names)
        {
            Transform found = FindBoneExact(body.transform, n);
            if (found != null)
            {
                Debug.Log($"[PlayerController] ResolveHandBone: found '{found.name}' by name");
                return found;
            }
        }

        // Humanoid avatar API fallback — most reliable for any standard rig
        Animator anim = body.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            Transform bone = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null)
            {
                Debug.Log($"[PlayerController] ResolveHandBone: found '{bone.name}' via HumanBodyBones");
                return bone;
            }
        }

        Debug.LogWarning("[PlayerController] ResolveHandBone: no hand bone found, falling back to body root.");
        return body.transform;
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

    private static Transform GetOrCreateChainsawSocket(Transform handBone)
    {
        Transform socket = handBone.Find(ChainsawSocketName);
        if (socket == null)
        {
            GameObject socketObject = new GameObject(ChainsawSocketName);
            socket = socketObject.transform;
            socket.SetParent(handBone, worldPositionStays: false);
        }

        socket.localPosition = ChainsawSocketLocalPosition;
        socket.localRotation = Quaternion.Euler(ChainsawSocketLocalEuler);

        Vector3 handLossy = handBone.lossyScale;
        socket.localScale = new Vector3(
            1f / Mathf.Max(Mathf.Abs(handLossy.x), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(handLossy.y), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(handLossy.z), 0.0001f));

        return socket;
    }

    private static void ApplyWeaponGripPose(Transform weaponRoot, Vector3 localPosition, Vector3 localEuler)
    {
        if (weaponRoot == null) return;
        weaponRoot.localPosition = localPosition;
        weaponRoot.localRotation = Quaternion.Euler(localEuler);
    }

    private static void ForceWeaponRenderable(GameObject weapon)
    {
        if (weapon == null) return;

        weapon.SetActive(true);
        foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
            child.gameObject.SetActive(true);

        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
        }
    }

    private static void ApplySickleHandPose(Transform handBone, Transform weaponRoot)
    {
        if (handBone == null || weaponRoot == null)
            return;

        SickleGripPoseDriver driver = handBone.GetComponent<SickleGripPoseDriver>();
        if (driver == null)
            driver = handBone.gameObject.AddComponent<SickleGripPoseDriver>();

        driver.Configure(handBone, weaponRoot);
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

    private Transform FindBone(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBone(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    private Transform FindBoneContaining(Transform root, string substring)
    {
        if (root.name.ToLowerInvariant().Contains(substring.ToLowerInvariant())) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneContaining(child, substring);
            if (found != null) return found;
        }
        return null;
    }

    private static void StripWeaponArmature(GameObject weapon)
    {
        string[] poisonKeywords = { "_ARM", "_Arm", "_arm", "Armature", "armature", "_Rig", "_rig" };
        var toDestroy = new List<GameObject>();

        foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
        {
            if (child == weapon.transform) continue;
            foreach (string keyword in poisonKeywords)
            {
                if (child.name.Contains(keyword))
                {
                    toDestroy.Add(child.gameObject);
                    break;
                }
            }
        }

        foreach (GameObject obj in toDestroy)
        {
            if (obj != null && obj != weapon)
                Object.DestroyImmediate(obj);
        }
    }

    private GameObject BuildWeaponModel(int level, Transform attachPoint, WeaponLoadout loadout)
    {
        GameObject levelPrefab = loadout.LoadPrefab();
        if (levelPrefab != null)
        {
            Transform weaponSocket = attachPoint != null && attachPoint.name == WeaponSocketName
                ? attachPoint
                : GetOrCreateWeaponSocket(attachPoint);
            GameObject weapon = Instantiate(levelPrefab);
            weapon.name = "WeaponModel";
            SetLayerRecursive(weapon, gameObject.layer);

            weapon.SetActive(true);
            foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);

            StripWeaponArmature(weapon);
            EquipmentManager.ApplyAutoScale(weapon, loadout.TargetSize);
            Vector3 desiredLossyScale = weapon.transform.lossyScale;

            weapon.transform.SetParent(weaponSocket, worldPositionStays: false);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            ApplyDesiredLossyScale(weapon.transform, desiredLossyScale);
            if (ShouldUsePlayerChainsawGrip(level, levelPrefab))
                ApplyPlayerChainsawGrip(weapon.transform);
            else if (!WeaponLoadoutCatalog.ApplyPlayerRuntimeGripPose(level, levelPrefab, weapon.transform))
                ApplyWeaponGripPose(weapon.transform, loadout.PlayerLocalPosition, loadout.PlayerLocalEuler);
            WeaponLoadoutCatalog.ApplyRuntimeOverrides(
                GameManager.Instance != null ? GameManager.Instance.currentLevel : 1,
                levelPrefab,
                weapon);

            // Safety: if scale ended up near-zero, force a visible fallback
            Vector3 lossyFinal = weapon.transform.lossyScale;
            float minAxis = Mathf.Min(Mathf.Abs(lossyFinal.x),
                            Mathf.Min(Mathf.Abs(lossyFinal.y), Mathf.Abs(lossyFinal.z)));
            if (minAxis < 0.005f)
            {
                weapon.transform.localScale = Vector3.one * 0.1f;
                Debug.LogWarning($"[PlayerController] Weapon near-zero scale — forced to (0.1,0.1,0.1).");
            }

            Debug.Log($"[PlayerController] Weapon '{weapon.name}' → hand '{weaponSocket.name}' " +
                      $"targetSize={loadout.TargetSize} localScale={weapon.transform.localScale} " +
                      $"lossyScale={weapon.transform.lossyScale}");

            WeaponBase weaponBase = weapon.GetComponent<WeaponBase>();
            if (weaponBase == null)
                weaponBase = weapon.AddComponent<WeaponBase>();

            weaponBase.weaponName  = equippedWeaponName;
            weaponBase.damage      = attackDamage;
            weaponBase.attackRange = attackDistance;
            weaponBase.isRanged    = false;

            if (weapon.GetComponent<WeaponVisibilityFix>() == null)
                weapon.AddComponent<WeaponVisibilityFix>();

            MeleeWeaponWallPullback wallPullBk = weapon.GetComponent<MeleeWeaponWallPullback>();
            if (wallPullBk == null) wallPullBk = weapon.AddComponent<MeleeWeaponWallPullback>();
            wallPullBk.Configure(transform, staticObstacleMask);

            WeaponHitbox hitbox = weapon.GetComponent<WeaponHitbox>();
            if (hitbox == null)
                hitbox = weapon.AddComponent<WeaponHitbox>();
            hitbox.damage = attackDamage;

            if (thirdPersonBody != null)
            {
                MeleeAnimationEventSink sink = thirdPersonBody.GetComponentInChildren<MeleeAnimationEventSink>(true);
                if (sink != null) sink.ClearCache();
            }

            equippedWeaponObject = weapon;
            return weapon;
        }

        return BuildPrimitiveWeapon(level, attachPoint, loadout);
    }

    private void CacheFirstPersonWeaponSlot()
    {
        firstPersonWeaponSlot = null;
        firstPersonWeaponMeshRenderer = null;
        if (firstPersonCam == null) return;

        // Search for an existing "Weapon" child under the FPS camera.
        Transform[] trs = firstPersonCam.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i].name != "Weapon") continue;
            firstPersonWeaponSlot = trs[i];
            firstPersonWeaponMeshRenderer = firstPersonWeaponSlot.GetComponent<MeshRenderer>();
            return;
        }

        // No "Weapon" slot found — create one directly under the camera so
        // RefreshFirstPersonWeaponModel never silently exits with a null slot.
        GameObject slotGo = new GameObject("Weapon");
        slotGo.transform.SetParent(firstPersonCam.transform, false);
        slotGo.transform.localPosition = Vector3.zero;
        slotGo.transform.localRotation = Quaternion.identity;
        firstPersonWeaponSlot = slotGo.transform;
    }

    private void ClearFirstPersonKnifeVisual()
    {
        if (firstPersonKnifeInstance != null)
        {
            Destroy(firstPersonKnifeInstance);
            firstPersonKnifeInstance = null;
        }
        if (firstPersonWeaponMeshRenderer != null)
            firstPersonWeaponMeshRenderer.enabled = true;
    }

    private void RefreshFirstPersonWeaponModel(int level)
    {
        if (firstPersonCam == null)
            firstPersonCam = GetComponentInChildren<Camera>(true);
        if (firstPersonCam == null)
            return;

        if (firstPersonWeaponSlot == null)
            CacheFirstPersonWeaponSlot();
        if (firstPersonWeaponSlot == null)
            return;

        ClearFirstPersonKnifeVisual();

        // Spawn a lightweight "viewmodel" weapon under the camera for COD-style FPS feel.
        float _;
        GameObject prefab = WeaponLoadoutCatalog.LoadPrefabWithFallback(Mathf.Clamp(level, 1, 16), out _);
        if (prefab == null)
            return;

        GameObject vm = Instantiate(prefab, firstPersonWeaponSlot, false);
        vm.name = "FP_WeaponModel";
        SetLayerRecursive(vm, gameObject.layer);

        foreach (Animator a in vm.GetComponentsInChildren<Animator>(true))
            a.enabled = false;
        foreach (Rigidbody rb in vm.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        foreach (Collider c in vm.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        // ── Weapon viewmodel offset rules (IMG_6923.mov fix) ────────────────
        // Mandatory offsets keep the hilt close to the visible right hand
        // instead of floating in the center of the FPV.
        vm.transform.localPosition = new Vector3(0.42f, -0.30f, 0.82f);
        vm.transform.localRotation = Quaternion.Euler(5f, 100f, -18f);
        vm.transform.localScale    = Vector3.one * 0.12f;

        // Per-weapon scale tuning so long blades don't fill the whole screen.
        if (level == 2) // Katana — long blade, needs a tighter scale
        {
            vm.transform.localPosition = new Vector3(0.44f, -0.32f, 0.92f);
            vm.transform.localRotation = Quaternion.Euler(7f, 96f, -20f);
            vm.transform.localScale = Vector3.one * 0.095f;
        }

        // Hide the placeholder mesh renderer if the camera has one.
        if (firstPersonWeaponMeshRenderer != null)
            firstPersonWeaponMeshRenderer.enabled = false;

        firstPersonKnifeInstance = vm;
    }

    private static void ApplyDesiredLossyScale(Transform target, Vector3 desiredLossyScale)
    {
        if (target == null) return;

        Vector3 parentLossyScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(
            desiredLossyScale.x / Mathf.Max(Mathf.Abs(parentLossyScale.x), 0.0001f),
            desiredLossyScale.y / Mathf.Max(Mathf.Abs(parentLossyScale.y), 0.0001f),
            desiredLossyScale.z / Mathf.Max(Mathf.Abs(parentLossyScale.z), 0.0001f));
    }

    private void NotifyFPSCameraRigHeadBone(Transform bodyRoot)
    {
        if (firstPersonCam == null) return;
        FPSCameraRig rig = firstPersonCam.GetComponent<FPSCameraRig>();
        if (rig == null) return;
        Transform headBone = FPSCameraRig_FindHeadBone(bodyRoot);
        if (headBone != null) rig.SetHeadBone(headBone);
    }

    // ── FPSCameraRig integration helpers ─────────────────────────────────────

    /// <summary>
    /// Searches the body hierarchy for a bone that looks like a head bone.
    /// Used to automatically anchor the FPS camera in front of the face.
    /// </summary>
    private static Transform FPSCameraRig_FindHeadBone(Transform root)
    {
        string[] candidates = { "Head", "Bip01 Head", "mixamorig:Head", "head", "Bip_Head" };
        foreach (string name in candidates)
        {
            Transform found = FPSCameraRig_SearchByName(root, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FPSCameraRig_SearchByName(Transform t, string target)
    {
        if (t == null) return null;
        if (string.Equals(t.name, target, System.StringComparison.OrdinalIgnoreCase)) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform result = FPSCameraRig_SearchByName(t.GetChild(i), target);
            if (result != null) return result;
        }
        return null;
    }

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj == null || layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private void HandleWeaponGripDebugTuning()
    {
        if (Keyboard.current == null || equippedWeaponObject == null)
            return;

        // Toggle live grip tuning mode with F8.
        if (Keyboard.current.f8Key.wasPressedThisFrame)
            gripTuningMode = !gripTuningMode;
        if (!gripTuningMode)
            return;

        Transform w = equippedWeaponObject.transform;
        Vector3 pos = w.localPosition;
        Vector3 eul = w.localEulerAngles;
        float posStep = 0.002f;
        float rotStep = 1f;

        bool changed = false;

        // Position (Alt + I/K, O/L, P/;)
        bool alt = Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;
        if (alt)
        {
            if (Keyboard.current.iKey.isPressed) { pos.x += posStep; changed = true; }
            if (Keyboard.current.kKey.isPressed) { pos.x -= posStep; changed = true; }
            if (Keyboard.current.oKey.isPressed) { pos.y += posStep; changed = true; }
            if (Keyboard.current.lKey.isPressed) { pos.y -= posStep; changed = true; }
            if (Keyboard.current.pKey.isPressed) { pos.z += posStep; changed = true; }
            if (Keyboard.current.semicolonKey.isPressed) { pos.z -= posStep; changed = true; }

            // Rotation (Alt + 1/2, 3/4, 5/6)
            if (Keyboard.current.digit1Key.isPressed) { eul.x += rotStep; changed = true; }
            if (Keyboard.current.digit2Key.isPressed) { eul.x -= rotStep; changed = true; }
            if (Keyboard.current.digit3Key.isPressed) { eul.y += rotStep; changed = true; }
            if (Keyboard.current.digit4Key.isPressed) { eul.y -= rotStep; changed = true; }
            if (Keyboard.current.digit5Key.isPressed) { eul.z += rotStep; changed = true; }
            if (Keyboard.current.digit6Key.isPressed) { eul.z -= rotStep; changed = true; }
        }

        if (changed)
        {
            w.localPosition = pos;
            w.localRotation = Quaternion.Euler(eul);
        }

        // Print current exact pose to Console (F9) for copy/paste into inspector/code.
        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            Debug.Log($"[GripTune] weapon={equippedWeaponObject.name} pos={w.localPosition} euler={w.localEulerAngles}");
        }

        // Save live tuned grip to persistent PlayerPrefs (F10).
        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            SaveCurrentWeaponGripToPrefs();
            Debug.Log("[GripTune] Saved persistent grip values (F10).");
        }

        // Clear saved persistent values and revert to inspector defaults (F11).
        if (Keyboard.current.f11Key.wasPressedThisFrame)
        {
            ClearSavedGripOverrides();
            Debug.Log("[GripTune] Cleared persistent grip values (F11).");
        }
    }

    private void SaveCurrentWeaponGripToPrefs()
    {
        if (equippedWeaponObject == null)
            return;

        Vector3 pos = equippedWeaponObject.transform.localPosition;
        Vector3 eul = equippedWeaponObject.transform.localEulerAngles;
        int level = equippedWeaponLevel >= 1
            ? equippedWeaponLevel
            : (GameManager.Instance != null ? GameManager.Instance.currentLevel : -1);

        if (level == 13)
        {
            level13SickleGripLocalPosition = pos;
            level13SickleGripLocalEuler = eul;
            SaveVector3Pref(PrefKeySicklePos, pos);
            SaveVector3Pref(PrefKeySickleEuler, eul);
            PlayerPrefs.Save();
            return;
        }

        if (level == 12)
        {
            level12SawGripLocalPosition = pos;
            level12SawGripLocalEuler = eul;
            SaveVector3Pref(PrefKeySawPos, pos);
            SaveVector3Pref(PrefKeySawEuler, eul);
            PlayerPrefs.Save();
            return;
        }

        Debug.LogWarning($"[GripTune] Save ignored for level {level}. Supported: level 12 (saw), level 13 (sickle).");
    }

    private void LoadSavedGripOverrides()
    {
        level13SickleGripLocalPosition = DefaultLevel13SickleGripLocalPosition;
        level13SickleGripLocalEuler = DefaultLevel13SickleGripLocalEuler;
        PlayerPrefs.DeleteKey(PrefKeySicklePos + ".x");
        PlayerPrefs.DeleteKey(PrefKeySicklePos + ".y");
        PlayerPrefs.DeleteKey(PrefKeySicklePos + ".z");
        PlayerPrefs.DeleteKey(PrefKeySickleEuler + ".x");
        PlayerPrefs.DeleteKey(PrefKeySickleEuler + ".y");
        PlayerPrefs.DeleteKey(PrefKeySickleEuler + ".z");
        level12SawGripLocalPosition = DefaultLevel12SawGripLocalPosition;
        level12SawGripLocalEuler = DefaultLevel12SawGripLocalEuler;
        PlayerPrefs.DeleteKey(PrefKeySawPos + ".x");
        PlayerPrefs.DeleteKey(PrefKeySawPos + ".y");
        PlayerPrefs.DeleteKey(PrefKeySawPos + ".z");
        PlayerPrefs.DeleteKey(PrefKeySawEuler + ".x");
        PlayerPrefs.DeleteKey(PrefKeySawEuler + ".y");
        PlayerPrefs.DeleteKey(PrefKeySawEuler + ".z");
    }

    private void ClearSavedGripOverrides()
    {
        PlayerPrefs.DeleteKey(PrefKeySicklePos + ".x");
        PlayerPrefs.DeleteKey(PrefKeySicklePos + ".y");
        PlayerPrefs.DeleteKey(PrefKeySicklePos + ".z");
        PlayerPrefs.DeleteKey(PrefKeySickleEuler + ".x");
        PlayerPrefs.DeleteKey(PrefKeySickleEuler + ".y");
        PlayerPrefs.DeleteKey(PrefKeySickleEuler + ".z");
        PlayerPrefs.DeleteKey(PrefKeySawPos + ".x");
        PlayerPrefs.DeleteKey(PrefKeySawPos + ".y");
        PlayerPrefs.DeleteKey(PrefKeySawPos + ".z");
        PlayerPrefs.DeleteKey(PrefKeySawEuler + ".x");
        PlayerPrefs.DeleteKey(PrefKeySawEuler + ".y");
        PlayerPrefs.DeleteKey(PrefKeySawEuler + ".z");
        PlayerPrefs.Save();
    }

    private static void SaveVector3Pref(string key, Vector3 value)
    {
        PlayerPrefs.SetFloat(key + ".x", value.x);
        PlayerPrefs.SetFloat(key + ".y", value.y);
        PlayerPrefs.SetFloat(key + ".z", value.z);
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

    private GameObject BuildPrimitiveWeapon(int level, Transform attachPoint, WeaponLoadout loadout)
    {
        Transform weaponSocket = attachPoint != null && attachPoint.name == WeaponSocketName
            ? attachPoint
            : GetOrCreateWeaponSocket(attachPoint);
        GameObject root = new GameObject("WeaponModel_Fallback");
        root.transform.SetParent(weaponSocket, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        ApplyWeaponGripPose(root.transform, loadout.PlayerLocalPosition, loadout.PlayerLocalEuler);
        SetLayerRecursive(root, gameObject.layer);

        BoxCollider weaponCollider = root.AddComponent<BoxCollider>();
        weaponCollider.isTrigger = true;
        weaponCollider.center    = Vector3.zero;
        weaponCollider.size      = Vector3.one * 0.1f;

        WeaponBase weaponBase  = root.AddComponent<WeaponBase>();
        weaponBase.weaponName  = equippedWeaponName;
        weaponBase.damage      = attackDamage;
        weaponBase.attackRange = attackDistance;
        weaponBase.isRanged    = false;

        root.AddComponent<WeaponVisibilityFix>();

        GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        meshObj.transform.SetParent(root.transform);
        meshObj.transform.localPosition = Vector3.zero;
        meshObj.transform.localScale    = new Vector3(0.05f, 0.3f, 0.05f);

        Destroy(meshObj.GetComponent<Collider>());

        MeleeWeaponWallPullback wallPullPm = root.GetComponent<MeleeWeaponWallPullback>();
        if (wallPullPm == null) wallPullPm = root.AddComponent<MeleeWeaponWallPullback>();
        wallPullPm.Configure(transform, staticObstacleMask);

        return root;
    }
}
