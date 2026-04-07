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
    private LayerMask resolvedAttackMask;

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

        if (equippedWeaponObject != null)
        {
            WeaponBase weapon = equippedWeaponObject.GetComponent<WeaponBase>();
            if (weapon != null) weapon.Attack();

            WeaponHitbox hitbox = equippedWeaponObject.GetComponent<WeaponHitbox>();
            if (hitbox != null)
            {
                hitbox.EnableHitbox();
                CancelInvoke(nameof(DisableWeaponHitbox));
                Invoke(nameof(DisableWeaponHitbox), attackDelay + 0.3f);
            }
        }

        CancelInvoke(nameof(AttackRaycast));
        Invoke(nameof(AttackRaycast), attackDelay);
    }

    private void DisableWeaponHitbox()
    {
        if (equippedWeaponObject != null)
        {
            WeaponHitbox hitbox = equippedWeaponObject.GetComponent<WeaponHitbox>();
            if (hitbox != null) hitbox.DisableHitbox();
        }
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
        Camera cam = ActiveCamera;
        if (cam == null) return;

        AttackMelee(cam);
        cameraKickTarget = -1.2f;
    }

    private const float MeleeHitAngle = 75f;   // widened from 60° for more forgiving melee

    private void AttackMelee(Camera cam)
    {
        // Sphere is cast from chest height, centred halfway to the attack limit.
        // We also try triggers (UseGlobal) so armour / hitbox triggers still register.
        Vector3 meleeOrigin = transform.position + Vector3.up * 1.0f
                            + transform.forward  * (attackDistance * 0.5f);

        Collider[] hits = Physics.OverlapSphere(
            meleeOrigin,
            attackRadius * 1.5f,         // slightly wider than before
            resolvedAttackMask,
            QueryTriggerInteraction.UseGlobal);  // was Ignore — triggers count now

        bool landed = false;

        // Sort by distance so the closest enemy is tried first
        System.Array.Sort(hits, (a, b) =>
        {
            float dA = (a.transform.position - meleeOrigin).sqrMagnitude;
            float dB = (b.transform.position - meleeOrigin).sqrMagnitude;
            return dA.CompareTo(dB);
        });

        for (int i = 0; i < hits.Length; i++)
        {
            Vector3 dirToTarget = (hits[i].transform.position - transform.position);
            dirToTarget.y = 0f;
            float angle = Vector3.Angle(transform.forward, dirToTarget);

            if (angle > MeleeHitAngle) continue;

            if (TryDamageTarget(hits[i].transform, attackDamage))
            {
                ApplyHitReaction(hits[i].transform, transform.forward);
                HitTarget(hits[i].ClosestPoint(meleeOrigin));
                landed = true;
                break;
            }
        }

        // ── Fallback A: camera-forward raycast ───────────────────────────────
        if (!landed)
        {
            if (Physics.Raycast(cam.transform.position, cam.transform.forward,
                out RaycastHit rayHit, attackDistance, resolvedAttackMask,
                QueryTriggerInteraction.UseGlobal))
            {
                if (TryDamageTarget(rayHit.transform, attackDamage))
                {
                    ApplyHitReaction(rayHit.transform, transform.forward);
                    HitTarget(rayHit.point);
                    landed = true;
                }
                else
                {
                    HitTarget(rayHit.point);
                }
            }
        }

        // ── Fallback B: direct component scan (bypasses colliders entirely) ──
        // Searches all EnemyController objects within range.
        // This catches enemies whose colliders may be mis-configured.
        if (!landed)
        {
            EnemyController[] allEnemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (EnemyController ec in allEnemies)
            {
                if (ec == null) continue;
                float dist = Vector3.Distance(transform.position, ec.transform.position);
                if (dist > attackDistance + 1f) continue;

                ec.TakeDamage(attackDamage, byPlayer: true);
                ApplyHitReaction(ec.transform, transform.forward);
                HitTarget(ec.transform.position + Vector3.up * 0.9f);
                landed = true;
                break;
            }
        }
    }

    private bool TryDamageTarget(Transform target, int damage)
    {
        if (target == null) return false;

        EnemyController enemy = target.GetComponentInParent<EnemyController>();
        if (enemy != null && enemy.gameObject != gameObject)
        {
            enemy.TakeDamage(damage, byPlayer: true);
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

        CameraController follow   = camObj.AddComponent<CameraController>();
        follow.target             = transform;
        follow.offset             = new Vector3(0f, 3.4f, -7.2f);
        follow.smoothSpeed        = 10f;
        follow.lookAheadDistance  = 4.5f;
        follow.lookHeight         = 1.6f;

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
        if (root.GetComponent<AnimationEventSink>() == null)
            root.AddComponent<AnimationEventSink>();

        if (root.GetComponent<MeleeAnimationEventSink>() == null)
            root.AddComponent<MeleeAnimationEventSink>();
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

        // Delegate bone-finding to EquipmentManager (handles Ronin j_wrist_ri,
        // Crosby bip_hand_R/weapon_bone_R, Mixamo, and any humanoid avatar).
        Transform handBone  = EquipmentManager.FindRightHand(body);
        Transform attachPoint = handBone != null ? handBone : body.transform;

        equippedWeaponObject = BuildWeaponModel(level, attachPoint);
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

    /// <summary>
    /// Per-level weapon target sizes (metres, longest axis).
    /// Matches LevelBuilder.GetWeaponTargetSize so player and enemy weapons are identical.
    /// </summary>
    private static float GetPlayerWeaponTargetSize(int level)
    {
        float[] sizes = {
            0.30f, 0.95f, 1.00f, 0.85f, 0.30f,   // Levels 1-5
            0.40f, 0.55f, 0.85f, 0.70f, 1.40f,   // Levels 6-10
            1.00f, 0.40f, 0.35f, 0.50f, 0.60f,   // Levels 11-15
            0.90f                                    // Level 16
        };
        return sizes[Mathf.Clamp(level - 1, 0, sizes.Length - 1)];
    }

    private GameObject BuildWeaponModel(int level, Transform attachPoint)
    {
        GameObject levelPrefab = ResolveWeaponPrefabForLevel(level);
        if (levelPrefab != null)
        {
            // Instantiate at world root first to avoid parent scale contamination
            GameObject weapon = Instantiate(levelPrefab);
            weapon.name = "WeaponModel";

            weapon.SetActive(true);
            foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);

            StripWeaponArmature(weapon);

            // ── Auto-scale BEFORE parenting (uses per-level target size) ──
            float targetSize = GetPlayerWeaponTargetSize(level);
            EquipmentManager.ApplyAutoScale(weapon, targetSize);

            // NOW parent to hand — worldPositionStays=true preserves scale
            weapon.transform.SetParent(attachPoint, true);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;

            // Safety clamp: if lossy scale is still wildly off, force a sane value
            Vector3 lossy = weapon.transform.lossyScale;
            float minAxis = Mathf.Min(Mathf.Abs(lossy.x),
                                      Mathf.Min(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
            if (minAxis < 0.005f)
            {
                weapon.transform.localScale = Vector3.one * 0.1f;
                Debug.LogWarning($"[PlayerController] Weapon '{weapon.name}' was near-zero scale " +
                                 "— forced to (0.1, 0.1, 0.1).");
            }

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

        return BuildPrimitiveWeapon(level, attachPoint);
    }

    private GameObject ResolveWeaponPrefabForLevel(int level)
    {
        string importedPath = GetImportedWeaponResourcePath(level);
        if (!string.IsNullOrEmpty(importedPath))
        {
            GameObject imported = Resources.Load<GameObject>(importedPath);
            if (imported != null) return imported;
        }

        string weaponName = GameManager.Instance != null
            ? GameManager.Instance.GetWeaponNameForLevel(level)
            : string.Empty;

        string sanitized = string.IsNullOrWhiteSpace(weaponName)
            ? string.Empty
            : weaponName.Replace(" ", string.Empty).Replace("-", string.Empty);

        if (!string.IsNullOrEmpty(sanitized))
        {
            GameObject byName = Resources.Load<GameObject>("Weapons/" + sanitized);
            if (byName != null) return byName;
        }

        return null;
    }

    private static string GetImportedWeaponResourcePath(int level)
    {
        switch (level)
        {
            case  1: return "Weapons/Imported/tactical-knife(level1)/source/TacticalKnife/Tactical Knife";
            case  2: return "Weapons/Imported/Katana(level2)/source/melee";
            case  3: return "Weapons/Imported/shovel(level3)/source/Shovel/Shovel";
            case  4: return "Weapons/Imported/baseball-bat(level4)/source/baseball_bat_1k";
            case  5: return "Weapons/Imported/nunchucks(level5)/Nunchucks";
            case  6: return "Weapons/Imported/Wrench(level6)/source/PipeWrenchUnreal";
            case  7: return "Weapons/Imported/crowbar(level7)/source/CrowbarV2";
            case  8: return "Weapons/Imported/Hammer(level8)l/source/Sledgehammer/Sledge hammer";
            case  9: return "Weapons/Imported/axe(level9)/source/axe";
            case 10: return "Weapons/Imported/Spear(level10)/source/Spear/Spear";
            case 11: return "Weapons/Imported/nailed-plank(level11)/source/NailedPlank/NailedPlank";
            case 12: return "Weapons/Imported/saw(level12)/source/extracted/saw_low";
            case 13: return "Weapons/Imported/sickle(level13)/source/Sickle";
            case 14: return "Weapons/Imported/medieval(level14)/source/Medieval_morgenstern_low2 scene";
            case 15: return "Weapons/Imported/l3fte(level15)/source/L3FT_E";
            case 16: return "Weapons/Imported/shield(level16)/source/RiotShield/Riot Shield";
            default: return null;
        }
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

    private GameObject BuildPrimitiveWeapon(int level, Transform attachPoint)
    {
        GameObject root = new GameObject("WeaponModel_Fallback");
        root.transform.SetParent(attachPoint, false);
        root.transform.localPosition = new Vector3(0f, 0.08f, 0.02f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

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
