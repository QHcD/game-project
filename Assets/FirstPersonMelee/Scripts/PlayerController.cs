using UnityEngine;
using UnityEngine.InputSystem;

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
            attackCount++;
        }
        else
        {
            ChangeAnimationState(ATTACK2);
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

        LayerMask mask = attackLayer.value == 0 ? resolvedAttackMask : attackLayer;
        if (Physics.SphereCast(cam.transform.position, 0.32f, cam.transform.forward, out RaycastHit hit, attackDistance, mask, QueryTriggerInteraction.Ignore))
        {
            HitTarget(hit.point);

            Actor actor = hit.transform.GetComponentInParent<Actor>();
            if (actor != null && actor.gameObject != gameObject)
            {
                actor.TakeDamage(attackDamage);
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
            thirdPersonBody.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            thirdPersonBody.transform.localRotation = Quaternion.identity;
            thirdPersonBody.transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);

            foreach (Animator childAnimator in thirdPersonBody.GetComponentsInChildren<Animator>(true))
            {
                childAnimator.enabled = false;
            }

            RemoveVisibleWeapons(thirdPersonBody.transform);
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

    private void RemoveVisibleWeapons(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string loweredName = child.name.ToLowerInvariant();
            if (loweredName.Contains("sword") || loweredName.Contains("shield") || loweredName.Contains("weapon"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
