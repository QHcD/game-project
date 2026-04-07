using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Animations;

/// <summary>
/// Main player controller for the female prisoner character.
/// Handles movement, camera, combat, weapon management, and animation.
/// Uses CharacterController for precise, non-physics movement.
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    //  INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

    [Header("Movement")]
    [Tooltip("Base walking speed in units per second.")]
    public float moveSpeed = 5f;

    [Tooltip("Multiplier applied when sprint key is held.")]
    public float sprintMultiplier = 1.55f;

    [Tooltip("Gravity acceleration (negative value).")]
    public float gravity = -25f;

    [Tooltip("Maximum jump height in units.")]
    public float jumpHeight = 1.8f;

    [Header("Movement Tuning")]
    [Tooltip("How fast the character reaches full speed (units/s²).")]
    public float acceleration = 24f;

    [Tooltip("How fast the character brakes to zero (units/s²).")]
    public float deceleration = 20f;

    [Header("Camera")]
    [Tooltip("First-person camera reference.")]
    public Camera firstPersonCam;

    [Tooltip("Third-person camera reference (auto-created if null).")]
    public Camera thirdPersonCam;

    [Tooltip("Mouse look sensitivity.")]
    public float sensitivity = 100f;

    [Header("Combat")]
    [Tooltip("Maximum distance for melee/ranged attacks.")]
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

    [Tooltip("Resources path with no extension (file must live under a Resources folder). Default: MWII tactical knife in Resources/Weapons/TacticalKnife/.")]
    public string combatKnifeResourcePath = "Weapons/TacticalKnife/TacticalKnife";

    [Tooltip("Local position on the third-person right hand.")]
    public Vector3 combatKnifeThirdPersonLocalPos = Vector3.zero;

    [Tooltip("Local rotation on the third-person right hand (degrees).")]
    public Vector3 combatKnifeThirdPersonLocalEuler = Vector3.zero;

    [Tooltip("Local scale on the third-person weapon socket. Use (1,1,1) first; shrink only if the model is huge.")]
    public Vector3 combatKnifeThirdPersonLocalScale = new Vector3(0.02f, 0.02f, 0.02f);

    [Tooltip("Local position on the first-person Weapon socket (camera / arms rig).")]
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
    private float comboCooldownTimer;
    private const float ComboCooldown = 0.15f;
    private GameManager.WeaponType currentWeaponType = GameManager.WeaponType.Melee;
    private LayerMask resolvedAttackMask;

    // Third-person body
    private GameObject thirdPersonBody;
    private Renderer[] firstPersonRenderers;
    [HideInInspector] public GameObject equippedWeaponObject;

    private Transform firstPersonWeaponSlot;
    private MeshRenderer firstPersonWeaponMeshRenderer;
    private GameObject firstPersonKnifeInstance;

    // Animator parameter hashes (connect these in an Animator Controller)
    private static readonly int AnimSpeed      = Animator.StringToHash("Speed");
    private static readonly int AnimGrounded   = Animator.StringToHash("IsGrounded");
    private static readonly int AnimAttacking  = Animator.StringToHash("IsAttacking");
    private static readonly int AnimSprinting  = Animator.StringToHash("IsSprinting");
    // Issue #3 — directional velocity for natural left/right leg movement
    private static readonly int AnimVelocityX  = Animator.StringToHash("VelocityX");
    private static readonly int AnimVelocityZ  = Animator.StringToHash("VelocityZ");

    // Legacy animation state names (first-person)
    public const string IDLE    = "Idle";
    public const string WALK    = "Walk";
    public const string ATTACK1 = "Attack 1";
    public const string ATTACK2 = "Attack 2";
    private string currentAnimationState;

    // ════════════════════════════════════════════════════════════════════════
    //  CAMERA PROPERTY — used by the active first/third person camera
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Returns whichever camera is currently active.</summary>
    public Camera ActiveCamera => isThirdPersonActive ? runtimeThirdPersonCamera : firstPersonCam;

    /// <summary>Returns the third person body GameObject for external scripts.</summary>
    public GameObject GetThirdPersonBody() => thirdPersonBody;

    /// <summary>Returns true if currently in melee weapon mode (not gun).</summary>
    public bool IsMeleeWeapon => currentWeaponType == GameManager.WeaponType.Melee
                              || currentWeaponType == GameManager.WeaponType.UltimateMelee;

    // ════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        controller  = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        // ── CharacterController calibration ──────────────────────────────────
        // Do this in code so it survives Inspector resets and prefab overrides.
        // height=1.8 → bottom at Y=0 when player.position.y=0
        // skinWidth=0.04 → minimal float without tunnelling
        // minMoveDistance=0 → allow sub-mm correction moves every frame
        if (controller != null)
        {
            controller.height          = 1.8f;
            controller.radius          = 0.4f;
            controller.center          = new Vector3(0f, 0.92f, 0f);
            controller.skinWidth       = 0.04f;
            controller.stepOffset      = 0.25f;
            controller.minMoveDistance = 0f;
        }

        // ── Find the correct animator ─────────────────────────────────────────
        // GetComponentInChildren() picks the FIRST animator it finds — which is
        // the first-person Arms animator (not humanoid, on a camera child object).
        // We want the Animator that drives the player's own mesh.
        // Priority: tagged "Player" → humanoid → anything but Arms.
        animator = null;
        foreach (Animator a in GetComponentsInChildren<Animator>(true))
        {
            if (a.gameObject.name.IndexOf("Arms", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue; // skip first-person arm rig
            animator = a;
            break;
        }
        // If all animators are Arms-type fall back gracefully
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false; // Prevent Mixamo root curves fighting CharacterController

        // Camera setup
        if (firstPersonCam == null)
            firstPersonCam = GetComponentInChildren<Camera>();
        if (firstPersonCam != null)
        {
            firstPersonLocalPos = firstPersonCam.transform.localPosition;
            firstPersonLocalRot = firstPersonCam.transform.localRotation;
        }

        CacheFirstPersonWeaponSlot();

        firstPersonRenderers = GetComponentsInChildren<Renderer>(true);
        resolvedAttackMask = attackLayer == 0 ? ~0 : attackLayer;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Start()
    {
        // ── Initial floor settle ──────────────────────────────────────────────
        // CharacterController.isGrounded is based on the PREVIOUS frame's Move call.
        // On frame 0 no Move has happened yet, so isGrounded = false and gravity
        // starts accumulating before the floor is detected — causing first-frame float.
        // A tiny downward Move here registers the floor contact before Update() runs.
        if (controller != null)
        {
            controller.Move(Vector3.down * 0.02f);
            verticalVelocity.y = -2f; // pre-load the grounded push value
        }

        // Apply perspective first so EnsureThirdPersonBody runs before we attach the weapon.
        ApplyPerspectivePreference();
        
        // Always ensure third person body exists for weapon attachment
        EnsureThirdPersonBody();

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
        UpdateAnimations();
        UpdateAnimatorParameters();
    }

    private void LateUpdate()
    {
        // ── UPRIGHT STABILITY ──
        // Runs after Animator writes its output, so it overrides any Mixamo
        // root curves that would tilt X or Z. Only Y (yaw) is ever allowed to change.
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

        // Body rotation is handled in Update (ApplyMovement) so it runs
        // BEFORE CharacterVisualAnimationPlayer.LateUpdate writes bone positions.
        // Rotating the body AFTER bones are placed causes double-rotation distortion.
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════════════

    private void ReadInput()
    {
        // Read devices directly here because the generated PlayerInput action map
        // throws during binding resolution in this project/editor setup.
        moveInputRaw = ReadMovementInput();
        moveInputRaw = Vector2.ClampMagnitude(moveInputRaw, 1f);

        // Smooth the input to remove digital snapping
        moveInputSmoothed = Vector2.Lerp(moveInputSmoothed, moveInputRaw, InputSmoothing * Time.deltaTime);

        lookInput = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    private void HandleActionInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                jumpRequested = true;

            if (Keyboard.current.vKey.wasPressedThisFrame)
                TogglePerspective();
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            jumpRequested = true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Attack();
    }

    private static Vector2 ReadMovementInput()
    {
        if (Keyboard.current == null)
            return Vector2.zero;

        Vector2 movement = Vector2.zero;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) movement.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) movement.y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) movement.x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) movement.x += 1f;

        return movement;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MOVEMENT
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyMovement()
    {
        isSprinting = Keyboard.current != null
                   && Keyboard.current.leftShiftKey.isPressed
                   && moveInputSmoothed.y > 0.1f;

        float targetSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        // Build desired world-space horizontal velocity from smoothed input
        Vector3 inputDir = new Vector3(moveInputSmoothed.x, 0f, moveInputSmoothed.y);
        Vector3 targetVelocity = transform.TransformDirection(inputDir) * targetSpeed;

        // Accelerate toward target, decelerate when no input
        float rate = moveInputSmoothed.sqrMagnitude > 0.01f ? acceleration : deceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);

        // Apply horizontal movement
        controller.Move(horizontalVelocity * Time.deltaTime);

        // ── Gravity ────────────────────────────────────────────────────────────
        // Reset UNCONDITIONALLY when grounded (not just when y < 0).
        // The old "only reset when negative" logic allowed positive velocity
        // to accumulate on the first frame after a jump lands, causing a
        // momentary upward push that looked like floating.
        if (isGrounded)
        {
            verticalVelocity.y = -2f; // small constant keeps CC touching floor every frame

            if (jumpRequested)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isGrounded = false;
            }
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime; // gravity is negative → accelerates down
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, -40f); // terminal velocity cap
        }

        jumpRequested = false;
        controller.Move(verticalVelocity * Time.deltaTime);

        Vector3 actualHorizontalVelocity = GetActualHorizontalVelocity();

        // Arena constraints
        ClampInsideArena();
        SnapToArenaFloor();

        // Animation state driven by actual velocity, not input
        wasMoving = actualHorizontalVelocity.sqrMagnitude > 0.1f;

        // ── BODY ROTATION (TPS, runs in Update before any LateUpdate) ──
        // Rotating in Update guarantees this happens BEFORE CharacterVisualAnimationPlayer
        // writes bone positions in its own LateUpdate — prevents the double-rotation
        // distortion that causes the "merged/melted bones" look.
        if (isThirdPersonActive && thirdPersonBody != null)
        {
            Vector3 moveFlat = actualHorizontalVelocity;

            if (moveFlat.sqrMagnitude > 0.01f)
            {
                float targetY = Quaternion.LookRotation(moveFlat).eulerAngles.y;
                thirdPersonBody.transform.rotation = Quaternion.Slerp(
                    thirdPersonBody.transform.rotation,
                    Quaternion.Euler(0f, targetY, 0f),
                    10f * Time.deltaTime);
            }
            // Keep last facing on idle to avoid snap-back when strafing.
        }
    }

    private Vector3 GetActualHorizontalVelocity()
    {
        if (controller == null)
            return Vector3.zero;

        Vector3 velocity = controller.velocity;
        velocity.y = 0f;
        return velocity;
    }

    public void TeleportTo(Vector3 position)
    {
        // Safe teleport logic requested
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

        // Vertical pitch (clamped)
        cameraPitch -= mouseY;
        cameraPitch  = Mathf.Clamp(cameraPitch, -80f, 80f);

        // Apply pitch to first-person camera
        if (firstPersonCam != null)
            firstPersonCam.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

        // Horizontal rotation on the player body
        transform.Rotate(Vector3.up * mouseX);

        // Feed pitch to third-person orbit camera
        if (isThirdPersonActive && runtimeThirdPersonCamera != null)
        {
            CameraController orbitCtrl = runtimeThirdPersonCamera.GetComponent<CameraController>();
            if (orbitCtrl != null)
                orbitCtrl.pitch = cameraPitch * 0.45f;
        }
    }

    private void UpdateHeadBob()
    {
        if (firstPersonCam == null) return;

        float targetVel = wasMoving && isGrounded ? 1f : 0f;
        headBobVelocity = Mathf.Lerp(headBobVelocity, targetVel, 8f * Time.deltaTime);

        if (headBobVelocity > 0.01f)
        {
            float freq = isSprinting ? HeadBobFrequency * 1.35f : HeadBobFrequency;
            headBobTimer += Time.deltaTime * freq;
        }
        else
        {
            headBobTimer = Mathf.MoveTowards(headBobTimer, 0f, Time.deltaTime * HeadBobFrequency);
        }

        float bob = Mathf.Sin(headBobTimer) * HeadBobAmplitude * headBobVelocity;
        firstPersonCam.transform.localPosition = firstPersonLocalPos + new Vector3(0f, bob, 0f);
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
        if (comboCooldownTimer > 0f) return;

        CharacterVisualAnimationPlayer visualAnim = thirdPersonBody != null
            ? thirdPersonBody.GetComponentInChildren<CharacterVisualAnimationPlayer>(true)
            : null;

        // Combo: if already attacking and in the combo window, chain next hit
        if (isAttacking)
        {
            if (visualAnim != null && visualAnim.IsComboReady)
            {
                visualAnim.PlayAttack();
                FireAttack();
            }
            return;
        }

        // Fresh attack
        isAttacking = true;
        comboCooldownTimer = ComboCooldown;

        if (visualAnim != null)
            visualAnim.PlayAttack();

        ChangeAnimationState(ATTACK1);
        FireAttack();
    }

    private void FireAttack()
    {
        comboCooldownTimer = ComboCooldown;
        ApplyAttackLunge();

        if (audioSource != null && swordSwing != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(swordSwing);
        }

        // Call the weapon's Attack method to ensure damage is applied
        if (equippedWeaponObject != null)
        {
            WeaponBase weapon = equippedWeaponObject.GetComponent<WeaponBase>();
            if (weapon != null)
                weapon.Attack();

            // Enable weapon hitbox during the swing window (fallback for clips without events)
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
        if (comboCooldownTimer > 0f)
            comboCooldownTimer -= Time.deltaTime;

        if (!isAttacking) return;

        CharacterVisualAnimationPlayer visualAnim = thirdPersonBody != null
            ? thirdPersonBody.GetComponentInChildren<CharacterVisualAnimationPlayer>(true)
            : null;

        if (visualAnim != null && !visualAnim.IsAttacking)
            isAttacking = false;
    }

    private void ApplyAttackLunge()
    {
        if (controller == null) return;
        controller.Move(transform.forward * attackLungeDistance);
        ClampInsideArena();
    }

    // ── Attack raycasts by weapon type ──

    private void AttackRaycast()
    {
        Camera cam = ActiveCamera;
        if (cam == null) return;

        AttackMelee(cam);
        cameraKickTarget = -1.2f;
    }

    /// <summary>Maximum angle (degrees) from player forward that counts as "in front".</summary>
    private const float MeleeHitAngle = 60f; // 60° each side = 120° total cone

    private void AttackMelee(Camera cam)
    {
        // Detection sphere centred slightly in front of the player
        Vector3 meleeOrigin = transform.position + Vector3.up * 1.0f + transform.forward * (attackDistance * 0.5f);
        Collider[] hits = Physics.OverlapSphere(meleeOrigin, attackRadius * 1.2f, resolvedAttackMask, QueryTriggerInteraction.Ignore);
        bool landed = false;

        // Sort by distance so closest enemy gets hit first
        System.Array.Sort(hits, (a, b) =>
        {
            float dA = (a.transform.position - meleeOrigin).sqrMagnitude;
            float dB = (b.transform.position - meleeOrigin).sqrMagnitude;
            return dA.CompareTo(dB);
        });

        for (int i = 0; i < hits.Length; i++)
        {
            // ── DIRECTIONAL CHECK: ignore targets behind the player ──
            Vector3 dirToTarget = (hits[i].transform.position - transform.position);
            dirToTarget.y = 0f; // horizontal plane only
            float angle = Vector3.Angle(transform.forward, dirToTarget);
            if (angle > MeleeHitAngle) continue; // target is behind us — skip

            if (TryDamageTarget(hits[i].transform, attackDamage))
            {
                ApplyHitReaction(hits[i].transform, transform.forward);
                HitTarget(hits[i].ClosestPoint(meleeOrigin));
                landed = true;
                break; // only hit one enemy per swing
            }
        }

        // Visual miss feedback — spark on walls etc.
        if (!landed && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
            HitTarget(hit.point);
    }

    /// <summary>
    /// Attempts to deal damage to a target. Returns true if damage was applied.
    /// Passes byPlayer:true so the kill feed only triggers on player kills.
    /// </summary>
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

    /// <summary>
    /// Sets Animator parameters so an Animator Controller can drive blended animations.
    ///
    /// Issue #3 fix — natural movement:
    ///   • Speed     is NORMALISED to [0,1] (dividing by max possible speed).
    ///               This maps cleanly onto blend trees without depending on raw
    ///               world-speed units (which caused the "puppet/clown walk").
    ///   • VelocityX / VelocityZ are LOCAL-SPACE directional values [-1,1].
    ///               These allow the Animator blend tree to drive left/right and
    ///               forward/back leg animations independently so both legs move
    ///               naturally during strafing and diagonal movement.
    ///
    /// Parameters exposed: Speed, VelocityX, VelocityZ, IsGrounded, IsAttacking, IsSprinting.
    /// </summary>
    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        float maxSpeed = moveSpeed * sprintMultiplier;
        Vector3 actualHorizontalVelocity = GetActualHorizontalVelocity();

        // Normalised overall speed [0,1]
        float normSpeed = Mathf.Clamp01(actualHorizontalVelocity.magnitude / Mathf.Max(0.01f, maxSpeed));

        // Local-space velocity — X = strafe, Z = forward/back, both [-1,1]
        Vector3 localVel = transform.InverseTransformDirection(actualHorizontalVelocity);
        float normX = Mathf.Clamp(localVel.x / Mathf.Max(0.01f, maxSpeed), -1f, 1f);
        float normZ = Mathf.Clamp(localVel.z / Mathf.Max(0.01f, maxSpeed), -1f, 1f);

        // Damp with deltaTime so the blend tree interpolates smoothly
        animator.SetFloat(AnimSpeed,     normSpeed, 0.1f, Time.deltaTime);
        animator.SetFloat(AnimVelocityX, normX,     0.1f, Time.deltaTime);
        animator.SetFloat(AnimVelocityZ, normZ,     0.1f, Time.deltaTime);
        animator.SetBool(AnimGrounded,   isGrounded);
        animator.SetBool(AnimAttacking,  isAttacking);
        animator.SetBool(AnimSprinting,  isSprinting);
    }

    private void UpdateAnimations()
    {
        if (isAttacking) return;

        if (wasMoving)
            ChangeAnimationState(WALK);
        else
            ChangeAnimationState(IDLE);
    }

    public void ChangeAnimationState(string newState)
    {
        if (animator == null || currentAnimationState == newState
            || !animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
            return;

        currentAnimationState = newState;
        animator.CrossFadeInFixedTime(currentAnimationState, 0.2f);
    }

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

        // Kill outward horizontal velocity so the character doesn't push into the wall
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
        // Skip during upward movement or when CC already detects ground.
        if (controller == null || verticalVelocity.y > 0.1f || isGrounded) return;

        // Cast from slightly above the player's feet, downward.
        // Long ray (maxFloorSnapDistance * 10) catches cases where the character
        // drifted far above the floor due to a missed grounding frame.
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        float   castDist = Mathf.Max(maxFloorSnapDistance * 10f, 5f);

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castDist, ~0, QueryTriggerInteraction.Ignore))
            return;

        float targetY  = hit.point.y;
        float currentY = transform.position.y;

        // Only snap DOWN (never push upward with this method)
        if (currentY <= targetY + 0.01f) return;

        // Snap speed scales with distance so close corrections are smooth
        // but large gaps (e.g. after floating) close quickly.
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
        if (isThirdPersonActive)
            EnableFirstPersonView();
        else
            EnableThirdPersonView();
    }

    /// <summary>Called by LevelBuilder after the player is placed in the scene.</summary>
    public void RefreshGameplayPreferences()
    {
        ApplyPerspectivePreference();
        
        // Ensure third person is always fully initialized when rendering
        if (thirdPersonBody != null && isThirdPersonActive)
        {
            thirdPersonBody.SetActive(true);
        }
    }

    private void ApplyPerspectivePreference()
    {
        GameManager.PerspectiveMode mode = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : GameManager.PerspectiveMode.ThirdPerson;

        if (mode == GameManager.PerspectiveMode.ThirdPerson)
            EnableThirdPersonView();
        else
            EnableFirstPersonView();
    }

    private void EnableFirstPersonView()
    {
        if (firstPersonCam == null) return;

        isThirdPersonActive = false;
        firstPersonCam.gameObject.SetActive(true);
        firstPersonCam.transform.SetParent(transform, false);
        firstPersonCam.transform.localPosition = firstPersonLocalPos;
        firstPersonCam.transform.localRotation = firstPersonLocalRot;

        if (runtimeThirdPersonCamera != null)
            runtimeThirdPersonCamera.gameObject.SetActive(false);

        SetFirstPersonRenderersVisible(true);
        EnsureThirdPersonBody();
        if (thirdPersonBody != null)
            thirdPersonBody.SetActive(false);
    }

    private void EnableThirdPersonView()
    {
        if (firstPersonCam == null) return;

        isThirdPersonActive = true;
        EnsureThirdPersonCamera();
        EnsureThirdPersonBody();

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
        runtimeThirdPersonCamera.fieldOfView   = Mathf.Max(firstPersonCam.fieldOfView, 70f);
        runtimeThirdPersonCamera.nearClipPlane  = firstPersonCam.nearClipPlane;
        runtimeThirdPersonCamera.farClipPlane   = firstPersonCam.farClipPlane;
        runtimeThirdPersonCamera.clearFlags     = firstPersonCam.clearFlags;
        runtimeThirdPersonCamera.backgroundColor = firstPersonCam.backgroundColor;
        runtimeThirdPersonCamera.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();

        CameraController follow = camObj.AddComponent<CameraController>();
        follow.target      = transform;
        follow.offset      = new Vector3(0f, 3.4f, -7.2f);
        follow.smoothSpeed = 10f;
        follow.lookAheadDistance = 4.5f;
        follow.lookHeight = 1.6f;

        thirdPersonCam = runtimeThirdPersonCamera;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  THIRD-PERSON BODY
    // ════════════════════════════════════════════════════════════════════════

    private void EnsureThirdPersonBody()
    {
        if (thirdPersonBody != null) return;

        // ── Primary: DragonSouls / Blink stylized human (meshes under Assets/DragonSouls, prefab in Resources) ──
        GameObject dragonSoulsBodyPrefab = Resources.Load<GameObject>("Player/DragonSoulsThirdPersonBody");
        if (dragonSoulsBodyPrefab != null)
        {
            thirdPersonBody = Instantiate(dragonSoulsBodyPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.identity;
            thirdPersonBody.transform.localScale    = Vector3.one;

            Animator bodyAnimator = thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (bodyAnimator != null)
            {
                bodyAnimator.applyRootMotion = false;
                EnsureMeleeAnimEventSink(bodyAnimator.gameObject);

                int lvl = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
                GameManager.WeaponType wType = GameManager.Instance != null
                    ? GameManager.Instance.GetWeaponTypeForLevel(lvl)
                    : GameManager.WeaponType.Melee;
                bool isMelee = (wType == GameManager.WeaponType.Melee || wType == GameManager.WeaponType.UltimateMelee);

                AnimationClip idleClip   = null;
                AnimationClip attackClip = null;

                // Use one-handed weapon combat animations for ALL melee levels
                if (isMelee)
                {
                    idleClip   = KevinMeleeResources.FindClip(KevinMeleeResources.OneHandedFolder, "CombatIdle", "1H@CombatIdle");
                    attackClip = KevinMeleeResources.FindClip(KevinMeleeResources.RightHandFolder, "Attack01", "RightHand");
                }
                if (idleClip == null)
                    idleClip = Resources.Load<AnimationClip>("Player/DragonSoulsClips/Unarmed-Idle");
                if (attackClip == null)
                    attackClip = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack1");

                if (attackClip == null)
                {
                    RuntimeAnimatorController rac = bodyAnimator.runtimeAnimatorController;
                    if (rac != null && rac.animationClips.Length > 0)
                        attackClip = rac.animationClips[0];
                }

                CharacterVisualAnimationPlayer animPlayer = thirdPersonBody.AddComponent<CharacterVisualAnimationPlayer>();
                animPlayer.Setup(bodyAnimator, idleClip, attackClip);
            }

            thirdPersonBody.AddComponent<CharacterVisualGrounder>();
            thirdPersonBody.AddComponent<CharacterVisualBob>();
            return;
        }

        // ── Fallback: Paladin knight ──
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
                importedAnimator.applyRootMotion = false;
                EnsureMeleeAnimEventSink(importedAnimator.gameObject);
                int lvl = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
                GameManager.WeaponType wType = GameManager.Instance != null
                    ? GameManager.Instance.GetWeaponTypeForLevel(lvl)
                    : GameManager.WeaponType.Melee;
                bool isMelee = (wType == GameManager.WeaponType.Melee || wType == GameManager.WeaponType.UltimateMelee);

                AnimationClip idleClip   = null;
                AnimationClip attackClip = null;
                if (isMelee)
                {
                    idleClip   = KevinMeleeResources.FindClip(KevinMeleeResources.OneHandedFolder, "CombatIdle", "1H@CombatIdle");
                    attackClip = KevinMeleeResources.FindClip(KevinMeleeResources.RightHandFolder, "Attack01", "RightHand");
                }
                if (idleClip == null)
                    idleClip = Resources.Load<AnimationClip>("Player/DragonSoulsClips/Unarmed-Idle");
                if (attackClip == null)
                    attackClip = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack1");
                CharacterVisualAnimationPlayer animPlayer = thirdPersonBody.AddComponent<CharacterVisualAnimationPlayer>();
                animPlayer.Setup(importedAnimator, idleClip, attackClip);
            }

            thirdPersonBody.AddComponent<CharacterVisualGrounder>();
            thirdPersonBody.AddComponent<CharacterVisualBob>();
            return;
        }

        // ── Last resort: primitive mannequin ──
        thirdPersonBody = new GameObject("ThirdPersonBody");
        thirdPersonBody.transform.SetParent(transform, false);
        thirdPersonBody.transform.localPosition = Vector3.zero;

        CreateBodyPart(thirdPersonBody.transform, "Torso", PrimitiveType.Capsule,
            new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 1.0f, 0.70f), new Color(0.76f, 0.78f, 0.86f));
        CreateBodyPart(thirdPersonBody.transform, "Head", PrimitiveType.Sphere,
            new Vector3(0f, 1.82f, 0f), new Vector3(0.42f, 0.42f, 0.42f), new Color(0.86f, 0.76f, 0.66f));
        CreateBodyPart(thirdPersonBody.transform, "LeftArm", PrimitiveType.Cylinder,
            new Vector3(-0.52f, 1.10f, 0f), new Vector3(0.14f, 0.54f, 0.14f), new Color(0.70f, 0.72f, 0.80f), new Vector3(0f, 0f, 22f));
        CreateBodyPart(thirdPersonBody.transform, "RightArm", PrimitiveType.Cylinder,
            new Vector3(0.48f, 1.04f, 0.12f), new Vector3(0.14f, 0.60f, 0.14f), new Color(0.70f, 0.72f, 0.80f), new Vector3(22f, 0f, -58f));
        CreateBodyPart(thirdPersonBody.transform, "LeftLeg", PrimitiveType.Cylinder,
            new Vector3(-0.18f, 0.30f, 0f), new Vector3(0.18f, 0.62f, 0.18f), new Color(0.10f, 0.10f, 0.12f));
        CreateBodyPart(thirdPersonBody.transform, "RightLeg", PrimitiveType.Cylinder,
            new Vector3(0.18f, 0.30f, 0f), new Vector3(0.18f, 0.62f, 0.18f), new Color(0.10f, 0.10f, 0.12f));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WEAPON SYSTEM
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Equips the weapon for a given level. Called at Start and by WeaponChest.
    /// Configures damage, range, weapon type, and attaches the visual model.
    /// </summary>
    public void EquipWeaponForLevel(int level)
    {
        level = Mathf.Max(1, level);

        if (GameManager.Instance != null)
        {
            equippedWeaponName = GameManager.Instance.GetWeaponNameForLevel(level);
            attackDamage       = Mathf.RoundToInt(GameManager.Instance.GetWeaponDamageForLevel(level));
            attackDistance     = GameManager.Instance.GetWeaponRangeForLevel(level);
            currentWeaponType  = GameManager.WeaponType.Melee;
            attackSpeed = 1.0f; attackDelay = 0.4f; attackRadius = 1.25f;
        }
        else
        {
            equippedWeaponName = "Combat Knife";
            currentWeaponType  = GameManager.WeaponType.Melee;
        }

        if (thirdPersonBody != null)
        {
            // Make sure third person body is active in third person mode
            if (isThirdPersonActive)
            {
                thirdPersonBody.SetActive(true);
                Debug.Log("[EquipWeaponForLevel] Activated third person body for weapon attachment");
            }

            AttachWeaponToHand(thirdPersonBody, level);

            // Setup two-handed IK for ranged weapons
            SetupWeaponIK(currentWeaponType);
        }
        else
        {
            Debug.LogError("[EquipWeaponForLevel] Third person body is NULL! Cannot attach weapon.");
        }

        RefreshFirstPersonWeaponModel(level);

        // Activate/deactivate GunController based on weapon type
        SetupGunController(currentWeaponType, level);
    }

    /// <summary>
    /// Sets up the WeaponIKHandler on the third-person body for two-handed weapon grip.
    /// </summary>
    private void SetupWeaponIK(GameManager.WeaponType weaponType)
    {
        if (thirdPersonBody == null) return;

        WeaponIKHandler ikHandler = thirdPersonBody.GetComponent<WeaponIKHandler>();
        if (ikHandler == null)
            ikHandler = thirdPersonBody.AddComponent<WeaponIKHandler>();

        if (equippedWeaponObject != null)
            ikHandler.SetupForWeapon(equippedWeaponObject, weaponType);
        else
            ikHandler.DisableIK();
    }

    /// <summary>
    /// All levels are now melee — GunController is always deactivated/removed.
    /// </summary>
    private void SetupGunController(GameManager.WeaponType weaponType, int level)
    {
        GunController gun = GetComponent<GunController>();
        if (gun != null)
        {
            gun.Deactivate();
            Debug.Log("[PlayerController] GunController deactivated — all weapons are melee.");
        }
    }

    /// <summary>
    /// Re-equips the current level's weapon when pressing Q key.
    /// This fixes visibility issues and ensures weapon is properly attached.
    /// </summary>
    public void ReequipCurrentWeapon()
    {
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        
        // Show debug message
        Debug.Log($"[PlayerController] Re-equipping weapon for level {level}: {GameManager.Instance.GetWeaponNameForLevel(level)}");
        
        // Check if third person body exists
        if (thirdPersonBody != null)
        {
            Debug.Log($"[PlayerController] Third person body found: {thirdPersonBody.name}");
        }
        else
        {
            Debug.LogWarning("[PlayerController] Third person body is NULL! This might be the problem.");
        }
        
        // Force re-equip weapon
        EquipWeaponForLevel(level);
    }

    /// <summary>
    /// Forces third person view for testing weapon visibility.
    /// </summary>
    public void ForceThirdPersonView()
    {
        Debug.Log("[PlayerController] Forcing third person view for weapon testing");
        
        // Force third person mode
        isThirdPersonActive = true;
        
        // Ensure third person body exists and is active
        EnsureThirdPersonBody();
        if (thirdPersonBody != null)
        {
            thirdPersonBody.SetActive(true);
            Debug.Log("[PlayerController] Third person body activated");
        }
        
        // Setup third person camera
        EnsureThirdPersonCamera();
        if (runtimeThirdPersonCamera != null)
        {
            runtimeThirdPersonCamera.gameObject.SetActive(true);
        }
        
        // Hide first person
        if (firstPersonCam != null)
        {
            firstPersonCam.gameObject.SetActive(false);
        }
        
        SetFirstPersonRenderersVisible(false);
        
        // Re-equip weapon to ensure it's attached
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        EquipWeaponForLevel(level);
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

        Debug.Log($"[AttachWeaponToHand] Attaching weapon level {level} to body: {body.name}");

        // Find the best weapon socket — prefer dedicated weapon joints (jointItemR),
        // then humanoid hand bone, then naming convention fallbacks.
        Transform handBone = null;
        Animator bodyAnimator = body.GetComponentInChildren<Animator>(true);

        // 0. Dedicated weapon socket joints (DragonSouls / Blink models)
        handBone = FindBone(body.transform, "jointItemR")
                ?? FindBone(body.transform, "RIGHT_HAND_COMBAT");

        // 1. Humanoid avatar mapping
        if (handBone == null && bodyAnimator != null && bodyAnimator.isHuman)
            handBone = bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand);

        // 2. DragonSouls / Blink bone names
        if (handBone == null)
        {
            handBone = FindBone(body.transform, "Wrist_R")
                    ?? FindBone(body.transform, "RIGHT_HAND_REST")
                    ?? FindBoneContaining(body.transform, "right_hand_combat")
                    ?? FindBoneContaining(body.transform, "right_hand_rest");
        }

        // 3. Exact name search — Mixamo and Unity conventions
        if (handBone == null)
        {
            handBone = FindBone(body.transform, "mixamorig:RightHand")
                    ?? FindBone(body.transform, "RightHand")
                    ?? FindBone(body.transform, "Hand_R")
                    ?? FindBone(body.transform, "right_hand")
                    ?? FindBone(body.transform, "Bip001 R Hand")
                    ?? FindBone(body.transform, "Bip01 R Hand")
                    ?? FindBone(body.transform, "R_Hand")
                    ?? FindBone(body.transform, "HandRight");
        }

        // 4. Case-insensitive partial-name search
        if (handBone == null)
            handBone = FindBoneContaining(body.transform, "righthand")
                    ?? FindBoneContaining(body.transform, "hand_r")
                    ?? FindBoneContaining(body.transform, "r_hand")
                    ?? FindBoneContaining(body.transform, "wrist_r")
                    ?? FindBoneContaining(body.transform, "jointitemr");

        // 5. Last resort: right upper arm keeps weapon near the hand area
        if (handBone == null && bodyAnimator != null && bodyAnimator.isHuman)
            handBone = bodyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);

        Transform attachPoint = handBone != null ? handBone : body.transform;
        
        Debug.Log($"[AttachWeaponToHand] Hand bone found: {(handBone != null ? handBone.name : "NULL")}");
        Debug.Log($"[AttachWeaponToHand] Attach point: {(attachPoint != null ? attachPoint.name : "NULL")}");
        
        equippedWeaponObject = BuildWeaponModel(level, attachPoint);
        
        if (equippedWeaponObject != null)
        {
            Debug.Log($"[AttachWeaponToHand] Weapon created successfully: {equippedWeaponObject.name}");
        }
        else
        {
            Debug.LogError("[AttachWeaponToHand] Failed to create weapon!");
        }
    }

    // ── Weapon model factory ──

    // Target max dimension (meters) for each weapon so it fits naturally in the hand.
    private static readonly float[] WeaponTargetSize = {
        0.30f, // 1  Tactical Knife
        0.95f, // 2  Katana
        1.00f, // 3  Shovel
        0.85f, // 4  Baseball Bat
        0.30f, // 5  Nunchucks
        0.40f, // 6  Wrench
        0.55f, // 7  Crowbar
        0.85f, // 8  Hammer
        0.70f, // 9  Axe
        1.40f, // 10 Spear
        1.00f, // 11 Nailed Plank
        0.40f, // 12 Saw
        0.35f, // 13 Sickle
        0.50f, // 14 Morgenstern (flail)
        0.60f, // 15 L3FTE
        0.90f  // 16 Riot Shield
    };

    // Local Euler rotation offsets per weapon for correct melee grip orientation.
    private static readonly Vector3[] WeaponRotationOffset = {
        new Vector3(-90f, 0f, 0f), // 1  Tactical Knife — blade forward
        new Vector3(-90f, 0f, 0f), // 2  Katana — blade forward
        new Vector3(-90f, 0f, 0f), // 3  Shovel
        new Vector3(-90f, 0f, 0f), // 4  Baseball Bat
        new Vector3(-90f, 0f, 0f), // 5  Nunchucks
        new Vector3(-90f, 0f, 0f), // 6  Wrench
        new Vector3(-90f, 0f, 0f), // 7  Crowbar
        new Vector3(-90f, 0f, 0f), // 8  Hammer
        new Vector3(-90f, 0f, 0f), // 9  Axe
        new Vector3(-90f, 0f, 0f), // 10 Spear
        new Vector3(-90f, 0f, 0f), // 11 Nailed Plank
        new Vector3(-90f, 0f, 0f), // 12 Saw
        new Vector3(-90f, 0f, 0f), // 13 Sickle
        new Vector3(-90f, 0f, 0f), // 14 Morgenstern — held upright
        new Vector3(-90f, 0f, 0f), // 15 L3FTE — melee grip
        new Vector3(-90f, 0f, 0f)  // 16 Riot Shield — held in front
    };

    // Local position offsets per weapon for hand grip alignment.
    private static readonly Vector3[] WeaponPositionOffset = {
        new Vector3(0.00f, 0.05f, 0.00f), // 1  Knife
        new Vector3(0.00f, 0.05f, 0.00f), // 2  Katana
        new Vector3(0.00f, 0.05f, 0.00f), // 3  Shovel
        new Vector3(0.00f, 0.05f, 0.00f), // 4  Baseball Bat
        new Vector3(0.00f, 0.05f, 0.00f), // 5  Nunchucks
        new Vector3(0.00f, 0.05f, 0.00f), // 6  Wrench
        new Vector3(0.00f, 0.05f, 0.00f), // 7  Crowbar
        new Vector3(0.00f, 0.05f, 0.00f), // 8  Hammer
        new Vector3(0.00f, 0.05f, 0.00f), // 9  Axe
        new Vector3(0.00f, 0.05f, 0.00f), // 10 Spear
        new Vector3(0.00f, 0.05f, 0.00f), // 11 Nailed Plank
        new Vector3(0.00f, 0.05f, 0.00f), // 12 Saw
        new Vector3(0.00f, 0.05f, 0.00f), // 13 Sickle
        new Vector3(0.00f, 0.05f, 0.00f), // 14 Morgenstern
        new Vector3(0.00f, 0.05f, 0.00f), // 15 L3FTE
        new Vector3(0.00f, 0.05f, 0.00f)  // 16 Riot Shield
    };

    private void NormalizeWeaponScale(GameObject weapon, float targetMaxDimension)
    {
        // ── Step 0: Strip arm rigs / armatures from weapon FBX ──────────────
        // Weapon FBX files often include a full character arm (SkinnedMeshRenderer)
        // that causes scale explosions when parented to a different skeleton.
        StripWeaponArmature(weapon);

        // Temporarily unparent so we measure the model at identity transform
        // without any parent bone scale distortion.
        Transform savedParent = weapon.transform.parent;
        weapon.transform.SetParent(null, false);
        weapon.transform.position = Vector3.zero;
        weapon.transform.rotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;

        // Measure bounds from MeshFilter ONLY (not SkinnedMeshRenderer —
        // those are always arm skins, not weapon blades)
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasAny = false;

        foreach (MeshFilter mf in weapon.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 worldCenter = mf.transform.TransformPoint(mb.center);
            Vector3 worldExtents = Vector3.Scale(mb.extents, mf.transform.lossyScale);
            Bounds wb = new Bounds(worldCenter, worldExtents * 2f);
            if (!hasAny) { combinedBounds = wb; hasAny = true; }
            else combinedBounds.Encapsulate(wb);
        }

        // Reparent before applying changes
        weapon.transform.SetParent(savedParent, false);

        if (!hasAny) return;

        float maxDim = Mathf.Max(combinedBounds.size.x, Mathf.Max(combinedBounds.size.y, combinedBounds.size.z));
        if (maxDim < 0.001f) return;

        float scale = targetMaxDimension / maxDim;
        weapon.transform.localScale = Vector3.one * scale;

        // Safety clamp — prevent any axis of lossy scale from exceeding 2x target
        Vector3 lossy = weapon.transform.lossyScale;
        float maxLossy = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
        float maxAllowed = targetMaxDimension * 2f;
        if (maxLossy > maxAllowed && maxLossy > 0.001f)
        {
            weapon.transform.localScale *= (maxAllowed / maxLossy);
            Debug.LogWarning($"[NormalizeWeaponScale] Clamped lossy scale from {maxLossy:F1} to {maxAllowed:F1}");
        }

        Debug.Log($"[NormalizeWeaponScale] Native size={maxDim:F2}m, target={targetMaxDimension:F2}m, applied scale={scale:F4}");
    }

    /// <summary>
    /// Destroys arm rigs, armatures, and SkinnedMeshRenderers from weapon FBX.
    /// These are character arm meshes for first-person view — they explode in
    /// scale when parented to a different character's hand bone.
    /// </summary>
    private static void StripWeaponArmature(GameObject weapon)
    {
        string[] poisonKeywords = { "_ARM", "_Arm", "_arm", "Armature", "armature", "_Rig", "_rig" };
        var toDestroy = new System.Collections.Generic.List<GameObject>();

        foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
        {
            if (child == weapon.transform) continue;
            foreach (string keyword in poisonKeywords)
            {
                if (child.name.Contains(keyword))
                {
                    toDestroy.Add(child.gameObject);
                    Debug.Log($"[StripWeaponArmature] Removing '{child.name}' from '{weapon.name}'");
                    break;
                }
            }
        }

        // Also destroy ALL SkinnedMeshRenderers — weapon blades use MeshRenderer,
        // SkinnedMeshRenderers are always character arm skins
        foreach (SkinnedMeshRenderer smr in weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!toDestroy.Contains(smr.gameObject))
            {
                toDestroy.Add(smr.gameObject);
                Debug.Log($"[StripWeaponArmature] Removing SkinnedMesh '{smr.gameObject.name}'");
            }
        }

        foreach (GameObject obj in toDestroy)
        {
            if (obj != null && obj != weapon)
                Object.DestroyImmediate(obj);
        }
    }

    private GameObject BuildWeaponModel(int level, Transform attachPoint)
    {
        Debug.Log($"[BuildWeaponModel] Building weapon level {level}");

        GameObject levelPrefab = ResolveWeaponPrefabForLevel(level);
        if (levelPrefab != null)
        {
            GameObject weapon = Instantiate(levelPrefab, attachPoint);
            weapon.name = "WeaponModel";

            int idx = Mathf.Clamp(level - 1, 0, WeaponTargetSize.Length - 1);
            NormalizeWeaponScale(weapon, WeaponTargetSize[idx]);
            weapon.transform.localPosition = WeaponPositionOffset[idx];
            weapon.transform.localRotation = Quaternion.Euler(WeaponRotationOffset[idx]);

            WeaponBase weaponBase = weapon.GetComponent<WeaponBase>();
            if (weaponBase == null)
                weaponBase = weapon.AddComponent<WeaponBase>();
            weaponBase.weaponName = equippedWeaponName;
            weaponBase.damage = attackDamage;
            weaponBase.attackRange = attackDistance;
            weaponBase.isRanged = (currentWeaponType != GameManager.WeaponType.Melee
                                && currentWeaponType != GameManager.WeaponType.UltimateMelee);

            if (weapon.GetComponent<WeaponVisibilityFix>() == null)
                weapon.AddComponent<WeaponVisibilityFix>();

            // Add hitbox collider for swing-based damage detection
            if (currentWeaponType == GameManager.WeaponType.Melee || currentWeaponType == GameManager.WeaponType.UltimateMelee)
            {
                WeaponHitbox hitbox = weapon.GetComponent<WeaponHitbox>();
                if (hitbox == null)
                    hitbox = weapon.AddComponent<WeaponHitbox>();
                hitbox.damage = attackDamage;
            }

            // Invalidate MeleeAnimationEventSink cache so it finds the new hitbox
            if (thirdPersonBody != null)
            {
                MeleeAnimationEventSink sink = thirdPersonBody.GetComponentInChildren<MeleeAnimationEventSink>(true);
                if (sink != null) sink.ClearCache();
            }

            return weapon;
        }

        return BuildPrimitiveWeapon(level, attachPoint);
    }

    private GameObject ResolveCombatKnifePrefab()
    {
        Debug.Log("[ResolveCombatKnifePrefab] Resolving combat knife prefab...");
        
        if (combatKnifePrefabOverride != null)
        {
            Debug.Log($"[ResolveCombatKnifePrefab] Using override: {combatKnifePrefabOverride.name}");
            return combatKnifePrefabOverride;
        }
        
        if (!string.IsNullOrEmpty(combatKnifeResourcePath))
        {
            Debug.Log($"[ResolveCombatKnifePrefab] Loading from resource path: {combatKnifeResourcePath}");
            GameObject p = Resources.Load<GameObject>(combatKnifeResourcePath);
            if (p != null)
            {
                Debug.Log($"[ResolveCombatKnifePrefab] Successfully loaded from Resources: {p.name}");
                return p;
            }
            else
            {
                Debug.LogError($"[ResolveCombatKnifePrefab] Failed to load from Resources: {combatKnifeResourcePath}");
            }
        }
        
        // Fallback: Blink dagger from DragonSouls pack (always under Resources)
        Debug.Log("[ResolveCombatKnifePrefab] Using fallback Blink dagger");
        GameObject fallback = Resources.Load<GameObject>("Weapons/BlinkDaggerPack/_PrefabsDaggers/Dagger4_1_3");
        if (fallback != null)
        {
            Debug.Log($"[ResolveCombatKnifePrefab] Successfully loaded fallback: {fallback.name}");
        }
        else
        {
            Debug.LogError("[ResolveCombatKnifePrefab] Failed to load fallback dagger!");
        }
        return fallback;
    }

    private GameObject ResolveWeaponPrefabForLevel(int level)
    {
        // Primary: load from the Imported folder (all 20 weapons are there)
        string importedPath = GetImportedWeaponResourcePath(level);
        if (!string.IsNullOrEmpty(importedPath))
        {
            GameObject imported = Resources.Load<GameObject>(importedPath);
            if (imported != null) return imported;
            Debug.LogWarning($"[ResolveWeaponPrefabForLevel] Failed to load imported weapon at: {importedPath}");
        }

        // Fallback: try by weapon name in Resources/Weapons/
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

    /// <summary>
    /// Shows the imported weapon model on the first-person "Weapon" socket for all levels.
    /// </summary>
    private void RefreshFirstPersonWeaponModel(int level)
    {
        ClearFirstPersonKnifeVisual();
        if (firstPersonWeaponSlot == null)
            return;

        GameObject weaponPrefab = ResolveWeaponPrefabForLevel(level);
        if (weaponPrefab == null)
            return;

        if (firstPersonWeaponMeshRenderer != null)
            firstPersonWeaponMeshRenderer.enabled = false;

        firstPersonKnifeInstance = Instantiate(weaponPrefab, firstPersonWeaponSlot);
        firstPersonKnifeInstance.name = "FirstPersonWeaponMesh";
        int idx = Mathf.Clamp(level - 1, 0, WeaponTargetSize.Length - 1);
        float fpScale = WeaponTargetSize[idx] * 0.7f;
        NormalizeWeaponScale(firstPersonKnifeInstance, fpScale);
        Transform t = firstPersonKnifeInstance.transform;
        t.localPosition = new Vector3(0.02f, -0.05f, 0.15f);
        t.localRotation = Quaternion.Euler(WeaponRotationOffset[idx]);
    }

    private GameObject BuildPrimitiveWeapon(int level, Transform attachPoint)
    {
        GameObject root = new GameObject("WeaponModel");
        root.transform.SetParent(attachPoint, false);
        root.transform.localPosition = new Vector3(0f, 0.08f, 0.02f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        // Add weapon collider for damage detection
        BoxCollider weaponCollider = root.AddComponent<BoxCollider>();
        weaponCollider.isTrigger = true;
        weaponCollider.center = Vector3.zero;
        weaponCollider.size = Vector3.one * 0.1f;

        // Add WeaponBase component for damage dealing
        WeaponBase weaponBase = root.AddComponent<WeaponBase>();
        weaponBase.weaponName = equippedWeaponName;
        weaponBase.damage = attackDamage;
        weaponBase.attackRange = attackDistance;
        weaponBase.isRanged = (currentWeaponType == GameManager.WeaponType.Sniper || currentWeaponType == GameManager.WeaponType.Explosive);
        
        // Add WeaponVisibilityFix to ensure weapon is visible
        WeaponVisibilityFix visibilityFix = root.AddComponent<WeaponVisibilityFix>();

        Color silver = new Color(0.80f, 0.82f, 0.88f);
        Color dark   = new Color(0.28f, 0.28f, 0.32f);
        Color brown  = new Color(0.52f, 0.32f, 0.18f);
        Color yellow = new Color(0.95f, 0.80f, 0.20f);
        Color orange = new Color(0.92f, 0.42f, 0.14f);
        Color red    = new Color(0.85f, 0.18f, 0.18f);

        switch (level)
        {
            case 1:
                MakeWeaponPart(root.transform, "Blade",  PrimitiveType.Cube, new Vector3(0f, 0f, 0.12f),  new Vector3(0.018f, 0.008f, 0.22f), silver);
                MakeWeaponPart(root.transform, "Guard",  PrimitiveType.Cube, Vector3.zero,                 new Vector3(0.055f, 0.012f, 0.018f), dark);
                MakeWeaponPart(root.transform, "Handle", PrimitiveType.Cube, new Vector3(0f, 0f, -0.07f),  new Vector3(0.022f, 0.022f, 0.10f),  brown);
                break;
            case 2:
                for (int i = 0; i < 4; i++)
                    MakeWeaponPart(root.transform, "Ring_" + i, PrimitiveType.Cylinder,
                        new Vector3((i - 1.5f) * 0.026f, 0f, 0.02f), new Vector3(0.018f, 0.008f, 0.018f), yellow, new Vector3(90f, 0f, 0f));
                MakeWeaponPart(root.transform, "Bar", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.02f), new Vector3(0.11f, 0.012f, 0.032f), yellow);
                break;
            case 3:
                MakeWeaponPart(root.transform, "Shaft",   PrimitiveType.Cylinder, Vector3.zero,                new Vector3(0.014f, 0.09f, 0.014f),  dark);
                MakeWeaponPart(root.transform, "WeightL", PrimitiveType.Cylinder, new Vector3(0f, 0.10f, 0f),  new Vector3(0.045f, 0.022f, 0.045f), dark);
                MakeWeaponPart(root.transform, "WeightR", PrimitiveType.Cylinder, new Vector3(0f, -0.10f, 0f), new Vector3(0.045f, 0.022f, 0.045f), dark);
                break;
            case 4:
                MakeWeaponPart(root.transform, "Glove", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.04f),  new Vector3(0.07f, 0.07f, 0.09f), red);
                MakeWeaponPart(root.transform, "Wrist", PrimitiveType.Cube,   new Vector3(0f, 0f, -0.02f), new Vector3(0.055f, 0.045f, 0.04f), red);
                break;
            case 5:
                MakeWeaponPart(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.02f), new Vector3(0.014f, 0.10f, 0.014f), silver);
                MakeWeaponPart(root.transform, "HeadA",  PrimitiveType.Cube,     new Vector3(-0.022f, 0f, 0.11f), new Vector3(0.012f, 0.032f, 0.04f), silver);
                MakeWeaponPart(root.transform, "HeadB",  PrimitiveType.Cube,     new Vector3(0.022f, 0f, 0.11f),  new Vector3(0.012f, 0.032f, 0.04f), silver);
                break;
            case 6:
                MakeWeaponPart(root.transform, "Handle",  PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.06f), new Vector3(0.014f, 0.10f, 0.014f), brown);
                MakeWeaponPart(root.transform, "Frame",   PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.11f),  new Vector3(0.11f, 0.008f, 0.14f),  silver, new Vector3(90f, 0f, 0f));
                MakeWeaponPart(root.transform, "Strings", PrimitiveType.Cube,     new Vector3(0f, 0f, 0.11f),  new Vector3(0.09f, 0.003f, 0.12f),  new Color(0.95f, 0.95f, 0.75f));
                break;
            case 7:
                MakeWeaponPart(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.05f), new Vector3(0.016f, 0.10f, 0.016f), brown);
                MakeWeaponPart(root.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.12f),  new Vector3(0.038f, 0.12f, 0.038f), brown);
                break;
            default:
                float barLen = 0.14f + (level - 8) * 0.004f;
                Color barCol = level >= 16 ? orange : silver;
                MakeWeaponPart(root.transform, "Bar", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, barLen * 0.5f), new Vector3(0.018f, barLen, 0.018f), barCol);
                if (level >= 15)
                    MakeWeaponPart(root.transform, "Head", PrimitiveType.Sphere,
                        new Vector3(0f, 0f, barLen), new Vector3(0.04f, 0.04f, 0.04f), barCol);
                break;
        }
        return root;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UTILITY HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void SetFirstPersonRenderersVisible(bool visible)
    {
        if (firstPersonRenderers == null) return;
        for (int i = 0; i < firstPersonRenderers.Length; i++)
        {
            Renderer r = firstPersonRenderers[i];
            if (r == null) continue;
            if (thirdPersonBody != null && r.transform.IsChildOf(thirdPersonBody.transform)) continue;
            r.enabled = visible;
        }
    }

    private Transform FindBone(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindBone(root.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Case-insensitive substring search across the entire bone hierarchy.
    /// Used when exact name matching fails (avatar not Humanoid or unusual naming).
    /// searchToken must be lowercase.
    /// </summary>
    private Transform FindBoneContaining(Transform root, string searchToken)
    {
        if (root.name.Replace(":", "").Replace(" ", "").ToLowerInvariant().Contains(searchToken))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindBoneContaining(root.GetChild(i), searchToken);
            if (found != null) return found;
        }
        return null;
    }

    private static void EnsureMeleeAnimEventSink(GameObject animatorHost)
    {
        if (animatorHost == null) return;
        if (animatorHost.GetComponent<MeleeAnimationEventSink>() == null)
            animatorHost.AddComponent<MeleeAnimationEventSink>();
    }

    private void HideKnightWeaponProp(GameObject knightBody)
    {
        string[] propNames = { "Sword", "Shield", "sword", "shield", "Weapon", "weapon", "Prop", "prop", "WProp", "wprop" };
        foreach (Transform t in knightBody.GetComponentsInChildren<Transform>(true))
        {
            foreach (string n in propNames)
            {
                if (t.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                }
            }
        }
    }

    private void MakeWeaponPart(Transform parent, string name, PrimitiveType type,
        Vector3 pos, Vector3 scale, Color color)
    {
        MakeWeaponPart(parent, name, type, pos, scale, color, Vector3.zero);
    }

    private void MakeWeaponPart(Transform parent, string name, PrimitiveType type,
        Vector3 pos, Vector3 scale, Color color, Vector3 euler)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;
        Collider col = part.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Renderer rend = part.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            rend.material = mat;
        }
    }

    private GameObject CreateBodyPart(Transform parent, string name, PrimitiveType type,
        Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;
        Collider c = part.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Renderer r = part.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color;
            r.material = mat;
        }
        return part;
    }
}

