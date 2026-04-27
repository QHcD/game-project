using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smooth first-person mouse look with clamped vertical rotation so the
/// player cannot flip upside down.
/// </summary>
public sealed class FirstPersonMouseLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerYawRoot;
    [SerializeField] private Transform cameraPitchRoot;

    [Header("Sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.14f;
    [SerializeField] private float smoothing = 18f;
    [SerializeField] private float maxMouseDeltaPerFrame = 80f;

    [Header("Pitch Clamp")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursor = true;

    private float _yaw;
    private float _pitch;
    private Vector2 _smoothedDelta;

    private void Awake()
    {
        if (playerYawRoot == null)
            playerYawRoot = transform;

        if (cameraPitchRoot == null && Camera.main != null)
            cameraPitchRoot = Camera.main.transform.parent != null ? Camera.main.transform.parent : Camera.main.transform;

        _yaw = playerYawRoot != null ? playerYawRoot.eulerAngles.y : 0f;
        _pitch = cameraPitchRoot != null ? NormalizePitch(cameraPitchRoot.localEulerAngles.x) : 0f;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (playerYawRoot == null || cameraPitchRoot == null || Mouse.current == null)
            return;

        Vector2 rawDelta = Mouse.current.delta.ReadValue();
        rawDelta = Vector2.ClampMagnitude(rawDelta, Mathf.Max(1f, maxMouseDeltaPerFrame));

        float smoothT = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        _smoothedDelta = Vector2.Lerp(_smoothedDelta, rawDelta, smoothT);

        _yaw += _smoothedDelta.x * mouseSensitivity;
        _pitch -= _smoothedDelta.y * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        playerYawRoot.rotation = Quaternion.Euler(0f, _yaw, 0f);
        cameraPitchRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private static float NormalizePitch(float angle)
    {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}
