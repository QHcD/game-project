using UnityEngine;

/// <summary>
/// Stable third-person shooter camera.
///
/// The camera follows a fixed shoulder-height pivot on the player root. It avoids
/// animated-bone jitter and keeps the character framed consistently, closer to a
/// Warzone-style readable gameplay camera.
/// </summary>
public class CameraController : MonoBehaviour
{
    // ── Target ───────────────────────────────────────────────────────────────
    [Tooltip("The player root transform. Auto-found by 'Player' tag if null.")]
    public Transform target;

    // ── Over-the-Shoulder offset ─────────────────────────────────────────────
    [Header("OTS Offset")]
    [Tooltip("Default camera position in local orbit space around the look target.")]
    public Vector3 offset = new Vector3(0.45f, 1.45f, -4.65f);

    [Tooltip("Tighter over-the-shoulder offset used while zooming.")]
    public Vector3 zoomOffset = new Vector3(0.55f, 0.45f, -2.35f);

    [Tooltip("Smooth-follow speed. Higher = snappier.")]
    public float smoothSpeed = 60f;

    // Vertical pitch (degrees) — set each frame by PlayerController.ApplyLook.
    // Public for back-compat with PlayerController.ApplyLook; the (minPitch,
    // maxPitch) clamp is enforced defensively in LateUpdate AND via the
    // Pitch property below for any new caller that wants automatic clamping.
    [HideInInspector] public float pitch = 0f;

    // ── Pitch (vertical-look) clamp ─────────────────────────────────────────
    // The range intentionally stays narrow: enough vertical freedom for combat,
    // but not enough to become a top-down or under-map debug camera.
    [Header("Pitch Clamp")]
    [Tooltip("Lowest allowed pitch in degrees. Prevents the camera orbiting under the player.")]
    public float minPitch = -12f;
    [Tooltip("Highest allowed pitch in degrees. Prevents awkward top-down framing.")]
    public float maxPitch = 45f;

    // ── Rotation speed (driven by the Options-menu sensitivity slider) ──────
    /// <summary>
    /// Designer-tuned base rotation speed. The actual value used by callers
    /// (PlayerController) is <see cref="RotationSpeed"/>, which scales this
    /// by the live mouse-sensitivity multiplier.
    /// </summary>
    [Header("Rotation Speed")]
    [Tooltip("Base rotation speed in degrees per second of mouse delta. " +
             "The Options-menu sensitivity slider (0–7) multiplies this at runtime.")]
    public float baseRotationSpeed = 100f;

    /// <summary>
    /// Effective rotation speed applied to the player's mouse-look math.
    /// Reads <see cref="LookSensitivityRuntime.LookMultiplier"/> live so the
    /// 0–7 slider in Options controls camera responsiveness in real time.
    /// </summary>
    public float RotationSpeed => baseRotationSpeed * LookSensitivityRuntime.LookMultiplier;

    /// <summary>Public pitch accessor — clamps every assignment to [minPitch, maxPitch].</summary>
    public float Pitch
    {
        get => pitch;
        set => pitch = Mathf.Clamp(value, minPitch, maxPitch);
    }

    // ── Look-at height ───────────────────────────────────────────────────────
    [Header("Look-At")]
    [Tooltip("World-space height to add to the player position when no spine bone is found.")]
    public float lookHeight = 1.55f;

    [Tooltip("Extra local offset applied from the resolved look bone.")]
    public Vector3 lookTargetLocalOffset = Vector3.zero;

    [Tooltip("How quickly the orbit pivot follows the target bone.")]
    public float lookTargetSmoothSpeed = 60f;

    // ── Zoom ────────────────────────────────────────────────────────────────
    [Header("Zoom")]
    [Tooltip("Default field of view when not zooming.")]
    public float defaultFieldOfView = 68f;

    [Tooltip("Field of view while zooming.")]
    public float zoomFieldOfView = 52f;

    [Tooltip("How quickly zoom offset and FOV blend in/out.")]
    public float zoomLerpSpeed = 10f;

    // ── Wall / Mesh Collision ────────────────────────────────────────────────
    [Header("Wall Collision")]
    [Tooltip("Enable SphereCast collision so the camera never enters walls or the floor.")]
    public bool enableCollision = true;

    [Tooltip("Minimum height above the resolved ground hit. Camera is lifted so the player cannot see under the map.")]
    public float minHeightAboveGround = 0.55f;

    [Tooltip("How quickly the camera is lifted off the floor after a downward ray hits ground.")]
    public float groundLiftSpeed = 15f;

    [Tooltip("SphereCast radius from the player neck/head to the desired camera position.")]
    [Range(0.05f, 0.5f)]
    public float collisionRadius = 0.24f;

    [Tooltip("Minimum distance the camera is allowed to reach before close-space failsafe kicks in.")]
    public float minDistance = 0.35f;

