using UnityEngine;

public class CameraController : MonoBehaviour
{
    // Target to follow (the player root transform)
    public Transform target;

    // Offset in local camera space (behind and above the player)
    public Vector3 offset = new Vector3(0f, 2.2f, -3.2f);

    // Smoothness of camera position interpolation
    public float smoothSpeed = 10f;

    // Vertical pitch in degrees — set externally by PlayerController for mouse look in TP mode
    [HideInInspector] public float pitch = 0f;

    void LateUpdate()
    {
        if (target == null) return;

        // Build orbit rotation: horizontal from the player's current Y rotation, vertical from pitch
        // This lets the camera orbit both horizontally (by rotating the player body) and vertically
        Quaternion orbitRotation = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3 desiredPosition = target.position + orbitRotation * offset;

        // Smoothly interpolate camera position to avoid jitter on fast turns
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Always look at the player's center of mass (chest height)
        transform.LookAt(target.position + Vector3.up * 1.35f);
    }
}