/// <summary>
/// Kevin Iglesias "Melee Warrior — One Handed" FBX clips (same pack as DragonSouls).
/// Lives under Resources/Player/KevinMeleeDS so they load at runtime for Level 1 knife.
/// </summary>
public static class KevinMeleeResources
{
    public const string OneHandedFolder = "Player/KevinMeleeDS/Animations/OneHanded";
    public const string MovementFolder  = "Player/KevinMeleeDS/Animations/OneHanded/Movement";
    public const string RightHandFolder = "Player/KevinMeleeDS/Animations/OneHanded/RightHand";

    public static AnimationClip FindClip(string resourcesDir, params string[] nameHintsInOrder)
    {
        if (string.IsNullOrEmpty(resourcesDir)) return null;
        AnimationClip[] clips = Resources.LoadAll<AnimationClip>(resourcesDir);
        if (clips == null || clips.Length == 0) return null;
        foreach (string hint in nameHintsInOrder)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip c = clips[i];
                if (c == null) continue;
                if (c.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
        }
        return null;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  HELPER COMPONENTS (used by the third-person character body)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Adds a subtle vertical bob to the third-person body based on movement speed.
/// </summary>
public class CharacterVisualBob : MonoBehaviour
{
    private Vector3 baseLocalPosition;
    private Transform actorRoot;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        actorRoot = transform.parent;
    }

    private void LateUpdate()
    {
        if (actorRoot == null) return;

        float speed = 0f;
        CharacterController cc = actorRoot.GetComponent<CharacterController>();
        if (cc != null)
            speed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        else
        {
            Rigidbody rb = actorRoot.GetComponent<Rigidbody>();
            if (rb != null) speed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        }

        float bob = speed > 0.1f ? Mathf.Sin(Time.time * 10f) * 0.03f : 0f;
        transform.localPosition = baseLocalPosition + new Vector3(0f, bob, 0f);
    }
}

/// <summary>
/// One-shot grounder: on first frame, aligns the model's feet with the parent's Y position.
/// </summary>
public class CharacterVisualGrounder : MonoBehaviour
{
    private bool grounded;