    [Tooltip("Padding gap between camera and wall surface.")]
    [Range(0.02f, 0.3f)]
    public float wallPadding = 0.22f;

    [Tooltip("Layers treated as solid obstacles (walls, terrain, buildings).")]
    public LayerMask collisionMask = ~0;

    [Tooltip("How quickly the camera eases back to full distance after clearing a wall.")]
    public float recoverySpeed = 8f;

    [Tooltip("How quickly the camera is pulled toward the player when a wall is detected. " +
             "Higher = less clip risk; lower = smoother. 20 is a good balance.")]
    public float pullInSpeed = 0.035f;

    [Tooltip("SmoothDamp time used when returning to full camera distance.")]
    public float recoverySmoothTime = 0.12f;

    [Tooltip("If camera distance is below this, apply close-space visibility failsafe.")]
    public float closeDistanceFailsafe = 0.5f;

    [Tooltip("Extra camera lift in cramped spaces.")]
    public float closeSpaceHeightBoost = 0.35f;

    [Tooltip("FOV used in cramped spaces so the player/action remains visible.")]
    public float closeSpaceFieldOfView = 76f;

    // ── Private ──────────────────────────────────────────────────────────────
    private float     _currentDistance;
    private Vector3   _smoothedLookTarget;
    private bool      _lookTargetInitialized;
    private Transform _lookAtBone;
    private Animator  _cachedAnimator;
    private float     _distanceVelocity;
    private Vector3   _positionVelocity;
    private float     _fieldOfViewVelocity;
    private bool      _closeSpaceActive;

    // ════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    public static CameraController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[CameraController] Duplicate destroyed: \"{gameObject.name}\". Only one CameraController allowed.");
            DestroyImmediate(this.gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Force near-clip to a sane value for third-person.
        // 0.01 can cause severe character clipping (seeing inside body/legs).
        Camera cam = GetComponent<Camera>();
        if (cam != null) cam.nearClipPlane = 0.1f;

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

        collisionMask = BuildSolidCameraMask();
    }

    private void IncludeLayerIfExists(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) collisionMask |= (1 << layer);
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

        // ── Defensive pitch clamp ────────────────────────────────────────────
        // PlayerController writes `pitch` directly each frame; clamp it here
        // as the last line of defense so the rotation never escapes the
        // (minPitch, maxPitch) range no matter what the caller did.
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Vector3 lookTarget = GetLookTarget();
        Vector3 currentOffset = GetCurrentOffset();
        if (!IsFinite(target.position) || !IsFinite(lookTarget) || !IsFinite(currentOffset))
        {
            RecoverFromInvalidCameraState();
            return;
        }
        if (!IsFinite(_positionVelocity))
            _positionVelocity = Vector3.zero;
        if (!IsFinite(_distanceVelocity))
            _distanceVelocity = 0f;
        if (!IsFinite(_fieldOfViewVelocity))
            _fieldOfViewVelocity = 0f;

        // ── Build orbit rotation ─────────────────────────────────────────────
        // Horizontal from the player's Y rotation, vertical from mouse pitch.
        Quaternion orbitRot     = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
        Vector3    desiredPos   = lookTarget + orbitRot * currentOffset;

        // ── Resolve wall collision ───────────────────────────────────────────
        if (enableCollision && IsFinite(desiredPos))
            desiredPos = ResolveCollision(desiredPos, orbitRot, lookTarget, currentOffset);
        if (!IsFinite(desiredPos))
        {
            RecoverFromInvalidCameraState();
            return;
        }

