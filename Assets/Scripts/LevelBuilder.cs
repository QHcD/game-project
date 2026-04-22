using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class LevelBuilder : MonoBehaviour
{
    private const string RuntimeObjectName  = "__LevelBuilderRuntime";
    private const string GameplayRootName   = "GameplayRoot";
    private const string ArenaRootName      = "UrbanArenaRoot";
    private const string EnemyRootName      = "EnemiesRoot";
    private const string MinimapCameraName  = "MinimapCamera";
    private static readonly Vector3 SafeFallbackSpawn = new Vector3(0f, 1f, 0f);
    private static LevelBuilder instance;
    private bool _navMeshReady;
#if UNITY_EDITOR
    private static bool _editorPreviewQueued;
    private static double _lastEditorPreviewTime;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;
        if (instance != null) return;
        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<LevelBuilder>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            DestroyObjectSafe(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    // Guard: the last frame on which we built, so we never double-build.
    private int _lastBuiltFrame = -1;

    // Public accessor so scene-local fallback can reach us.
    public static LevelBuilder Instance => instance;

    private void OnEnable()
    {
        if (Application.isPlaying)
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
        else
            QueueEditorPreviewBuild();
#endif
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        HandleScene(SceneManager.GetActiveScene());
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void RegisterEditorPreviewHooks()
    {
        EditorApplication.delayCall -= QueueEditorPreviewBuild;
        EditorApplication.delayCall += QueueEditorPreviewBuild;
        EditorSceneManager.sceneOpened -= OnEditorSceneOpened;
        EditorSceneManager.sceneOpened += OnEditorSceneOpened;
    }

    private static void OnEditorSceneOpened(Scene scene, OpenSceneMode mode)
    {
        QueueEditorPreviewBuild();
    }

    [MenuItem("PRISM/Build Scene Preview (No Play Mode)")]
    private static void BuildScenePreviewFromMenu()
    {
        QueueEditorPreviewBuild(force: true);
    }

    private static void QueueEditorPreviewBuild()
    {
        QueueEditorPreviewBuild(force: false);
    }

    private static void QueueEditorPreviewBuild(bool force)
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (_editorPreviewQueued && !force)
            return;

        _editorPreviewQueued = true;
        EditorApplication.delayCall += () =>
        {
            _editorPreviewQueued = false;
            BuildEditorPreviewIfGameScene(force);
        };
    }

    private static void BuildEditorPreviewIfGameScene(bool force)
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded || activeScene.name != "GameScene")
            return;

        LevelBuilder builder = GetOrCreateEditorBuilder();
        if (builder == null)
            return;

        builder.BuildEditorScenePreview(force);
    }

    private static LevelBuilder GetOrCreateEditorBuilder()
    {
        LevelBuilder builder = FindFirstObjectByType<LevelBuilder>();
        if (builder != null)
            return builder;

        GameObject levelManager = GameObject.Find("LevelManager");
        if (levelManager == null)
            levelManager = new GameObject("LevelManager");

        builder = levelManager.GetComponent<LevelBuilder>();
        if (builder == null)
            builder = levelManager.AddComponent<LevelBuilder>();

        return builder;
    }

    public void BuildEditorScenePreview(bool force = false)
    {
        if (Application.isPlaying)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (!force && now - _lastEditorPreviewTime < 0.5d)
            return;

        _lastEditorPreviewTime = now;

        if (!force && HasCompleteScenePreview())
        {
            EnsurePreviewObjectsVisible();
            return;
        }

        Debug.Log("[LevelBuilder] Building edit-mode GameScene preview.");
        BuildGameScene();
        EnsurePreviewObjectsVisible();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        SceneView.RepaintAll();
    }

    private static bool HasCompleteScenePreview()
    {
        GameObject gameplayRoot = GameObject.Find(GameplayRootName);
        GameObject arenaRoot = GameObject.Find(ArenaRootName);
        GameObject enemyRoot = GameObject.Find(EnemyRootName);
        PlayerController player = FindFirstObjectByType<PlayerController>();

        bool hasArenaVisuals = arenaRoot != null &&
            arenaRoot.GetComponentsInChildren<Renderer>(true).Length > 0;
        bool hasEnemies = enemyRoot != null &&
            enemyRoot.GetComponentsInChildren<EnemyController>(true).Length > 0;

        return gameplayRoot != null && hasArenaVisuals && hasEnemies && player != null;
    }

    private static void EnsurePreviewObjectsVisible()
    {
        SetRootActive(GameplayRootName);
        SetRootActive(ArenaRootName);
        SetRootActive(EnemyRootName);

        GameObject arenaRoot = GameObject.Find(ArenaRootName);
        EnsureSceneGroundVisible(arenaRoot != null ? arenaRoot.transform : null);
    }

    private static void SetRootActive(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null && !obj.activeSelf)
            obj.SetActive(true);
    }
