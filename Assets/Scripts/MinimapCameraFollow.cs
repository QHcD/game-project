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

    private RenderTexture _rt;
    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        ConfigureCamera();
    }

    private void ConfigureCamera()
    {
        if (_cam == null) return;

        _cam.orthographic = true;
        _cam.orthographicSize = viewRadius;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = height + 10f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
        _cam.cullingMask = ~0; // render all layers
        _cam.depth = -2;       // render before main cameras
        _cam.enabled = false;  // HUDManager calls Render() manually — disable auto rendering
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
        // Clean up the RenderTexture to avoid GPU memory leaks
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }
}
