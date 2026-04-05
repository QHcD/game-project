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
    public float jumpHeight = 0.6f;

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

    [Header("Ranged Combat")]
    [Tooltip("Projectile prefab to instantiate on Fire1 (needs a Rigidbody).")]
    public GameObject bulletPrefab;

    [Tooltip("Spawn point and forward direction for projectiles.")]
    public Transform firePoint;

    [Tooltip("Force applied to the bullet on spawn (units/s).")]
    public float bulletForce = 25f;

    [Header("Weapon References")]
    [Tooltip("Current weapon display name (read by HUD).")]
    public string equippedWeaponName = "Combat Knife";

    [Tooltip("Optional hit-effect prefab spawned at impact point.")]
    public GameObject hitEffect;

    [Tooltip("Sound played on attack swing.")]
    public AudioClip swordSwing;

    [Tooltip("Sound played on successful hit.")]
    public AudioClip hitSound;

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
    private PlayerInput playerInput;
    private PlayerInput.MainActions input;

    // Movement
    private Vector3 verticalVelocity;
    private Vector3 horizontalVelocity;
    private Vector2 moveInputRaw;
    private Vector2 moveInputSmoothed;
    private Vector2 lookInput;
    private bool isGrounded;
    private bool isSprinting;
    private bool wasMoving;
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
    private float explosionRadius;
    private LayerMask resolvedAttackMask;

    // Third-person body
    private GameObject thirdPersonBody;
    private Renderer[] firstPersonRenderers;
    private GameObject equippedWeaponObject;

    // Animator parameter hashes (connect these in an Animator Controller)
    private static readonly int AnimSpeed      = Animator.StringToHash("Speed");
    private static readonly int AnimGrounded   = Animator.StringToHash("IsGrounded");
    private static readonly int AnimAttacking  = Animator.StringToHash("IsAttacking");
    private static readonly int AnimSprinting  = Animator.StringToHash("IsSprinting");

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
    private Camera ActiveCamera => isThirdPersonActive ? runtimeThirdPersonCamera : firstPersonCam;

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

        firstPersonRenderers = GetComponentsInChildren<Renderer>(true);
        resolvedAttackMask = attackLayer == 0 ? ~0 : attackLayer;

        // Input System
        playerInput = new PlayerInput();
        input = playerInput.Main;
        input.Jump.performed   += _ => Jump();
        input.Attack.started   += _ => Attack();

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

        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        EquipWeaponForLevel(level);
        ApplyPerspectivePreference();
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;

        ReadInput();
        ApplyMovement();
        ApplyLook();
        UpdateHeadBob();
        UpdateCameraKick();
        UpdateCombatState();
        UpdateAnimations();
        UpdateAnimatorParameters();

        // Ranged shoot (legacy Fire1 / left mouse)
        if (Input.GetButtonDown("Fire1") && bulletPrefab != null)
            Shoot();

        // Perspective toggle (V key)
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            TogglePerspective();
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

    private void OnEnable()
    {
        if (!input.Get().enabled) input.Enable();
    }

    private void OnDisable()
    {
        if (playerInput != null) input.Disable();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════════════

    private void ReadInput()
    {
        // WASD from Input System
        Vector2 wasd = input.Movement.ReadValue<Vector2>();

        // Also read arrow keys directly so both schemes always work
        Vector2 arrows = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed)    arrows.y += 1f;
            if (Keyboard.current.downArrowKey.isPressed)  arrows.y -= 1f;
            if (Keyboard.current.leftArrowKey.isPressed)  arrows.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) arrows.x += 1f;
        }

        // Use whichever has greater magnitude
        moveInputRaw = wasd.sqrMagnitude >= arrows.sqrMagnitude ? wasd : arrows;
        moveInputRaw = Vector2.ClampMagnitude(moveInputRaw, 1f);

        // Smooth the input to remove digital snapping
        moveInputSmoothed = Vector2.Lerp(moveInputSmoothed, moveInputRaw, InputSmoothing * Time.deltaTime);

        lookInput = input.Look.ReadValue<Vector2>();
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
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime; // gravity is negative → accelerates down
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, -40f); // terminal velocity cap
        }
        controller.Move(verticalVelocity * Time.deltaTime);

        // Arena constraints
        ClampInsideArena();
        SnapToArenaFloor();

        // Animation state driven by actual velocity, not input
        wasMoving = horizontalVelocity.sqrMagnitude > 0.1f;

        // ── BODY ROTATION (TPS, runs in Update before any LateUpdate) ──
        // Rotating in Update guarantees this happens BEFORE CharacterVisualAnimationPlayer
        // writes bone positions in its own LateUpdate — prevents the double-rotation
        // distortion that causes the "merged/melted bones" look.
        if (isThirdPersonActive && thirdPersonBody != null)
        {
            Vector3 moveFlat = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);

            if (moveFlat.sqrMagnitude > 0.01f)
            {
                float targetY = Quaternion.LookRotation(moveFlat).eulerAngles.y;
                thirdPersonBody.transform.rotation = Quaternion.Slerp(
                    thirdPersonBody.transform.rotation,
                    Quaternion.Euler(0f, targetY, 0f),
                    10f * Time.deltaTime);
            }
            else
            {
                // Idle: gradually align body with player root yaw
                thirdPersonBody.transform.rotation = Quaternion.Slerp(
                    thirdPersonBody.transform.rotation,
                    Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
                    5f * Time.deltaTime);
            }
        }
    }

    private void Jump()
    {
        if (isGrounded)
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
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

    /// <summary>
    /// Spawns a bullet at firePoint and propels it forward. Triggered by Fire1
    /// when bulletPrefab is assigned. Falls back to melee Attack() otherwise.
    /// </summary>
    private void Shoot()
    {
        if (firePoint == null)
        {
            Debug.LogWarning("[PlayerController] firePoint not assigned – falling back to melee.");
            Attack();
            return;
        }

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(firePoint.forward * bulletForce, ForceMode.VelocityChange);
        else
            Debug.LogWarning("[PlayerController] bulletPrefab has no Rigidbody – AddForce skipped.");

        // Reuse IsAttacking bool to trigger attack animation
        isAttacking = true;
        ChangeAnimationState(ATTACK1);
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

        CancelInvoke(nameof(AttackRaycast));
        Invoke(nameof(AttackRaycast), attackDelay);
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

        switch (currentWeaponType)
        {
            case GameManager.WeaponType.Flamethrower: AttackFlamethrower(cam); break;
            case GameManager.WeaponType.Sniper:       AttackSniper(cam);      break;
            case GameManager.WeaponType.Explosive:    AttackExplosive(cam);   break;
            default:                                  AttackMelee(cam);       break;
        }

        cameraKickTarget = currentWeaponType switch
        {
            GameManager.WeaponType.Sniper      => -4.5f,
            GameManager.WeaponType.Explosive   => -3.5f,
            GameManager.WeaponType.Flamethrower => -0.6f,
            _ => -1.2f
        };
    }

    private void AttackMelee(Camera cam)
    {
        Vector3 center = cam.transform.position + cam.transform.forward * attackDistance;
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, resolvedAttackMask, QueryTriggerInteraction.Ignore);
        bool landed = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (TryDamageTarget(hits[i].transform, attackDamage))
            {
                ApplyHitReaction(hits[i].transform, cam.transform.forward);
                HitTarget(hits[i].ClosestPoint(center));
                landed = true;
                break;
            }
        }

        if (!landed && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
            HitTarget(hit.point);
    }

    private void AttackFlamethrower(Camera cam)
    {
        int rays = 7;
        float spread = 12f;
        bool hitAny = false;

        for (int r = 0; r < rays; r++)
        {
            Vector3 dir = Quaternion.Euler(Random.Range(-spread * 0.5f, spread * 0.5f), Random.Range(-spread, spread), 0f) * cam.transform.forward;
            if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
            {
                if (TryDamageTarget(hit.collider.transform, attackDamage)) hitAny = true;
                if (r == 0) HitTarget(hit.point);
            }
        }

        if (!hitAny)
            HitTarget(cam.transform.position + cam.transform.forward * (attackDistance * 0.7f));
    }

    private void AttackSniper(Camera cam)
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
        {
            TryDamageTarget(hit.collider.transform, attackDamage);
            HitTarget(hit.point);
        }
    }

    private void AttackExplosive(Camera cam)
    {
        Vector3 blast;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
            blast = hit.point;
        else
            blast = cam.transform.position + cam.transform.forward * Mathf.Min(attackDistance, 60f);

        HitTarget(blast);

        float radius = explosionRadius > 0f ? explosionRadius : 5f;
        Collider[] blasted = Physics.OverlapSphere(blast, radius, resolvedAttackMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < blasted.Length; i++)
        {
            float dist = Vector3.Distance(blast, blasted[i].transform.position);
            float falloff = 1f - Mathf.Clamp01(dist / radius);
            int dmg = Mathf.RoundToInt(attackDamage * (0.4f + 0.6f * falloff));
            TryDamageTarget(blasted[i].transform, dmg);
        }
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
    /// Parameters: Speed (float), IsGrounded (bool), IsAttacking (bool), IsSprinting (bool).
    /// </summary>
    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        animator.SetFloat(AnimSpeed, horizontalVelocity.magnitude);
        animator.SetBool(AnimGrounded, isGrounded);
        animator.SetBool(AnimAttacking, isAttacking);
        animator.SetBool(AnimSprinting, isSprinting);
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
        transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);

        // Kill outward horizontal velocity so the character doesn't push into the wall
        if (Vector3.Dot(horizontalVelocity, planar.normalized) > 0f)
            horizontalVelocity = Vector3.zero;

        if (transform.position.y > arenaFloorHeight + 0.6f)
        {
            transform.position = new Vector3(transform.position.x, arenaFloorHeight, transform.position.z);
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
        transform.position = new Vector3(transform.position.x, snappedY, transform.position.z);
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
        runtimeThirdPersonCamera.fieldOfView   = firstPersonCam.fieldOfView;
        runtimeThirdPersonCamera.nearClipPlane  = firstPersonCam.nearClipPlane;
        runtimeThirdPersonCamera.farClipPlane   = firstPersonCam.farClipPlane;
        runtimeThirdPersonCamera.clearFlags     = firstPersonCam.clearFlags;
        runtimeThirdPersonCamera.backgroundColor = firstPersonCam.backgroundColor;
        runtimeThirdPersonCamera.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();

        CameraController follow = camObj.AddComponent<CameraController>();
        follow.target      = transform;
        follow.offset      = new Vector3(0f, 3.2f, -5.8f);
        follow.smoothSpeed = 10f;

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

                AnimationClip idleClip   = Resources.Load<AnimationClip>("Player/DragonSoulsClips/Unarmed-Idle");
                AnimationClip attackClip = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack1");

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
            AttachWeaponToHand(thirdPersonBody);
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
                CharacterVisualAnimationPlayer animPlayer = thirdPersonBody.AddComponent<CharacterVisualAnimationPlayer>();
                animPlayer.Setup(importedAnimator,
                    Resources.Load<AnimationClip>("Player/DragonSoulsClips/Unarmed-Idle"),
                    Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack1"));
            }

            thirdPersonBody.AddComponent<CharacterVisualGrounder>();
            thirdPersonBody.AddComponent<CharacterVisualBob>();
            AttachWeaponToHand(thirdPersonBody);
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
        AttachWeaponToHand(thirdPersonBody);
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
        if (GameManager.Instance == null) return;

        equippedWeaponName = GameManager.Instance.GetWeaponNameForLevel(level);
        attackDamage       = Mathf.RoundToInt(GameManager.Instance.GetWeaponDamageForLevel(level));
        attackDistance      = GameManager.Instance.GetWeaponRangeForLevel(level);
        currentWeaponType  = GameManager.Instance.GetWeaponTypeForLevel(level);
        explosionRadius    = GameManager.Instance.GetWeaponExplosionRadiusForLevel(level);

        switch (currentWeaponType)
        {
            case GameManager.WeaponType.Flamethrower:
                attackSpeed = 0.12f; attackDelay = 0.04f; attackRadius = 1.6f; break;
            case GameManager.WeaponType.Sniper:
                attackSpeed = 2.2f; attackDelay = 0.05f; attackRadius = 0f; break;
            case GameManager.WeaponType.Explosive:
                attackSpeed = 1.6f; attackDelay = 0.15f; attackRadius = 0f; break;
            case GameManager.WeaponType.UltimateMelee:
                attackSpeed = 0.45f; attackDelay = 0.18f; attackRadius = 1.6f; break;
            default:
                attackSpeed = 1.0f; attackDelay = 0.4f; attackRadius = 1.25f; break;
        }

        if (thirdPersonBody != null)
            AttachWeaponToHand(thirdPersonBody);
    }

    private void AttachWeaponToHand(GameObject body)
    {
        if (body == null) return;

        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
            equippedWeaponObject = null;
        }

        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        // Find right hand bone — try every possible naming convention Mixamo uses,
        // then fall back to a case-insensitive contains search so the weapon never
        // ends up at the character's feet (body.transform = Y:0 = ground level).
        Transform handBone = null;
        Animator bodyAnimator = body.GetComponentInChildren<Animator>(true);

        // 1. Humanoid avatar mapping (works only when avatar is set to Humanoid in import)
        if (bodyAnimator != null && bodyAnimator.isHuman)
            handBone = bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand);

        // 2. Exact name search — covers common Mixamo and Unity conventions
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

        // 3. Case-insensitive partial-name search — catches any remaining variant
        if (handBone == null)
            handBone = FindBoneContaining(body.transform, "righthand")
                    ?? FindBoneContaining(body.transform, "hand_r")
                    ?? FindBoneContaining(body.transform, "r_hand");

        // 4. Last resort: right upper arm keeps the weapon near the hand area,
        //    infinitely better than body.transform which puts it at the feet.
        if (handBone == null && bodyAnimator != null && bodyAnimator.isHuman)
            handBone = bodyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);

        Transform attachPoint = handBone != null ? handBone : body.transform;
        equippedWeaponObject = BuildWeaponModel(level, attachPoint);
    }

    // ── Weapon model factory ──

    private GameObject BuildWeaponModel(int level, Transform attachPoint)
    {
        if (level == 2)
        {
            GameObject kdPrefab = Resources.Load<GameObject>("Weapons/KnuckleDuster");
            if (kdPrefab != null)
            {
                GameObject kd = Instantiate(kdPrefab, attachPoint);
                kd.name = "WeaponModel";
                kd.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                kd.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                kd.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                return kd;
            }
        }
        return BuildPrimitiveWeapon(level, attachPoint);
    }

    private GameObject BuildPrimitiveWeapon(int level, Transform attachPoint)
    {
        GameObject root = new GameObject("WeaponModel");
        root.transform.SetParent(attachPoint, false);
        root.transform.localPosition = new Vector3(0f, 0.08f, 0.02f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

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
    // Playable graph
    private Animator targetAnimator;
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip[] attackClips;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable walkPlayable;
    private AnimationClipPlayable[] attackPlayables;
    private AnimationMixerPlayable mixer;
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

        // Build combo chain from available clips (DragonSouls / Explosive RPG pack first, then legacy knight).
        AnimationClip ds1 = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack1");
        AnimationClip ds2 = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack2");
        AnimationClip ds3 = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedLightAttack3");
        AnimationClip dsH = Resources.Load<AnimationClip>("Player/DragonSoulsClips/UnarmedHeavyAttack1");
        AnimationClip attack1 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack1");
        AnimationClip attack2 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack2");
        AnimationClip attack3 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack3");
        AnimationClip kick    = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Kick");
        AnimationClip meleeDownward = Resources.Load<AnimationClip>("Player/Ch28_nonPBR@Standing Melee Attack Downward");

        var clips = new System.Collections.Generic.List<AnimationClip>();
        if (ds1 != null) clips.Add(ds1);
        if (ds2 != null) clips.Add(ds2);
        if (ds3 != null) clips.Add(ds3);
        if (dsH != null) clips.Add(dsH);
        if (attack1 != null) clips.Add(attack1);
        if (attack2 != null) clips.Add(attack2);
        if (attack3 != null) clips.Add(attack3);
        if (kick != null) clips.Add(kick);
        if (meleeDownward != null) clips.Add(meleeDownward);
        if (clips.Count == 0 && attack != null) clips.Add(attack);
        attackClips = clips.ToArray();

        // Walk / locomotion clip priority
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

        // Build PlayableGraph: idle(0), walk(1), attacks(2+)
        int totalInputs = 2 + attackClips.Length;
        graph  = PlayableGraph.Create("CombatAnimPlayer");
        output = AnimationPlayableOutput.Create(graph, "Animation", targetAnimator);
        mixer  = AnimationMixerPlayable.Create(graph, totalInputs);

        idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
        idlePlayable.SetApplyFootIK(false);
        graph.Connect(idlePlayable, 0, mixer, 0);

        walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
        walkPlayable.SetApplyFootIK(false);
        graph.Connect(walkPlayable, 0, mixer, 1);

        attackPlayables = new AnimationClipPlayable[attackClips.Length];
        for (int i = 0; i < attackClips.Length; i++)
        {
            attackPlayables[i] = AnimationClipPlayable.Create(graph, attackClips[i]);
            attackPlayables[i].SetApplyFootIK(false);
            graph.Connect(attackPlayables[i], 0, mixer, 2 + i);
        }

        mixer.SetInputWeight(0, 1f);
        for (int i = 1; i < totalInputs; i++)
            mixer.SetInputWeight(i, 0f);

        output.SetSourcePlayable(mixer);
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
                return;
            }

            if (norm >= AutoResetTime)
                ReturnToIdle();
            return;
        }

        // Movement blend
        float speed = 0f;
        if (parentRoot != null)
        {
            CharacterController cc = parentRoot.GetComponent<CharacterController>();
            if (cc != null) speed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        }

        float targetWalk = speed > 0.5f ? 1f : 0f;
        currentWalkWeight = Mathf.MoveTowards(currentWalkWeight, targetWalk, 6f * Time.deltaTime);

        SetAllWeights(0f);
        mixer.SetInputWeight(0, 1f - currentWalkWeight);
        mixer.SetInputWeight(1, currentWalkWeight);

        if (walkPlayable.IsValid() && speed > 0.5f)
            walkPlayable.SetSpeed(Mathf.Clamp(speed / 4f, 0.8f, 2f));
    }

    /// <summary>
    /// Procedural walk applied after PlayableGraph — rotates legs, arms, hips, spine.
    /// </summary>
    private void LateUpdate()
    {
        if (!isInitialized || isAttacking) return;
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

        // Upper legs swing forward/backward
        if (leftUpperLeg != null)  leftUpperLeg.localRotation  *= Quaternion.Euler(sin * UpperLegSwing * w, 0f, 0f);
        if (rightUpperLeg != null) rightUpperLeg.localRotation *= Quaternion.Euler(-sin * UpperLegSwing * w, 0f, 0f);

        // Knee bend when thigh swings forward
        float leftKnee  = Mathf.Max(0f, sin)  * LowerLegBend;
        float rightKnee = Mathf.Max(0f, -sin) * LowerLegBend;
        if (leftLowerLeg != null)  leftLowerLeg.localRotation  *= Quaternion.Euler(leftKnee * w, 0f, 0f);
        if (rightLowerLeg != null) rightLowerLeg.localRotation *= Quaternion.Euler(rightKnee * w, 0f, 0f);

        // Arms swing opposite to legs
        if (leftUpperArm != null)  leftUpperArm.localRotation  *= Quaternion.Euler(-sin * ArmSwing * w, 0f, 0f);
        if (rightUpperArm != null) rightUpperArm.localRotation *= Quaternion.Euler(sin * ArmSwing * w, 0f, 0f);

        // Hip bounce (double frequency — one bounce per step)
        if (hips != null)
        {
            float bounce = Mathf.Abs(Mathf.Sin(walkCycleTimer * 2f)) * HipBounce;
            hips.localPosition += new Vector3(0f, -bounce * w, 0f);
        }

        // Slight forward lean and lateral sway
        if (spine != null)
            spine.localRotation *= Quaternion.Euler(SpineTilt * w, 0f, cos * SpineTilt * 0.5f * w);
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

        SetAllWeights(0f);
        mixer.SetInputWeight(2 + step, 1f);

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
    }

    private void SetAllWeights(float w)
    {
        int count = mixer.GetInputCount();
        for (int i = 0; i < count; i++)
            mixer.SetInputWeight(i, w);
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}
