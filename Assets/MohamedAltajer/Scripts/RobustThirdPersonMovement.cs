using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Clean third-person movement controller for melee games.
/// Uses CharacterController because it is more predictable than Rigidbody for
/// close-range combat, root-less animation locomotion, and tight indoor maps.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RobustThirdPersonMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.2f;
    [SerializeField] private float sprintSpeed = 8.0f;
    [SerializeField] private float rotationSpeed = 16f;
    [SerializeField] private float acceleration = 26f;
    [SerializeField] private float deceleration = 32f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -28f;
    [SerializeField] private float groundedOffset = 0.1f;
    [SerializeField] private float groundedRadius = 0.28f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private CharacterController _controller;
    private Vector3 _moveVelocity;
    private float _verticalVelocity;
    private bool _isGrounded;
    private bool _isSprinting;

    public Vector3 MoveDirection { get; private set; }
    public bool IsGrounded => _isGrounded;
    public bool IsSprinting => _isSprinting;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        UpdateGroundedState();
        HandleMovement();
        HandleJumpAndGravity();
        ApplyMovement();
        UpdateAnimator();
    }

    private void UpdateGroundedState()
    {
        Vector3 spherePosition = transform.position + Vector3.down * groundedOffset;
        _isGrounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

        if (_isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
    }

    private void HandleMovement()
    {
        Vector2 input = ReadMoveInput();
        _isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        Vector3 inputDirection = new Vector3(input.x, 0f, input.y);
        inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

        MoveDirection = inputDirection.normalized;

        float targetSpeed = inputDirection.sqrMagnitude > 0.001f
            ? (_isSprinting ? sprintSpeed : moveSpeed)
            : 0f;

        Vector3 targetVelocity = MoveDirection * targetSpeed;
        // Separate accel/decel: ramp up fast, but brake even faster on key release
        // so the player does not slide past their stopping point.
        float rate = inputDirection.sqrMagnitude > 0.001f ? acceleration : deceleration;
        _moveVelocity = Vector3.MoveTowards(_moveVelocity, targetVelocity, rate * Time.deltaTime);

        if (MoveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(MoveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleJumpAndGravity()
    {
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (_isGrounded && jumpPressed)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _verticalVelocity += gravity * Time.deltaTime;
    }

    private void ApplyMovement()
    {
        Vector3 finalVelocity = _moveVelocity;
        finalVelocity.y = _verticalVelocity;
        _controller.Move(finalVelocity * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        float horizontalSpeed = new Vector3(_moveVelocity.x, 0f, _moveVelocity.z).magnitude;
        float normalizedSpeed = sprintSpeed > 0.01f ? horizontalSpeed / sprintSpeed : 0f;

        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsGrounded", _isGrounded);
        animator.SetBool("IsSprinting", _isSprinting && horizontalSpeed > 0.1f);
    }

    private static Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null)
            return Vector2.zero;

        Vector2 input = Vector2.zero;
        Keyboard k = Keyboard.current;
        if (k.wKey.isPressed || k.upArrowKey.isPressed || k.numpad8Key.isPressed) input.y += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed || k.numpad2Key.isPressed) input.y -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed || k.numpad6Key.isPressed) input.x += 1f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed || k.numpad4Key.isPressed) input.x -= 1f;
        return input;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 spherePosition = transform.position + Vector3.down * groundedOffset;
        Gizmos.DrawWireSphere(spherePosition, groundedRadius);
    }
}
