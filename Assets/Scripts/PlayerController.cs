using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("Health")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Camera")]
    public Camera firstPersonCam;
    public Camera thirdPersonCam;
    private bool isFirstPerson = false;

    [Header("Attack")]
    public float attackRange = 2f;
    public float attackCooldown = 0.5f;
    private float lastAttackTime;
    public Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private float gravity = -9.81f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentHealth = maxHealth;

        // Default: Third Person
        SetThirdPerson();

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        HandleMovement();
        HandleCameraSwitch();
        HandleAttack();
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Gravity
        if (!controller.isGrounded)
            velocity.y += gravity * Time.deltaTime;
        else
            velocity.y = -2f;

        controller.Move(velocity * Time.deltaTime);

        // Mouse look
        float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
        transform.Rotate(Vector3.up * mouseX);

        // Animation
        bool isMoving = h != 0 || v != 0;
        if (animator != null)
            animator.SetBool("IsMoving", isMoving);
    }

    void HandleCameraSwitch()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            isFirstPerson = !isFirstPerson;
            if (isFirstPerson) SetFirstPerson();
            else SetThirdPerson();
        }
    }

    void SetFirstPerson()
    {
        firstPersonCam.gameObject.SetActive(true);
        thirdPersonCam.gameObject.SetActive(false);
    }

    void SetThirdPerson()
    {
        firstPersonCam.gameObject.SetActive(false);
        thirdPersonCam.gameObject.SetActive(true);
    }

    void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0) && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;

            if (animator != null)
                animator.SetTrigger("Attack");

            // Raycast attack
            RaycastHit hit;
            Camera activeCam = isFirstPerson ? firstPersonCam : thirdPersonCam;
            if (Physics.Raycast(activeCam.transform.position, activeCam.transform.forward, out hit, attackRange))
            {
                EnemyController enemy = hit.collider.GetComponent<EnemyController>();
                if (enemy != null)
                    enemy.TakeDamage(GetWeaponDamage());
            }
        }
    }

    float GetWeaponDamage()
    {
        if (GameManager.Instance == null) return 25f;
        int level = GameManager.Instance.currentLevel;
        // Guns do more damage
        return level >= 16 ? 100f : 25f;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void Die()
    {
        if (animator != null)
            animator.SetTrigger("Die");

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
    }

    public float GetHealthPercent()
    {
        return currentHealth / maxHealth;
    }
}