    private void LateUpdate()
    {
        if (grounded) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        float lowest = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
            lowest = Mathf.Min(lowest, renderers[i].bounds.min.y);

        float delta = transform.parent.position.y - lowest;
        transform.position += new Vector3(0f, delta, 0f);
        grounded = true;
    }
}

/// <summary>
/// PlayableGraph-based animation system with combo attacks and procedural walk cycle.
/// Drives idle/walk blend via CharacterController velocity, and supports a multi-clip combo chain.
/// The procedural walk rotates humanoid leg and arm bones in LateUpdate so the character
/// walks naturally even without a dedicated walk animation clip.
/// </summary>
public class CharacterVisualAnimationPlayer : MonoBehaviour
{
    // Playable graph — uses AnimationLayerMixerPlayable for upper/lower body split.
    // Layer 0: Locomotion (idle/walk) — full body
    // Layer 1: Combat (attack clips) — upper body only (via AvatarMask)
    // This prevents "paralyzed legs" during attacks.
    private Animator targetAnimator;
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip[] attackClips;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable walkPlayable;
    private AnimationClipPlayable[] attackPlayables;
    private AnimationMixerPlayable locoMixer;     // idle/walk blend (layer 0)
    private AnimationMixerPlayable attackMixer;    // attack clips (layer 1)
    private AnimationLayerMixerPlayable layerMixer; // combines layers with avatar mask
    private AvatarMask upperBodyMask;
    private bool isInitialized;

