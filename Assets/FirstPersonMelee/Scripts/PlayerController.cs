using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Animations;

public class PlayerController : MonoBehaviour
{
    [Header("Compatibility")]
    public Camera firstPersonCam;
    public Camera thirdPersonCam;
    public string equippedWeaponName = "Combat Knife";

    private PlayerInput playerInput;
    private PlayerInput.MainActions input;

    private CharacterController controller;
    private Animator animator;
    private AudioSource audioSource;

    [Header("Controller")]
    public float moveSpeed = 5;
    public float gravity = -25f;
    public float jumpHeight = 0.6f;
    public float arenaBoundaryRadius = 22.8f;
    public float arenaBoundaryPadding = 0.35f;
    public float arenaFloorHeight = 0.1f;
    public float floorSnapSpeed = 14f;
    public float maxFloorSnapDistance = 0.45f;

    private Vector3 playerVelocity;
    private Vector2 moveInput;
    private Vector2 smoothedMoveInput;
    private Vector2 lookInput;
    private bool wasMoving;
    private const float MoveSmoothing = 12f;
    private const float SprintMultiplier = 1.55f;
    private bool isSprinting;

    private bool isGrounded;

    // Head bob
    private float headBobTimer;
    private const float HeadBobFrequency = 10f;
    private const float HeadBobAmplitude = 0.038f;
    private float headBobVelocity;

    [Header("Camera")]
    public Camera cam;
    public float sensitivity = 100f;

    private float xRotation;
    private Vector3 firstPersonLocalPosition;
    private Quaternion firstPersonLocalRotation;
    private Camera runtimeThirdPersonCamera;
    private GameObject thirdPersonBody;
    private Renderer[] firstPersonRenderers;
    private LayerMask resolvedAttackMask;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();

        if (firstPersonCam == null)
        {
            firstPersonCam = cam;
        }

        if (cam == null)
        {
            cam = firstPersonCam != null ? firstPersonCam : GetComponentInChildren<Camera>();
        }

        if (cam != null)
        {
            firstPersonLocalPosition = cam.transform.localPosition;
            firstPersonLocalRotation = cam.transform.localRotation;
        }

        firstPersonRenderers = GetComponentsInChildren<Renderer>(true);
        resolvedAttackMask = ~0;

        playerInput = new PlayerInput();
        input = playerInput.Main;
        AssignInputs();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        EquipWeaponForLevel(level);
        ApplyGameplayPreferences();
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;
        moveInput = ReadMovementInput();
        lookInput = input.Look.ReadValue<Vector2>();

        smoothedMoveInput = Vector2.Lerp(smoothedMoveInput, moveInput, MoveSmoothing * Time.deltaTime);

