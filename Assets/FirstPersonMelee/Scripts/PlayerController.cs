using System.Collections.Generic;
using UnityEngine;
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

    // ════════════════════════════════════════════════════════════════════════
    //  INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

    [Header("Movement")]
    [Tooltip("Base walking speed in units per second.")]
    public float moveSpeed = 3.5f;

    [Tooltip("Multiplier applied when sprint key is held.")]
    public float sprintMultiplier = 1.37f;

    [Tooltip("Gravity acceleration (negative value).")]
    public float gravity = -25f;

    [Tooltip("Maximum jump height in units.")]
    public float jumpHeight = 1.8f;

    [Header("Animation")]
    [Tooltip("Animator controller for the spawned third-person body.")]
    public RuntimeAnimatorController playerAnimatorController;

    [Tooltip("Optional humanoid avatar override for the spawned third-person body.")]
    public Avatar playerAvatar;

    [Header("Movement Tuning")]
    [Tooltip("How fast the character reaches full speed (units/s²).")]
    public float acceleration = 12f;

    [Tooltip("How fast the character brakes to zero (units/s²).")]
    public float deceleration = 12f;

    [Header("Camera")]
    [Tooltip("First-person camera reference.")]
    public Camera firstPersonCam;

    [Tooltip("Third-person camera reference (auto-created if null).")]
    public Camera thirdPersonCam;

    [Tooltip("Mouse look sensitivity.")]
    public float sensitivity = 100f;

    [Header("Combat")]
    [Tooltip("Maximum distance for melee attacks.")]
    public float attackDistance = 3f;

    [Tooltip("Delay before damage is applied after attack input.")]
    public float attackDelay = 0.4f;

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
    private const float InputSmoothing = 10f;
    private bool zoomHeld;

    // Camera
    private float cameraPitch;
    private Vector3 firstPersonLocalPos;
    private Quaternion firstPersonLocalRot;
    private Camera runtimeThirdPersonCamera;
    private bool isThirdPersonActive;

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

    // Third-person body
    private GameObject thirdPersonBody;
    private Renderer[] firstPersonRenderers;
    [HideInInspector] public GameObject equippedWeaponObject;

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

    // Third-person ONLY — the first-person camera is permanently disabled.
    public Camera ActiveCamera => runtimeThirdPersonCamera;
    public GameObject GetThirdPersonBody() => thirdPersonBody;
    public bool IsMeleeWeapon => true;

    // ════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        controller  = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        if (controller != null)
        {
            controller.height          = 1.8f;
            controller.radius          = 0.4f;
            controller.center          = new Vector3(0f, 0.92f, 0f);
            controller.skinWidth       = 0.04f;
            controller.stepOffset      = 0.25f;
            controller.minMoveDistance  = 0f;
        }

        // ── Third-person ONLY: locate the first-person camera so we can DISABLE it.
        //    We keep the reference so EnsureThirdPersonCamera can borrow FOV/clip
        //    settings, but the GameObject is immediately turned off and never shown.
        if (firstPersonCam == null)
            firstPersonCam = GetComponentInChildren<Camera>();
        if (firstPersonCam != null)
        {
            firstPersonLocalPos = firstPersonCam.transform.localPosition;
            firstPersonLocalRot = firstPersonCam.transform.localRotation;
            firstPersonCam.gameObject.SetActive(false);   // ← permanently off
        }

        CacheFirstPersonWeaponSlot();

        firstPersonRenderers = GetComponentsInChildren<Renderer>(true);
        resolvedAttackMask   = attackLayer == 0 ? ~0 : attackLayer;

        foreach (Animator childAnimator in GetComponentsInChildren<Animator>(true))
            childAnimator.applyRootMotion = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Start()
    {
        // ── 1. Snap player above the floor ──
        if (controller != null) controller.enabled = false;
        Vector3 startPos = transform.position;
        startPos.y = Mathf.Max(startPos.y, arenaFloorHeight + 1.0f);
        transform.position = startPos;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (controller != null) controller.enabled = true;
        verticalVelocity.y = -2f;

        // ── 2. Spawn the third-person body ──
        ApplyPerspectivePreference();
        EnsureThirdPersonBody();

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

    private void Update()
    {
        isGrounded = controller.isGrounded;

        ReadInput();
        HandleActionInput();
        ApplyMovement();
        ApplyLook();
        // UpdateCameraZoom removed — zoom is permanently disabled.
        UpdateHeadBob();
        UpdateCameraKick();
        UpdateCombatState();
        UpdateAnimatorParameters();
    }

    private void LateUpdate()
    {
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════════════

    private void ReadInput()
    {
        moveInputRaw = ReadMovementInput();
        moveInputRaw = Vector2.ClampMagnitude(moveInputRaw, 1f);
        moveInputSmoothed = Vector2.Lerp(moveInputSmoothed, moveInputRaw, InputSmoothing * Time.deltaTime);
        lookInput = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        // Zoom disabled — camera is permanently fixed over-the-shoulder.
        zoomHeld = false;
    }

    private void HandleActionInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                jumpRequested = true;

            // V-key perspective toggle removed — game is third-person only.
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            jumpRequested = true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Attack();
    }

    private static Vector2 ReadMovementInput()
    {
        if (Keyboard.current == null) return Vector2.zero;

        Vector2 movement = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    movement.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  movement.y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  movement.x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) movement.x += 1f;
        return movement;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MOVEMENT
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyMovement()
    {
        if (controller == null) return;

        Vector3 frameStartPosition = transform.position;

        isSprinting = Keyboard.current != null
                   && Keyboard.current.leftShiftKey.isPressed
                   && moveInputSmoothed.y > 0.1f;

        float targetSpeed    = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 inputDir     = new Vector3(moveInputSmoothed.x, 0f, moveInputSmoothed.y);
        Vector3 targetVelocity = transform.TransformDirection(inputDir) * targetSpeed;

        float rate = moveInputSmoothed.sqrMagnitude > 0.01f ? acceleration : deceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);

        controller.Move(horizontalVelocity * Time.deltaTime);

        if (isGrounded)
        {
            verticalVelocity.y = -2f;
            if (jumpRequested)
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
        controller.Move(verticalVelocity * Time.deltaTime);

        ClampInsideArena();
        SnapToArenaFloor();

        Vector3 frameDisplacement = transform.position - frameStartPosition;
        frameDisplacement.y = 0f;
        actualHorizontalVelocity = Time.deltaTime > 0f
            ? frameDisplacement / Time.deltaTime
            : Vector3.zero;

        wasMoving = actualHorizontalVelocity.sqrMagnitude > 0.01f;

        if (isThirdPersonActive && thirdPersonBody != null)
        {
            Vector3 moveFlat = actualHorizontalVelocity;
            if (moveFlat.sqrMagnitude > 0.01f)
            {
                float targetY = Quaternion.LookRotation(moveFlat).eulerAngles.y;
                thirdPersonBody.transform.rotation = Quaternion.Slerp(
                    thirdPersonBody.transform.rotation,
                    Quaternion.Euler(0f, targetY, 0f),
                    8f * Time.deltaTime);
            }
        }
    }

    private Vector3 GetActualHorizontalVelocity()
        => actualHorizontalVelocity;

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
        if (controller != null) controller.enabled = false;
        transform.position = position;
        if (controller != null) controller.enabled = true;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CAMERA / LOOK
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyLook()
    {
        Camera cam = ActiveCamera;
        if (cam == null) return;

        float mouseX = lookInput.x * sensitivity * Time.deltaTime;
        float mouseY = lookInput.y * sensitivity * Time.deltaTime;

        cameraPitch -= mouseY;
        cameraPitch  = Mathf.Clamp(cameraPitch, -80f, 80f);

        // First-person camera is permanently disabled — no pitch update needed.

        transform.Rotate(Vector3.up * mouseX);

        if (isThirdPersonActive && runtimeThirdPersonCamera != null)
        {
            CameraController orbitCtrl = runtimeThirdPersonCamera.GetComponent<CameraController>();
            if (orbitCtrl != null)
                orbitCtrl.pitch = cameraPitch * 0.45f;
        }
    }

    private void UpdateHeadBob()
    {
        // Head-bob was a first-person effect. Third-person only — nothing to do.
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
        cameraPitch  = Mathf.Clamp(cameraPitch, -80f, 80f);
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
            audioSource.PlayOneShot(swordSwing);
        }

        // ── Weapon callback (visuals / VFX only) ─────────────────────────────
        // NOTE: The physical WeaponHitbox trigger collider is intentionally
        // NEVER enabled from the player. All damage is routed through
        // AttackMelee() which performs a strict single-target overlap/sweep
        // query against real colliders.
        // Enabling the trigger hitbox here caused massive AoE "Bluetooth"
        // damage on large weapons (Baseball Bat, Axe) in levels 4/7/9.
        if (equippedWeaponObject != null)
        {
            WeaponBase weapon = equippedWeaponObject.GetComponent<WeaponBase>();
            if (weapon != null) weapon.Attack();
        }

        CancelInvoke(nameof(AttackRaycast));
        Invoke(nameof(AttackRaycast), attackDelay);
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

    private void ResetAttackState()
    {
        isAttacking = false;
        attackResetTimer = 0f;
        attackFailsafeTimer = 0f;
    }

    private void ApplyAttackLunge()
    {
        if (controller == null) return;
        controller.Move(transform.forward * attackLungeDistance);
        ClampInsideArena();
    }

    private void AttackRaycast()
    {
        AttackMelee();
        cameraKickTarget = -1.2f;
    }

    private const float MeleeHitAngle = 60f;   // strict forward-facing cone (half-angle)

    private void AttackMelee()
    {
        // ════════════════════════════════════════════════════════════════════
        //  STRICT SINGLE-TARGET MELEE
        //  • Non-alloc OverlapCapsule based on actual colliders, not target pivots
        //  • Point-blank overlaps are valid hits even when already intersecting
        //  • Forward-cone filter still prevents "behind the player" hits
        //  • Damages EXACTLY ONE enemy per swing (the closest valid target)
        // ════════════════════════════════════════════════════════════════════

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

        if (TryFindBestMeleeTarget(playerCenter, forwardFlat, out Transform bestTarget, out Vector3 hitPoint))
        {
            if (TryDamageTarget(bestTarget, attackDamage))
            {
                ApplyHitReaction(bestTarget, forwardFlat);
                HitTarget(hitPoint);
            }
            return;
        }

        // No overlap candidate was valid — try a narrow forward sweep so thin
        // targets at the edge of the knife still connect cleanly.
        TryPrecisionSweepFallback(playerCenter, forwardFlat);
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
            audioSource.PlayOneShot(hitSound);
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
        Vector3 planar = transform.position;
        planar.y = 0f;

        float maxR = Mathf.Max(1f, arenaBoundaryRadius - controller.radius - arenaBoundaryPadding);
        if (planar.sqrMagnitude <= maxR * maxR) return;

        Vector3 clamped = planar.normalized * maxR;
        if (controller != null) controller.enabled = false;
        transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
        if (controller != null) controller.enabled = true;

        if (Vector3.Dot(horizontalVelocity, planar.normalized) > 0f)
            horizontalVelocity = Vector3.zero;

        if (transform.position.y > arenaFloorHeight + 0.6f)
        {
            if (controller != null) controller.enabled = false;
            transform.position = new Vector3(transform.position.x, arenaFloorHeight, transform.position.z);
            if (controller != null) controller.enabled = true;
            verticalVelocity.y = Mathf.Min(verticalVelocity.y, 0f);
        }
    }

    private void SnapToArenaFloor()
    {
        if (controller == null || verticalVelocity.y > 0.1f || isGrounded) return;

        Vector3 origin   = transform.position + Vector3.up * 0.3f;
        float   castDist = Mathf.Max(maxFloorSnapDistance * 10f, 5f);

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castDist, ~0, QueryTriggerInteraction.Ignore))
            return;

        float targetY  = hit.point.y;
        float currentY = transform.position.y;

        if (currentY <= targetY + 0.01f) return;

        float dist      = currentY - targetY;
        float snapSpeed = Mathf.Lerp(floorSnapSpeed, floorSnapSpeed * 8f, Mathf.Clamp01(dist / 3f));
        float snappedY  = Mathf.MoveTowards(currentY, targetY, snapSpeed * Time.deltaTime);

        if (controller != null) controller.enabled = false;
        transform.position = new Vector3(transform.position.x, snappedY, transform.position.z);
        if (controller != null) controller.enabled = true;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PERSPECTIVE MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════

    private void TogglePerspective()
    {
        // Perspective toggle disabled — game is third-person only.
        // Kept so that any external callers don't cause a compile error.
    }

    public void RefreshGameplayPreferences()
    {
        ApplyPerspectivePreference();
        if (thirdPersonBody != null && isThirdPersonActive)
            thirdPersonBody.SetActive(true);
    }

    private void ApplyPerspectivePreference()
    {
        // Game is third-person only — always enable the third-person view.
        EnableThirdPersonView();
    }

    private void EnableFirstPersonView()
    {
        // First-person view permanently removed — redirect to third-person.
        EnableThirdPersonView();
    }

    private void EnableThirdPersonView()
    {
        isThirdPersonActive = true;
        EnsureThirdPersonCamera();
        EnsureThirdPersonBody();

        // Keep first-person camera off (was disabled in Awake).
        if (firstPersonCam != null)
            firstPersonCam.gameObject.SetActive(false);

        if (runtimeThirdPersonCamera != null)
            runtimeThirdPersonCamera.gameObject.SetActive(true);

        SetFirstPersonRenderersVisible(false);
        if (thirdPersonBody != null)
            thirdPersonBody.SetActive(true);
    }

    private void EnsureThirdPersonCamera()
    {
        if (runtimeThirdPersonCamera != null)
        {
            thirdPersonCam = runtimeThirdPersonCamera;
            return;
        }

        GameObject camObj = new GameObject("RuntimeThirdPersonCamera");
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
        follow.target           = transform;
        follow.offset           = new Vector3(1.2f, 2.2f, -5.5f);  // Fixed OTS right-shoulder
        follow.smoothSpeed      = 12f;
        follow.lookHeight       = 1.4f;
        follow.defaultFieldOfView = runtimeThirdPersonCamera.fieldOfView;
        follow.lookTargetLocalOffset = new Vector3(0f, 0.08f, 0f);

        thirdPersonCam = runtimeThirdPersonCamera;
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

        SetFirstPersonRenderersVisible(false);

        GameObject roninBodyPrefab = Resources.Load<GameObject>("Player/Ronin/source/Ronin");
        if (roninBodyPrefab != null)
        {
            thirdPersonBody = Instantiate(roninBodyPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
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
            return;
        }

        GameObject dragonSoulsBodyPrefab = Resources.Load<GameObject>("Player/DragonSoulsThirdPersonBody");
        if (dragonSoulsBodyPrefab != null)
        {
            thirdPersonBody = Instantiate(dragonSoulsBodyPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.identity;
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
    /// Place your texture at:  Assets/Resources/Textures/RoninTexture.png
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

        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null) continue;
            smr.material = material;
        }
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
        level = Mathf.Max(1, level);

        if (GameManager.Instance != null)
        {
            equippedWeaponName = GameManager.Instance.GetWeaponNameForLevel(level);
            attackDamage       = Mathf.RoundToInt(GameManager.Instance.GetWeaponDamageForLevel(level));
            attackDistance     = GameManager.Instance.GetWeaponRangeForLevel(level);
            attackSpeed = 1.0f; attackDelay = 0.4f; attackRadius = 1.25f;
        }
        else
        {
            equippedWeaponName = "Combat Knife";
        }

        if (thirdPersonBody != null)
        {
            if (isThirdPersonActive)
                thirdPersonBody.SetActive(true);

            AttachWeaponToHand(thirdPersonBody, level);
            SetupWeaponIK();
        }

        // RefreshFirstPersonWeaponModel removed — third-person only.
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

    private void AttachWeaponToHand(GameObject body, int weaponLevel = -1)
    {
        if (body == null) return;

        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
            equippedWeaponObject = null;
        }

        int level = weaponLevel >= 1
            ? weaponLevel
            : (GameManager.Instance != null ? GameManager.Instance.currentLevel : 1);
        WeaponLoadout loadout = WeaponLoadoutCatalog.Get(level);

        // ── 1. Load prefab with guaranteed fallback chain ───────────────────
        float finalTargetSize;
        GameObject prefab = WeaponLoadoutCatalog.LoadPrefabWithFallback(level, out finalTargetSize);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerController] All weapon sources exhausted for level {level}, using primitive.");
            Transform fallbackBone = FindPlayerHandBone(body) ?? body.transform;
            Transform fallbackSocket = GetOrCreateWeaponSocket(fallbackBone);
            equippedWeaponObject = BuildPrimitiveWeapon(level, fallbackSocket, loadout);
            return;
        }

        // ── 2. Find right-hand bone ─────────────────────────────────────────
        Transform handBone = FindPlayerHandBone(body);
        if (handBone == null)
        {
            Debug.LogWarning("[PlayerController] Right hand bone not found, attaching to body root.");
            handBone = body.transform;
        }
        Transform weaponSocket = GetOrCreateWeaponSocket(handBone);

        // ── 3. Instantiate (unparented to get clean world-space bounds) ─────
        GameObject weapon = Instantiate(prefab);
        weapon.name = "WeaponModel";
        weapon.SetActive(true);
        SetLayerRecursive(weapon, gameObject.layer);

        foreach (Transform t in weapon.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);

        // NOTE: StripWeaponArmature removed — it was destroying mesh nodes
        // inside weapons whose hierarchy uses "Armature" as the mesh parent.
        // Embedded animators are disabled in step 7 instead, which is safe.

        // ── 4. Measure bounds at unit scale ─────────────────────────────────
        weapon.transform.localScale = Vector3.one;
        float weaponExtent = GetMaxRendererExtent(weapon);
        if (weaponExtent < 0.001f) weaponExtent = 1f;

        // ── 5. Parent to hand, reset local transform ────────────────────────
        weapon.transform.SetParent(weaponSocket, worldPositionStays: false);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        Transform runtimeGripParent = WeaponLoadoutCatalog.GetOrCreateRuntimeGripAnchor(level, prefab, weaponSocket);
        if (runtimeGripParent != weaponSocket)
        {
            weapon.transform.SetParent(runtimeGripParent, worldPositionStays: false);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
        }

        // ── 6. Compute localScale so weapon reaches desired WORLD size ──────
        float desiredWorldSize = finalTargetSize;
        float uniformScale = desiredWorldSize / weaponExtent;
        Vector3 parentLossy = weapon.transform.parent != null ? weapon.transform.parent.lossyScale : weaponSocket.lossyScale;
        weapon.transform.localScale = new Vector3(
            uniformScale / Mathf.Max(Mathf.Abs(parentLossy.x), 0.0001f),
            uniformScale / Mathf.Max(Mathf.Abs(parentLossy.y), 0.0001f),
            uniformScale / Mathf.Max(Mathf.Abs(parentLossy.z), 0.0001f));
        if (!WeaponLoadoutCatalog.ApplyRuntimeGripPose(level, prefab, weapon.transform))
            ApplyWeaponGripPose(weapon.transform, loadout.PlayerLocalPosition, loadout.PlayerLocalEuler);
        WeaponLoadoutCatalog.ApplyRuntimeOverrides(level, prefab, weapon);

        Debug.Log($"[PlayerController] Weapon '{weapon.name}' → hand '{handBone.name}' " +
                  $"targetSize={desiredWorldSize} extent={weaponExtent} " +
                  $"localPosition={weapon.transform.localPosition} " +
                  $"localEuler={weapon.transform.localEulerAngles} " +
                  $"localScale={weapon.transform.localScale} lossyScale={weapon.transform.lossyScale}");

        // ── 7. Disable physics, embedded animators, colliders ───────────────
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

        // ── 8. Wire WeaponBase ──────────────────────────────────────────────
        WeaponBase wb = weapon.GetComponent<WeaponBase>();
        if (wb == null) wb = weapon.AddComponent<WeaponBase>();
        wb.weaponName  = equippedWeaponName;
        wb.damage      = attackDamage;
        wb.attackRange = attackDistance;
        wb.isRanged    = false;

        // ── 9. Wire WeaponHitbox ────────────────────────────────────────────
        WeaponHitbox hitbox = weapon.GetComponent<WeaponHitbox>();
        if (hitbox == null) hitbox = weapon.AddComponent<WeaponHitbox>();
        hitbox.damage = attackDamage;

        // ── 10. Visibility fix (URP) ────────────────────────────────────────
        if (weapon.GetComponent<WeaponVisibilityFix>() == null)
            weapon.AddComponent<WeaponVisibilityFix>();

        // ── 11. Refresh melee animation event sink cache ────────────────────
        if (thirdPersonBody != null)
        {
            MeleeAnimationEventSink sink = thirdPersonBody.GetComponentInChildren<MeleeAnimationEventSink>(true);
            if (sink != null) sink.ClearCache();
        }

        equippedWeaponObject = weapon;
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

    private static void ApplyWeaponGripPose(Transform weaponRoot, Vector3 localPosition, Vector3 localEuler)
    {
        if (weaponRoot == null) return;
        weaponRoot.localPosition = localPosition;
        weaponRoot.localRotation = Quaternion.Euler(localEuler);
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

        Transform[] trs = firstPersonCam.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i].name != "Weapon") continue;
            firstPersonWeaponSlot = trs[i];
            firstPersonWeaponMeshRenderer = firstPersonWeaponSlot.GetComponent<MeshRenderer>();
            break;
        }
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
        // First-person weapon display removed — weapon is on the 3rd-person body only.
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

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj == null || layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
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

        return root;
    }
}
