using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EnemyMinimapMarker : MonoBehaviour
{
    private const string MarkerLayerName = "MinimapIcons";
    private const string MarkerChildName = "__MinimapMarker";
    private const float HoverHeightFallback = 3.2f;
    private const float MarkerScaleFraction = 0.22f;
    private const float MarkerSizeFallback = 6.0f;
    private const float MarkerSizeMin = 4.0f;
    private const float MarkerSizeMax = 28.0f;
    private const float MinClearanceAboveGeometry = 8f;
    private const float MarkerOffsetBelowCamera = 1.5f;

    private static readonly Color MarkerColor = new Color(1f, 0.18f, 0.16f, 1f);
    private static Material s_sharedMaterial;
    private static Mesh s_sharedQuadMesh;

    private EnemyController _enemy;
    private Transform _markerTransform;
    private MeshRenderer _markerRenderer;
    private bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded) OnSceneLoaded(active, LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        string lower = scene.name.ToLowerInvariant();
        if (lower.Contains("menu") || lower.Contains("lobby") || lower.Contains("loading")) return;

        GameObject host = GameObject.Find("EnemyMinimapMarker_Installer");
        if (host == null) host = new GameObject("EnemyMinimapMarker_Installer");
        if (host.GetComponent<MinimapMarkerInstaller>() == null)
            host.AddComponent<MinimapMarkerInstaller>();
    }

    public static void EnsureOn(GameObject enemyRoot)
    {
        if (enemyRoot == null) return;
        if (enemyRoot.GetComponent<EnemyMinimapMarker>() != null) return;
        enemyRoot.AddComponent<EnemyMinimapMarker>();
    }

    private void Awake()
    {
        _enemy = GetComponent<EnemyController>();
    }

    private void OnEnable()
    {
        BuildMarkerIfNeeded();
    }

    private void LateUpdate()
    {
        if (!_initialized) BuildMarkerIfNeeded();
        if (_markerTransform == null) return;

        bool visible = _enemy == null || _enemy.IsAlive;
        if (_markerRenderer != null && _markerRenderer.enabled != visible)
            _markerRenderer.enabled = visible;

        if (visible)
        {
            float markerY = ResolveMarkerWorldY();
            float markerScale = ResolveMarkerScale();
            _markerTransform.position = new Vector3(transform.position.x, markerY, transform.position.z);
            _markerTransform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _markerTransform.localScale = new Vector3(markerScale, markerScale, markerScale);
        }
    }

    private float ResolveMarkerWorldY()
    {
        MinimapCameraFollow follow = MinimapCameraFollow.Instance;
        if (follow != null && follow.MapCamera != null)
            return follow.MapCamera.transform.position.y - MarkerOffsetBelowCamera;

        return transform.position.y + Mathf.Max(HoverHeightFallback, MinClearanceAboveGeometry);
    }

    private float ResolveMarkerScale()
    {
        MinimapCameraFollow follow = MinimapCameraFollow.Instance;
        if (follow == null || follow.MapCamera == null) return MarkerSizeFallback;
        Camera cam = follow.MapCamera;
        if (!cam.orthographic) return MarkerSizeFallback;
        float scaled = cam.orthographicSize * MarkerScaleFraction;
        return Mathf.Clamp(scaled, MarkerSizeMin, MarkerSizeMax);
    }

    private void OnDisable()
    {
        if (_markerRenderer != null) _markerRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (_markerTransform != null && _markerTransform.gameObject != null)
            Destroy(_markerTransform.gameObject);
    }

    private void BuildMarkerIfNeeded()
    {
        if (_initialized && _markerTransform != null) return;

        int layer = LayerMask.NameToLayer(MarkerLayerName);
        if (layer < 0) layer = gameObject.layer;

        Transform existing = transform.Find(MarkerChildName);
        GameObject markerObj;
        if (existing != null)
        {
            markerObj = existing.gameObject;
        }
        else
        {
            markerObj = new GameObject(MarkerChildName);
            markerObj.transform.SetParent(transform, false);
        }

        markerObj.layer = layer;
        SetLayerRecursive(markerObj.transform, layer);

        MeshFilter mf = markerObj.GetComponent<MeshFilter>();
        if (mf == null) mf = markerObj.AddComponent<MeshFilter>();
        mf.sharedMesh = GetSharedQuadMesh();

        MeshRenderer mr = markerObj.GetComponent<MeshRenderer>();
        if (mr == null) mr = markerObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetSharedMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        mr.allowOcclusionWhenDynamic = false;

        Collider[] strayColliders = markerObj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < strayColliders.Length; i++)
            if (strayColliders[i] != null) Destroy(strayColliders[i]);

        _markerTransform = markerObj.transform;
        _markerRenderer = mr;
        _initialized = true;
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        if (t == null) return;
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }

    private static Mesh GetSharedQuadMesh()
    {
        if (s_sharedQuadMesh != null) return s_sharedQuadMesh;

        Mesh m = new Mesh();
        m.name = "MinimapTriangleMesh";
        Vector3[] verts =
        {
            new Vector3(0f, 0f, 0.6f),
            new Vector3(-0.5f, 0f, -0.4f),
            new Vector3(0.5f, 0f, -0.4f),
        };
        int[] tris = { 0, 2, 1 };
        Vector3[] normals = { Vector3.up, Vector3.up, Vector3.up };
        m.vertices = verts;
        m.triangles = tris;
        m.normals = normals;
        m.RecalculateBounds();
        s_sharedQuadMesh = m;
        return s_sharedQuadMesh;
    }

    private static Material GetSharedMaterial()
    {
        if (s_sharedMaterial != null) return s_sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "MinimapMarker_SharedMat";
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", MarkerColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", MarkerColor);
        mat.color = MarkerColor;
        mat.enableInstancing = true;
        s_sharedMaterial = mat;
        return s_sharedMaterial;
    }
}

internal sealed class MinimapMarkerInstaller : MonoBehaviour
{
    private const float FastScanInterval = 0.1f;
    private const float SteadyScanInterval = 0.5f;
    private const float FastScanWindowSeconds = 6f;

    private float _nextScan;
    private float _startTime;
    private readonly List<GameObject> _scratch = new List<GameObject>(64);

    private void OnEnable()
    {
        _startTime = Time.unscaledTime;
        _nextScan = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextScan) return;
        bool fastWindow = (Time.unscaledTime - _startTime) < FastScanWindowSeconds;
        _nextScan = Time.unscaledTime + (fastWindow ? FastScanInterval : SteadyScanInterval);

        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController e = enemies[i];
            if (e == null || e.gameObject == null) continue;
            if (!e.IsAlive) continue;
            EnemyMinimapMarker.EnsureOn(e.gameObject);
        }
    }
}