        MoveInput(smoothedMoveInput);
        LookInput(lookInput);
        UpdateHeadBob();
        UpdateCameraKick();
        UpdateCombatState();
        SetAnimations();
    }

    private void UpdateCameraKick()
    {
        if (cam == null) return;

        // Spring cameraKick back to zero
        cameraKickCurrent = Mathf.Lerp(cameraKickCurrent, cameraKickTarget, 18f * Time.deltaTime);
        cameraKickTarget = Mathf.Lerp(cameraKickTarget, 0f, 14f * Time.deltaTime);

        // Apply as additional pitch on top of player aim
        xRotation += cameraKickCurrent * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
    }

    private Vector2 ReadMovementInput()
    {
        if (GameManager.Instance != null && GameManager.Instance.GetMovementScheme() == GameManager.MovementScheme.ArrowKeys)
        {
            if (Keyboard.current == null)
            {
                return Vector2.zero;
            }

            Vector2 movement = Vector2.zero;
            if (Keyboard.current.upArrowKey.isPressed) movement.y += 1f;
            if (Keyboard.current.downArrowKey.isPressed) movement.y -= 1f;
            if (Keyboard.current.leftArrowKey.isPressed) movement.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) movement.x += 1f;
            return Vector2.ClampMagnitude(movement, 1f);
        }

        return input.Movement.ReadValue<Vector2>();
    }

    private void MoveInput(Vector2 inputValue)
    {
        if (controller != null)
        {
            controller.stepOffset = 0.08f;
            controller.slopeLimit = 28f;
        }

        isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && inputValue.y > 0.1f;
        float speed = isSprinting ? moveSpeed * SprintMultiplier : moveSpeed;

        Vector3 moveDirection = new Vector3(inputValue.x, 0f, inputValue.y);
        Vector3 worldMove = transform.TransformDirection(moveDirection) * speed;

        controller.Move(worldMove * Time.deltaTime);

        if (isGrounded && playerVelocity.y < 0f)
        {
            playerVelocity.y = -6f;
        }

        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        ClampInsideArena();
        SnapToArenaFloor();

        wasMoving = moveInput.sqrMagnitude > 0.01f;
    }

    private void ClampInsideArena()
    {
        Vector3 planarPosition = transform.position;
        planarPosition.y = 0f;

        float maxRadius = Mathf.Max(1f, arenaBoundaryRadius - controller.radius - arenaBoundaryPadding);
        if (planarPosition.sqrMagnitude <= maxRadius * maxRadius)
        {
            return;
        }

        Vector3 clampedPlanar = planarPosition.normalized * maxRadius;
        Vector3 correctedPosition = new Vector3(clampedPlanar.x, transform.position.y, clampedPlanar.z);
        transform.position = correctedPosition;

        Vector3 planarVelocity = new Vector3(playerVelocity.x, 0f, playerVelocity.z);
        if (Vector3.Dot(planarVelocity, planarPosition.normalized) > 0f)
        {
            playerVelocity.x = 0f;
            playerVelocity.z = 0f;
        }

        if (transform.position.y > arenaFloorHeight + 0.6f)
        {
            transform.position = new Vector3(transform.position.x, arenaFloorHeight, transform.position.z);
            playerVelocity.y = Mathf.Min(playerVelocity.y, 0f);
        }
    }

    private void SnapToArenaFloor()
    {
        if (controller == null || playerVelocity.y > 0.05f)
        {
            return;
        }

        Vector3 rayOrigin = transform.position + Vector3.up * 0.25f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit floorHit, maxFloorSnapDistance + 0.25f, ~0, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float targetY = floorHit.point.y;
        float currentY = transform.position.y;
        if (Mathf.Abs(currentY - targetY) < 0.01f || currentY < targetY)
        {
            return;
        }

        if (currentY - targetY > maxFloorSnapDistance)
        {
            return;
        }

        float snappedY = Mathf.MoveTowards(currentY, targetY, floorSnapSpeed * Time.deltaTime);
        transform.position = new Vector3(transform.position.x, snappedY, transform.position.z);
    }

    private void ApplyAttackLunge()
    {
        if (controller == null)
        {
            return;
        }

        Vector3 lunge = transform.forward * attackLungeDistance;
        controller.Move(lunge);
        ClampInsideArena();
    }

    private void ApplyHitReaction(Transform hitTransform, Vector3 hitDirection)
    {
        if (hitTransform == null)
        {
            return;
        }

        Vector3 pushDirection = new Vector3(hitDirection.x, 0f, hitDirection.z).normalized;
        if (pushDirection.sqrMagnitude < 0.001f)
        {
            pushDirection = hitTransform.position - transform.position;
            pushDirection.y = 0f;
            pushDirection.Normalize();
        }

        Rigidbody hitBody = hitTransform.GetComponentInParent<Rigidbody>();
        if (hitBody != null)
        {
            hitBody.AddForce(pushDirection * hitKnockbackForce, ForceMode.VelocityChange);
            return;
        }

        hitTransform.position += pushDirection * 0.18f;
    }

    private void UpdateHeadBob()
    {
        if (cam == null) return;

        float targetVelocity = wasMoving && isGrounded ? 1f : 0f;
        headBobVelocity = Mathf.Lerp(headBobVelocity, targetVelocity, 8f * Time.deltaTime);

        if (headBobVelocity > 0.01f)
        {
            float freq = isSprinting ? HeadBobFrequency * 1.35f : HeadBobFrequency;
            headBobTimer += Time.deltaTime * freq;
        }
        else
        {
            headBobTimer = Mathf.MoveTowards(headBobTimer, 0f, Time.deltaTime * HeadBobFrequency);
        }

        float bobOffset = Mathf.Sin(headBobTimer) * HeadBobAmplitude * headBobVelocity;
        Vector3 basePos = firstPersonLocalPosition;
        cam.transform.localPosition = new Vector3(basePos.x, basePos.y + bobOffset, basePos.z);
    }

    private void LookInput(Vector2 inputValue)
    {
        if (cam == null)
        {
            return;
        }

        float mouseX = inputValue.x;
        float mouseY = inputValue.y;

        xRotation -= mouseY * sensitivity * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.Rotate(Vector3.up * (mouseX * sensitivity * Time.deltaTime));
    }

    private void OnEnable()
    {
        if (input.Get().enabled == false)
        {
            input.Enable();
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            input.Disable();
        }
    }

    private void Jump()
    {
        if (isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void AssignInputs()
    {
        input.Jump.performed += ctx => Jump();
        input.Attack.started += ctx => Attack();
    }

    // ---------- //
    // ANIMATIONS //
    // ---------- //

    public const string IDLE = "Idle";
    public const string WALK = "Walk";
    public const string ATTACK1 = "Attack 1";
    public const string ATTACK2 = "Attack 2";

    private string currentAnimationState;

    public void ChangeAnimationState(string newState) 
    {
        if (animator == null || currentAnimationState == newState || !animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
        {
            return;
        }

        currentAnimationState = newState;
        animator.CrossFadeInFixedTime(currentAnimationState, 0.2f);
    }

    private void SetAnimations()
    {
        // Don't override first-person animation during combo attacks
        if (attacking) return;

        if (wasMoving)
        {
            ChangeAnimationState(WALK);
        }
        else
        {
            ChangeAnimationState(IDLE);
        }
    }

    // ------------------- //
    // ATTACKING BEHAVIOUR //
    // ------------------- //

    [Header("Attacking")]
    public float attackDistance = 3f;
    public float attackDelay = 0.4f;
    public float attackSpeed = 1f;
    public int attackDamage = 1;
    public LayerMask attackLayer;
    public float attackRadius = 1.25f;
    public float attackLungeDistance = 0.45f;
    public float hitKnockbackForce = 4.5f;

    public GameObject hitEffect;
    public AudioClip swordSwing;
    public AudioClip hitSound;

    private bool attacking;

    // Special weapon state
    private GameManager.WeaponType currentWeaponType = GameManager.WeaponType.Melee;
    private float explosionRadius;
    private float cameraKickTarget;
    private float cameraKickCurrent;

    // Combo cooldown — matches the video's CoolDown parameter
    private float comboCooldownTimer;
    private const float ComboCooldown = 0.15f;

    public void Attack()
    {
        if (comboCooldownTimer > 0f) return;

        CharacterVisualAnimationPlayer visualAnim = thirdPersonBody != null
            ? thirdPersonBody.GetComponentInChildren<CharacterVisualAnimationPlayer>(true)
            : null;

        // If already attacking, only allow combo if animation system says we're in the combo window
        if (attacking)
        {
            if (visualAnim != null && visualAnim.IsComboReady)
            {
                visualAnim.PlayAttack();
                FireAttack();
            }
            return;
        }

        // Fresh attack
        attacking = true;
        comboCooldownTimer = ComboCooldown;

        if (visualAnim != null)
        {
            visualAnim.PlayAttack();
        }

        // First-person animation
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

        // Delay the damage raycast to sync with animation hit frame
        CancelInvoke(nameof(AttackRaycast));
        Invoke(nameof(AttackRaycast), attackDelay);
    }

    private void UpdateCombatState()
    {
        if (comboCooldownTimer > 0f)
            comboCooldownTimer -= Time.deltaTime;

        if (!attacking) return;

        // Check if the 3rd-person animation system has auto-reset (normalizedTime > threshold)
        CharacterVisualAnimationPlayer visualAnim = thirdPersonBody != null
            ? thirdPersonBody.GetComponentInChildren<CharacterVisualAnimationPlayer>(true)
            : null;

        if (visualAnim != null && !visualAnim.IsAttacking)
        {
            attacking = false;
        }
    }

    private void AttackRaycast()
    {
        if (cam == null) return;

        switch (currentWeaponType)
        {
            case GameManager.WeaponType.Flamethrower:
                AttackFlamethrower();
                break;
            case GameManager.WeaponType.Sniper:
                AttackSniper();
                break;
            case GameManager.WeaponType.Explosive:
                AttackExplosive();
                break;
            default:
                AttackMelee();
                break;
        }

        // Camera kick feedback
        cameraKickTarget = currentWeaponType == GameManager.WeaponType.Sniper ? -4.5f :
                           currentWeaponType == GameManager.WeaponType.Explosive ? -3.5f :
                           currentWeaponType == GameManager.WeaponType.Flamethrower ? -0.6f : -1.2f;
    }

    private void AttackMelee()
    {
        Vector3 hitCenter = cam.transform.position + cam.transform.forward * attackDistance;
        Collider[] hits = Physics.OverlapSphere(hitCenter, attackRadius, resolvedAttackMask, QueryTriggerInteraction.Ignore);
        bool landedHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Actor actor = hits[i].GetComponentInParent<Actor>();
            if (actor != null && actor.gameObject != gameObject)
            {
                actor.TakeDamage(attackDamage);
                ApplyHitReaction(actor.transform, cam.transform.forward);
                HitTarget(hits[i].ClosestPoint(hitCenter));
                landedHit = true;
                break;
            }
        }

        if (!landedHit && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
        {
            HitTarget(hit.point);
        }
    }

    // Flamethrower: spray 7 rays in a cone, hit everything in the spread
    private void AttackFlamethrower()
    {
        int rays = 7;
        float spread = 12f; // degrees half-angle
        bool hitAnything = false;

        for (int r = 0; r < rays; r++)
        {
            float angleH = Random.Range(-spread, spread);
            float angleV = Random.Range(-spread * 0.5f, spread * 0.5f);
            Vector3 dir = Quaternion.Euler(angleV, angleH, 0f) * cam.transform.forward;

            if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
            {
                Actor actor = hit.collider.GetComponentInParent<Actor>();
                if (actor != null && actor.gameObject != gameObject)
                {
                    actor.TakeDamage(attackDamage);
                    hitAnything = true;
                }

                if (r == 0)
                {
                    HitTarget(hit.point);
                }
            }
        }

        if (!hitAnything)
        {
            HitTarget(cam.transform.position + cam.transform.forward * (attackDistance * 0.7f));
        }
    }

    // Sniper: single precision raycast, extreme range
    private void AttackSniper()
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
        {
            Actor actor = hit.collider.GetComponentInParent<Actor>();
            if (actor != null && actor.gameObject != gameObject)
            {
                actor.TakeDamage(attackDamage);
            }

            HitTarget(hit.point);
        }
    }

    // Explosive: raycast to point, then AoE sphere damage around it
    private void AttackExplosive()
    {
        Vector3 blastPoint;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, attackDistance, resolvedAttackMask, QueryTriggerInteraction.Ignore))
        {
            blastPoint = hit.point;
        }
        else
        {
            blastPoint = cam.transform.position + cam.transform.forward * Mathf.Min(attackDistance, 60f);
        }

        HitTarget(blastPoint);

        float radius = explosionRadius > 0f ? explosionRadius : 5f;
        Collider[] blasted = Physics.OverlapSphere(blastPoint, radius, resolvedAttackMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < blasted.Length; i++)
        {
            Actor actor = blasted[i].GetComponentInParent<Actor>();
            if (actor != null && actor.gameObject != gameObject)
            {
                // Damage falls off with distance from blast centre
                float dist = Vector3.Distance(blastPoint, blasted[i].transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / radius);
                actor.TakeDamage(Mathf.RoundToInt(attackDamage * (0.4f + 0.6f * falloff)));
            }
        }
    }

    public void EquipWeaponForLevel(int level)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        equippedWeaponName = GameManager.Instance.GetWeaponNameForLevel(level);
        attackDamage = Mathf.RoundToInt(GameManager.Instance.GetWeaponDamageForLevel(level));
        attackDistance = GameManager.Instance.GetWeaponRangeForLevel(level);
        currentWeaponType = GameManager.Instance.GetWeaponTypeForLevel(level);
        explosionRadius = GameManager.Instance.GetWeaponExplosionRadiusForLevel(level);

        // Adjust attack timing per weapon type
        switch (currentWeaponType)
        {
            case GameManager.WeaponType.Flamethrower:
                attackSpeed = 0.12f;
                attackDelay = 0.04f;
                attackRadius = 1.6f;
                break;
            case GameManager.WeaponType.Sniper:
                attackSpeed = 2.2f;
                attackDelay = 0.05f;
                attackRadius = 0f; // precision raycast only
                break;
            case GameManager.WeaponType.Explosive:
                attackSpeed = 1.6f;
                attackDelay = 0.15f;
                attackRadius = 0f;
                break;
            case GameManager.WeaponType.UltimateMelee:
                attackSpeed = 0.45f;
                attackDelay = 0.18f;
                attackRadius = 1.6f;
                break;
            default:
                attackSpeed = 1.0f;
                attackDelay = 0.4f;
                attackRadius = 1.25f;
                break;
        }

        // Update 3rd-person weapon model to match current level
        if (thirdPersonBody != null)
        {
            AttachWeaponToHand(thirdPersonBody);
        }
    }

    public void RefreshGameplayPreferences()
    {
        ApplyGameplayPreferences();
    }

    private void ApplyGameplayPreferences()
    {
        GameManager.PerspectiveMode perspective = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : GameManager.PerspectiveMode.FirstPerson;

        if (perspective == GameManager.PerspectiveMode.ThirdPerson)
        {
            EnableThirdPersonView();
        }
        else
        {
            EnableFirstPersonView();
        }
    }

    private void EnableFirstPersonView()
    {
        if (cam == null)
        {
            return;
        }

        cam.gameObject.SetActive(true);
        cam.transform.SetParent(transform, false);
        cam.transform.localPosition = firstPersonLocalPosition;
        cam.transform.localRotation = firstPersonLocalRotation;

        if (runtimeThirdPersonCamera != null)
        {
            runtimeThirdPersonCamera.gameObject.SetActive(false);
        }

        SetFirstPersonRenderersVisible(true);
        EnsureThirdPersonBody();
        if (thirdPersonBody != null)
        {
            thirdPersonBody.SetActive(false);
        }
    }

    private void EnableThirdPersonView()
    {
        if (cam == null)
        {
            return;
        }

        EnsureThirdPersonCamera();
        EnsureThirdPersonBody();

        cam.gameObject.SetActive(false);
        if (runtimeThirdPersonCamera != null)
        {
            runtimeThirdPersonCamera.gameObject.SetActive(true);
        }

        SetFirstPersonRenderersVisible(false);
        if (thirdPersonBody != null)
        {
            thirdPersonBody.SetActive(true);
        }
    }

    private void EnsureThirdPersonCamera()
    {
        if (runtimeThirdPersonCamera != null)
        {
            thirdPersonCam = runtimeThirdPersonCamera;
            return;
        }

        GameObject cameraObject = new GameObject("RuntimeThirdPersonCamera");
        runtimeThirdPersonCamera = cameraObject.AddComponent<Camera>();
        runtimeThirdPersonCamera.fieldOfView = cam.fieldOfView;
        runtimeThirdPersonCamera.nearClipPlane = cam.nearClipPlane;
        runtimeThirdPersonCamera.farClipPlane = cam.farClipPlane;
        runtimeThirdPersonCamera.clearFlags = cam.clearFlags;
        runtimeThirdPersonCamera.backgroundColor = cam.backgroundColor;
        runtimeThirdPersonCamera.tag = "MainCamera";
        cameraObject.AddComponent<AudioListener>();

        CameraController follow = cameraObject.AddComponent<CameraController>();
        follow.target = transform;
        follow.offset = new Vector3(0f, 3.2f, -5.8f);
        follow.smoothSpeed = 10f;

        thirdPersonCam = runtimeThirdPersonCamera;
    }

    // Track the weapon attached to the player's right hand
    private GameObject equippedWeaponObject;

    private void EnsureThirdPersonBody()
    {
        if (thirdPersonBody != null)
        {
            return;
        }

        // ── Try Mixamo Ch33 player character first ────────────────────────────
        GameObject ch33Prefab = Resources.Load<GameObject>("Player/Ch33Player");
        if (ch33Prefab != null)
        {
            thirdPersonBody = Instantiate(ch33Prefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.identity;
            thirdPersonBody.transform.localScale = Vector3.one;

            Animator ch33Animator = thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (ch33Animator != null)
            {
                // Load humanoid-retargetable animation clips
                AnimationClip idleClip = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/SwordIdle");
                AnimationClip attackClip = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack1");

                // If knight clips not available, try embedded FBX animation
                if (attackClip == null)
                {
                    RuntimeAnimatorController rac = ch33Animator.runtimeAnimatorController;
                    if (rac != null && rac.animationClips.Length > 0)
                        attackClip = rac.animationClips[0];
                }

                CharacterVisualAnimationPlayer animPlayer = thirdPersonBody.AddComponent<CharacterVisualAnimationPlayer>();
                animPlayer.Setup(ch33Animator, idleClip, attackClip);
            }

            AttachWeaponToHand(thirdPersonBody);
            return;
        }

        // ── Fallback: original Paladin knight (without the built-in sword) ───
        GameObject knightPrefab = Resources.Load<GameObject>("ThirdPersonKnight/Paladin WProp J Nordstrom");
        if (knightPrefab != null)
        {
            thirdPersonBody = Instantiate(knightPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.identity;
            thirdPersonBody.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);

            // Hide the knight's baked-in weapon props so only our attached weapon shows
            HideKnightWeaponProp(thirdPersonBody);

            Animator importedAnimator = thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (importedAnimator != null)
            {
                CharacterVisualAnimationPlayer animationPlayer = thirdPersonBody.AddComponent<CharacterVisualAnimationPlayer>();
                animationPlayer.Setup(importedAnimator,
                    Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/SwordIdle"),
                    Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack1"));
            }

            thirdPersonBody.AddComponent<CharacterVisualGrounder>();
            thirdPersonBody.AddComponent<CharacterVisualBob>();
            AttachWeaponToHand(thirdPersonBody);
            return;
        }

        // ── Last resort: primitive mannequin ─────────────────────────────────
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

    // Find the right-hand bone and attach the correct weapon model
    private void AttachWeaponToHand(GameObject body)
    {
        if (body == null) return;

        // Remove existing weapon first
        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
            equippedWeaponObject = null;
        }

        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        // Primary method: use Animator humanoid bone mapping (works for all humanoid rigs)
        Transform handBone = null;
        Animator bodyAnimator = body.GetComponentInChildren<Animator>(true);
        if (bodyAnimator != null && bodyAnimator.isHuman)
        {
            handBone = bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        // Fallback: search by name
        if (handBone == null)
        {
            handBone = FindBone(body.transform, "mixamorig:RightHand")
                    ?? FindBone(body.transform, "RightHand")
                    ?? FindBone(body.transform, "Hand_R")
                    ?? FindBone(body.transform, "right_hand");
        }

        // Last resort: attach to the body root
        Transform attachPoint = handBone != null ? handBone : body.transform;

        equippedWeaponObject = BuildWeaponModel(level, attachPoint);
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

    // Build a weapon visual for the given level and parent it to attachPoint
    private GameObject BuildWeaponModel(int level, Transform attachPoint)
    {
        // Level 2 — try to load the real KnuckleDuster FBX
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

        // Primitive weapon models for all other levels
        return BuildPrimitiveWeapon(level, attachPoint);
    }

    private GameObject BuildPrimitiveWeapon(int level, Transform attachPoint)
    {
        GameObject root = new GameObject("WeaponModel");
        root.transform.SetParent(attachPoint, false);
        root.transform.localPosition = new Vector3(0f, 0.08f, 0.02f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        Color metalSilver = new Color(0.80f, 0.82f, 0.88f);
        Color darkMetal   = new Color(0.28f, 0.28f, 0.32f);
        Color brown       = new Color(0.52f, 0.32f, 0.18f);
        Color yellow      = new Color(0.95f, 0.80f, 0.20f);
        Color orange      = new Color(0.92f, 0.42f, 0.14f);
        Color red         = new Color(0.85f, 0.18f, 0.18f);

        switch (level)
        {
            case 1: // Combat Knife
                CreateWeaponPart(root.transform, "Blade", PrimitiveType.Cube,
                    new Vector3(0f, 0f, 0.12f), new Vector3(0.018f, 0.008f, 0.22f), metalSilver);
                CreateWeaponPart(root.transform, "Guard", PrimitiveType.Cube,
                    new Vector3(0f, 0f, 0f), new Vector3(0.055f, 0.012f, 0.018f), darkMetal);
                CreateWeaponPart(root.transform, "Handle", PrimitiveType.Cube,
                    new Vector3(0f, 0f, -0.07f), new Vector3(0.022f, 0.022f, 0.10f), brown);
                break;

            case 2: // Knuckle Duster (primitive fallback)
                for (int i = 0; i < 4; i++)
                {
                    CreateWeaponPart(root.transform, "Ring_" + i, PrimitiveType.Cylinder,
                        new Vector3((i - 1.5f) * 0.026f, 0f, 0.02f),
                        new Vector3(0.018f, 0.008f, 0.018f), yellow,
                        new Vector3(90f, 0f, 0f));
                }
                CreateWeaponPart(root.transform, "Bar", PrimitiveType.Cube,
                    new Vector3(0f, -0.02f, 0.02f), new Vector3(0.11f, 0.012f, 0.032f), yellow);
                break;

            case 3: // Dumbbell
                CreateWeaponPart(root.transform, "Shaft", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, 0f), new Vector3(0.014f, 0.09f, 0.014f), darkMetal);
                CreateWeaponPart(root.transform, "WeightL", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.10f, 0f), new Vector3(0.045f, 0.022f, 0.045f), darkMetal);
                CreateWeaponPart(root.transform, "WeightR", PrimitiveType.Cylinder,
                    new Vector3(0f, -0.10f, 0f), new Vector3(0.045f, 0.022f, 0.045f), darkMetal);
                break;

            case 4: // Boxing Glove
                CreateWeaponPart(root.transform, "Glove", PrimitiveType.Sphere,
                    new Vector3(0f, 0f, 0.04f), new Vector3(0.07f, 0.07f, 0.09f), red);
                CreateWeaponPart(root.transform, "Wrist", PrimitiveType.Cube,
                    new Vector3(0f, 0f, -0.02f), new Vector3(0.055f, 0.045f, 0.04f), red);
                break;

            case 5: // Wrench
                CreateWeaponPart(root.transform, "Handle", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, -0.02f), new Vector3(0.014f, 0.10f, 0.014f), metalSilver);
                CreateWeaponPart(root.transform, "HeadA", PrimitiveType.Cube,
                    new Vector3(-0.022f, 0f, 0.11f), new Vector3(0.012f, 0.032f, 0.04f), metalSilver);
                CreateWeaponPart(root.transform, "HeadB", PrimitiveType.Cube,
                    new Vector3(0.022f, 0f, 0.11f), new Vector3(0.012f, 0.032f, 0.04f), metalSilver);
                break;

            case 6: // Tennis Racket
                CreateWeaponPart(root.transform, "Handle", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, -0.06f), new Vector3(0.014f, 0.10f, 0.014f), brown);
                CreateWeaponPart(root.transform, "Frame", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, 0.11f), new Vector3(0.11f, 0.008f, 0.14f), metalSilver,
                    new Vector3(90f, 0f, 0f));
                CreateWeaponPart(root.transform, "Strings_H", PrimitiveType.Cube,
                    new Vector3(0f, 0f, 0.11f), new Vector3(0.09f, 0.003f, 0.12f),
                    new Color(0.95f, 0.95f, 0.75f));
                break;

            case 7: // Baseball Bat
                CreateWeaponPart(root.transform, "Handle", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, -0.05f), new Vector3(0.016f, 0.10f, 0.016f), brown);
                CreateWeaponPart(root.transform, "Barrel", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, 0.12f), new Vector3(0.038f, 0.12f, 0.038f), brown);
                break;

            default: // Generic club/bar for higher levels
                float barLen = 0.14f + (level - 8) * 0.004f;
                Color barColor = level >= 16 ? orange : metalSilver;
                CreateWeaponPart(root.transform, "Bar", PrimitiveType.Cylinder,
                    new Vector3(0f, 0f, barLen * 0.5f), new Vector3(0.018f, barLen, 0.018f), barColor);
                if (level >= 15) // Heavy end weight
                {
                    CreateWeaponPart(root.transform, "Head", PrimitiveType.Sphere,
                        new Vector3(0f, 0f, barLen), new Vector3(0.04f, 0.04f, 0.04f), barColor);
                }
                break;
        }

        return root;
    }

    private void CreateWeaponPart(Transform parent, string name, PrimitiveType type,
        Vector3 localPos, Vector3 localScale, Color color)
    {
        CreateWeaponPart(parent, name, type, localPos, localScale, color, Vector3.zero);
    }

    private void CreateWeaponPart(Transform parent, string name, PrimitiveType type,
        Vector3 localPos, Vector3 localScale, Color color, Vector3 eulerRot)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.Euler(eulerRot);
        part.transform.localScale = localScale;
        // Remove colliders so weapon doesn't interfere with physics
        Collider col = part.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Renderer rend = part.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null) mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            rend.material = mat;
        }
    }

    // Hide sword / shield props on the Paladin knight model
    private void HideKnightWeaponProp(GameObject knightBody)
    {
        string[] propNames = { "Sword", "Shield", "sword", "shield", "Weapon", "weapon",
                               "Prop", "prop", "WProp", "wprop" };
        foreach (Transform t in knightBody.GetComponentsInChildren<Transform>(true))
        {
            foreach (string n in propNames)
            {
                if (t.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Disable renderer but keep the transform for bone weighting
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                }
            }
        }
    }

    private GameObject CreateBodyPart(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color)
    {
        return CreateBodyPart(parent, name, primitiveType, localPosition, localScale, color, Vector3.zero);
    }

    private GameObject CreateBodyPart(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color, Vector3 localRotationEuler)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.Euler(localRotationEuler);
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            renderer.material = material;
        }

        return part;
    }

    private void SetFirstPersonRenderersVisible(bool isVisible)
    {
        if (firstPersonRenderers == null)
        {
            return;
        }

        for (int i = 0; i < firstPersonRenderers.Length; i++)
        {
            Renderer rendererComponent = firstPersonRenderers[i];
            if (rendererComponent == null)
            {
                continue;
            }

            if (thirdPersonBody != null && rendererComponent.transform == thirdPersonBody.transform)
            {
                continue;
            }

            rendererComponent.enabled = isVisible;
        }
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
            GameObject hitEffectInstance = Instantiate(hitEffect, pos, Quaternion.identity);
            Destroy(hitEffectInstance, 20f);
        }
    }
}

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
        if (actorRoot == null)
        {
            return;
        }

        float planarSpeed = 0f;
        CharacterController controller = actorRoot.GetComponent<CharacterController>();
        if (controller != null)
        {
            planarSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
        }
        else
        {
            Rigidbody body = actorRoot.GetComponent<Rigidbody>();
            if (body != null)
            {
                planarSpeed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
            }
        }

        float bob = planarSpeed > 0.1f ? Mathf.Sin(Time.time * 10f) * 0.03f : 0f;
        transform.localPosition = baseLocalPosition + new Vector3(0f, bob, 0f);
    }
}

