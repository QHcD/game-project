using UnityEngine;

/// <summary>
/// Over-the-shoulder third-person camera.
///
/// Changes from original:
///   • Offset is now right-shoulder (X = +1.2) instead of dead-centre.
///   • No zoom logic — zoom has been removed entirely.
///   • LookAt targets the character's spine/chest bone so the body is always
///     centred on screen instead of empty space ahead of the player.
///   • Near clip plane is set to 0.01 in Start() to stop the camera from
///     rendering the inside of the character mesh.
///   • Wall collision uses a SphereCast so the camera never enters buildings
///     or terrain; it snaps inward instantly and eases outward smoothly.
/// </summary>
public class CameraController : MonoBehaviour
{
    // ── Target ───────────────────────────────────────────────────────────────
    [Tooltip("The player root transform. Auto-found by 'Player' tag if null.")]
    public Transform target;

    // ── Over-the-Shoulder offset ─────────────────────────────────────────────
    [Header("OTS Offset")]
    [Tooltip("Camera position relative to the player in local (orbit) space.\n" +
             "X +1.2 = right shoulder, Y = height above pivot, Z = distance behind.")]
    public Vector3 offset = new Vector3(1.2f, 2.2f, -5.5f);

    [Tooltip("Smooth-follow speed. Higher = snappier.")]
    public float smoothSpeed = 12f;

    // Vertical pitch (degrees) — set each frame by PlayerController.ApplyLook
    [HideInInspector] public float pitch = 0f;

    // ── Look-at height ───────────────────────────────────────────────────────
    [Header("Look-At")]
    [Tooltip("World-space height to add to the player position when no spine bone is found.")]
    public float lookHeight = 1.4f;

    // ── Wall / Mesh Collision ────────────────────────────────────────────────
    [Header("Wall Collision")]
    [Tooltip("Enable SphereCast collision so the camera never enters walls.")]
    public bool enableCollision = true;

    [Tooltip("SphereCast radius. Larger = more conservative pull-in.")]
    [Range(0.05f, 0.5f)]
    public float collisionRadius = 0.2f;

    [Tooltip("Minimum distance the camera is allowed to reach (prevents pop-through).")]
    public float minDistance = 0.5f;

    [Tooltip("Padding gap between camera and wall surface.")]
    [Range(0.02f, 0.3f)]
    public float wallPadding = 0.1f;

    [Tooltip("Layers treated as solid obstacles (walls, terrain, buildings).")]
    public LayerMask collisionMask = ~0;

    [Tooltip("How quickly the camera eases back to full distance after clearing a wall.")]
    public float recoverySpeed = 6f;

    // ── Private ──────────────────────────────────────────────────────────────
    private float     _currentDistance;
    private float     _maxDistance;
    private Transform _lookAtBone;   // cached spine bone, found once

    // ════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    private void Start()
    {
        _maxDistance     = offset.magnitude;
        _currentDistance = _maxDistance;

        // Force near-clip to 0.01 so the camera never renders the inside of
        // the character mesh, even when pulled very close.
        Camera cam = GetComponent<Camera>();
        if (cam != null) cam.nearClipPlane = 0.01f;
    }

    private void LateUpdate()
    {
        // Auto-find player if target is missing
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                SnapToTarget();
            }
            return;
        }

        // ── Build orbit rotation ─────────────────────────────────────────────
        // Horizontal from the player's Y rotation, vertical from mouse pitch.
        Quaternion orbitRot     = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3    desiredPos   = target.position + orbitRot * offset;

        // ── Resolve wall collision ───────────────────────────────────────────
        if (enableCollision)
            desiredPos = ResolveCollision(desiredPos, orbitRot);

        // ── Smooth follow ────────────────────────────────────────────────────
        transform.position = Vector3.Lerp(transform.position, desiredPos,
                                          smoothSpeed * Time.deltaTime);

        // ── Look at spine bone (never at empty space ahead of the player) ────
        transform.LookAt(GetLookTarget());
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WALL COLLISION
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SphereCasts from chest height toward the desired camera position.
    /// If the cast hits a wall the camera is pulled in. Eases back out when clear.
    /// </summary>
    private Vector3 ResolveCollision(Vector3 desiredPos, Quaternion orbitRot)
    {
        // Cast origin at roughly chest height so the sphere starts inside the body
        Vector3 castOrigin = target.position + Vector3.up * 1.2f;
        Vector3 castDir    = desiredPos - castOrigin;
        float   castDist   = castDir.magnitude;
        castDir.Normalize();

        float targetDist = _maxDistance;

        if (Physics.SphereCast(castOrigin, collisionRadius, castDir,
                               out RaycastHit hit, castDist,
                               collisionMask, QueryTriggerInteraction.Ignore))
        {
            // Pull to the hit point minus padding; never closer than minDistance
            targetDist = Mathf.Max(hit.distance - wallPadding, minDistance);
        }

        // Snap inward instantly, ease outward slowly to avoid pop
        if (targetDist < _currentDistance)
            _currentDistance = targetDist;
        else
            _currentDistance = Mathf.Lerp(_currentDistance, targetDist,
                                           recoverySpeed * Time.deltaTime);

        // Rebuild position along the same orbit direction at the clamped distance
        Vector3 offsetDir = (orbitRot * offset).normalized;
        return castOrigin + offsetDir * _currentDistance;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LOOK-AT TARGET — spine bone so body stays on-screen
    // ════════════════════════════════════════════════════════════════════════

    private Vector3 GetLookTarget()
    {
        if (target == null) return transform.position + transform.forward * 5f;

        // Cache the spine bone — re-search if the body was respawned
        if (_lookAtBone == null)
            _lookAtBone = FindSpineBone(target);

        if (_lookAtBone != null)
            return _lookAtBone.position;

        // Fallback: fixed height above player pivot (no look-ahead drift)
        return target.position + Vector3.up * lookHeight;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BONE HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private static Transform FindSpineBone(Transform root)
    {
        // ── Pass 1: Humanoid avatar API (works for Ronin / Crosby / Mixamo) ──
        Animator anim = root.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            Transform b;
            b = anim.GetBoneTransform(HumanBodyBones.UpperChest); if (b != null) return b;
            b = anim.GetBoneTransform(HumanBodyBones.Chest);      if (b != null) return b;
            b = anim.GetBoneTransform(HumanBodyBones.Spine);      if (b != null) return b;
            b = anim.GetBoneTransform(HumanBodyBones.Head);       if (b != null) return b;
        }

        // ── Pass 2: Name-based search — covers non-humanoid / Generic rigs ───
        string[] names = {
            "bip_spine_02", "bip_spine_1", "bip_spine",
            "spine_02",     "Spine2",       "Spine1",  "Spine",
            "j_spine_up",   "j_spine",
            "mixamorig:Spine2", "mixamorig:Spine1", "mixamorig:Spine",
            "Head",         "head",
        };
        foreach (string n in names)
        {
            Transform found = FindBoneByName(root, n);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindBoneByName(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneByName(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC UTILS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Instantly snaps the camera to its desired position (call on scene load).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        _maxDistance     = offset.magnitude;
        _currentDistance = _maxDistance;

        Quaternion orbitRot   = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3    desiredPos = target.position + orbitRot * offset;
        if (enableCollision)
            desiredPos = ResolveCollision(desiredPos, orbitRot);

        transform.position = desiredPos;
        transform.LookAt(GetLookTarget());
    }
}