    // Combat state
    private bool isAttacking;
    private int currentAttackIndex = -1;
    private int comboStep;
    private bool comboQueued;
    private const float ComboWindowStart = 0.5f;
    private const float AutoResetTime    = 0.85f;

    // Movement blend
    private Transform parentRoot;
    private float currentWalkWeight;

    // Procedural walk bones
    private Transform leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg;
    private Transform leftUpperArm, rightUpperArm;
    private Transform hips, spine;
    private float walkCycleTimer;
    private const float WalkCycleSpeed = 7.5f;
    private const float UpperLegSwing  = 22f;
    private const float LowerLegBend   = 30f;
    private const float ArmSwing       = 12f;
    private const float HipBounce      = 0.012f;
    private const float SpineTilt      = 2f;

    // Public state
    public bool IsAttacking => isAttacking;
    public bool IsComboReady => !isAttacking || (currentAttackIndex >= 0 && GetAttackNormalizedTime() >= ComboWindowStart);

    /// <summary>
    /// Initialises the PlayableGraph with idle, walk, and attack clips.
    /// </summary>
    public void Setup(Animator animator, AnimationClip idle, AnimationClip attack)
    {
        targetAnimator = animator;
        idleClip = idle;

        // Disable root motion BEFORE the PlayableGraph takes control.
        // Mixamo FBX bakes position/rotation into the root bone — if root motion
        // is on, the animator moves the body container every frame, which fights
        // the CharacterController and causes the drifting/tilting/merged-bone look.
        if (targetAnimator != null)
        {
            targetAnimator.applyRootMotion = false;
            // Also zero the body's local transform here so the mesh sits at the
            // correct position relative to the Player root (not sunken into ground).
            targetAnimator.transform.localPosition = Vector3.zero;
            targetAnimator.transform.localRotation = Quaternion.identity;
        }

        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        GameManager.WeaponType wType = GameManager.Instance != null
            ? GameManager.Instance.GetWeaponTypeForLevel(level)
            : GameManager.WeaponType.Melee;
        bool isMelee = (wType == GameManager.WeaponType.Melee || wType == GameManager.WeaponType.UltimateMelee);

        var clips = new List<AnimationClip>();

        // For melee weapon levels: use one-handed weapon swing animations
        if (isMelee)
        {
            AnimationClip kRight = KevinMeleeResources.FindClip(KevinMeleeResources.RightHandFolder, "Attack01", "RightHand");
            if (kRight != null) clips.Add(kRight);

            // Blink one-handed melee swing (copied to Resources/Player/WeaponAnimations)
            AnimationClip[] blinkClips = Resources.LoadAll<AnimationClip>("Player/WeaponAnimations");
            if (blinkClips != null)
            {
                foreach (AnimationClip bc in blinkClips)
                {
                    if (bc != null && bc.name.IndexOf("Melee", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        clips.Add(bc);
                }
            }

            // Pad to 3 combo steps if we have at least 1 clip
            if (clips.Count > 0)
            {
                while (clips.Count < 3)
                    clips.Add(clips[clips.Count - 1]);
            }
        }

        // Fallback to unarmed clips (for ranged weapons or if no weapon swing clips found)
        if (clips.Count == 0)
        {
            AnimationClip ds1 = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack1");
            AnimationClip ds2 = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack2");
            AnimationClip ds3 = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack3");
            AnimationClip dsH = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedHeavyAttack1");
            AnimationClip attack1 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack1");
            AnimationClip attack2 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack2");
            AnimationClip attack3 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack3");
            AnimationClip kick    = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Kick");
            AnimationClip meleeDownward = Resources.Load<AnimationClip>("Player/Ch28_nonPBR@Standing Melee Attack Downward");
            if (ds1 != null) clips.Add(ds1);
            if (ds2 != null) clips.Add(ds2);
            if (ds3 != null) clips.Add(ds3);
            if (dsH != null) clips.Add(dsH);
            if (attack1 != null) clips.Add(attack1);
            if (attack2 != null) clips.Add(attack2);
            if (attack3 != null) clips.Add(attack3);
            if (kick != null) clips.Add(kick);
            if (meleeDownward != null) clips.Add(meleeDownward);
        }
        if (clips.Count == 0 && attack != null) clips.Add(attack);
        attackClips = clips.ToArray();

        // Walk / locomotion — use one-handed weapon run for melee levels.
        walkClip = null;
        if (isMelee)
            walkClip = KevinMeleeResources.FindClip(KevinMeleeResources.MovementFolder, "1H@Run", "Run01", "SwordRun", "Sprint");
        if (walkClip == null)
            walkClip = Resources.Load<AnimationClip>("Player/DragonSoulsClips/Unarmed-Run-Forward");
        if (walkClip == null)
            walkClip = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Equip");
        if (walkClip == null)
            walkClip = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/SwordIdle");
        if (walkClip == null)
            walkClip = idle;

        if (targetAnimator == null || idleClip == null) return;

        parentRoot = transform.parent;
        targetAnimator.runtimeAnimatorController = null;

        // Grab humanoid bones for procedural walk
        leftUpperLeg  = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftLowerLeg  = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightLowerLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        leftUpperArm  = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        hips          = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
        spine         = targetAnimator.GetBoneTransform(HumanBodyBones.Spine);

        // Build PlayableGraph with upper/lower body split:
        // Layer 0: Locomotion (idle + walk blend) — drives full body including legs
        // Layer 1: Attack clips — masked to upper body only so legs keep walking

        graph  = PlayableGraph.Create("CombatAnimPlayer");
        output = AnimationPlayableOutput.Create(graph, "Animation", targetAnimator);

        // ── Layer 0: Locomotion (idle / walk) ──
        locoMixer = AnimationMixerPlayable.Create(graph, 2);

        idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
        idlePlayable.SetApplyFootIK(false);
        graph.Connect(idlePlayable, 0, locoMixer, 0);

        walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
        walkPlayable.SetApplyFootIK(false);
        graph.Connect(walkPlayable, 0, locoMixer, 1);

        locoMixer.SetInputWeight(0, 1f);
        locoMixer.SetInputWeight(1, 0f);

        // ── Layer 1: Attack clips (upper body only) ──
        int attackCount = attackClips.Length;
        attackMixer = AnimationMixerPlayable.Create(graph, attackCount > 0 ? attackCount : 1);

        attackPlayables = new AnimationClipPlayable[attackCount];
        for (int i = 0; i < attackCount; i++)
        {
            attackPlayables[i] = AnimationClipPlayable.Create(graph, attackClips[i]);
            attackPlayables[i].SetApplyFootIK(false);
            graph.Connect(attackPlayables[i], 0, attackMixer, i);
            attackMixer.SetInputWeight(i, 0f);
        }

        // ── Layer mixer: combines locomotion (full body) + attack (upper body) ──
        layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);
        graph.Connect(locoMixer, 0, layerMixer, 0);
        graph.Connect(attackMixer, 0, layerMixer, 1);

        layerMixer.SetInputWeight(0, 1f);  // Locomotion always active
        layerMixer.SetInputWeight(1, 0f);  // Attack layer off by default

        // Create upper body avatar mask at runtime
        upperBodyMask = new AvatarMask();
        // Enable only upper body parts for the attack layer
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            upperBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);

        // Apply mask to attack layer
        layerMixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);