public class CharacterVisualGrounder : MonoBehaviour
{
    private bool grounded;

    private void LateUpdate()
    {
        if (grounded)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        float lowestPoint = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            lowestPoint = Mathf.Min(lowestPoint, renderers[i].bounds.min.y);
        }

        float delta = transform.parent.position.y - lowestPoint;
        transform.position += new Vector3(0f, delta, 0f);
        grounded = true;
    }
}

public class CharacterVisualAnimationPlayer : MonoBehaviour
{
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

    // Combat state — mirrors the video's Animator parameter logic
    private bool isAttacking;
    private int currentAttackIndex = -1;
    private int comboStep;
    private bool comboQueued;
    private const float ComboWindowStart = 0.5f; // normalizedTime when combo input opens
    private const float AutoResetTime = 0.85f;   // normalizedTime when attack auto-resets (like the video's 0.6 threshold)

    // Movement blend
    private Transform parentRoot;
    private float currentWalkWeight;

    // Public state for PlayerController to read
    public bool IsAttacking => isAttacking;
    public bool IsComboReady => !isAttacking || (currentAttackIndex >= 0 && GetAttackNormalizedTime() >= ComboWindowStart);

    public void Setup(Animator animator, AnimationClip idle, AnimationClip attack)
    {
        targetAnimator = animator;
        idleClip = idle;

        // Load all attack clips for combo chain: Attack1 → Attack2 → Attack3 → Kick
        AnimationClip attack1 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack1");
        AnimationClip attack2 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack2");
        AnimationClip attack3 = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack3");
        AnimationClip kick    = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Kick");

        // Build combo chain from available clips
        var clips = new System.Collections.Generic.List<AnimationClip>();
        if (attack1 != null) clips.Add(attack1);
        if (attack2 != null) clips.Add(attack2);
        if (attack3 != null) clips.Add(attack3);
        if (kick != null) clips.Add(kick);
        if (clips.Count == 0 && attack != null) clips.Add(attack); // fallback to single clip
        attackClips = clips.ToArray();

        // Walk clip
        walkClip = Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Block");
        if (walkClip == null) walkClip = idle;

        if (targetAnimator == null || idleClip == null) return;

        parentRoot = transform.parent;
        targetAnimator.runtimeAnimatorController = null;

        // Build PlayableGraph: idle(0), walk(1), attacks(2+)
        int totalInputs = 2 + attackClips.Length;
        graph = PlayableGraph.Create("CombatAnimPlayer");
        output = AnimationPlayableOutput.Create(graph, "Animation", targetAnimator);
        mixer = AnimationMixerPlayable.Create(graph, totalInputs);

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

        // Start in idle
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

        // ── Auto-reset logic (equivalent to video's DisableParamAfterPlaying) ──
        if (isAttacking && currentAttackIndex >= 0)
        {
            float norm = GetAttackNormalizedTime();

            // Combo window: if player queued next attack and we passed the window threshold
            if (comboQueued && norm >= ComboWindowStart)
            {
                comboQueued = false;
                PlayNextCombo();
                return;
            }

            // Auto-reset: animation passed threshold with no combo queued
            if (norm >= AutoResetTime)
            {
                ReturnToIdle();
            }
            return; // don't blend movement while attacking
        }

        // ── Movement blend (equivalent to video's FB/RL parameters) ──
        float speed = 0f;
        if (parentRoot != null)
        {
            CharacterController cc = parentRoot.GetComponent<CharacterController>();
            if (cc != null)
                speed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        }

        float targetWalk = speed > 0.5f ? 1f : 0f;
        currentWalkWeight = Mathf.MoveTowards(currentWalkWeight, targetWalk, 6f * Time.deltaTime);

        SetAllWeights(0f);
        mixer.SetInputWeight(0, 1f - currentWalkWeight);
        mixer.SetInputWeight(1, currentWalkWeight);

        if (walkPlayable.IsValid() && speed > 0.5f)
            walkPlayable.SetSpeed(Mathf.Clamp(speed / 4f, 0.8f, 2f));
    }

    /// <summary>Start a combo attack chain or queue the next hit.</summary>
    public void PlayAttack()
    {
        if (!isInitialized || !graph.IsValid() || attackClips.Length == 0) return;

        if (!isAttacking)
        {
            // Start fresh combo
            comboStep = 0;
            PlayComboStep(0);
        }
        else if (GetAttackNormalizedTime() >= ComboWindowStart * 0.7f)
        {
            // Queue next combo hit (input buffering like the video)
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

        // Zero all weights, full weight on current attack
        SetAllWeights(0f);
        mixer.SetInputWeight(2 + step, 1f);

        // Reset playable to start
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

        float clipLen = attackClips[currentAttackIndex].length;
        if (clipLen <= 0f) return 1f;
        return (float)(attackPlayables[currentAttackIndex].GetTime() / clipLen);
    }

    public void ResetAttack()
    {
        ReturnToIdle();
    }

    private void ReturnToIdle()
    {
        isAttacking = false;
        currentAttackIndex = -1;
        comboStep = 0;
        comboQueued = false;
        // Update() will smoothly blend back to idle/walk
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