#endif

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleScene(scene);
    }

    /// <summary>
    /// Handles scene build DIRECTLY from the callback — no deferral to
    /// Update(), which was proven unreliable on DDOL objects after scene
    /// transitions in certain Unity versions.
    /// </summary>
    private void HandleScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        // Prevent double-build in the same frame (Start + OnSceneLoaded both fire).
        if (_lastBuiltFrame == Time.frameCount) return;
        _lastBuiltFrame = Time.frameCount;

        Debug.Log($"[LevelBuilder] HandleScene '{scene.name}' on frame {Time.frameCount}");

        if (scene.name == "GameScene")
        {
            Debug.Log("[LevelBuilder] Building GameScene (synchronous)...");
            BuildGameScene();
        }
        else if (scene.name == "MainMenu")
        {
            CleanupMainMenu();
        }
    }

    /// <summary>
    /// Public entry point so the scene-local fallback trigger can call us.
    /// </summary>
    public void TriggerBuild()
    {
        if (_lastBuiltFrame == Time.frameCount) return;
        Scene active = SceneManager.GetActiveScene();
        if (active.name == "GameScene")
        {
            _lastBuiltFrame = Time.frameCount;
            Debug.Log("[LevelBuilder] TriggerBuild called from scene-local fallback.");
            BuildGameScene();
        }
    }

    /// <summary>
    /// Synchronous GameScene build. Replaces the old coroutine approach
    /// that was silently dying during DDOL scene transitions.
    /// </summary>
    private void BuildGameScene()
    {
        Debug.Log("[LevelBuilder] ===== BUILD START =====");
        try
        {
            EnsureGameManager();
            Debug.Log("[LevelBuilder] Step 1: GameManager ensured");

            GameManager manager = GameManager.Instance;
            if (manager != null)
                manager.SetPerspectiveMode(GameManager.PerspectiveMode.ThirdPerson);

            Transform gameplayRoot = GetOrCreateRoot(GameplayRootName);
            Transform arenaRoot    = GetOrCreateChildRoot(gameplayRoot, ArenaRootName);
            Transform enemyRoot    = GetOrCreateChildRoot(gameplayRoot, EnemyRootName);
            ClearChildren(arenaRoot);
            ClearChildren(enemyRoot);
            Debug.Log("[LevelBuilder] Step 2: Roots created");

            GameObject plane = GameObject.Find("Plane");
            if (plane != null)
            {
                plane.SetActive(true);
                plane.transform.position   = Vector3.zero;
                plane.transform.localScale = new Vector3(6f, 1f, 6f);
                ForceGroundVisible(plane);
            }

            BuildArena(arenaRoot);
            EnsureSceneGroundVisible(arenaRoot);
            Debug.Log("[LevelBuilder] Step 3: Arena built");

            // Spawn environmental props BEFORE NavMesh so they become walkable surfaces
            Transform propRoot = GetOrCreateChildRoot(gameplayRoot, "PropsRoot");
            ClearChildren(propRoot);
            SpawnEnvironmentProps(propRoot);
            Debug.Log("[LevelBuilder] Step 4: Props spawned");

            EnsureMinimapCamera();
            Debug.Log("[LevelBuilder] Step 5: Minimap camera");

            _navMeshReady = TryBuildNavMesh();
            Debug.Log(_navMeshReady
                ? "[LevelBuilder] Step 6: NavMesh built"
                : "[LevelBuilder] Step 6: NavMesh skipped; using fallback spawn");

            ConfigurePlayer();
            Debug.Log("[LevelBuilder] Step 7: Player configured");

            TryInitializeOptionalAISystems();
            SpawnEnemies(enemyRoot);
            Debug.Log("[LevelBuilder] Step 8: Enemies spawned: " +
                (GameManager.Instance != null ? GameManager.Instance.enemiesRemaining.ToString() : "?"));

            EnsureHud();
            EnsurePauseMenu();
            Debug.Log("[LevelBuilder] ===== BUILD COMPLETE =====");
        }
        catch (System.Exception e)
        {
            string errorMsg = $"[LevelBuilder] BUILD FAILED: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            Debug.LogWarning(errorMsg);
            // Also write to file so we can read it even if console logs are unreachable
            try { System.IO.File.WriteAllText(Application.dataPath + "/../build_error.log", errorMsg); }
            catch { }
        }
    }

    private void CleanupMainMenu()
    {
        GameObject urbanArenaRoot = GameObject.Find(ArenaRootName);
        if (urbanArenaRoot != null) urbanArenaRoot.SetActive(false);
        GameObject enemiesRoot = GameObject.Find(EnemyRootName);
        if (enemiesRoot != null) enemiesRoot.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ARENA BUILDING
    // ════════════════════════════════════════════════════════════════════════

    private void BuildArena(Transform arenaRoot)
    {
        GameManager.ArenaMap map = GameManager.Instance != null
            ? GameManager.Instance.GetSelectedMap()
            : GameManager.ArenaMap.Map1;

        // Always create physics bounds for NavMesh and collision. The floor is visible.
        CreatePhysicsBounds(arenaRoot);

        // Load the FBX map as visual layer
        LoadFbxMap(arenaRoot, map);
    }

    /// <summary>Creates a visible floor and invisible walls for the arena.</summary>
    private void CreatePhysicsBounds(Transform parent)
    {
        // Floor — wide and flat for NavMesh baking
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Ground_PhysicsFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale    = new Vector3(44f, 0.1f, 44f);
        ForceGroundVisible(floor);

        // Walls
        string[] wallNames = { "PhysicsWall_N", "PhysicsWall_S", "PhysicsWall_E", "PhysicsWall_W" };
        Vector3[] wallPos  = {
            new Vector3(0f, 2.4f,  22f), new Vector3(0f, 2.4f, -22f),
            new Vector3(22f, 2.4f,  0f), new Vector3(-22f, 2.4f, 0f)
        };
        Vector3[] wallScale = {
            new Vector3(44f, 4.8f, 1f), new Vector3(44f, 4.8f, 1f),
            new Vector3(1f, 4.8f, 44f), new Vector3(1f, 4.8f, 44f)
        };
        for (int i = 0; i < 4; i++)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallNames[i];
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = wallPos[i];
            wall.transform.localScale    = wallScale[i];
            Renderer wr = wall.GetComponent<Renderer>();
            if (wr != null) wr.enabled = false; // invisible
        }
    }

    private static void EnsureSceneGroundVisible(Transform fallbackParent)
    {
        bool foundVisibleGround = false;

        string[] knownGroundNames =
        {
            "Plane", "Ground", "ground", "PhysicsFloor",
            "Ground_PhysicsFloor", "ArenaFloor", "VisibleGround_Fallback"
        };

        for (int i = 0; i < knownGroundNames.Length; i++)
        {
            GameObject ground = GameObject.Find(knownGroundNames[i]);
            if (ground != null)
                foundVisibleGround |= ForceGroundVisible(ground);
        }

        if (fallbackParent != null)
        {
            Renderer[] renderers = fallbackParent.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null || !LooksLikeGround(rend.gameObject.name))
                    continue;

                foundVisibleGround |= ForceGroundVisible(rend.gameObject);
            }
        }

        if (foundVisibleGround || fallbackParent == null)
            return;

        GameObject fallbackGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallbackGround.name = "VisibleGround_Fallback";
        fallbackGround.transform.SetParent(fallbackParent, false);
        fallbackGround.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        fallbackGround.transform.localScale = new Vector3(44f, 0.1f, 44f);
        ForceGroundVisible(fallbackGround);
        Debug.LogWarning("[LevelBuilder] No visible ground found; created VisibleGround_Fallback.");
    }

    private static bool ForceGroundVisible(GameObject groundObject)
    {
        if (groundObject == null)
            return false;

        groundObject.SetActive(true);

        bool hasRenderer = false;
        Renderer[] renderers = groundObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null)
                continue;

            rend.enabled = true;
            ApplyGroundMaterial(rend);
            hasRenderer = true;
        }

        return hasRenderer;
    }

    private static bool LooksLikeGround(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
        if (lower.Contains("wall"))
            return false;

        return lower.Contains("ground")
            || lower.Contains("floor")
            || lower.Contains("plane");
    }

    private static void ApplyGroundMaterial(Renderer rend)
    {
        if (rend == null)
            return;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
        if (litShader == null)
            return;

        Material mat = new Material(litShader);
        Color groundColor = new Color(0.42f, 0.44f, 0.39f, 1f);
        mat.color = groundColor;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", groundColor);

        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        rend.receiveShadows = true;
    }

    /// <summary>Loads the FBX map from Resources and places it as visual geometry.</summary>
    private void LoadFbxMap(Transform parent, GameManager.ArenaMap map)
    {
        string resourcePath = map == GameManager.ArenaMap.Map1
            ? "Maps/Map1/NukeTown"
            : "Maps/Map2/ccc";

        GameObject mapPrefab = Resources.Load<GameObject>(resourcePath);
        if (mapPrefab == null)
        {
            Debug.LogWarning($"[LevelBuilder] FBX map not found at Resources/{resourcePath}. Using procedural fallback.");
            CreateProceduralFallback(parent, map);
            return;
        }

        GameObject mapInstance = Instantiate(mapPrefab, parent);
        mapInstance.name = "FbxMap";
        mapInstance.transform.localPosition = Vector3.zero;
        mapInstance.transform.localRotation = Quaternion.identity;

        // Destroy any cameras baked into the FBX so they don't compete with
        // our runtime cameras (RuntimeThirdPersonCamera, MinimapCamera).
        foreach (Camera embeddedCam in mapInstance.GetComponentsInChildren<Camera>(true))
        {
            Debug.Log($"[LevelBuilder] Removing embedded camera '{embeddedCam.name}' from FBX map.");
            DestroyObjectSafe(embeddedCam.gameObject);
        }

        // Also destroy any AudioListener that shipped with the FBX
        foreach (AudioListener al in mapInstance.GetComponentsInChildren<AudioListener>(true))
            DestroyObjectSafe(al);

        // Auto-scale so the map fills roughly the 44×44 arena
        AutoScaleMap(mapInstance, 40f);

        // Add colliders to every mesh for physics & NavMesh interaction.
        // MeshCollider requires the mesh to be readable at runtime.
        // If a mesh isn't readable, fall back to a BoxCollider so NavMesh
        // baking still works and enemies can still walk on the surface.
        foreach (MeshFilter mf in mapInstance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.GetComponent<Collider>() != null) continue;
            if (mf.sharedMesh == null) continue;

            if (mf.sharedMesh.isReadable)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }
            else
            {
                // Fallback: bounding-box collider — still gives NavMesh a surface
                BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
                box.center = mf.sharedMesh.bounds.center;
                box.size   = mf.sharedMesh.bounds.size;
                Debug.LogWarning($"[LevelBuilder] Mesh '{mf.sharedMesh.name}' is not readable — " +
                                 "using BoxCollider fallback. Enable Read/Write in the FBX import settings " +
                                 "for precise collisions (PRISM > Fix All Map Mesh Read-Write).");
            }
        }

        // Fix URP shader incompatibility — imported FBX materials default to the
        // Standard (Built-in) shader, which URP renders as solid magenta/pink.
        // Re-assign every material to URP/Lit to restore correct visuals at runtime.
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Lit");
        if (urpLit != null)
        {
            foreach (Renderer rend in mapInstance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = rend.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && mats[m].shader != urpLit)
                    {
                        // Preserve albedo/base colour and main texture if present
                        Color albedo    = mats[m].HasProperty("_Color")       ? mats[m].GetColor("_Color")     : Color.white;
                        Texture mainTex = mats[m].HasProperty("_MainTex")     ? mats[m].GetTexture("_MainTex") : null;
                        mats[m]         = new Material(urpLit);
                        mats[m].SetColor("_BaseColor", albedo);
                        if (mainTex != null) mats[m].SetTexture("_BaseMap", mainTex);
                    }
                }
                rend.materials = mats;
            }
            Debug.Log("[LevelBuilder] FBX map materials upgraded to URP/Lit.");
        }
        else
        {
            Debug.LogWarning("[LevelBuilder] URP/Lit shader not found — map may appear magenta. " +
                             "Ensure Universal Render Pipeline is installed and set as the active pipeline.");
        }

        Debug.Log($"[LevelBuilder] FBX map loaded: {resourcePath}");
    }

    /// <summary>Scales the map so its largest horizontal dimension equals targetSize.</summary>
    private void AutoScaleMap(GameObject mapObj, float targetSize)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasAny = false;

        foreach (Renderer rend in mapObj.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasAny) { bounds = rend.bounds; hasAny = true; }
            else bounds.Encapsulate(rend.bounds);
        }

        if (!hasAny) return;

        float maxDim = Mathf.Max(bounds.size.x, bounds.size.z);
        if (maxDim < 0.01f) return;

        float scale = targetSize / maxDim;
        mapObj.transform.localScale = Vector3.one * scale;

        // Re-centre on arena floor after scaling
        Bounds newBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool reHas = false;
        foreach (Renderer rend in mapObj.GetComponentsInChildren<Renderer>(true))
        {
            if (!reHas) { newBounds = rend.bounds; reHas = true; }
            else newBounds.Encapsulate(rend.bounds);
        }
        if (reHas)
        {
            Vector3 pos = mapObj.transform.position;
            pos.x -= newBounds.center.x;
            pos.y  = -newBounds.min.y; // sit on Y=0
            pos.z -= newBounds.center.z;
            mapObj.transform.position = pos;
        }
    }

    /// <summary>Fallback: simple coloured arena when no FBX is available.</summary>
    private void CreateProceduralFallback(Transform parent, GameManager.ArenaMap map)
    {
        Color floorColor = new Color(0.18f, 0.19f, 0.22f);
        Color wallColor  = new Color(0.23f, 0.25f, 0.29f);

        CreatePrimitive(parent, "ArenaFloor", PrimitiveType.Cube,
            Vector3.zero, new Vector3(44f, 0.5f, 44f), floorColor);
        CreatePrimitive(parent, "NorthWall", PrimitiveType.Cube,
            new Vector3(0f, 2.4f,  22f), new Vector3(44f, 4.8f, 1f), wallColor);
        CreatePrimitive(parent, "SouthWall", PrimitiveType.Cube,
            new Vector3(0f, 2.4f, -22f), new Vector3(44f, 4.8f, 1f), wallColor);
        CreatePrimitive(parent, "EastWall",  PrimitiveType.Cube,
            new Vector3( 22f, 2.4f, 0f), new Vector3(1f, 4.8f, 44f), wallColor);
        CreatePrimitive(parent, "WestWall",  PrimitiveType.Cube,
            new Vector3(-22f, 2.4f, 0f), new Vector3(1f, 4.8f, 44f), wallColor);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ENEMY SPAWNING — 12 enemies plus the player
    // ════════════════════════════════════════════════════════════════════════

    private void SpawnEnemies(Transform enemyRoot)
    {
        int   enemyCount  = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 12;
        float chaseSpeed  = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() : 3.8f;
        float enemyDamage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() : 10f;
        int   currentLvl  = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        // Try loading the Crosby enemy model
        GameObject enemyPrefab = Resources.Load<GameObject>("Enemy/Crosby");

        Vector3[] spawnPoints =
        {
            new Vector3(-15f, 0.01f, -15f),
            new Vector3(  0f, 0.01f, -15f),
            new Vector3( 15f, 0.01f, -15f),
            new Vector3(-15f, 0.01f,   0f),
            new Vector3( 15f, 0.01f,   0f),
            new Vector3(-15f, 0.01f,  15f),
            new Vector3(  0f, 0.01f,  15f),
            new Vector3( 15f, 0.01f,  15f),
            new Vector3( -8f, 0.01f,  10f),
            new Vector3(  8f, 0.01f,  10f),
            new Vector3( -8f, 0.01f, -10f),
            new Vector3(  8f, 0.01f, -10f)
        };

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPos = spawnPoints[i % spawnPoints.Length];
            GameObject enemyObject;

            if (enemyPrefab != null)
            {
                // Instantiate the Crosby character model
                enemyObject = Instantiate(enemyPrefab);
                enemyObject.transform.SetParent(enemyRoot, false);
                enemyObject.name = "Enemy_" + (i + 1);
                enemyObject.transform.position = spawnPos;
                NormalizeEnemyScale(enemyObject, 1.8f);

                // Assign animator controller so enemies aren't stuck in T-pose
                Animator anim = enemyObject.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    RuntimeAnimatorController animCtrl =
                        Resources.Load<RuntimeAnimatorController>("Enemy/CrosbyAnimator");
                    if (animCtrl != null)
                    {
                        anim.runtimeAnimatorController = animCtrl;
                        Debug.Log($"[LevelBuilder] Animator assigned to Enemy_{i + 1}");
                    }
                    else
                    {
                        Debug.LogWarning("[LevelBuilder] CrosbyAnimator controller not found in Resources/Enemy/");
                    }
                }
            }
            else
            {
                // Fallback: coloured capsule
                enemyObject = CreatePrimitive(enemyRoot, "Enemy_" + (i + 1),
                    PrimitiveType.Capsule, spawnPos, new Vector3(1f, 1.1f, 1f),
                    new Color(0.60f, 0.10f, 0.16f));
            }

            enemyObject.tag = "Enemy";
            SetLayerRecursive(enemyObject, LayerMask.NameToLayer("Character"));

            // NavMeshAgent
            NavMeshAgent agent = EnsureComponent<NavMeshAgent>(enemyObject);
            agent.speed                  = chaseSpeed;
            agent.acceleration           = 14f;
            agent.angularSpeed           = 360f;
            agent.stoppingDistance       = 1.5f;
            agent.radius                 = 0.45f;
            agent.height                 = 2f;
            agent.avoidancePriority      = 30 + (i * 3) % 40;
            agent.obstacleAvoidanceType  = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // Main collider — ensure a CapsuleCollider is present for hit detection.
            // Use WORLD-SPACE target dimensions and convert to local space so the
            // collider is always the correct size regardless of the model's scale.
            if (enemyObject.GetComponent<Collider>() == null)
            {
                CapsuleCollider cap = enemyObject.AddComponent<CapsuleCollider>();

                // Convert desired world dimensions into local space of this transform.
                float worldHeight   = 1.8f;
                float worldRadius   = 0.45f;
                float worldCenterY  = worldHeight * 0.5f;   // 0.9 m — mid-body

                Vector3 ls = enemyObject.transform.lossyScale;
                float scaleY = Mathf.Abs(ls.y) > 0.0001f ? ls.y : 1f;
                float scaleXZ = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z));
                if (scaleXZ < 0.0001f) scaleXZ = 1f;

                cap.height = worldHeight  / scaleY;
                cap.radius = worldRadius  / scaleXZ;
                cap.center = new Vector3(0f, worldCenterY / scaleY, 0f);

                Debug.Log($"[LevelBuilder] CapsuleCollider: local h={cap.height:F3} r={cap.radius:F3} " +
                          $"(worldH={worldHeight} worldR={worldRadius} lossyScale={ls})");
            }

            EnemyController controller = EnsureComponent<EnemyController>(enemyObject);
            controller.moveSpeed   = Mathf.Max(3f, chaseSpeed - 0.6f);
            controller.chaseSpeed  = chaseSpeed;
            controller.attackDamage = enemyDamage;
            controller.maxHealth   = 55 + Mathf.RoundToInt((currentLvl - 1) * 5f);

            // Find an open-air spawn point (not inside buildings)
            Vector3 openSpawn = FindOpenEnemySpawn(spawnPos, i);
            enemyObject.transform.position = openSpawn;
            if (_navMeshReady && agent.isOnNavMesh)
                agent.Warp(openSpawn);
            else
                agent.transform.position = openSpawn;
            Debug.Log($"[LevelBuilder] Enemy_{i + 1} spawned at {openSpawn}");

            // Attach the same melee weapon the player is using
            AttachWeaponToEnemy(enemyObject, currentLvl);
        }

        // ── Issue #5: register the authoritative count with GameManager ──────
        // InitializeEnemyCount() sets BOTH enemiesRemaining AND totalEnemiesSpawned
        // so EnemyKilled() can compare against the real number of spawned enemies.
        if (GameManager.Instance != null)
            GameManager.Instance.InitializeEnemyCount(enemyCount);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ENEMY WEAPON ATTACHMENT
    // ════════════════════════════════════════════════════════════════════════

    private void AttachWeaponToEnemy(GameObject enemy, int level)
    {
        WeaponLoadout loadout = WeaponLoadoutCatalog.Get(level);
        float targetSize = loadout.TargetSize;
        GameObject weaponPrefab = loadout.LoadPrefab();
        if (weaponPrefab == null)
            weaponPrefab = WeaponLoadoutCatalog.LoadPrefabWithFallback(level, out targetSize);
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[LevelBuilder] All weapon sources exhausted for level {level}.");
            return;
        }

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller == null)
        {
            Debug.LogWarning("[LevelBuilder] EnemyController missing; cannot attach enemy weapon.");
            return;
        }

        // Prefer the rig-authored weapon socket so Crosby presents the melee
        // weapon with the same underhand grip silhouette as the player body.
        Transform handBone = FindRightHandBone(enemy.transform);
        if (handBone != null)
            controller.weaponAttachPoint = handBone;

        // Grip pose, socket euler, and stabilisation are now resolved inside
        // AttachWeaponToHand from the catalog — no pre-fill required here.
        controller.AttachWeaponToHand(weaponPrefab, targetSize, level);

        if (controller.equippedWeaponObject != null)
            SetLayerRecursive(controller.equippedWeaponObject, enemy.layer);
    }

    /// <summary>
    /// Destroys obvious armature helper nodes that ship inside some weapon FBX
    /// files. We deliberately keep renderer components intact because some
    /// imported weapons use SkinnedMeshRenderer for the visible weapon mesh.
    /// </summary>
    private static void StripWeaponArmature(GameObject weapon)
    {
        // Destroy any child whose name contains arm/rig/armature keywords
        string[] poisonKeywords = { "_ARM", "_Arm", "_arm", "Armature", "armature", "_Rig", "_rig" };
        var toDestroy = new System.Collections.Generic.List<GameObject>();

        foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
        {
            if (child == weapon.transform) continue;
            string n = child.name;
            foreach (string keyword in poisonKeywords)
            {
                if (n.Contains(keyword))
                {
                    toDestroy.Add(child.gameObject);
                    Debug.Log($"[StripWeaponArmature] Removing '{n}' from weapon '{weapon.name}'");
                    break;
                }
            }
        }

        foreach (GameObject obj in toDestroy)
        {
            if (obj != null && obj != weapon)
                Object.DestroyImmediate(obj);
        }
    }

    /// <summary>
    /// Safety net: if the weapon's world-space (lossy) scale exceeds maxWorldSize
    /// in any axis, force localScale down proportionally.
    /// </summary>
    private static void ClampWeaponWorldScale(GameObject weapon, float maxWorldSize)
    {
        Vector3 lossy = weapon.transform.lossyScale;
        float maxAxis = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
        if (maxAxis > maxWorldSize && maxAxis > 0.001f)
        {
            float clampFactor = maxWorldSize / maxAxis;
            weapon.transform.localScale *= clampFactor;
            Debug.LogWarning($"[ClampWeaponWorldScale] Clamped '{weapon.name}' from lossy {lossy} (max={maxAxis:F1}) by {clampFactor:F4}");
        }
    }

    /// <summary>Searches the transform hierarchy for a right-hand bone.</summary>
    private Transform FindRightHandBone(Transform root)
    {
        string[] exactNames = {
            "weapon_bone_R",                      // dedicated weapon socket
            "bip_hand_R",                         // Crosby / BIP rig (primary)
            "Bip01 R Hand", "Bip001 R Hand",     // 3ds Max Biped
            "mixamorig:RightHand", "RightHand",  // Mixamo
            "Hand_R", "right_hand", "R_Hand",
            "HandRight", "Wrist_R",
            "jointItemR", "RIGHT_HAND_COMBAT", "RIGHT_HAND_REST"
        };

        foreach (string name in exactNames)
        {
            Transform t = FindDeepChild(root, name);
            if (t != null) return t;
        }

        // Case-insensitive fallback
        return FindDeepChildContaining(root, "righthand")
            ?? FindDeepChildContaining(root, "hand_r")
            ?? FindDeepChildContaining(root, "wrist_r")
            ?? FindDeepChildContaining(root, "r_hand");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Scales the enemy so it stands ~targetHeight world units tall.</summary>
    private static void NormalizeEnemyScale(GameObject enemy, float targetHeight)
    {
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;

        foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any || b.size.y < 0.01f) return;

        float scale = targetHeight / b.size.y;
        enemy.transform.localScale = Vector3.one * scale;
    }

    private static void ApplyDesiredLossyScale(Transform target, Vector3 desiredLossyScale)
    {
        if (target == null) return;

        Vector3 parentLossyScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(
            desiredLossyScale.x / Mathf.Max(Mathf.Abs(parentLossyScale.x), 0.0001f),
            desiredLossyScale.y / Mathf.Max(Mathf.Abs(parentLossyScale.y), 0.0001f),
            desiredLossyScale.z / Mathf.Max(Mathf.Abs(parentLossyScale.z), 0.0001f));
    }

    private Transform FindDeepChild(Transform root, string boneName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == boneName) return child;
        return null;
    }

    private Transform FindDeepChildContaining(Transform root, string partial)
    {
        string lower = partial.ToLower();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name.ToLower().Contains(lower)) return child;
        return null;
    }

    /// <summary>Sets the layer on a GameObject and all of its children recursively.</summary>
    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj == null || layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private static void EnsureGameManager()
    {
        if (GameManager.Instance != null) return;
        GameObject managerObject = new GameObject("GameManager");
        managerObject.AddComponent<GameManager>();
    }

    private static Transform GetOrCreateRoot(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        return existing != null ? existing.transform : new GameObject(objectName).transform;
    }

    private static Transform GetOrCreateChildRoot(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null) return existing;
        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyObjectSafe(root.GetChild(i).gameObject);
    }

    private static void DestroyObjectSafe(Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private void ConfigurePlayer()
    {
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            GameObject playerPrefab = Resources.Load<GameObject>("FirstPersonMelee/Player");
            if (playerPrefab != null)
            {
                GameObject playerObject = Instantiate(playerPrefab);
                playerObject.name = "Player";
                playerController = playerObject.GetComponent<PlayerController>();
            }
        }
        if (playerController == null)
        {
            Debug.LogWarning("[LevelBuilder] PlayerController missing; scene preview continues without player controller.");
            return;
        }

        if (!playerController.gameObject.CompareTag("Player"))
            playerController.gameObject.tag = "Player";

        // Assign the "Character" layer so OverlapSphere-based FFA detection works
        SetLayerRecursive(playerController.gameObject, LayerMask.NameToLayer("Character"));

        // ── CharacterController must be DISABLED to set transform.position ──
        // Handled cleanly by PlayerController.TeleportTo
        CharacterController cc = playerController.GetComponent<CharacterController>();

        // ── Find safe spawn on OPEN ground (not inside buildings) ──────────
        // Try many candidate positions spread across the arena. For each one,
        // check NavMesh AND verify there's open sky above (no roof/wall).
        Vector3 safeSpawn = _navMeshReady
            ? FindOpenSpawnPoint(SafeFallbackSpawn)
            : SafeFallbackSpawn;
        Debug.Log($"[LevelBuilder] Player spawn: {safeSpawn}");

        playerController.TeleportTo(safeSpawn);
        playerController.transform.rotation = Quaternion.identity;
        Physics.SyncTransforms();

        // ── Configure CharacterController dimensions ────────
        if (cc != null)
        {
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;
            // cc.enabled is already toggled safely inside TeleportTo
        }

        EnsureComponent<PlayerHealth>(playerController.gameObject);
        playerController.RefreshGameplayPreferences();

        // ── Force the camera to snap to the new position immediately ────────
        // Without this, the camera lerps from (0,0,0) to the player over
        // several frames → "stuck at a wall" for the first second.
        Camera tpCam = playerController.ActiveCamera;
        if (tpCam != null)
        {
            CameraController camCtrl = tpCam.GetComponent<CameraController>();
            if (camCtrl != null)
            {
                camCtrl.target = playerController.transform;
                camCtrl.SnapToTarget();
            }
        }
        else
        {
            Debug.LogWarning("[LevelBuilder] Third-person camera missing after player setup; PlayerController will retry.");
        }

        Debug.Log($"[LevelBuilder] Player configured at {playerController.transform.position}");
    }
    private NavMeshSurface EnsureNavMeshSurface()
    {
        NavMeshSurface navMeshSurface = FindFirstObjectByType<NavMeshSurface>();
        if (navMeshSurface != null)
        {
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return navMeshSurface;
        }
        GameObject surfaceObject = new GameObject("NavMesh Surface");
        navMeshSurface = surfaceObject.AddComponent<NavMeshSurface>();
        navMeshSurface.collectObjects = CollectObjects.All;
        navMeshSurface.useGeometry    = NavMeshCollectGeometry.PhysicsColliders;
        return navMeshSurface;
    }

    private bool TryBuildNavMesh()
    {
        try
        {
            NavMeshSurface navMeshSurface = EnsureNavMeshSurface();
            if (navMeshSurface == null)
                return false;

            navMeshSurface.BuildNavMesh();
            return NavMesh.SamplePosition(SafeFallbackSpawn, out _, 6f, NavMesh.AllAreas);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LevelBuilder] NavMesh build failed; continuing with fallback spawn. {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    private void TryInitializeOptionalAISystems()
    {
        try
        {
            // Runtime combat does not depend on editor AI, Sentis, MCP, or npm-backed tooling.
            // Keep this wrapper as the isolation point so package failures cannot block play.
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LevelBuilder] Optional AI/Sentis setup skipped. Combat remains enabled. {e.GetType().Name}: {e.Message}");
        }
    }

    private void EnsureHud()
    {
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject("HUDManager");
            hud = hudObject.AddComponent<HUDManager>();
        }
        if (hud != null && GameManager.Instance != null)
        {
            hud.UpdateEnemyCount(GameManager.Instance.enemiesRemaining);
            hud.UpdateScore(GameManager.Instance.score);
        }
    }

    private void EnsurePauseMenu()
    {
        if (GetComponent<PauseMenuController>() == null)
            gameObject.AddComponent<PauseMenuController>();
    }

    private void EnsureMinimapCamera()
    {
        if (FindFirstObjectByType<MinimapCameraFollow>() != null) return;
        GameObject minimapObject = new GameObject(MinimapCameraName);
        Camera minimapCamera = minimapObject.AddComponent<Camera>();
        minimapCamera.transform.position = new Vector3(0f, 35f, 0f);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        MinimapCameraFollow follow = minimapObject.AddComponent<MinimapCameraFollow>();
        follow.lockToArenaCenter = false;
        follow.height = 35f;
        follow.viewRadius = 26f;
    }

    private static GameObject CreatePrimitive(Transform parent, string objectName,
        PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localScale    = scale;

        Renderer rend = primitive.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");
            Material mat = new Material(litShader);
            mat.color = color;
            rend.material = mat;
        }
        return primitive;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ENVIRONMENT PROPS — crates, barriers, ramps, vehicles
    // ════════════════════════════════════════════════════════════════════════

    private void SpawnEnvironmentProps(Transform propRoot)
    {
        // ── Crate clusters (scattered cover) ─────────────────────────────
        Vector3[] cratePositions = new Vector3[]
        {
            new Vector3(  5f, 0.5f,   3f),
            new Vector3(  5f, 0.5f,   4.2f),
            new Vector3(  5f, 1.5f,   3.6f),   // stacked on top
            new Vector3( -6f, 0.5f,  -4f),
            new Vector3( -6f, 0.5f,  -2.8f),
            new Vector3( 12f, 0.5f,   9f),
            new Vector3( 12f, 0.5f,  10.2f),
            new Vector3( 12f, 1.5f,   9.6f),   // stacked
            new Vector3(-10f, 0.5f,  12f),
            new Vector3(  0f, 0.5f, -12f),
            new Vector3(  8f, 0.5f, -10f),
            new Vector3( -8f, 0.5f,   8f),
        };

        Color crateColor = new Color(0.55f, 0.35f, 0.15f);  // wood brown
        for (int i = 0; i < cratePositions.Length; i++)
        {
            GameObject crate = CreatePrimitive(propRoot, $"Crate_{i + 1}",
                PrimitiveType.Cube, cratePositions[i], Vector3.one, crateColor);
            crate.isStatic = true;
        }

        // ── Barrier walls (waist-high cover) ─────────────────────────────
        Vector3[] barrierPos   = { new Vector3(3f,0.6f,-6f), new Vector3(-4f,0.6f,6f), new Vector3(10f,0.6f,-2f), new Vector3(-12f,0.6f,-8f), new Vector3(7f,0.6f,14f) };
        Vector3[] barrierScale = { new Vector3(4f,1.2f,0.3f), new Vector3(5f,1.2f,0.3f), new Vector3(3f,1.2f,0.3f), new Vector3(4f,1.2f,0.3f), new Vector3(6f,1.2f,0.3f) };
        float[]   barrierYRot  = { 0f, 30f, 90f, 45f, 0f };

        Color barrierColor = new Color(0.45f, 0.45f, 0.50f);  // concrete grey
        for (int i = 0; i < barrierPos.Length; i++)
        {
            GameObject wall = CreatePrimitive(propRoot, $"Barrier_{i + 1}",
                PrimitiveType.Cube, barrierPos[i], barrierScale[i], barrierColor);
            wall.transform.rotation = Quaternion.Euler(0f, barrierYRot[i], 0f);
            wall.isStatic = true;
        }

        // ── Ramps / stairs (climbable surfaces) ──────────────────────────
        Vector3[] rampPos   = { new Vector3(-2f,0.4f,-10f), new Vector3(14f,0.4f,5f), new Vector3(-9f,0.4f,-2f) };
        Vector3[] rampScale = { new Vector3(2f,0.2f,4f), new Vector3(2f,0.2f,4f), new Vector3(2f,0.2f,4f) };
        Vector3[] rampRot   = { new Vector3(15f,0f,0f), new Vector3(15f,90f,0f), new Vector3(15f,180f,0f) };

        Color rampColor = new Color(0.40f, 0.38f, 0.35f);  // dark stone
        for (int i = 0; i < rampPos.Length; i++)
        {
            GameObject ramp = CreatePrimitive(propRoot, $"Ramp_{i + 1}",
                PrimitiveType.Cube, rampPos[i], rampScale[i], rampColor);
            ramp.transform.rotation = Quaternion.Euler(rampRot[i]);
            ramp.isStatic = true;
        }

        // ── Vehicle husks (large cover, built from grouped cubes) ────────
        SpawnVehicleHusk(propRoot, "Car_1", new Vector3( 8f, 0f, -7f),   0f);
        SpawnVehicleHusk(propRoot, "Car_2", new Vector3(-5f, 0f, 10f),  45f);
        SpawnVehicleHusk(propRoot, "Car_3", new Vector3(15f, 0f,  2f), -30f);

        // ── Barrels (cylindrical cover) ──────────────────────────────────
        Vector3[] barrelPositions = new Vector3[]
        {
            new Vector3(  2f, 0.6f,   7f),
            new Vector3(  2.8f, 0.6f, 7f),
            new Vector3( -7f, 0.6f,  -7f),
            new Vector3( 11f, 0.6f,  12f),
            new Vector3(-14f, 0.6f,   0f),
            new Vector3(  0f, 0.6f,  15f),
        };

        Color barrelColor = new Color(0.25f, 0.30f, 0.20f);  // military green
        for (int i = 0; i < barrelPositions.Length; i++)
        {
            GameObject barrel = CreatePrimitive(propRoot, $"Barrel_{i + 1}",
                PrimitiveType.Cylinder, barrelPositions[i],
                new Vector3(0.5f, 0.6f, 0.5f), barrelColor);
            barrel.isStatic = true;
        }

        Debug.Log($"[LevelBuilder] Environment props placed: " +
            $"{cratePositions.Length} crates, {barrierPos.Length} barriers, " +
            $"{rampPos.Length} ramps, 3 cars, {barrelPositions.Length} barrels");
    }

    /// <summary>
    /// Builds a simple car-shaped husk from box primitives (body + roof + 4 wheels).
    /// </summary>
    private void SpawnVehicleHusk(Transform parent, string name, Vector3 position, float yRotation)
    {
        GameObject car = new GameObject(name);
        car.transform.SetParent(parent, false);
        car.transform.position = position;
        car.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        car.isStatic = true;

        Color bodyColor  = new Color(0.20f, 0.22f, 0.28f);  // dark steel
        Color roofColor  = new Color(0.15f, 0.15f, 0.20f);
        Color wheelColor = new Color(0.10f, 0.10f, 0.10f);

        // Car body
        CreatePrimitive(car.transform, "Body",
            PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f),
            new Vector3(3.8f, 1.0f, 1.6f), bodyColor).isStatic = true;

        // Roof / cabin
        CreatePrimitive(car.transform, "Roof",
            PrimitiveType.Cube, new Vector3(-0.2f, 1.3f, 0f),
            new Vector3(1.8f, 0.8f, 1.4f), roofColor).isStatic = true;

        // 4 wheels
        string[] wheelNames = { "WheelFL", "WheelFR", "WheelBL", "WheelBR" };
        Vector3[] wheelOffsets = {
            new Vector3( 1.2f, 0.2f,  0.8f),
            new Vector3( 1.2f, 0.2f, -0.8f),
            new Vector3(-1.2f, 0.2f,  0.8f),
            new Vector3(-1.2f, 0.2f, -0.8f),
        };
        for (int w = 0; w < 4; w++)
        {
            CreatePrimitive(car.transform, wheelNames[w],
                PrimitiveType.Cylinder, wheelOffsets[w],
                new Vector3(0.4f, 0.1f, 0.4f), wheelColor).isStatic = true;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  OPEN-AIR SPAWN LOGIC — avoids spawning inside buildings
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Finds an outdoor, NavMesh-valid spawn point. Tests many candidates
    /// across the arena and picks the first one that:
    ///   1) Has valid NavMesh below it
    ///   2) Has open sky above it (no roof/geometry)
    ///   3) Has enough horizontal clearance (no walls within 1m)
    /// </summary>
    private static Vector3 FindOpenSpawnPoint(Vector3 fallback)
    {
        if (!NavMesh.SamplePosition(fallback, out _, 6f, NavMesh.AllAreas))
            return SafeFallbackSpawn;

        // Grid of candidate XZ positions spread across the arena streets
        Vector3[] candidates =
        {
            new Vector3(  0f, 0f,   0f),
            new Vector3(  0f, 0f,  -8f),
            new Vector3(  0f, 0f,   8f),
            new Vector3( -8f, 0f,   0f),
            new Vector3(  8f, 0f,   0f),
            new Vector3( -5f, 0f,  -5f),
            new Vector3(  5f, 0f,  -5f),
            new Vector3( -5f, 0f,   5f),
            new Vector3(  5f, 0f,   5f),
            new Vector3(  0f, 0f, -14f),
            new Vector3(  0f, 0f,  14f),
            new Vector3(-12f, 0f,   0f),
            new Vector3( 12f, 0f,   0f),
            new Vector3(-10f, 0f, -10f),
            new Vector3( 10f, 0f, -10f),
            new Vector3(-10f, 0f,  10f),
            new Vector3( 10f, 0f,  10f),
            new Vector3(-16f, 0f,   0f),
            new Vector3( 16f, 0f,   0f),
            new Vector3(  0f, 0f, -18f),
            new Vector3(  0f, 0f,  18f),
        };

        foreach (Vector3 candidate in candidates)
        {
            if (IsOpenSpawnPoint(candidate, out Vector3 groundPos))
            {
                Debug.Log($"[LevelBuilder] Found open spawn at {groundPos} (candidate {candidate})");
                return groundPos;
            }
        }

        // Last resort — try the physics floor at origin
        NavMeshHit lastHit;
        if (NavMesh.SamplePosition(Vector3.zero, out lastHit, 5f, NavMesh.AllAreas))
            return lastHit.position + Vector3.up * 0.1f;

        return SafeFallbackSpawn;
    }

    /// <summary>
    /// Tests if a candidate XZ position is valid: on NavMesh, outdoors, and clear.
    /// </summary>
    private static bool IsOpenSpawnPoint(Vector3 xzCandidate, out Vector3 groundPos)
    {
        groundPos = Vector3.zero;

        // 1) Find NavMesh surface near this XZ position
        Vector3 samplePoint = new Vector3(xzCandidate.x, 0.5f, xzCandidate.z);
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(samplePoint, out hit, 3f, NavMesh.AllAreas))
            return false;

        // Reject points that are clearly on rooftops (too high above the arena floor)
        if (hit.position.y > 2.5f)
            return false;

        Vector3 feetPos = hit.position + Vector3.up * 0.1f;

        // 2) Check open sky — raycast upward from the spawn point. If it hits
        //    something within 4m, we're inside a building.
        if (Physics.Raycast(feetPos + Vector3.up * 0.5f, Vector3.up, 4f))
            return false;

        // 3) Check horizontal clearance — no walls within 1m in cardinal directions
        Vector3 chestHeight = feetPos + Vector3.up * 1f;
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (Vector3 dir in dirs)
        {
            if (Physics.Raycast(chestHeight, dir, 1f))
                return false; // too close to a wall
        }

        groundPos = feetPos;
        return true;
    }

    /// <summary>
    /// Finds an open spawn point for enemies. Same logic but with a wider set
    /// of candidates around the given position.
    /// </summary>
    private static Vector3 FindOpenEnemySpawn(Vector3 preferred, int index)
    {
        // Try the preferred position first
        if (IsOpenSpawnPoint(preferred, out Vector3 pos))
            return pos;

        // Try offsets from the preferred position
        Vector3[] offsets =
        {
            new Vector3( 2f, 0f,  0f), new Vector3(-2f, 0f,  0f),
            new Vector3( 0f, 0f,  2f), new Vector3( 0f, 0f, -2f),
            new Vector3( 4f, 0f,  0f), new Vector3(-4f, 0f,  0f),
            new Vector3( 0f, 0f,  4f), new Vector3( 0f, 0f, -4f),
            new Vector3( 3f, 0f,  3f), new Vector3(-3f, 0f, -3f),
        };

        foreach (Vector3 offset in offsets)
        {
            if (IsOpenSpawnPoint(preferred + offset, out Vector3 offsetPos))
                return offsetPos;
        }

        // Fallback — just NavMesh snap without clearance check
        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(preferred + Vector3.up * 0.5f, out fallbackHit, 5f, NavMesh.AllAreas))
            return fallbackHit.position + Vector3.up * 0.1f;

        return new Vector3(preferred.x, 0.1f, preferred.z);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
