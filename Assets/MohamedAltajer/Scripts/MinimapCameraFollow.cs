using UnityEngine;

/// <summary>
/// Attach this component to the minimap Camera GameObject.
/// HUDManager uses FindFirstObjectByType to locate it and call EnsureRenderTexture().
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapCameraFollow : MonoBehaviour
{
    // When true, camera stays fixed above arena centre (full overview).
    // When false, HUDManager repositions this transform every frame to follow the player.
    public bool lockToArenaCenter = true;

    // World-space Y height of the minimap camera above the ground plane.
    public float height = 35f;

    // Resolution of the minimap render texture (square).
    public int textureSize = 256;

    // Orthographic size — controls how much of the arena is visible in the minimap.
    public float viewRadius = 28f;

    [Tooltip("Extra padding added around the calculated arena bounds for the full-map view.")]
    public float fullMapPadding = 12f;

    private RenderTexture _rt;
    private Camera _cam;
    private Bounds _arenaBounds;
    private bool _hasArenaBounds;
    private float _foreignCameraScanTimer;

    public static MinimapCameraFollow Instance { get; private set; }
    public Camera MapCamera => _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (Instance == null || Instance == this) Instance = this;
        CacheArenaBounds();
        ConfigureCamera();
        ScrubForeignCameras();
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    private void LateUpdate()
    {
        _foreignCameraScanTimer -= Time.unscaledDeltaTime;
        if (_foreignCameraScanTimer <= 0f)
        {
            _foreignCameraScanTimer = 1.0f;
            ScrubForeignCameras();
        }
    }

    private void ScrubForeignCameras()
    {
        int minimapIconsLayer = LayerMask.NameToLayer("MinimapIcons");
        if (minimapIconsLayer < 0) return;
        int bit = 1 << minimapIconsLayer;
        Camera[] cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || c == _cam) continue;
            if ((c.cullingMask & bit) != 0)
                c.cullingMask &= ~bit;
        }
    }

    private void ConfigureCamera()
    {
        if (_cam == null) return;

        _cam.orthographic = true;
        _cam.orthographicSize = viewRadius;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = ComputeRequiredFarClip();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
        _cam.cullingMask = BuildEnvironmentCullingMask();
        _cam.depth = -2;
        _cam.enabled = false;

        ApplyAdaptiveCameraHeight();
    }

    private static int BuildEnvironmentCullingMask()
    {
        int mask = ~0;
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) mask &= ~(1 << uiLayer);
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycast >= 0) mask &= ~(1 << ignoreRaycast);
        int weaponLayer = LayerMask.NameToLayer("Weapon");
        if (weaponLayer >= 0) mask &= ~(1 << weaponLayer);
        int firstPersonLayer = LayerMask.NameToLayer("FirstPersonOnly");
        if (firstPersonLayer >= 0) mask &= ~(1 << firstPersonLayer);
        int minimapHiddenLayer = LayerMask.NameToLayer("MinimapHidden");
        if (minimapHiddenLayer >= 0) mask &= ~(1 << minimapHiddenLayer);

        int forced = 0;
        forced |= EnableLayerBit("Default");
        forced |= EnableLayerBit("LevelContent");
        forced |= EnableLayerBit("Environment");
        forced |= EnableLayerBit("Ground");
        forced |= EnableLayerBit("Floor");
        forced |= EnableLayerBit("Walls");
        forced |= EnableLayerBit("Wall");
        forced |= EnableLayerBit("Props");
        forced |= EnableLayerBit("Hittable");
        forced |= EnableLayerBit("Character");
        forced |= EnableLayerBit("Enemies");
        forced |= EnableLayerBit("Player");
        forced |= EnableLayerBit("MinimapIcons");
        return mask | forced;
    }

    private static int EnableLayerBit(string layerName)
    {
        int idx = LayerMask.NameToLayer(layerName);
        return idx >= 0 ? (1 << idx) : 0;
    }

    private static void EnforceCeilingHiddenLayer()
    {
        int layer = LayerMask.NameToLayer("MinimapHidden");
        if (layer < 0) return;
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            GameObject g = all[i];
            if (g == null) continue;
            string n = g.name;
            if (n == null) continue;
            if (n.StartsWith("UNIVERSAL_CEILING_CAP") || n.IndexOf("CeilingCap", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (g.layer != layer) g.layer = layer;
                for (int c = 0; c < g.transform.childCount; c++)
                {
                    Transform ch = g.transform.GetChild(c);
                    if (ch != null && ch.gameObject.layer != layer) ch.gameObject.layer = layer;
                }
            }
        }
    }

    private float ComputeRequiredFarClip()
    {
        if (_hasArenaBounds)
            return Mathf.Max(60f, _arenaBounds.size.y + height + 40f);
        return height + 40f;
    }

    private void ApplyAdaptiveCameraHeight()
    {
        if (!_hasArenaBounds) return;
        float boundsTop = _arenaBounds.max.y;
        float requiredHeight = boundsTop + 20f;
        if (requiredHeight > height) height = requiredHeight;
        if (_cam != null) _cam.farClipPlane = ComputeRequiredFarClip();
    }

    public void SetFullMapMode(bool enabled, Transform playerTarget = null)
    {
        EnforceCeilingHiddenLayer();
        CacheArenaBounds();
        ApplyAdaptiveCameraHeight();
        if (_cam != null) _cam.cullingMask = BuildEnvironmentCullingMask();

        if (enabled)
        {
            lockToArenaCenter = true;
            if (_hasArenaBounds)
            {
                Vector3 center = _arenaBounds.center;
                float cameraY = Mathf.Max(height, _arenaBounds.max.y + 20f);
                transform.position = new Vector3(center.x, cameraY, center.z);
                _cam.orthographicSize = Mathf.Max(24f, Mathf.Max(_arenaBounds.extents.x, _arenaBounds.extents.z) + fullMapPadding);
                _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, (cameraY - _arenaBounds.min.y) + 20f);
            }
            else
            {
                _cam.orthographicSize = Mathf.Max(viewRadius, 42f);
            }

            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return;
        }

        lockToArenaCenter = false;
        _cam.orthographicSize = viewRadius;
        if (playerTarget != null)
        {
            float cameraY = _hasArenaBounds ? Mathf.Max(height, _arenaBounds.max.y + 20f) : height;
            transform.position = new Vector3(playerTarget.position.x, cameraY, playerTarget.position.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    /// <summary>
    /// Returns the RenderTexture used by the minimap, creating it on first call.
    /// Called by HUDManager every frame inside UpdateMinimap().
    /// </summary>
    public RenderTexture EnsureRenderTexture()
    {
        if (_rt == null || !_rt.IsCreated())
        {
            // Release any stale texture before creating a fresh one
            if (_rt != null) _rt.Release();

            _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
            _rt.name = "MinimapRenderTexture";
            _rt.antiAliasing = 1;
            _rt.filterMode = FilterMode.Bilinear;
            _rt.Create();

            if (_cam != null)
                _cam.targetTexture = _rt;
        }

        return _rt;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Clean up the RenderTexture to avoid GPU memory leaks
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }

    private void CacheArenaBounds()
    {
        // Prefer the SciFiArena's Floors group when present — it gives perfectly
        // centered playable bounds without ceiling cap, perimeter overhangs, or
        // decorative props skewing the result.
        if (TryGetSciFiFloorBounds(out Bounds floorBounds))
        {
            _hasArenaBounds = true;
            _arenaBounds = floorBounds;
            return;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        bool hasBounds = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (renderer.GetComponentInParent<Canvas>() != null)
                continue;

            string lowerName = renderer.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("weapon") || lowerName.Contains("player") || lowerName.Contains("enemy"))
                continue;
            // Exclude items that intentionally extend past the playable footprint.
            if (lowerName.Contains("ceiling_sealedcap") || lowerName.Contains("ceiling_opaquecap")
                || lowerName.Contains("ceiling_detail") || lowerName.Contains("catwalk")
                || lowerName.Contains("hanglight") || lowerName.Contains("sprinkler")
                || lowerName.Contains("securitycam") || lowerName.StartsWith("cam_"))
                continue;

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        _hasArenaBounds = hasBounds;
        _arenaBounds = combinedBounds;
    }

    /// <summary>
    /// Computes bounds from the SciFiArena's Floors group only, giving a clean
    /// centered playable area. Returns false if the arena isn't loaded or the
    /// Floors hierarchy isn't found.
    /// </summary>
    private bool TryGetSciFiFloorBounds(out Bounds bounds)
    {
        bounds = default;

        GameObject arena = GameObject.Find("FbxMap")
                        ?? GameObject.Find("SciFiArena")
                        ?? GameObject.Find("SciFiArena(Clone)");
        if (arena == null) return false;

        Transform floors = arena.transform.Find("Floors");
        if (floors == null) return false;

        Renderer[] floorRenderers = floors.GetComponentsInChildren<Renderer>(true);
        if (floorRenderers == null || floorRenderers.Length == 0) return false;

        bool init = false;
        for (int i = 0; i < floorRenderers.Length; i++)
        {
            Renderer r = floorRenderers[i];
            if (r == null) continue;
            if (!init) { bounds = r.bounds; init = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return init;
    }
}
