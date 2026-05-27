using UnityEngine;

/// <summary>
/// Production FPS camera rig.
///
/// Responsibilities:
///   1. Anchors the camera to a bone socket in FRONT of the face so it never
///      sits inside the skull geometry.
///   2. Configures the culling mask to exclude the PlayerBody layer so the
///      player's own mesh is invisible to their camera (but fully visible to
///      enemies and the third-person camera).
///   3. Sets the near clip plane to the minimum safe value (0.01 m) to
///      eliminate near-surface clipping artefacts.
///   4. Optionally stacks a Weapon Overlay Camera that renders the weapon on a
///      separate depth pass (prevents guns from clipping through walls).
///
/// Setup:
///   • Attach this component to the same GameObject as your first-person Camera.
///   • Assign headBone in the Inspector (the character's "Head" transform).
///   • If you want weapon-overlay rendering, tick enableWeaponOverlayCamera and
///     assign the FirstPersonWeapon layer in the Inspector.
/// </summary>
[RequireComponent(typeof(Camera))]
public class FPSCameraRig : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Anchor")]
    [Tooltip("The character's head bone. If null the rig falls back to playerRoot + headHeightFallback.")]
    public Transform headBone;

    [Tooltip("Parent transform of the player capsule (the root with CharacterController).")]
    public Transform playerRoot;

    [Tooltip("Used when headBone is null: world-space Y height above playerRoot.")]
    public float headHeightFallback = 1.62f;

    [Tooltip("Forward offset from the head pivot so the camera sits just in front of the face (meters).")]
    public float forwardOffset = 0.12f;

    [Tooltip("Lateral (right) offset. 0 = centred, positive = slightly right for a cinematic feel.")]
    public float lateralOffset = 0f;

    [Tooltip("Vertical nudge relative to the head bone. Negative = lower the eye point slightly.")]
    public float verticalOffset = 0.04f;

    [Header("Near Clip")]
    [Tooltip("Near clip plane distance. 0.1 m is a safe default to avoid seeing inside meshes.")]
    [Range(0.001f, 0.1f)]
    public float nearClipPlane = 0.1f;

    [Header("Layer Culling")]
    [Tooltip("Name of the Unity layer that your player body mesh is assigned to. " +
             "This layer will be EXCLUDED from this camera so the body is invisible to itself.")]
    public string playerBodyLayerName = "PlayerBody";

    [Header("Weapon Overlay Camera (optional)")]
    [Tooltip("When true, a second camera is created at runtime that renders ONLY the " +
             "FirstPersonWeapon layer on top of this camera. Weapons then never clip walls.")]
    public bool enableWeaponOverlayCamera = false;

    [Tooltip("Layer name for first-person weapon models. They must be assigned to this layer.")]
    public string weaponLayerName = "FirstPersonWeapon";

    [Tooltip("FOV for the weapon overlay camera. Slightly lower than the main FOV gives a " +
             "zoomed-in, grounded weapon look (Call of Duty style).")]
    [Range(30f, 90f)]
    public float weaponOverrideFOV = 55f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private Camera _cam;
    private Camera _weaponCam;
    private Transform _anchor;
    private int _playerBodyLayerIndex = -1;
    private int _weaponLayerIndex     = -1;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cam = GetComponent<Camera>();

        _playerBodyLayerIndex = LayerMask.NameToLayer(playerBodyLayerName);
        _weaponLayerIndex     = LayerMask.NameToLayer(weaponLayerName);

        ApplyNearClip();
        ApplyCullingMask();
        BuildAnchor();

        if (enableWeaponOverlayCamera)
            BuildWeaponOverlayCamera();
    }

    private void LateUpdate()
    {
        // Drive position every frame so the anchor tracks the animated bone.
        if (_anchor != null)
            PositionCameraAtAnchor();
    }

    // ── Near clip ─────────────────────────────────────────────────────────────

    private void ApplyNearClip()
    {
        if (_cam == null) return;
        _cam.nearClipPlane = Mathf.Clamp(nearClipPlane, 0.001f, 0.1f);
    }

    // ── Culling mask ──────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the PlayerBody layer from this camera's culling mask.
    /// The body stays visible to all other cameras (enemies, third-person rig).
    /// </summary>
    private void ApplyCullingMask()
    {
        if (_cam == null) return;
        if (_playerBodyLayerIndex < 0)
        {
            Debug.LogWarning(
                $"[FPSCameraRig] Layer '{playerBodyLayerName}' not found. " +
                "Create it in Edit → Project Settings → Tags and Layers, " +
                "then assign your player body mesh to it.", this);
            return;
        }

        // Start from the current mask (respects any other intentional exclusions
        // already set in the Inspector) and clear only the PlayerBody bit.
        _cam.cullingMask &= ~(1 << _playerBodyLayerIndex);

        int minimapIconsLayer = LayerMask.NameToLayer("MinimapIcons");
        if (minimapIconsLayer >= 0)
        {
            _cam.cullingMask &= ~(1 << minimapIconsLayer);
            if (_weaponCam != null)
                _weaponCam.cullingMask &= ~(1 << minimapIconsLayer);
        }

        if (_weaponCam != null)
            _weaponCam.cullingMask &= ~(1 << _playerBodyLayerIndex);
    }

    // ── Camera anchor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates or resolves the Transform the camera will track every LateUpdate.
    /// Priority: headBone → search player hierarchy for "Head" → fallback offset.
    /// </summary>
    private void BuildAnchor()
    {
        if (headBone != null)
        {
            _anchor = headBone;
            return;
        }

        // Auto-detect: walk up from the camera until we find a CharacterController
        // root, then search that hierarchy for a bone named "Head" or "Bip01 Head".
        Transform root = playerRoot != null ? playerRoot : ResolvePlayerRoot();
        if (root != null)
        {
            Transform found = FindBoneByName(root, "Head")
                           ?? FindBoneByName(root, "Bip01 Head")
                           ?? FindBoneByName(root, "mixamorig:Head")
                           ?? FindBoneByName(root, "head");
            if (found != null)
            {
                _anchor = found;
                headBone = found; // cache for Inspector visibility
                return;
            }

            // No head bone found — create a stable floating anchor above the root.
            GameObject anchorGo = new GameObject("FPSCameraAnchor");
            anchorGo.transform.SetParent(root, false);
            anchorGo.transform.localPosition = new Vector3(lateralOffset, headHeightFallback, forwardOffset);
            anchorGo.transform.localRotation = Quaternion.identity;
            _anchor = anchorGo.transform;
            return;
        }

        // Last resort: anchor is this camera's own parent.
        _anchor = transform.parent != null ? transform.parent : transform;
    }

    /// <summary>Moves the camera to the offset position relative to the anchor bone.</summary>
    private void PositionCameraAtAnchor()
    {
        if (_anchor == null) return;

        // Compute offset in anchor-local space:
        //   forward = the direction the head faces (bone local +Z after rig-normalisation)
        //   up      = world up, not the bone up, so the camera never tilts with head lean
        Vector3 forward = _anchor.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.parent != null ? transform.parent.forward : Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 worldAnchor = _anchor.position
            + forward * forwardOffset
            + right   * lateralOffset
            + Vector3.up * verticalOffset;

        transform.position = worldAnchor;
        // Rotation is driven externally by PlayerController.ApplyLook — do not overwrite it here.
    }

    // ── Weapon Overlay Camera ─────────────────────────────────────────────────

    /// <summary>
    /// Spawns a second camera that renders ONLY the FirstPersonWeapon layer
    /// on top of this camera (depth > _cam.depth) with a cleared depth buffer.
    /// Result: weapons always draw in front of world geometry — they can never
    /// clip through a wall because their depth is evaluated in isolation.
    /// </summary>
    private void BuildWeaponOverlayCamera()
    {
        if (_weaponLayerIndex < 0)
        {
            Debug.LogWarning(
                $"[FPSCameraRig] Weapon layer '{weaponLayerName}' not found. " +
                "Create it in Edit → Project Settings → Tags and Layers and " +
                "assign your weapon meshes to it.", this);
            return;
        }

        // Make sure the MAIN camera does NOT render weapon models itself,
        // otherwise they'd be drawn twice (once normally, once in overlay).
        _cam.cullingMask &= ~(1 << _weaponLayerIndex);

        GameObject weaponCamGo = new GameObject("WeaponOverlayCamera");
        weaponCamGo.transform.SetParent(transform, false);
        weaponCamGo.transform.localPosition = Vector3.zero;
        weaponCamGo.transform.localRotation = Quaternion.identity;

        _weaponCam = weaponCamGo.AddComponent<Camera>();

        // Mirror main camera settings
        _weaponCam.fieldOfView      = weaponOverrideFOV;
        _weaponCam.nearClipPlane    = nearClipPlane;
        _weaponCam.farClipPlane     = _cam.farClipPlane;
        _weaponCam.clearFlags       = CameraClearFlags.Depth; // preserve scene, clear depth only
        _weaponCam.renderingPath    = _cam.renderingPath;
        _weaponCam.depth            = _cam.depth + 1;         // renders AFTER the main camera

        // Render ONLY the weapon layer
        _weaponCam.cullingMask = 1 << _weaponLayerIndex;

        // Child this camera so it rotates with the main FPS camera automatically
        weaponCamGo.transform.SetParent(transform, true);

        Debug.Log("[FPSCameraRig] Weapon overlay camera created. " +
                  $"Assign your weapon GameObjects to the '{weaponLayerName}' layer.", this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Transform ResolvePlayerRoot()
    {
        // Walk up the hierarchy until we find the CharacterController owner.
        Transform t = transform;
        while (t != null)
        {
            if (t.GetComponent<CharacterController>() != null)
                return t;
            t = t.parent;
        }
        return transform.root;
    }

    private static Transform FindBoneByName(Transform root, string boneName)
    {
        if (root == null) return null;
        if (string.Equals(root.name, boneName, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindBoneByName(root.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this at runtime after re-assigning the player body to a new
    /// layer (e.g. from another script) to refresh the culling mask.
    /// </summary>
    public void RefreshCullingMask()
    {
        _playerBodyLayerIndex = LayerMask.NameToLayer(playerBodyLayerName);
        ApplyCullingMask();
    }

    /// <summary>
    /// Assigns a specific head bone at runtime (called by PlayerController
    /// after the third-person body is spawned).
    /// </summary>
    public void SetHeadBone(Transform bone)
    {
        headBone = bone;
        _anchor  = bone;
    }
}
