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
    public float gravity = -9.8f;
    public float jumpHeight = 1.2f;

    private Vector3 playerVelocity;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool wasMoving;

    private bool isGrounded;

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

        MoveInput(moveInput);
        LookInput(lookInput);

        SetAnimations();
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
        Vector3 moveDirection = new Vector3(inputValue.x, 0f, inputValue.y);
        Vector3 worldMove = transform.TransformDirection(moveDirection) * moveSpeed;

        controller.Move(worldMove * Time.deltaTime);

        if (isGrounded && playerVelocity.y < 0f)
        {
            playerVelocity.y = -2f;
        }

        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        wasMoving = inputValue.sqrMagnitude > 0.01f;
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
        if (!attacking)
        {
            if (wasMoving)
            {
                ChangeAnimationState(WALK);
            }
            else
            {
                ChangeAnimationState(IDLE);
            }
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

    public GameObject hitEffect;
    public AudioClip swordSwing;
    public AudioClip hitSound;

    private bool attacking;
    private bool readyToAttack = true;
    private int attackCount;

    public void Attack()
    {
        if (!readyToAttack || attacking)
        {
            return;
        }

        readyToAttack = false;
        attacking = true;

        Invoke(nameof(ResetAttack), attackSpeed);
        Invoke(nameof(AttackRaycast), attackDelay);

        if (audioSource != null && swordSwing != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(swordSwing);
        }

        if (attackCount == 0)
        {
            ChangeAnimationState(ATTACK1);
            CharacterVisualAnimationPlayer visualAnimation = thirdPersonBody != null ? thirdPersonBody.GetComponentInChildren<CharacterVisualAnimationPlayer>(true) : null;
            visualAnimation?.PlayAttack();
            attackCount++;
        }
        else
        {
            ChangeAnimationState(ATTACK2);
            CharacterVisualAnimationPlayer visualAnimation = thirdPersonBody != null ? thirdPersonBody.GetComponentInChildren<CharacterVisualAnimationPlayer>(true) : null;
            visualAnimation?.PlayAttack();
            attackCount = 0;
        }
    }

    private void ResetAttack()
    {
        attacking = false;
        readyToAttack = true;
    }

    private void AttackRaycast()
    {
        if (cam == null)
        {
            return;
        }

        Vector3 hitCenter = cam.transform.position + cam.transform.forward * attackDistance;
        Collider[] hits = Physics.OverlapSphere(hitCenter, attackRadius, resolvedAttackMask, QueryTriggerInteraction.Ignore);
        bool landedHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Actor actor = hits[i].GetComponentInParent<Actor>();
            if (actor != null && actor.gameObject != gameObject)
            {
                actor.TakeDamage(attackDamage);
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

    public void EquipWeaponForLevel(int level)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        equippedWeaponName = GameManager.Instance.GetWeaponNameForLevel(level);
        attackDamage = Mathf.RoundToInt(GameManager.Instance.GetWeaponDamageForLevel(level));
        attackDistance = GameManager.Instance.GetWeaponRangeForLevel(level);
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

    private void EnsureThirdPersonBody()
    {
        if (thirdPersonBody != null)
        {
            return;
        }

        GameObject knightPrefab = Resources.Load<GameObject>("ThirdPersonKnight/Paladin WProp J Nordstrom");
        if (knightPrefab != null)
        {
            thirdPersonBody = Instantiate(knightPrefab, transform);
            thirdPersonBody.name = "ThirdPersonBody";
            thirdPersonBody.transform.localPosition = Vector3.zero;
            thirdPersonBody.transform.localRotation = Quaternion.identity;
            thirdPersonBody.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);

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
            return;
        }

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
    private AnimationClip attackClip;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable currentPlayable;
    private string currentState;

    public void Setup(Animator animator, AnimationClip idle, AnimationClip attack)
    {
        targetAnimator = animator;
        idleClip = idle;
        attackClip = attack;

        if (targetAnimator == null || idleClip == null)
        {
            return;
        }

        targetAnimator.runtimeAnimatorController = null;
        graph = PlayableGraph.Create("CharacterVisualAnimationPlayer");
        output = AnimationPlayableOutput.Create(graph, "Animation", targetAnimator);
        currentPlayable = AnimationClipPlayable.Create(graph, idleClip);
        currentPlayable.SetApplyFootIK(false);
        currentPlayable.SetDuration(idleClip.length);
        output.SetSourcePlayable(currentPlayable);
        graph.Play();
        currentState = "Idle";
    }

    public void PlayAttack()
    {
        if (attackClip == null || targetAnimator == null)
        {
            return;
        }

        PlayClip(attackClip, false);
        CancelInvoke(nameof(ReturnToIdle));
        Invoke(nameof(ReturnToIdle), Mathf.Max(0.2f, attackClip.length));
    }

    public void ResetAttack()
    {
        ReturnToIdle();
    }

    private void ReturnToIdle()
    {
        if (idleClip != null)
        {
            PlayClip(idleClip, true);
        }
    }

    private void PlayClip(AnimationClip clip, bool loop)
    {
        if (!graph.IsValid())
        {
            return;
        }

        if (currentPlayable.IsValid())
        {
            currentPlayable.Destroy();
        }

        currentPlayable = AnimationClipPlayable.Create(graph, clip);
        currentPlayable.SetApplyFootIK(false);
        currentPlayable.SetDuration(clip.length);
        currentPlayable.SetTime(0d);
        if (!loop)
        {
            currentPlayable.SetDone(false);
        }

        output.SetSourcePlayable(currentPlayable);
        currentState = clip == idleClip ? "Idle" : "Attack";
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }
}
