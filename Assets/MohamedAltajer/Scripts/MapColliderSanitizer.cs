using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MapColliderSanitizer : MonoBehaviour
{
    private static MapColliderSanitizer _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var host = new GameObject("MapColliderSanitizer");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<MapColliderSanitizer>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SanitizeCurrentScene();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SanitizeCurrentScene();
    }

    private static void SanitizeCurrentScene()
    {
        int fixedColliders = 0;
        int fixedRigidbodies = 0;
        int addedColliders = 0;

        int envLayer = LayerMask.NameToLayer("Environment");
        int mapLayer = LayerMask.NameToLayer("Map");
        int wallLayer = LayerMask.NameToLayer("Wall");
        int buildingLayer = LayerMask.NameToLayer("Building");
        int groundLayer = LayerMask.NameToLayer("Ground");
        int obstacleLayer = LayerMask.NameToLayer("StaticObstacle");
        int levelLayer = LayerMask.NameToLayer("LevelContent");
        int defaultLayer = 0;

        MeshCollider[] meshColliders = Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider mc = meshColliders[i];
            if (mc == null) continue;

            if (mc.sharedMesh == null)
            {
                MeshFilter mf = mc.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                {
                    mc.sharedMesh = mf.sharedMesh;
                    fixedColliders++;
                }
            }

            Rigidbody rb = mc.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic && !mc.convex)
            {
                if (mc.sharedMesh != null && mc.sharedMesh.vertexCount <= 255)
                    mc.convex = true;
                else
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
                fixedColliders++;
            }

            if (IsEnvironmentLayer(mc.gameObject.layer, envLayer, mapLayer, wallLayer,
                    buildingLayer, groundLayer, obstacleLayer, levelLayer, defaultLayer))
            {
                if (rb != null && !rb.isKinematic)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    fixedRigidbodies++;
                }
            }
        }

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            int layer = r.gameObject.layer;
            if (!IsEnvironmentLayer(layer, envLayer, mapLayer, wallLayer,
                    buildingLayer, groundLayer, obstacleLayer, levelLayer, -1))
                continue;

            if (r.GetComponent<Collider>() != null) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r is TrailRenderer) continue;
            if (r is LineRenderer) continue;

            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Bounds b = mf.sharedMesh.bounds;
            float maxExtent = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
            if (maxExtent < 0.1f) continue;

            if (mf.sharedMesh.isReadable)
            {
                MeshCollider mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }
            else
            {
                BoxCollider box = r.gameObject.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            addedColliders++;
        }

        if (fixedColliders > 0 || fixedRigidbodies > 0 || addedColliders > 0)
        {
            Debug.Log($"[MapColliderSanitizer] Scene sanitized: " +
                      $"colliders fixed={fixedColliders}, rigidbodies fixed={fixedRigidbodies}, " +
                      $"colliders added={addedColliders}");
        }
    }

    private static bool IsEnvironmentLayer(int layer, int env, int map, int wall,
        int building, int ground, int obstacle, int level, int def)
    {
        return layer == env || layer == map || layer == wall ||
               layer == building || layer == ground || layer == obstacle ||
               layer == level || (def >= 0 && layer == def);
    }
}