        output.SetSourcePlayable(layerMixer);
        graph.Play();
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized || !graph.IsValid()) return;

        // Auto-reset attack when animation finishes
        if (isAttacking && currentAttackIndex >= 0)
        {
            float norm = GetAttackNormalizedTime();

            if (comboQueued && norm >= ComboWindowStart)
            {
                comboQueued = false;
                PlayNextCombo();
                // Don't return — still update locomotion below so legs keep walking
            }
            else if (norm >= AutoResetTime)
            {
                ReturnToIdle();
            }
        }

        // ── Locomotion blend (Layer 0) — always runs, even during attacks ──
        // The avatar mask on layer 1 ensures attacks only affect the upper body,
        // so locomotion (idle/walk) continues driving the legs at all times.
        float speed = 0f;
        if (parentRoot != null)
        {
            CharacterController cc = parentRoot.GetComponent<CharacterController>();
            if (cc != null) speed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        }

        float targetWalk = speed > 0.5f ? 1f : 0f;
        currentWalkWeight = Mathf.MoveTowards(currentWalkWeight, targetWalk, 6f * Time.deltaTime);

        locoMixer.SetInputWeight(0, 1f - currentWalkWeight);
        locoMixer.SetInputWeight(1, currentWalkWeight);

        if (walkPlayable.IsValid() && speed > 0.5f)
            walkPlayable.SetSpeed(Mathf.Clamp(speed / 4f, 0.8f, 2f));
    }

    /// <summary>
    /// Procedural walk applied after PlayableGraph — rotates legs, arms, hips, spine.
    /// With the layer system, legs keep walking during attacks (layer 0 locomotion is
    /// never masked out). Upper body procedural motion is skipped during attacks
    /// because the attack animation (layer 1) owns those bones.
    /// </summary>
    private void LateUpdate()
    {
        if (!isInitialized) return;
        if (currentWalkWeight < 0.05f)
        {
            walkCycleTimer = 0f;
            return;
        }

        float w = currentWalkWeight;

        // Sync cycle speed to actual movement
        float speed = 0f;
        if (parentRoot != null)
        {
            CharacterController cc = parentRoot.GetComponent<CharacterController>();
            if (cc != null) speed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        }
        float cycleRate = Mathf.Clamp(speed / 3.5f, 0.7f, 2.2f) * WalkCycleSpeed;
        walkCycleTimer += Time.deltaTime * cycleRate;

        float sin = Mathf.Sin(walkCycleTimer);
        float cos = Mathf.Cos(walkCycleTimer);

        // ── Legs: always apply, even during attacks ──
        // Upper legs swing forward/backward
        if (leftUpperLeg != null)  leftUpperLeg.localRotation  *= Quaternion.Euler(sin * UpperLegSwing * w, 0f, 0f);
        if (rightUpperLeg != null) rightUpperLeg.localRotation *= Quaternion.Euler(-sin * UpperLegSwing * w, 0f, 0f);

        // Knee bend when thigh swings forward
        float leftKnee  = Mathf.Max(0f, sin)  * LowerLegBend;
        float rightKnee = Mathf.Max(0f, -sin) * LowerLegBend;
        if (leftLowerLeg != null)  leftLowerLeg.localRotation  *= Quaternion.Euler(leftKnee * w, 0f, 0f);
        if (rightLowerLeg != null) rightLowerLeg.localRotation *= Quaternion.Euler(rightKnee * w, 0f, 0f);

        // Hip bounce (double frequency — one bounce per step)
        if (hips != null)
        {
            float bounce = Mathf.Abs(Mathf.Sin(walkCycleTimer * 2f)) * HipBounce;
            hips.localPosition += new Vector3(0f, -bounce * w, 0f);
        }

        // ── Upper body: only apply when NOT attacking ──
        // During attacks, the attack layer (layer 1) drives arms/spine via avatar mask.
        // Adding procedural motion on top would fight the attack animation.
        if (!isAttacking)
        {
            // Arms swing opposite to legs
            if (leftUpperArm != null)  leftUpperArm.localRotation  *= Quaternion.Euler(-sin * ArmSwing * w, 0f, 0f);
            if (rightUpperArm != null) rightUpperArm.localRotation *= Quaternion.Euler(sin * ArmSwing * w, 0f, 0f);

            // Slight forward lean and lateral sway
            if (spine != null)
                spine.localRotation *= Quaternion.Euler(SpineTilt * w, 0f, cos * SpineTilt * 0.5f * w);
        }
    }

    public void PlayAttack()
    {
        if (!isInitialized || !graph.IsValid() || attackClips.Length == 0) return;

        if (!isAttacking)
        {
            comboStep = 0;
            PlayComboStep(0);
        }
        else if (GetAttackNormalizedTime() >= ComboWindowStart * 0.7f)
        {
            comboQueued = true;
        }
    }

    private void PlayNextCombo()
    {
        comboStep = (comboStep + 1) % attackClips.Length;
        PlayComboStep(comboStep);
    }

    private void PlayComboStep(int step)
    {
        isAttacking = true;
        currentAttackIndex = step;
        comboQueued = false;

        // Clear all attack clip weights, then activate the current step
        ClearAttackWeights();
        attackMixer.SetInputWeight(step, 1f);

        // Enable the attack layer (upper body override)
        if (layerMixer.IsValid())
            layerMixer.SetInputWeight(1, 1f);

        if (attackPlayables[step].IsValid())
        {
            attackPlayables[step].SetTime(0d);
            attackPlayables[step].SetDone(false);
        }
    }

    private float GetAttackNormalizedTime()
    {
        if (currentAttackIndex < 0 || currentAttackIndex >= attackClips.Length) return 1f;
        if (!attackPlayables[currentAttackIndex].IsValid()) return 1f;

        float len = attackClips[currentAttackIndex].length;
        if (len <= 0f) return 1f;
        return (float)(attackPlayables[currentAttackIndex].GetTime() / len);
    }

    public void ResetAttack() => ReturnToIdle();

    private void ReturnToIdle()
    {
        isAttacking = false;
        currentAttackIndex = -1;
        comboStep = 0;
        comboQueued = false;

        // Disable the attack layer so locomotion (layer 0) drives the full body
        if (layerMixer.IsValid())
            layerMixer.SetInputWeight(1, 0f);

        // Zero out all attack clip weights
        ClearAttackWeights();
    }

    /// <summary>
    /// Zeros all attack clip weights in the attack mixer.
    /// </summary>
    private void ClearAttackWeights()
    {
        if (!attackMixer.IsValid()) return;
        int count = attackMixer.GetInputCount();
        for (int i = 0; i < count; i++)
            attackMixer.SetInputWeight(i, 0f);
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}
