using UnityEngine;

/// <summary>
/// Third-person orbit camera with wall-collision avoidance.
/// The camera casts a thick sphere (SphereCast) from the player towards its desired
/// position. If geometry is in the way, the camera pulls forward so it never clips
/// through walls, buildings, or terrain.
/// </summary>
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

    // ── Camera Collision ────────────────────────────────────────────────────
    [Header("Wall Collision")]
    [Tooltip("Enable camera collision avoidance with walls/buildings.")]
    public bool enableCollision = true;

    [Tooltip("SphereCast radius — thicker = more conservative (less clipping).")]
    [Range(0.1f, 0.5f)]
    public float collisionRadius = 0.25f;

    [Tooltip("Extra padding from the wall surface (prevents near-plane clipping).")]
    [Range(0.05f, 0.5f)]
    public float wallPadding = 0.15f;

    [Tooltip("Layers the camera treats as solid obstacles.")]
    public LayerMask collisionMask = ~0; // Everything by default

    [Tooltip("How quickly the camera recovers to full distance after leaving a wall.")]
    public float recoverySpeed = 8f;

    // Smooth recovery (avoids pop when leaving a wall)
    private float _currentDistance;
    private float _maxDistance;

    private void Start()
    {
        _maxDistance     = offset.magnitude;
        _currentDistance = _maxDistance;
    }

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

        // ── Wall Collision ──────────────────────────────────────────────────
        if (enableCollision)
            desiredPosition = ResolveCollision(desiredPosition, orbitRotation);

        // Smoothly interpolate camera position to avoid jitter on fast turns.
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        transform.LookAt(GetLookTarget());
    }

    /// <summary>
    /// SphereCasts from the player towards the desired camera position.
    /// If the cast hits a wall, pulls the camera forward to the hit point
    /// plus a small padding offset to avoid near-plane clipping.
    /// Smoothly recovers to full distance when the wall is cleared.
    /// </summary>
    private Vector3 ResolveCollision(Vector3 desiredPosition, Quaternion orbitRotation)
    {
        // Cast origin: slightly above the player's pivot (roughly chest height)
        Vector3 castOrigin = target.position + Vector3.up * 1.2f;
        Vector3 castDir    = (desiredPosition - castOrigin);
        float   castDist   = castDir.magnitude;
        castDir.Normalize();

        float targetDistance = _maxDistance;

        if (Physics.SphereCast(castOrigin, collisionRadius, castDir, out RaycastHit hit,
                               castDist, collisionMask, QueryTriggerInteraction.Ignore))
        {
            // Pull the camera to the hit point, minus padding so the near plane stays clear
            targetDistance = Mathf.Max(hit.distance - wallPadding, 0.3f);
        }

        // Smooth recovery: snap inward instantly, ease outward slowly
        if (targetDistance < _currentDistance)
            _currentDistance = targetDistance;                                  // snap in
        else
            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance,
                                          recoverySpeed * Time.deltaTime);     // ease out

        // Rebuild position along the same orbit direction but at the clamped distance
        Vector3 offsetDir = (orbitRotation * offset).normalized;
        return castOrigin + offsetDir * _currentDistance;
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        Quaternion orbitRotation = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3 desiredPosition = target.position + orbitRotation * offset;

        if (enableCollision)
            desiredPosition = ResolveCollision(desiredPosition, orbitRotation);

        _currentDistance = Vector3.Distance(target.position + Vector3.up * 1.2f, desiredPosition);
        transform.position = desiredPosition;
        transform.LookAt(GetLookTarget());
    }

    private Vector3 GetLookTarget()
    {
        if (target == null) return transform.position + transform.forward * 5f;
        return target.position + (target.forward * lookAheadDistance) + Vector3.up * lookHeight;
    }
}
