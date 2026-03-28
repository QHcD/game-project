using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.0f;
    public float runSpeed = 6.0f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Player Stats (Health System)")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Attack Settings")]
    public float attackRange = 2.5f;      // مسافة الطعنة
    public float attackDamage = 50f;     // قوة الضربة
    public LayerMask enemyLayer;         // حدد طبقة الأعداء من المفتش (Inspector)

    [Header("Look Settings")]
    public float mouseSensitivity = 100f;
    public Transform playerBody;
    private float xRotation = 0f;

    [Header("Camera System")]
    public Camera firstPersonCam;
    public Camera thirdPersonCam;
    private bool isFirstPerson = false;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // تعيين الصحة عند البداية
        currentHealth = maxHealth;

        // إخفاء الماوس داخل اللعبة
        Cursor.lockState = CursorLockMode.Locked;

        // البحث عن الكاميرات تلقائياً بأسماء النظام عندنا
        if (firstPersonCam == null) firstPersonCam = GameObject.Find("FirstPersonCam")?.GetComponent<Camera>();
        if (thirdPersonCam == null) thirdPersonCam = GameObject.Find("ThirdPersonCam")?.GetComponent<Camera>();

        UpdateCameraMode();
    }

    void Update()
    {
        HandleMovement();
        HandleLook();
        HandleCameraSwitch();
        HandleAttack(); // تفعيل نظام الهجوم في كل فريم
    }

    // --- وظيفة الهجوم بالسكينة ---
    void HandleAttack()
    {
        // التحقق من ضغطة زر الماوس الأيسر
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("PRISM-7: Knife Attack!");

            // تحديد الكاميرا الحالية التي نطلق منها الشعاع
            Camera activeCam = isFirstPerson ? firstPersonCam : thirdPersonCam;

            RaycastHit hit;
            // إطلاق شعاع وهمي من وسط الكاميرا للأمام
            if (Physics.Raycast(activeCam.transform.position, activeCam.transform.forward, out hit, attackRange, enemyLayer))
            {
                // إذا لمسنا شيئاً يحمل تاغ "Enemy"
                if (hit.collider.CompareTag("Enemy"))
                {
                    Debug.Log("Direct Hit on: " + hit.collider.name);

                    // محاولة الوصول لسكربت العدو لإنقاص دمه
                    EnemyController enemy = hit.collider.GetComponent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(attackDamage);
                    }
                }
            }
        }
    }

    // --- وظيفة استقبال الضرر ---
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log("PRISM-7 Player Hit! Health: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Mission Failed! Player Died.");
        // لإعادة تشغيل المرحلة (اختياري)
        // UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = 0;
        float z = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) z = 1;
            if (Keyboard.current.sKey.isPressed) z = -1;
            if (Keyboard.current.aKey.isPressed) x = -1;
            if (Keyboard.current.dKey.isPressed) x = 1;
        }

        float currentSpeed = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ? runSpeed : walkSpeed;

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

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
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            firstPersonCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }
        else
        {
            transform.Rotate(Vector3.up * mouseX);
        }
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
}