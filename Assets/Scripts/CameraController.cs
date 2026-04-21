using UnityEngine;

/// <summary>
/// Over-the-shoulder third-person camera.
///
/// The camera orbits around a live upper-body bone pivot instead of the player
/// root, so zooming cannot drift toward empty space ahead of the character.
/// </summary>
public class CameraController : MonoBehaviour
{
    // ── Target ───────────────────────────────────────────────────────────────
    [Tooltip("The player root transform. Auto-found by 'Player' tag if null.")]
    public Transform target;

    // ── Over-the-Shoulder offset ─────────────────────────────────────────────
    [Header("OTS Offset")]
    [Tooltip("Default camera position in local orbit space around the look target.")]
    public Vector3 offset = new Vector3(1.2f, 2.2f, -5.5f);

    [Tooltip("Tighter over-the-shoulder offset used while zooming.")]
    public Vector3 zoomOffset = new Vector3(0.55f, 0.45f, -2.35f);

    [Tooltip("Smooth-follow speed. Higher = snappier.")]
    public float smoothSpeed = 12f;

    // Vertical pitch (degrees) — set each frame by PlayerController.ApplyLook
    [HideInInspector] public float pitch = 0f;

    // ── Look-at height ───────────────────────────────────────────────────────
    [Header("Look-At")]
    [Tooltip("World-space height to add to the player position when no spine bone is found.")]
    public float lookHeight = 1.4f;

    [Tooltip("Extra local offset applied from the resolved look bone.")]
    public Vector3 lookTargetLocalOffset = new Vector3(0f, 0.08f, 0f);

    [Tooltip("How quickly the orbit pivot follows the target bone.")]
    public float lookTargetSmoothSpeed = 20f;

    // ── Zoom ────────────────────────────────────────────────────────────────
    [Header("Zoom")]
    [Tooltip("Default field of view when not zooming.")]
    public float defaultFieldOfView = 70f;

    [Tooltip("Field of view while zooming.")]
    public float zoomFieldOfView = 52f;

    [Tooltip("How quickly zoom offset and FOV blend in/out.")]
    public float zoomLerpSpeed = 10f;

    // ── Wall / Mesh Collision ────────────────────────────────────────────────
    [Header("Wall Collision")]
    [Tooltip("Enable SphereCast collision so the camera never enters walls.")]
    public bool enableCollision = false;

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
    public float recoverySpeed = 8f;

    [Tooltip("How quickly the camera is pulled toward the player when a wall is detected. " +
             "Higher = less clip risk; lower = smoother. 20 is a good balance.")]
    public float pullInSpeed = 20f;

    // ── Private ──────────────────────────────────────────────────────────────
    private float     _currentDistance;
    private Vector3   _smoothedLookTarget;
    private bool      _lookTargetInitialized;
    private Transform _lookAtBone;
    private Animator  _cachedAnimator;

