using UnityEngine;

public class CameraController : MonoBehaviour
{
    // Target to follow (the player root transform)
    public Transform target;

    // Offset in local camera space (behind and above the player)
    public Vector3 offset = new Vector3(0f, 3.4f, -7.2f);

    // Smoothness of camera position interpolation
    public float smoothSpeed = 10f;

    // Look a little ahead of the player so the screen shows more of the arena
    // and approaching enemies instead of centering too tightly on the feet.
    public float lookAheadDistance = 4.5f;
    public float lookHeight = 1.6f;

    // Vertical pitch in degrees — set externally by PlayerController for mouse look in TP mode
    [HideInInspector] public float pitch = 0f;

    void LateUpdate()
    {
        // Auto-find the player if target was never assigned or was destroyed.
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                SnapToTarget();
            }
            else
            {
                return;
            }
        }

        // Build orbit rotation: horizontal from the player's current Y rotation, vertical from pitch.
        Quaternion orbitRotation = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3 desiredPosition = target.position + orbitRotation * offset;

        // Smoothly interpolate camera position to avoid jitter on fast turns.
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        transform.LookAt(GetLookTarget());
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        Quaternion orbitRotation = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        transform.position = target.position + orbitRotation * offset;
        transform.LookAt(GetLookTarget());
    }

    private Vector3 GetLookTarget()
    {
        if (target == null) return transform.position + transform.forward * 5f;
        return target.position + (target.forward * lookAheadDistance) + Vector3.up * lookHeight;
    }
}