        // ── Smooth follow ────────────────────────────────────────────────────
        if (!IsFinite(transform.position) || Vector3.Distance(transform.position, desiredPos) > 30f)
            transform.position = desiredPos;
        else
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPos,
                ref _positionVelocity,
                1f / Mathf.Max(1f, smoothSpeed));

        UpdateFieldOfView();

        // ── Look at spine bone (never at empty space ahead of the player) ────
        if (IsFinite(lookTarget) && (lookTarget - transform.position).sqrMagnitude > 0.0001f)
            transform.LookAt(lookTarget);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WALL COLLISION
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SphereCasts from neck/head height toward the desired camera position.
    /// If world geometry is between the player and camera, the camera is pulled
    /// to the hit distance with padding so it never enters the mesh.
    /// </summary>
    private Vector3 ResolveCollision(Vector3 desiredPos, Quaternion orbitRot, Vector3 lookTarget, Vector3 currentOffset)
    {
        Vector3 castOrigin = GetCollisionCastOrigin();
        Vector3 castDir    = desiredPos - castOrigin;
        float   castDist   = castDir.magnitude;
        if (castDist <= 0.001f)
            return desiredPos;

        castDir /= castDist;

        if (_currentDistance <= 0.001f)
            _currentDistance = castDist;

        float targetDist = FindNearestCollisionDistance(castOrigin, castDir, castDist);
        float smoothTime = targetDist < _currentDistance ? pullInSpeed : recoverySmoothTime;
        _currentDistance = Mathf.SmoothDamp(
            _currentDistance,
            targetDist,
            ref _distanceVelocity,
            Mathf.Max(0.001f, smoothTime));

        // Rebuild position along the same orbit direction at the clamped distance
        Vector3 offsetDir = castDir;
        Vector3 resolved = castOrigin + offsetDir * _currentDistance;
        _closeSpaceActive = _currentDistance <= closeDistanceFailsafe;
        if (_closeSpaceActive)
            resolved += Vector3.up * closeSpaceHeightBoost;

        return EnforceGroundFloor(resolved);
    }

    private Vector3 GetCollisionCastOrigin()
    {
        if (target == null)
            return transform.position;

        return target.position + Vector3.up * Mathf.Max(1.45f, lookHeight);
    }

    private float FindNearestCollisionDistance(Vector3 origin, Vector3 direction, float desiredDistance)
    {
        float nearest = desiredDistance;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            collisionRadius,
            direction,
            desiredDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null)
                continue;
            if (target != null && col.transform.IsChildOf(target))
                continue;
            if (!IsSolidCameraLayer(col.gameObject.layer))
                continue;

            nearest = Mathf.Min(nearest, Mathf.Max(hits[i].distance - wallPadding, minDistance));
        }

        return nearest;
    }

    /// <summary>
    /// Drop a downward ray from the proposed camera point. If it hits ground,
    /// lift the camera so it sits at least <see cref="minHeightAboveGround"/>
    /// above the surface — guarantees the player can never see under the map.
    /// </summary>
    private Vector3 EnforceGroundFloor(Vector3 candidate)
    {
        if (Physics.Raycast(candidate + Vector3.up * 0.05f, Vector3.down,
                            out RaycastHit groundHit, 6f,
                            collisionMask, QueryTriggerInteraction.Ignore))
        {
            float minY = groundHit.point.y + minHeightAboveGround;
            if (candidate.y < minY)
            {
                candidate.y = Mathf.Lerp(candidate.y, minY,
                                         groundLiftSpeed * Time.deltaTime);
                if (candidate.y < minY) candidate.y = minY;
            }
        }
        return candidate;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LOOK-AT TARGET — spine bone so body stays on-screen
    // ════════════════════════════════════════════════════════════════════════

    private Vector3 GetLookTarget()
    {
        if (target == null) return transform.position + transform.forward * 5f;

        Vector3 rawTarget = target.position + Vector3.up * lookHeight + target.TransformVector(lookTargetLocalOffset);

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
        if (!IsFinite(desiredPos))
            desiredPos = target.position + Quaternion.Euler(0f, target.eulerAngles.y, 0f) * offset;

        transform.position = desiredPos;
        Vector3 fallbackCastOrigin = target.position + Vector3.up * 1.2f;
        Vector3 castOrigin = Vector3.Lerp(fallbackCastOrigin, lookTarget, 0.85f);
        _currentDistance = Vector3.Distance(castOrigin, desiredPos);
        UpdateFieldOfView(immediate: true);
        if (IsFinite(lookTarget) && (lookTarget - transform.position).sqrMagnitude > 0.0001f)
            transform.LookAt(lookTarget);
    }

    private void RecoverFromInvalidCameraState()
    {
        _positionVelocity = Vector3.zero;
        _distanceVelocity = 0f;
        _fieldOfViewVelocity = 0f;
        _lookTargetInitialized = false;
        pitch = Mathf.Clamp(float.IsNaN(pitch) || float.IsInfinity(pitch) ? 0f : pitch, minPitch, maxPitch);

        if (target == null || !IsFinite(target.position))
            return;

        Vector3 fallback = target.position + Quaternion.Euler(0f, target.eulerAngles.y, 0f) * offset;
        if (IsFinite(fallback))
            transform.position = fallback;
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

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void UpdateFieldOfView(bool immediate = false)
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        float targetFov = _closeSpaceActive ? closeSpaceFieldOfView : defaultFieldOfView;
        cam.fieldOfView = immediate
            ? targetFov
            : Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref _fieldOfViewVelocity, 0.12f);
    }

    private static LayerMask BuildSolidCameraMask()
    {
        int mask = 0;
        int environment = LayerMask.NameToLayer("Environment");
        if (environment >= 0) mask |= 1 << environment;
        mask |= 1 << 0; // Default
        return mask;
    }

    private static bool IsSolidCameraLayer(int layer)
    {
        int environment = LayerMask.NameToLayer("Environment");
        return layer == 0 || (environment >= 0 && layer == environment);
    }
}
