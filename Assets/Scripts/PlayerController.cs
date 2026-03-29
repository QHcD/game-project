using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4.5f;
    public float runSpeed = 7f;
    public float jumpHeight = 1.5f;
    public float gravity = -18f;

    [Header("Player Stats")]
    public float maxHealth = 100f;

    [Header("Combat")]
    public LayerMask enemyLayer;
    public float attackCooldown = 0.55f;

    [Header("Look Settings")]
    public float mouseSensitivity = 100f;
    public Transform playerBody;

    [Header("Camera System")]
    public Camera firstPersonCam;
    public Camera thirdPersonCam;

    [Header("Runtime Weapon")]
    public string equippedWeaponName = "Combat Knife";
    public float equippedWeaponDamage = 32f;
    public float equippedWeaponRange = 2f;

    private float xRotation = 0f;
    private bool isFirstPerson = false;
    private float nextAttackTime = 0f;

    private CharacterController controller;
    private PlayerHealth playerHealth;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.radius = 0.35f;
        controller.height = 1.8f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = gameObject.AddComponent<PlayerHealth>();
        playerHealth.SetMaxHealth(maxHealth, true);

        gameObject.tag = "Player";

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (firstPersonCam == null) firstPersonCam = GameObject.Find("FirstPersonCam")?.GetComponent<Camera>();
        if (thirdPersonCam == null) thirdPersonCam = GameObject.Find("ThirdPersonCam")?.GetComponent<Camera>();

        if (playerBody == null)
            playerBody = transform;

        equippedWeaponName = "Starter Knife";
        equippedWeaponDamage = 28f;
        equippedWeaponRange = 1.9f;
        CreateRuntimeWeaponModel(new Color(0.82f, 0.86f, 0.9f), 1);
        if (HUDManager.Instance != null && HUDManager.Instance.weaponText != null)
            HUDManager.Instance.weaponText.text = equippedWeaponName;
        UpdateCameraMode();
    }

    void Update()
    {
        HandleMovement();
        HandleLook();
        HandleCameraSwitch();
        HandleAttack();
    }

    public void EquipWeaponForLevel(int level)
    {
        if (GameManager.Instance == null) return;

        equippedWeaponName = GameManager.Instance.GetWeaponNameForLevel(level);
        equippedWeaponDamage = GameManager.Instance.GetWeaponDamageForLevel(level);
        equippedWeaponRange = GameManager.Instance.GetWeaponRangeForLevel(level);

        CreateRuntimeWeaponModel(GameManager.Instance.GetWeaponColorForLevel(level), level);

        if (HUDManager.Instance != null && HUDManager.Instance.weaponText != null)
            HUDManager.Instance.weaponText.text = equippedWeaponName;
    }

    public void TakeDamage(float damage)
    {
        playerHealth?.TakeDamage(damage);
    }

    void HandleAttack()
    {
        if (Mouse.current == null || Time.time < nextAttackTime) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        nextAttackTime = Time.time + attackCooldown;

        Vector3 attackOrigin = transform.position + Vector3.up * 1.1f + transform.forward * 1.0f;
        Collider[] hits = Physics.OverlapSphere(attackOrigin, equippedWeaponRange, enemyLayer.value == 0 ? ~0 : enemyLayer.value);

        EnemyController bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = enemy;
            }
        }

        if (bestTarget != null)
            bestTarget.TakeDamage(equippedWeaponDamage);
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        float x = 0f;
        float z = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) z = 1f;
            if (Keyboard.current.sKey.isPressed) z = -1f;
            if (Keyboard.current.aKey.isPressed) x = -1f;
            if (Keyboard.current.dKey.isPressed) x = 1f;
        }

        float currentSpeed = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ? runSpeed : walkSpeed;

        Vector3 move = (transform.right * x + transform.forward * z).normalized;
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleLook()
    {
        if (Mouse.current == null) return;

        float mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity * Time.deltaTime;
        float mouseY = Mouse.current.delta.y.ReadValue() * mouseSensitivity * Time.deltaTime;

        if (isFirstPerson && firstPersonCam != null)
        {
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -85f, 85f);
            firstPersonCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleCameraSwitch()
    {
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            isFirstPerson = !isFirstPerson;
            UpdateCameraMode();
        }
    }

    void UpdateCameraMode()
    {
        if (firstPersonCam != null) firstPersonCam.gameObject.SetActive(isFirstPerson);
        if (thirdPersonCam != null) thirdPersonCam.gameObject.SetActive(!isFirstPerson);
    }

    void CreateRuntimeWeaponModel(Color color, int level)
    {
        Transform holdPoint = transform.Find("WeaponHoldPoint");
        if (holdPoint == null)
        {
            GameObject hp = new GameObject("WeaponHoldPoint");
            hp.transform.SetParent(transform);
            hp.transform.localPosition = new Vector3(0.4f, 1.1f, 0.6f);
            holdPoint = hp.transform;
        }

        foreach (Transform child in holdPoint)
            Destroy(child.gameObject);

        PrimitiveType type = level % 4 == 0 ? PrimitiveType.Cube :
            level % 4 == 1 ? PrimitiveType.Cylinder :
            level % 4 == 2 ? PrimitiveType.Capsule :
            PrimitiveType.Sphere;

        GameObject weapon = GameObject.CreatePrimitive(type);
        weapon.name = equippedWeaponName.Replace(" ", string.Empty);
        weapon.transform.SetParent(holdPoint, false);
        weapon.transform.localPosition = new Vector3(0f, 0f, 0f);
        weapon.transform.localRotation = Quaternion.Euler(15f, 0f, 90f);
        weapon.transform.localScale = level % 4 == 3 ? new Vector3(0.18f, 0.45f, 0.18f) : new Vector3(0.12f, 0.45f, 0.12f);

        Collider col = weapon.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer renderer = weapon.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.material = mat;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.1f + transform.forward, equippedWeaponRange);
    }
}