    // ════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    private void Start()
    {
        // Force near-clip to 0.01 so the camera never renders the inside of
        // the character mesh, even when pulled very close.
        Camera cam = GetComponent<Camera>();
        if (cam != null) cam.nearClipPlane = 0.01f;

        if (cam != null)
            defaultFieldOfView = cam.fieldOfView;

        _currentDistance = GetCurrentOffset().magnitude;

        // ── Exclude the player's own colliders from wall-collision checks ────
        // Without this, the SphereCast can hit the player mesh itself and
        // cause the camera to zoom in erratically whenever the player turns.
        // We strip out whatever layer the player root is on, plus the common
        // named layers "Player" and "Character", from collisionMask.
        if (target != null)
            collisionMask &= ~(1 << target.gameObject.layer);

        ExcludeLayerIfExists("Player");
        ExcludeLayerIfExists("Character");

        // Also exclude layers that should never block a camera.
        ExcludeLayerIfExists("UI");
        ExcludeLayerIfExists("TransparentFX");
        ExcludeLayerIfExists("Ignore Raycast");
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

        Vector3 lookTarget = GetLookTarget();
        Vector3 currentOffset = GetCurrentOffset();

        // ── Build orbit rotation ─────────────────────────────────────────────
        // Horizontal from the player's Y rotation, vertical from mouse pitch.
        Quaternion orbitRot     = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3    desiredPos   = lookTarget + orbitRot * currentOffset;

        // ── Resolve wall collision ───────────────────────────────────────────
        if (enableCollision && IsFinite(desiredPos))
            desiredPos = ResolveCollision(desiredPos, orbitRot, lookTarget, currentOffset);

        // ── Smooth follow ────────────────────────────────────────────────────
        if (!IsFinite(transform.position) || Vector3.Distance(transform.position, desiredPos) > 30f)
            transform.position = desiredPos;
        else
            transform.position = Vector3.Lerp(transform.position, desiredPos,
                                              smoothSpeed * Time.deltaTime);

        UpdateFieldOfView();

        // ── Look at spine bone (never at empty space ahead of the player) ────
        transform.LookAt(lookTarget);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WALL COLLISION
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SphereCasts from chest height toward the desired camera position.
    /// If the cast hits a wall the camera is pulled in. Eases back out when clear.
    /// </summary>
    private Vector3 ResolveCollision(Vector3 desiredPos, Quaternion orbitRot, Vector3 lookTarget, Vector3 currentOffset)
    {
        Vector3 fallbackCastOrigin = target.position + Vector3.up * 1.2f;
        Vector3 castOrigin = Vector3.Lerp(fallbackCastOrigin, lookTarget, 0.85f);
        Vector3 castDir    = desiredPos - castOrigin;
        float   castDist   = castDir.magnitude;
        if (castDist <= 0.001f)
            return desiredPos;

        castDir /= castDist;

        float targetDist = castDist;

        if (Physics.SphereCast(castOrigin, collisionRadius, castDir,
                               out RaycastHit hit, castDist,
                               collisionMask, QueryTriggerInteraction.Ignore))
        {
            // Pull to the hit point minus padding; never closer than minDistance
            targetDist = Mathf.Max(hit.distance - wallPadding, minDistance);
        }

        // ── Smooth in both directions — no more instant snap ─────────────────
        // Old code snapped the camera inward instantly (= jerky zoom).
        // New behaviour:
        //   • Wall detected   → lerp at pullInSpeed (fast, ~20 f/s) so the
        //     camera closes the gap before clipping but the motion is smooth.
        //   • Wall cleared    → lerp at recoverySpeed (slow, ~8 f/s) so the
        //     camera eases back out without a visible pop.
        float blendSpeed = targetDist < _currentDistance ? pullInSpeed : recoverySpeed;
        _currentDistance = Mathf.Lerp(_currentDistance, targetDist,
                                       blendSpeed * Time.deltaTime);

        // Rebuild position along the same orbit direction at the clamped distance
        Vector3 offsetDir = (orbitRot * currentOffset).normalized;
        return castOrigin + offsetDir * _currentDistance;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LOOK-AT TARGET — spine bone so body stays on-screen
    // ════════════════════════════════════════════════════════════════════════

    private Vector3 GetLookTarget()
    {
        if (target == null) return transform.position + transform.forward * 5f;

        Transform resolvedBone = ResolveLookBone(target);
        Vector3 rawTarget = resolvedBone != null
            ? resolvedBone.TransformPoint(lookTargetLocalOffset)
            : target.position + Vector3.up * lookHeight;

        if (!_lookTargetInitialized)
        {
            _smoothedLookTarget = rawTarget;
            _lookTargetInitialized = true;
        }
        else
        {
            _smoothedLookTarget = Vector3.Lerp(
                _smoothedLookTarget,
                rawTarget,
                lookTargetSmoothSpeed * Time.deltaTime);
        }

        return _smoothedLookTarget;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BONE HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private Transform ResolveLookBone(Transform root)
    {
        Animator animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
        if (animator != _cachedAnimator)
        {
            _cachedAnimator = animator;
            _lookAtBone = null;
        }

        if (_lookAtBone == null || !IsChildOf(_lookAtBone, root))
            _lookAtBone = FindLookBone(root, animator);

        return _lookAtBone;
    }

    private static Transform FindLookBone(Transform root, Animator anim)
    {
        // ── Pass 1: Humanoid avatar API (works for Ronin / Crosby / Mixamo) ──
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
            "bip_spine_02", "Bip_Spine_02", "bip_spine2", "bip_spine_01",
            "bip_spine_1",  "bip_spine",
            "spine_02",     "spine2",       "Spine2",      "spine_01",
            "Spine1",       "Spine",
            "j_spine4",     "j_spineupper", "j_spine_up",  "j_spine",
            "B-chest",      "B-spine",      "mixamorig:Spine2",
            "mixamorig:Spine1", "mixamorig:Spine",
            "j_head",       "Head",         "head",
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

    private static bool IsChildOf(Transform child, Transform potentialRoot)
    {
        if (child == null || potentialRoot == null) return false;

        Transform current = child;
        while (current != null)
        {
            if (current == potentialRoot)
                return true;
            current = current.parent;
        }

        return false;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC UTILS
    // ════════════════════════════════════════════════════════════════════════

    private void ExcludeLayerIfExists(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            collisionMask &= ~(1 << layer);
    }

    public void SetZoom(bool isZooming)
    {
        // Zoom permanently disabled — camera stays at fixed OTS position.
        // Method kept for API compatibility.
    }

    /// <summary>Instantly snaps the camera to its desired position (call on scene load).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        _lookTargetInitialized = false;
        Vector3 lookTarget = GetLookTarget();
        Vector3 currentOffset = GetCurrentOffset();
        Quaternion orbitRot   = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3    desiredPos = lookTarget + orbitRot * currentOffset;
        if (enableCollision)
            desiredPos = ResolveCollision(desiredPos, orbitRot, lookTarget, currentOffset);

        transform.position = desiredPos;
        Vector3 fallbackCastOrigin = target.position + Vector3.up * 1.2f;
        Vector3 castOrigin = Vector3.Lerp(fallbackCastOrigin, lookTarget, 0.85f);
        _currentDistance = Vector3.Distance(castOrigin, desiredPos);
        UpdateFieldOfView(immediate: true);
        transform.LookAt(lookTarget);
    }

    private Vector3 GetCurrentOffset()
    {
        // Zoom disabled — always use the default OTS offset.
        return offset;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private void UpdateFieldOfView(bool immediate = false)
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        // Zoom disabled — always use the default field of view.
        cam.fieldOfView = immediate
            ? defaultFieldOfView
            : Mathf.Lerp(cam.fieldOfView, defaultFieldOfView, 10f * Time.deltaTime);
    }
}
