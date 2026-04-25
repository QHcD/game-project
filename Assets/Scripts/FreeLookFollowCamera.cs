using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Unrestricted third-person mouse look camera.
/// Full 360 Y rotation, clamped X rotation, always follows the player.
/// </summary>
public class FreeLookFollowCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.7f, 0f);

    [Header("Orbit")]
    [SerializeField] private float distance = 4.5f;
    [SerializeField] private float sensitivityX = 180f;
    [SerializeField] private float sensitivityY = 140f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private float followSmoothTime = 14f;
    [SerializeField] private float lookSmoothTime = 18f;

    [Header("Collision")]
    [SerializeField] private bool preventClipping = true;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float collisionPadding = 0.1f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    private float _yaw;
    private float _pitch = 18f;

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (target != null)
            _yaw = target.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        _yaw += mouseDelta.x * sensitivityX * Time.deltaTime;
        _pitch -= mouseDelta.y * sensitivityY * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        Vector3 pivotPosition = target.position + pivotOffset;
        Quaternion lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 desiredDirection = lookRotation * Vector3.back;
        Vector3 desiredPosition = pivotPosition + desiredDirection * distance;

        if (preventClipping)
        {
            Vector3 castDirection = desiredPosition - pivotPosition;
            float castDistance = castDirection.magnitude;
            if (castDistance > 0.001f)
            {
                castDirection /= castDistance;
                if (Physics.SphereCast(pivotPosition, collisionRadius, castDirection, out RaycastHit hit, castDistance, collisionLayers, QueryTriggerInteraction.Ignore))
                    desiredPosition = pivotPosition + castDirection * Mathf.Max(0.5f, hit.distance - collisionPadding);
            }
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSmoothTime * Time.deltaTime);
        Quaternion targetRotation = Quaternion.LookRotation((pivotPosition - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookSmoothTime * Time.deltaTime);
    }
}
