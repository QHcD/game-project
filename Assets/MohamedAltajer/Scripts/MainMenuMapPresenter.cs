using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class MainMenuMapPresenter : MonoBehaviour
{
    private const string MapRootName = "IndustrialMap_v3";
    private static bool _setupQueued;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif
        QueueSetup();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void RegisterEditorHook()
    {
        EditorApplication.delayCall -= QueueSetup;
        EditorApplication.delayCall += QueueSetup;
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
    {
        if (Application.isPlaying)
            return;

        QueueSetup();
    }
#endif

    private static void QueueSetup()
    {
        if (Application.isPlaying)
            return;

        if (_setupQueued)
            return;

        _setupQueued = true;
#if UNITY_EDITOR
        EditorApplication.delayCall += RunSetupDeferred;
#else
        RunSetup();
#endif
    }

#if UNITY_EDITOR
    private static void RunSetupDeferred()
    {
        _setupQueued = false;
        if (Application.isPlaying)
            return;

        RunSetup();
    }
#endif

    [ContextMenu("Setup Main Menu Map")]
    public static void RunSetup()
    {
        _setupQueued = false;

        if (!IsMainMenuScene())
            return;

#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif

        RemoveGameSceneLeftovers();
        MapAtmosphereCleanup.RemoveFromActiveScene();

        Transform mapRoot = FindMapRoot();
        if (mapRoot == null)
            return;

        mapRoot.gameObject.SetActive(true);
        HideNonGameplayMapMeshes(mapRoot);
        CenterAndGroundMap(mapRoot);
        PositionMainCamera(mapRoot);

#if UNITY_EDITOR
        FrameSceneView(mapRoot);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SceneView.RepaintAll();
#endif
    }

    private static bool IsMainMenuScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return scene.IsValid() && scene.isLoaded && scene.name == "MainMenu";
    }

    private static Transform FindMapRoot()
    {
        foreach (string name in new[] { "SciFiArena", MapRootName, "IndustrialMap", "FbxMap" })
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
                return go.transform;
        }

        return null;
    }

    private static void RemoveGameSceneLeftovers()
    {
        foreach (string name in new[] { "UrbanArenaRoot", "EnemiesRoot", "GameplayRoot", "__LevelBuilderRuntime" })
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
                continue;

            if (Application.isPlaying)
                Destroy(go);
#if UNITY_EDITOR
            else
                DestroyImmediate(go);
#endif
        }
    }

    private static void HideNonGameplayMapMeshes(Transform mapRoot)
    {
        Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("skybox") || n.Contains("reflection") || n.Contains("invisible"))
                r.enabled = false;
        }
    }

    private static bool TryGetMapBounds(Transform mapRoot, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || r is ParticleSystemRenderer)
                continue;

            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("skybox") || n.Contains("reflection") || n.Contains("invisible"))
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return hasBounds;
    }

    private static void CenterAndGroundMap(Transform mapRoot)
    {
        if (!TryGetMapBounds(mapRoot, out Bounds bounds))
            return;

        Vector3 pos = mapRoot.position;
        pos.x -= bounds.center.x;
        pos.z -= bounds.center.z;
        pos.y -= bounds.min.y;
        mapRoot.position = pos;
    }

    private static void PositionMainCamera(Transform mapRoot)
    {
        if (!TryGetMapBounds(mapRoot, out Bounds bounds))
            return;

        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
            return;

        float span = Mathf.Max(bounds.extents.x, bounds.extents.z, 24f);
        Vector3 focus = new Vector3(bounds.center.x, bounds.min.y + 2f, bounds.center.z);
        Vector3 offset = new Vector3(-span * 0.85f, span * 0.55f, -span * 0.85f);
        cam.transform.position = focus + offset;
        cam.transform.rotation = Quaternion.LookRotation((focus - cam.transform.position).normalized, Vector3.up);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = Mathf.Max(1000f, span * 8f);
    }

#if UNITY_EDITOR
    [MenuItem("PRISM/Setup Main Menu Map")]
    private static void SetupMainMenuMapMenu()
    {
        if (Application.isPlaying)
            return;

        RunSetup();
    }

    private static void FrameSceneView(Transform mapRoot)
    {
        if (!TryGetMapBounds(mapRoot, out Bounds bounds))
            return;

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return;

        sceneView.Frame(new Bounds(bounds.center, bounds.size + Vector3.up * 6f), false);
    }
#endif
}
