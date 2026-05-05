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
    [SerializeField] private float moveSpeed = 7.0f;
    [SerializeField] private float sprintSpeed = 12.0f;
    [SerializeField] private float rotationSpeed = 40f;
    [SerializeField] private bool instantAcceleration = true;
    [SerializeField] private float acceleration = 9999f;
    [SerializeField] private float deceleration = 9999f;

    [Header("Slide (Warzone-style)")]
    [SerializeField] private bool enableSlide = true;
    [SerializeField] private float slideDuration = 0.45f;
    [SerializeField] private float slideSpeedMultiplier = 1.25f;
    [SerializeField] private float slideCooldown = 0.35f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 2.5f;
    [SerializeField] private float gravity = -32f;
    [SerializeField] private float groundedOffset = 0.1f;
    [SerializeField] private float groundedRadius = 0.28f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private CharacterController _controller;
    private Vector3 _moveVelocity;
    private float _verticalVelocity;
    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isSliding;
    private float _slideTimer;
    private float _nextSlideTime;

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

        // Slide trigger: crouch while sprinting + grounded.
        if (enableSlide && _isGrounded && !_isSliding && Time.time >= _nextSlideTime)
        {
            bool crouchPressed = Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame;
            if (crouchPressed && _isSprinting && inputDirection.sqrMagnitude > 0.2f)
            {
                _isSliding = true;
                _slideTimer = slideDuration;
            }
        }

        float baseSpeed = _isSprinting ? sprintSpeed : moveSpeed;
        float targetSpeed = inputDirection.sqrMagnitude > 0.001f ? baseSpeed : 0f;

        Vector3 targetVelocity = MoveDirection * targetSpeed;
        if (_isSliding)
        {
            // Maintain forward momentum during slide; full air-control is handled elsewhere.
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : MoveDirection;
            _moveVelocity = forward * (sprintSpeed * Mathf.Max(1f, slideSpeedMultiplier));
        }
        else if (instantAcceleration)
        {
            // Instant acceleration/deceleration: no ramp-up.
            _moveVelocity = targetVelocity;
        }
        else
        {
            float rate = inputDirection.sqrMagnitude > 0.001f ? acceleration : deceleration;
            _moveVelocity = Vector3.MoveTowards(_moveVelocity, targetVelocity, rate * Time.deltaTime);
        }

        if (MoveDirection.sqrMagnitude > 0.001f && !_isSliding)
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
        // 100% air control: always allow full horizontal direction changes mid-air.
        // Because we use a CharacterController (not Rigidbody), we can simply
        // apply the requested horizontal velocity every frame.
        Vector3 finalVelocity = _moveVelocity;
        finalVelocity.y = _verticalVelocity;
        _controller.Move(finalVelocity * Time.deltaTime);
    }

    private void LateUpdate()
    {
        // Slide timer & cooldown.
        if (_isSliding)
        {
            _slideTimer -= Time.deltaTime;
            if (_slideTimer <= 0f)
            {
                _isSliding = false;
                _nextSlideTime = Time.time + slideCooldown;
            }
        }
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
