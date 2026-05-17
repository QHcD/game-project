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
    private const string RuntimeThirdPersonCameraName = "RuntimeThirdPersonCamera";
    private static readonly Vector3 SafeFallbackSpawn = new Vector3(0f, 1f, 0f);
    private static LevelBuilder instance;
    private bool _navMeshReady;
#if UNITY_EDITOR
    private static bool _editorPreviewQueued;
    private static double _lastEditorPreviewTime;
    private static string _lastEditorPreviewSignature;
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

    [Header("Enemy spawn spacing")]
    [Tooltip("Preferred minimum horizontal distance between enemy spawn positions.")]
    public float minEnemySpawnSpacing = 4f;
    [Tooltip("Logs spawn spacing validation when enabled.")]
    public bool debugSpawnSpacing = false;

    private const float MinEnemySpawnHardFloor = 2f;

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
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorSceneManager.sceneOpened -= OnEditorSceneOpened;
        EditorSceneManager.sceneOpened += OnEditorSceneOpened;
    }

    private static void OnEditorSceneOpened(Scene scene, OpenSceneMode mode)
    {
        QueueEditorPreviewBuild();
    }

    private static void OnEditorUpdate()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded || activeScene.name != "GameScene")
            return;

        string signature = GetEditorPreviewSignature();
        if (!string.Equals(signature, _lastEditorPreviewSignature, System.StringComparison.Ordinal))
            QueueEditorPreviewBuild(force: true);
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
        LevelBuilder builder = Object.FindFirstObjectByType<LevelBuilder>();
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

        CleanupDuplicateGameManagersInEditor();
        CleanupDuplicateRuntimeThirdPersonCamerasInEditor();

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
        _lastEditorPreviewSignature = GetEditorPreviewSignature();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        SceneView.RepaintAll();
    }

    private static string GetEditorPreviewSignature()
    {
        GameManager manager = GameManager.Instance;
        int level = manager != null ? manager.currentLevel : 1;
        int map = manager != null ? (int)manager.GetSelectedMap() : 0;
        return $"{level}:{map}";
    }

    private static bool HasCompleteScenePreview()
    {
        GameObject gameplayRoot = GameObject.Find(GameplayRootName);
        GameObject arenaRoot = GameObject.Find(ArenaRootName);
        GameObject enemyRoot = GameObject.Find(EnemyRootName);
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();

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

            // ── Clear stale tagged level content from the previous level ───────
            // Kills any "Environment"/"LevelContent"/"Map"-tagged objects left
            // over from a previous build so the new map spawns into a clean
            // scene (fixes the "old map still visible in Editor" bug).
            ClearExistingLevel();

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
                HideNavOnlySurface(plane);
            }

            BuildArena(arenaRoot);
            // EnsureSceneGroundVisible skipped — industrial map has its own ground
            StabilizeGround(arenaRoot);
            EnsureIndustrialDoorsInteractable(arenaRoot);
            Debug.Log("[LevelBuilder] Step 3: Arena built + floor stabilised");

            // Environmental props are provided by the RPG/FPS industrial map prefab — skip procedural spawning.
            Debug.Log("[LevelBuilder] Step 4: Props skipped (industrial map provides own environment)");

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

    /// <summary>
    /// Imported industrial meshes name door pieces "…door…" / "…gate…" but ship without
    /// <see cref="DoorController"/>. Adds interaction on collider objects so
    /// the player can open them with the same raycast used for <see cref="IInteractable"/>.
    /// </summary>
    private static void EnsureIndustrialDoorsInteractable(Transform arenaRoot)
    {
        if (arenaRoot == null) return;

        Collider[] colliders = arenaRoot.GetComponentsInChildren<Collider>(true);
        int fixedCount = 0;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.enabled) continue;

            Transform doorRoot = FindNearestGeneratedDoorRoot(c);
            if (doorRoot == null) continue;
            if (doorRoot.GetComponentInParent<DoorController>(true) != null) continue;

            DoorController door = doorRoot.GetComponent<DoorController>();
            if (door == null)
                door = doorRoot.gameObject.AddComponent<DoorController>();

            DoorPassThroughOpen passThrough = doorRoot.GetComponent<DoorPassThroughOpen>();
            if (passThrough == null)
                passThrough = doorRoot.gameObject.AddComponent<DoorPassThroughOpen>();

            door.openOnStart         = false;
            door.openOnPlayerTrigger = false;
            door.interactiveToggle   = true;
            passThrough.hideOnOpen    = true;

            int envLayer = LayerMask.NameToLayer("Environment");
            if (envLayer >= 0)
                SetLayerRecursive(doorRoot.gameObject, envLayer);

            fixedCount++;
            Debug.Log($"[DoorFix] generatedDoor={doorRoot.name} collider={c.name} controllerAttached=True");
        }

        if (fixedCount > 0)
            Debug.Log($"[DoorFix] Ensured {fixedCount} generated door collider(s) have DoorController in their hierarchy.");
    }

    private static Transform FindNearestGeneratedDoorRoot(Collider collider)
    {
        if (collider == null) return null;

        for (Transform t = collider.transform; t != null; t = t.parent)
        {
            if (IsGeneratedDoorName(t.name))
                return t;

            if (t == collider.transform && t.name == "Object084" && IsKnownImportedDoorMesh(t))
                return t;
        }

        return null;
    }

    private static bool IsGeneratedDoorName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
        return lower.Contains("door")
            || lower.Contains("gate")
            || lower.Contains("garage")
            || lower.Contains("shutter")
            || lower.Contains("rollup");
    }

    private static bool IsKnownImportedDoorMesh(Transform t)
    {
        if (t == null || t.name != "Object084")
            return false;

        if (t.GetComponent<Collider>() == null ||
            t.GetComponent<Renderer>() == null ||
            t.GetComponent<MeshFilter>() == null)
            return false;

        for (Transform parent = t.parent; parent != null; parent = parent.parent)
        {
            string lower = parent.name.ToLowerInvariant();
            if (lower.Contains("hangar") || lower.Contains("industrial"))
                return true;
        }

        return false;
    }

    private void BuildArena(Transform arenaRoot)
    {
        GameManager.ArenaMap map = GameManager.Instance != null
            ? GameManager.Instance.GetSelectedMap()
            : GameManager.ArenaMap.Map1;

        // The industrial map provides its own ground and colliders —
        // skip procedural physics bounds entirely to avoid the green floor
        // and cyan wall artefacts that conflict with the map geometry.
        // LoadFbxMap adds MeshColliders to every mesh for NavMesh + physics.
        LoadFbxMap(arenaRoot, map);
    }

    // Arena half-size for the RPG/FPS industrial map (larger than the old 44×44 primitive arenas)
    private const float ArenaHalfSize = 80f;

    /// <summary>Creates a NavMesh floor and invisible boundary walls sized for the industrial map.</summary>
    private void CreatePhysicsBounds(Transform parent)
    {
        float full = ArenaHalfSize * 2f;

        // Physics floor — kept invisible; the industrial map provides its own visible ground.
        // It still gives NavMesh a solid surface to bake on.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Ground_PhysicsFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, -0.3f, 0f);  // slightly below map ground
        floor.transform.localScale    = new Vector3(full, 0.1f, full);
        Renderer floorRend = floor.GetComponent<Renderer>();
        if (floorRend != null) floorRend.enabled = false;  // invisible — industrial map ground shows instead

        // Invisible boundary walls — keep players and enemies inside the industrial arena
        string[] wallNames = { "PhysicsWall_N", "PhysicsWall_S", "PhysicsWall_E", "PhysicsWall_W" };
        Vector3[] wallPos  = {
            new Vector3(0f, 5f,  ArenaHalfSize), new Vector3(0f, 5f, -ArenaHalfSize),
            new Vector3( ArenaHalfSize, 5f, 0f), new Vector3(-ArenaHalfSize, 5f, 0f)
        };
        Vector3[] wallScale = {
            new Vector3(full, 10f, 1f), new Vector3(full, 10f, 1f),
            new Vector3(1f, 10f, full), new Vector3(1f, 10f, full)
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
            if (ground == null)
                continue;

            if (IsNavOnlySurfaceName(ground.name))
            {
                HideNavOnlySurface(ground);
                continue;
            }

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
        HideNavOnlySurface(fallbackGround);
        Debug.LogWarning("[LevelBuilder] No visible ground found; created hidden VisibleGround_Fallback for physics/NavMesh only.");
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

    private static void HideNavOnlySurface(GameObject surface)
    {
        if (surface == null)
            return;

        surface.SetActive(true);

        Renderer[] renderers = surface.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null)
                continue;

            rend.enabled = false;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        Collider[] colliders = surface.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }

        int envLayer = LayerMask.NameToLayer("Environment");
        if (envLayer >= 0)
            SetLayerRecursive(surface, envLayer);
    }

    private static bool IsNavOnlySurfaceName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
        return lower == "plane"
            || lower.Contains("physicsfloor")
            || lower.Contains("physics_floor")
            || lower.Contains("visibleground_fallback")
            || lower.Contains("navmesh")
            || lower.Contains("debug");
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

    /// <summary>Loads the industrial map prefab from Resources and places it as visual geometry.</summary>
    private void LoadFbxMap(Transform parent, GameManager.ArenaMap map)
    {
        // Both map slots now use the RPG/FPS industrial arena.
        // Run PRISM-7 ▸ Setup Industrial Map once in the editor to generate the prefab.
        string resourcePath = "Maps/IndustrialMap/IndustrialMap";

        GameObject mapPrefab = Resources.Load<GameObject>(resourcePath);

        // If the industrial prefab isn't generated yet, log a clear message.
        // Run PRISM-7 ▸ Setup Industrial Map (or wait for auto-setup) then re-enter Play.
        if (mapPrefab == null)
        {
            Debug.LogWarning("[LevelBuilder] Industrial map prefab not found at Resources/" + resourcePath +
                ".\nRun  PRISM-7 ▸ Setup Industrial Map  in the Editor (exit Play mode first), then press Play again.");
            CreateProceduralFallback(parent, map);
            return;
        }

        GameObject mapInstance = Instantiate(mapPrefab, parent);
        mapInstance.name = "FbxMap";
        mapInstance.transform.localPosition = Vector3.zero;
        mapInstance.transform.localRotation = Quaternion.identity;
        TagObjectIfDefined(mapInstance, "Map");
        TagHierarchyByName(mapInstance.transform);

        // ── Activate EVERYTHING in the industrial map ──────────────────────
        // The prefab may have been captured with some objects inactive.
        // Force every child object and renderer on so the full map is visible.
        foreach (Transform t in mapInstance.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);

        foreach (Renderer rend in mapInstance.GetComponentsInChildren<Renderer>(true))
            rend.enabled = true;

        RemoveUnwantedMapProps(mapInstance.transform);

        // ── Remove cameras / audio listeners that compete with ours ────────
        foreach (Camera embeddedCam in mapInstance.GetComponentsInChildren<Camera>(true))
        {
            Debug.Log($"[LevelBuilder] Removing embedded camera '{embeddedCam.name}' from industrial map.");
            DestroyObjectSafe(embeddedCam.gameObject);
        }
        foreach (AudioListener al in mapInstance.GetComponentsInChildren<AudioListener>(true))
            DestroyObjectSafe(al);

        // ── The industrial map ships at real-world scale — no scaling needed ─
        // ── DO NOT replace its materials — it already has correct URP textures ─
        // The old FBX material-swap logic is intentionally skipped here.
        // Replacing materials would wipe all industrial textures and make the
        // map appear as flat grey geometry.

        // ── Add colliders only where none exist (for NavMesh + physics) ────
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
                BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
                box.center = mf.sharedMesh.bounds.center;
                box.size   = mf.sharedMesh.bounds.size;
            }
        }

        EnsureIndustrialDoorsInteractable(mapInstance.transform);

        Debug.Log($"[LevelBuilder] Industrial map loaded and fully activated: {resourcePath}");
    }

    private static void RemoveUnwantedMapProps(Transform mapRoot)
    {
        if (mapRoot == null) return;

        var toRemove = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in mapRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == mapRoot) continue;
            string n = child.name.ToLowerInvariant();

            bool redBuilding = n.Contains("redbuilding") || n.Contains("red_building") || n.Contains("red building");
            bool car = n == "car" || n.StartsWith("car_") || n.Contains(" car ");
            bool woodenBoxes = n.Contains("woodenboxes") || n.Contains("wooden_boxes") || n.Contains("wooden_box") || n.Contains("wooden boxes");

            if (redBuilding || car || woodenBoxes)
                toRemove.Add(child.gameObject);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            if (toRemove[i] != null)
                toRemove[i].SetActive(false);
        }

        if (toRemove.Count > 0)
            Debug.Log($"[LevelBuilder] Hid {toRemove.Count} unwanted map prop(s): Car, WoodenBoxes, and RedBuilding only.");
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

    /// <summary>
    /// Arena anchors: legacy grid + rings + cardinals so spawns rotate around the map.
    /// </summary>
    private static System.Collections.Generic.List<Vector3> BuildDistributedArenaAnchors()
    {
        var anchors = new System.Collections.Generic.List<Vector3>(96)
        {
            new Vector3(-30f, 0.01f, -30f),
            new Vector3(  0f, 0.01f, -30f),
            new Vector3( 30f, 0.01f, -30f),
            new Vector3(-30f, 0.01f,   0f),
            new Vector3( 30f, 0.01f,   0f),
            new Vector3(-30f, 0.01f,  30f),
            new Vector3(  0f, 0.01f,  30f),
            new Vector3( 30f, 0.01f,  30f),
            new Vector3(-15f, 0.01f,  20f),
            new Vector3( 15f, 0.01f,  20f),
            new Vector3(-15f, 0.01f, -20f),
            new Vector3( 15f, 0.01f, -20f),
        };

        float[] radii = { 8f, 14f, 20f, 26f, 32f };
        const int segments = 16;
        for (int ri = 0; ri < radii.Length; ri++)
        {
            float r = radii[ri];
            for (int s = 0; s < segments; s++)
            {
                float ang = (s / (float)segments) * Mathf.PI * 2f;
                anchors.Add(new Vector3(Mathf.Cos(ang) * r, 0.01f, Mathf.Sin(ang) * r));
            }
        }

        anchors.Add(new Vector3(0f, 0.01f, -28f));
        anchors.Add(new Vector3(0f, 0.01f, 28f));
        anchors.Add(new Vector3(-28f, 0.01f, 0f));
        anchors.Add(new Vector3(28f, 0.01f, 0f));

        return anchors;
    }

    private static bool PassesEnemySeparationSq(Vector3 candidate, System.Collections.Generic.List<Vector3> placed, float minSepSqr)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            if ((placed[i] - candidate).sqrMagnitude < minSepSqr)
                return false;
        }

        return true;
    }

    private static float DistanceToNearestPlaced(Vector3 candidate, System.Collections.Generic.List<Vector3> placed)
    {
        if (placed == null || placed.Count == 0)
            return -1f;

        float best = float.MaxValue;
        for (int i = 0; i < placed.Count; i++)
            best = Mathf.Min(best, Vector3.Distance(candidate, placed[i]));

        return best == float.MaxValue ? -1f : best;
    }

    private static bool HasPathCompleteFromPlayer(Vector3 playerNavPos, Vector3 candidateWorld, NavMeshPath path)
    {
        if (!NavMesh.SamplePosition(candidateWorld, out NavMeshHit toHit, 3f, NavMesh.AllAreas))
            return false;
        if (!NavMesh.CalculatePath(playerNavPos, toHit.position, NavMesh.AllAreas, path))
            return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>
    /// Picks an open spawn with spacing + NavMesh PathComplete from player. Tier 1: minEnemySpawnSpacing; tier 2: 2 m floor.
    /// </summary>
    private bool TryPickEnemySpawnPosition(
        Vector3 playerNavPos,
        Vector3 rawPlayerPos,
        System.Collections.Generic.List<Vector3> placedSoFar,
        System.Collections.Generic.List<Vector3> anchors,
        NavMeshPath path,
        int enemyIndex,
        out Vector3 spawnWorld)
    {
        spawnWorld = default;

        float primary = Mathf.Max(MinEnemySpawnHardFloor, minEnemySpawnSpacing);
        float primarySq = primary * primary;
        const int attemptsTierA = 48;

        for (int a = 0; a < attemptsTierA; a++)
        {
            Vector3 anchor = anchors[(enemyIndex * 13 + a * 7) % anchors.Count];
            Vector3 open = FindOpenEnemySpawn(anchor, enemyIndex + a, rawPlayerPos);
            Vector3 cand = ResolveAgentSpawnPosition(open);
            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;
            cand = hit.position;

            if (!PassesEnemySeparationSq(cand, placedSoFar, primarySq))
                continue;
            if (!HasPathCompleteFromPlayer(playerNavPos, cand, path))
                continue;

            spawnWorld = cand;
            return true;
        }

        float fallbackSq = MinEnemySpawnHardFloor * MinEnemySpawnHardFloor;
        const int attemptsTierB = 40;

        for (int a = 0; a < attemptsTierB; a++)
        {
            Vector3 anchor = anchors[(enemyIndex * 19 + a * 11) % anchors.Count];
            Vector3 open = FindOpenEnemySpawn(anchor, enemyIndex * 3 + a, rawPlayerPos);
            Vector3 cand = ResolveAgentSpawnPosition(open);
            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;
            cand = hit.position;

            if (!PassesEnemySeparationSq(cand, placedSoFar, fallbackSq))
                continue;
            if (!HasPathCompleteFromPlayer(playerNavPos, cand, path))
                continue;

            spawnWorld = cand;
            return true;
        }

        return false;
    }

    private Vector3 EmergencyEnemySpawn(
        Vector3 playerNavPos,
        Vector3 rawPlayerPos,
        System.Collections.Generic.List<Vector3> placed,
        System.Collections.Generic.List<Vector3> anchors,
        NavMeshPath path,
        int enemyIndex)
    {
        float fallbackSq = MinEnemySpawnHardFloor * MinEnemySpawnHardFloor;

        for (int attempt = 0; attempt < 90; attempt++)
        {
            Vector3 jitter = Random.insideUnitSphere * (3f + attempt * 0.2f);
            jitter.y = 0f;
            Vector3 seed = anchors[(enemyIndex + attempt * 5) % anchors.Count] + jitter;
            Vector3 open = FindOpenEnemySpawn(seed, enemyIndex + attempt, rawPlayerPos);
            Vector3 cand = ResolveAgentSpawnPosition(open);
            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 14f, NavMesh.AllAreas))
                continue;
            cand = hit.position;

            if (!PassesEnemySeparationSq(cand, placed, fallbackSq))
                continue;
            if (!HasPathCompleteFromPlayer(playerNavPos, cand, path))
                continue;

            return cand;
        }

        for (int attempt = 0; attempt < 100; attempt++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float rad = Random.Range(8f, 36f);
            Vector3 probe = new Vector3(Mathf.Cos(ang) * rad, 0.01f, Mathf.Sin(ang) * rad);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 20f, NavMesh.AllAreas))
                continue;
            Vector3 cand = hit.position;

            if (!PassesEnemySeparationSq(cand, placed, fallbackSq))
                continue;
            if (!HasPathCompleteFromPlayer(playerNavPos, cand, path))
                continue;

            return cand;
        }

        // Exhaust anchors with small jitter — still requires PathComplete + ≥ 2 m separation.
        for (int ai = 0; ai < anchors.Count; ai++)
        {
            for (int jitterPass = 0; jitterPass < 6; jitterPass++)
            {
                Vector3 jitter = Random.insideUnitSphere * (1.5f + jitterPass * 0.8f);
                jitter.y = 0f;
                Vector3 seed = anchors[ai] + jitter;
                Vector3 open = FindOpenEnemySpawn(seed, enemyIndex + ai + jitterPass, rawPlayerPos);
                Vector3 cand = ResolveAgentSpawnPosition(open);
                if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 18f, NavMesh.AllAreas))
                    continue;
                cand = hit.position;

                if (!PassesEnemySeparationSq(cand, placed, fallbackSq))
                    continue;
                if (!HasPathCompleteFromPlayer(playerNavPos, cand, path))
                    continue;

                return cand;
            }
        }

        Vector3 last = ResolveAgentSpawnPosition(FindOpenEnemySpawn(anchors[0], enemyIndex, rawPlayerPos));
        if (NavMesh.SamplePosition(last, out NavMeshHit fh, 25f, NavMesh.AllAreas))
            last = fh.position;

        Debug.LogWarning(
            $"[LevelBuilder] Enemy spawn emergency fallback (spacing/path not guaranteed) index={enemyIndex}",
            this);

        return last;
    }

    private void SpawnEnemies(Transform enemyRoot)
    {
        int   enemyCount  = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 12;
        float enemyDamage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() : 10f;
        int   currentLvl  = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        PlayerController playerRef = Object.FindFirstObjectByType<PlayerController>();
        Vector3 playerPos = playerRef != null ? playerRef.transform.position : Vector3.zero;

        Vector3 playerNavPos = playerPos;
        if (NavMesh.SamplePosition(playerPos, out NavMeshHit playerSnap, 10f, NavMesh.AllAreas))
            playerNavPos = playerSnap.position;

        // Captured for the post-spawn reachability validation pass below.
        var spawnedEnemies = new System.Collections.Generic.List<GameObject>(enemyCount);

        // Try loading the Crosby enemy model
        GameObject enemyPrefab = Resources.Load<GameObject>("Enemy/Crosby");

        var arenaAnchors = BuildDistributedArenaAnchors();
        NavMeshPath spawnPath = new NavMeshPath();
        var placedPositions = new System.Collections.Generic.List<Vector3>(enemyCount);

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPos = arenaAnchors[(i * 5) % arenaAnchors.Count];
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
            SetLayerRecursive(enemyObject, ResolveHittableLayer());

            Vector3 agentSpawn;
            if (!TryPickEnemySpawnPosition(playerNavPos, playerPos, placedPositions, arenaAnchors,
                    spawnPath, i, out agentSpawn))
                agentSpawn = EmergencyEnemySpawn(playerNavPos, playerPos, placedPositions, arenaAnchors,
                    spawnPath, i);

            enemyObject.transform.position = agentSpawn;

            // NavMeshAgent
            // IMPORTANT: add the agent disabled first, snap onto NavMesh, then enable.
            // This prevents "Failed to create agent because it is not close enough to the NavMesh".
            NavMeshAgent agent = EnsureComponent<NavMeshAgent>(enemyObject);
            if (agent.enabled) agent.enabled = false;
            agent.speed                  = 5.2f;
            agent.acceleration           = 14f;
            agent.angularSpeed           = 540f;
            agent.stoppingDistance       = 1.7f;
            agent.radius                 = 0.45f;
            agent.height                 = 2f;
            agent.avoidancePriority      = 30 + (i * 3) % 40;
            agent.obstacleAvoidanceType  = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.updateRotation         = false;

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
            controller.moveSpeed          = 3.2f;
            controller.chaseSpeed         = 5.2f;
            controller.sprintChaseSpeed   = 6.2f;
            controller.agentAcceleration  = 14f;
            controller.agentAngularSpeed  = 540f;
            controller.attackDamage       = enemyDamage;
            controller.maxHealth          = 55 + Mathf.RoundToInt((currentLvl - 1) * 5f);

            agent.speed            = controller.chaseSpeed;
            agent.stoppingDistance = Mathf.Max(0.08f, controller.attackRadius * 0.85f);

            // Snap the agent onto the nearest NavMesh position before it begins moving.
            enemyObject.transform.position = agentSpawn;
            agent.enabled = true;
            PlaceAgentOnNavMesh(agent, enemyObject.transform, agentSpawn);

            Vector3 finalPos = enemyObject.transform.position;
            float distanceToNearest = DistanceToNearestPlaced(finalPos, placedPositions);
            placedPositions.Add(finalPos);

            if (debugSpawnSpacing)
            {
                Debug.Log(
                    $"[SpawnSpacing] enemy={enemyObject.name} pos={finalPos} distanceToNearest={distanceToNearest}");
            }

            // Attach the same melee weapon the player is using
            AttachWeaponToEnemy(enemyObject, currentLvl);

            spawnedEnemies.Add(enemyObject);
        }

        // ── Reachability validation ─────────────────────────────────────────
        // Walk every spawned enemy and ensure NavMesh.CalculatePath from the
        // player's position completes. If the agent ended up on a disconnected
        // NavMesh island (sealed room baked separately, locked building) the
        // player can never reach it; relocate it to a candidate point that is
        // reachable, on the NavMesh, and clear of other enemies.
        ValidateEnemyReachability(spawnedEnemies, playerPos, arenaAnchors);

        // ── Issue #5: register the authoritative count with GameManager ──────
        // InitializeEnemyCount() sets BOTH enemiesRemaining AND totalEnemiesSpawned
        // so EnemyKilled() can compare against the real number of spawned enemies.
        if (GameManager.Instance != null)
            GameManager.Instance.InitializeEnemyCount(enemyCount);
    }

    /// <summary>
    /// Verifies each spawned enemy is reachable from the player via NavMesh
    /// pathing. Enemies stuck on a disconnected NavMesh island are relocated
    /// to a reachable candidate ≥ 2 m from the player and other enemies.
    /// </summary>
    private static void ValidateEnemyReachability(
        System.Collections.Generic.List<GameObject> enemies,
        Vector3 playerPos,
        System.Collections.Generic.List<Vector3> candidatePool)
    {
        if (enemies == null || enemies.Count == 0) return;

        // Sanity-check the player's position is on the NavMesh — otherwise
        // CalculatePath always fails and we'd relocate everything pointlessly.
        if (!NavMesh.SamplePosition(playerPos, out NavMeshHit playerHit, 6f, NavMesh.AllAreas))
            return;
        Vector3 fromPos = playerHit.position;

        NavMeshPath path = new NavMeshPath();
        const float minSeparation = 2f;
        const float minSepSqr     = minSeparation * minSeparation;

        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            Vector3 enemyPos = enemy.transform.position;
            if (IsReachable(fromPos, enemyPos, path)) continue;

            Vector3 newPos;
            if (!FindReachableRelocation(fromPos, enemy, enemies, candidatePool,
                                         minSepSqr, path, out newPos))
                continue; // No safe spot found — leave enemy where it is.

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            PlaceAgentOnNavMesh(agent, enemy.transform, newPos);
            Debug.Log($"[SpawnValidation] enemy={enemy.name} reachable=false action=moved newPos={enemy.transform.position}");
        }
    }

    private static bool IsReachable(Vector3 from, Vector3 to, NavMeshPath path)
    {
        if (!NavMesh.SamplePosition(to, out NavMeshHit toHit, 2f, NavMesh.AllAreas))
            return false;
        if (!NavMesh.CalculatePath(from, toHit.position, NavMesh.AllAreas, path))
            return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>
    /// Tries every candidate spawn point + a ring of points around the player
    /// until it finds one that is on the NavMesh, has a complete path from the
    /// player, and is far enough from the player and every other enemy.
    /// </summary>
    private static bool FindReachableRelocation(
        Vector3 fromPos,
        GameObject enemy,
        System.Collections.Generic.List<GameObject> allEnemies,
        System.Collections.Generic.List<Vector3> candidatePool,
        float minSepSqr,
        NavMeshPath path,
        out Vector3 result)
    {
        // Build the candidate set: original spawn pool first (open arena), then
        // a ring around the player so we always have something to fall back to.
        var candidates = new System.Collections.Generic.List<Vector3>();
        if (candidatePool != null) candidates.AddRange(candidatePool);
        const int ringSamples = 12;
        for (int s = 0; s < ringSamples; s++)
        {
            float ang = (s / (float)ringSamples) * Mathf.PI * 2f;
            for (float r = 6f; r <= 18f; r += 4f)
            {
                candidates.Add(new Vector3(
                    fromPos.x + Mathf.Cos(ang) * r,
                    fromPos.y,
                    fromPos.z + Mathf.Sin(ang) * r));
            }
        }

        for (int c = 0; c < candidates.Count; c++)
        {
            if (!NavMesh.SamplePosition(candidates[c], out NavMeshHit hit, 4f, NavMesh.AllAreas))
                continue;

            Vector3 p = hit.position;
            if ((p - fromPos).sqrMagnitude < minSepSqr) continue;

            bool tooCloseToOther = false;
            for (int j = 0; j < allEnemies.Count; j++)
            {
                GameObject other = allEnemies[j];
                if (other == null || other == enemy) continue;
                if ((other.transform.position - p).sqrMagnitude < minSepSqr)
                { tooCloseToOther = true; break; }
            }
            if (tooCloseToOther) continue;

            if (!NavMesh.CalculatePath(fromPos, p, NavMesh.AllAreas, path)) continue;
            if (path.status != NavMeshPathStatus.PathComplete) continue;

            result = p;
            return true;
        }

        result = Vector3.zero;
        return false;
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

    private static int ResolveHittableLayer()
    {
        int layer = LayerMask.NameToLayer("Hittable");
        if (layer < 0) layer = LayerMask.NameToLayer("Character");
        return layer;
    }

    private static void EnsureGameManager()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            CleanupDuplicateGameManagersInEditor();
            CleanupDuplicateRuntimeThirdPersonCamerasInEditor();
            return;
        }
#endif

        if (GameManager.Instance != null) return;

        GameManager existing = Object.FindFirstObjectByType<GameManager>();
        if (existing != null) return;

        GameObject managerObject = new GameObject("GameManager");
        managerObject.AddComponent<GameManager>();
    }

#if UNITY_EDITOR
    private static void CleanupDuplicateGameManagersInEditor()
    {
        if (Application.isPlaying)
            return;

        GameManager[] managers = Object.FindObjectsByType<GameManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);

        if (managers.Length <= 1)
            return;

        for (int i = managers.Length - 1; i >= 1; i--)
        {
            if (managers[i] != null)
                DestroyObjectSafe(managers[i].gameObject);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[LevelBuilder] Removed {managers.Length - 1} duplicate GameManager object(s) from the editor scene.");
    }

    private static void CleanupDuplicateRuntimeThirdPersonCamerasInEditor()
    {
        if (Application.isPlaying)
            return;

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);

        int kept = 0;
        int removed = 0;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.gameObject.name != RuntimeThirdPersonCameraName)
                continue;

            if (kept == 0)
            {
                kept++;
                continue;
            }

            DestroyObjectSafe(camera.gameObject);
            removed++;
        }

        if (removed <= 0)
            return;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[LevelBuilder] Removed {removed} duplicate RuntimeThirdPersonCamera object(s) from the editor scene.");
    }
#endif

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
        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
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

        // Assign the Hittable layer so deterministic melee never scans scenery.
        SetLayerRecursive(playerController.gameObject, ResolveHittableLayer());

        // ── CharacterController must be DISABLED to set transform.position ──
        // Handled cleanly by PlayerController.TeleportTo
        CharacterController cc = playerController.GetComponent<CharacterController>();

        // ── Find safe spawn on OPEN ground (not inside buildings) ──────────
        // Try many candidate positions spread across the arena. For each one,
        // check NavMesh AND verify there's open sky above (no roof/wall).
        // 2026-05: also validate against environment colliders so the player
        // never spawns intersecting tanks/walls/props (the #1 "stuck at start"
        // issue reported in melee FFA).
        Vector3 safeSpawn = _navMeshReady
            ? FindValidatedPlayerSpawnPoint(SafeFallbackSpawn, playerController)
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
        NavMeshSurface navMeshSurface = Object.FindFirstObjectByType<NavMeshSurface>();
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
        HUDManager hud = Object.FindFirstObjectByType<HUDManager>();
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

        // MatchCommentator is attached to the DDOL GameManager in GameManager.Awake (fully programmatic VO).
    }

    private void EnsurePauseMenu()
    {
        if (GetComponent<PauseMenuController>() == null)
            gameObject.AddComponent<PauseMenuController>();
    }

    private void EnsureMinimapCamera()
    {
        if (Object.FindFirstObjectByType<MinimapCameraFollow>() != null) return;
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
    private static Vector3 FindRandomOpenSpawnPoint(Vector3 fallback)
    {
        if (!NavMesh.SamplePosition(fallback, out _, 6f, NavMesh.AllAreas))
            return SafeFallbackSpawn;

        // Grid of candidate XZ positions spread across the industrial arena (80×80)
        Vector3[] candidates =
        {
            new Vector3(  0f, 0f,   0f),
            new Vector3(  0f, 0f, -15f),
            new Vector3(  0f, 0f,  15f),
            new Vector3(-15f, 0f,   0f),
            new Vector3( 15f, 0f,   0f),
            new Vector3(-10f, 0f, -10f),
            new Vector3( 10f, 0f, -10f),
            new Vector3(-10f, 0f,  10f),
            new Vector3( 10f, 0f,  10f),
            new Vector3(  0f, 0f, -25f),
            new Vector3(  0f, 0f,  25f),
            new Vector3(-25f, 0f,   0f),
            new Vector3( 25f, 0f,   0f),
            new Vector3(-20f, 0f, -20f),
            new Vector3( 20f, 0f, -20f),
            new Vector3(-20f, 0f,  20f),
            new Vector3( 20f, 0f,  20f),
            new Vector3(-35f, 0f,   0f),
            new Vector3( 35f, 0f,   0f),
            new Vector3(  0f, 0f, -35f),
            new Vector3(  0f, 0f,  35f),
        };
        Shuffle(candidates);

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
    /// Like <see cref="FindRandomOpenSpawnPoint"/> but also guarantees the player capsule
    /// is not intersecting environment colliders and is not too close to enemies/props.
    /// </summary>
    private static Vector3 FindValidatedPlayerSpawnPoint(Vector3 fallback, PlayerController playerController)
    {
        Vector3 baseSpawn = FindRandomOpenSpawnPoint(fallback);

        // Use the player's live CharacterController if available (authoritative capsule).
        CharacterController cc = playerController != null ? playerController.GetComponent<CharacterController>() : null;
        float radius = cc != null ? Mathf.Max(0.2f, cc.radius) : 0.4f;
        float height = cc != null ? Mathf.Max(radius * 2f, cc.height) : 2.0f;
        Vector3 center = cc != null ? cc.center : new Vector3(0f, height * 0.5f, 0f);

        // Environment layers to avoid. We intentionally include everything except characters
        // so we don't spawn inside tanks/walls/stairs/props even if they are on Default.
        int hittable = ResolveHittableLayer();
        int mask = ~0;
        if (hittable >= 0) mask &= ~(1 << hittable);

        // Try multiple nearby offsets; the first that passes all checks wins.
        Vector3[] offsets =
        {
            Vector3.zero,
            new Vector3( 2f, 0f,  0f), new Vector3(-2f, 0f,  0f),
            new Vector3( 0f, 0f,  2f), new Vector3( 0f, 0f, -2f),
            new Vector3( 3f, 0f,  3f), new Vector3(-3f, 0f, -3f),
            new Vector3( 3f, 0f, -3f), new Vector3(-3f, 0f,  3f),
            new Vector3( 5f, 0f,  0f), new Vector3( 0f, 0f,  5f),
        };
        Shuffle(offsets);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 candidate = baseSpawn + offsets[i];
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3.0f, NavMesh.AllAreas))
                continue;
            Vector3 pos = navHit.position + Vector3.up * 0.1f;

            if (!TryProjectToGround(pos, out Vector3 ground, 6f, mask))
                continue;

            ground += Vector3.up * 0.02f;

            if (!IsCapsuleSpawnClear(ground, center, radius, height, mask, minObstacleDistance: 1.0f))
                continue;

            if (!IsFarFromEnemies(ground, minEnemyDistance: 6.0f))
                continue;

            // Explicit no-spawn zones by name (tanks/walls/stairs/ramps/containers).
            if (!IsFarFromNamedObstacles(ground, minDistance: 4.0f))
                continue;

            return ground;
        }

        // Last resort: pull to nearest NavMesh and accept (better than spawning inside geometry).
        if (NavMesh.SamplePosition(baseSpawn, out NavMeshHit last, 10f, NavMesh.AllAreas))
            return last.position + Vector3.up * 0.1f;

        return SafeFallbackSpawn;
    }

    private static bool TryProjectToGround(Vector3 origin, out Vector3 ground, float maxDistance, int mask)
    {
        ground = origin;
        Vector3 start = origin + Vector3.up * 2.5f;
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
        {
            ground = hit.point;
            return true;
        }
        return false;
    }

    private static readonly Collider[] _spawnOverlapBuffer = new Collider[64];

    private static bool IsCapsuleSpawnClear(
        Vector3 worldPosition,
        Vector3 capsuleCenterLocal,
        float radius,
        float height,
        int mask,
        float minObstacleDistance)
    {
        // Build capsule in world space.
        float half = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 center = worldPosition + capsuleCenterLocal;
        Vector3 p1 = center + Vector3.up * half;
        Vector3 p2 = center - Vector3.up * half;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            p1, p2,
            radius + Mathf.Max(0f, minObstacleDistance),
            _spawnOverlapBuffer,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _spawnOverlapBuffer[i];
            if (c == null) continue;
            if (!c.enabled) continue;
            if (c.isTrigger) continue;
            // Reject any solid overlap.
            return false;
        }

        return true;
    }

    private static bool IsFarFromEnemies(Vector3 pos, float minEnemyDistance)
    {
        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        float minSq = minEnemyDistance * minEnemyDistance;
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController e = enemies[i];
            if (e == null || !e.IsAlive) continue;
            Vector3 d = e.transform.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude < minSq)
                return false;
        }
        return true;
    }

    private static bool IsFarFromNamedObstacles(Vector3 pos, float minDistance)
    {
        string[] keywords = { "Tank", "WaterTank", "Container", "Wall", "Stairs", "Ramp" };
        Collider[] all = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        float minSq = minDistance * minDistance;

        for (int i = 0; i < all.Length; i++)
        {
            Collider c = all[i];
            if (c == null || !c.enabled || c.isTrigger) continue;

            // Skip characters (player/enemies) — handled by separate distance checks.
            if (c.GetComponentInParent<IDamageable>() != null) continue;

            string n = c.gameObject.name;
            bool match = false;
            for (int k = 0; k < keywords.Length; k++)
            {
                if (n.IndexOf(keywords[k], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = true;
                    break;
                }
            }
            if (!match) continue;

            Vector3 closest = c.ClosestPoint(pos);
            Vector3 d = closest - pos;
            d.y = 0f;
            if (d.sqrMagnitude < minSq)
                return false;
        }

        return true;
    }

    private static Vector3 FindOpenSpawnPoint(Vector3 fallback)
    {
        return FindRandomOpenSpawnPoint(fallback);
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
    private static Vector3 FindOpenEnemySpawn(Vector3 preferred, int index, Vector3 playerPosition)
    {
        float[] minClearMeters = { 15f, 11f, 7f, 4f, 0f };

        for (int tier = 0; tier < minClearMeters.Length; tier++)
        {
            if (TryFindEnemySpawnWithHorizontalClearance(preferred, playerPosition, minClearMeters[tier], out Vector3 found))
                return found;
        }

        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(preferred + Vector3.up * 0.5f, out fallbackHit, 5f, NavMesh.AllAreas))
            return fallbackHit.position + Vector3.up * 0.1f;

        return new Vector3(preferred.x, 0.1f, preferred.z);
    }

    private static bool TryFindEnemySpawnWithHorizontalClearance(
        Vector3 preferred, Vector3 playerPosition, float minHorizontal, out Vector3 result)
    {
        result = default;
        float minSq = minHorizontal * minHorizontal;

        Vector3[] offsets =
        {
            Vector3.zero,
            new Vector3( 2f, 0f,  0f), new Vector3(-2f, 0f,  0f),
            new Vector3( 0f, 0f,  2f), new Vector3( 0f, 0f, -2f),
            new Vector3( 4f, 0f,  0f), new Vector3(-4f, 0f,  0f),
            new Vector3( 0f, 0f,  4f), new Vector3( 0f, 0f, -4f),
            new Vector3( 6f, 0f,  0f), new Vector3(-6f, 0f,  0f),
            new Vector3( 0f, 0f,  6f), new Vector3( 0f, 0f, -6f),
            new Vector3( 3f, 0f,  3f), new Vector3(-3f, 0f, -3f),
            new Vector3( 3f, 0f, -3f), new Vector3(-3f, 0f,  3f),
            new Vector3( 5f, 0f,  5f), new Vector3(-5f, 0f, -5f),
            new Vector3( 8f, 0f,  0f), new Vector3(-8f, 0f,  0f),
            new Vector3( 0f, 0f,  8f), new Vector3( 0f, 0f, -8f),
        };
        Shuffle(offsets);

        Vector2 pXZ = new Vector2(playerPosition.x, playerPosition.z);

        foreach (Vector3 offset in offsets)
        {
            if (!IsOpenSpawnPoint(preferred + offset, out Vector3 offsetPos))
                continue;

            Vector2 eXZ = new Vector2(offsetPos.x, offsetPos.z);
            if ((eXZ - pXZ).sqrMagnitude >= minSq)
            {
                result = offsetPos;
                return true;
            }
        }

        return false;
    }

    // Progressive search radii — each step doubles the previous.
    // An enemy spawning inside a building or at a map edge will be pulled
    // to the nearest reachable NavMesh surface before the agent is enabled,
    // eliminating "Failed to create agent" warnings entirely.
    private static readonly float[] NavSnapRadii = { 1.5f, 4f, 10f, 25f };

    private static Vector3 ResolveAgentSpawnPosition(Vector3 preferred)
    {
        foreach (float r in NavSnapRadii)
        {
            if (NavMesh.SamplePosition(preferred, out NavMeshHit hit, r, NavMesh.AllAreas))
                return hit.position;
        }

        // Nothing found at any radius — return with a small upward offset so
        // the agent sits just above the terrain.
        return new Vector3(preferred.x, preferred.y + 0.2f, preferred.z);
    }

    private static void PlaceAgentOnNavMesh(NavMeshAgent agent, Transform target, Vector3 spawnPosition)
    {
        if (target == null) return;

        if (agent == null)
        {
            target.position = spawnPosition;
            return;
        }

        // 1. Disable agent before moving — avoids "Stop can only be called
        //    on an active agent".
        agent.enabled = false;
        target.position = spawnPosition;

        // 2. Re-enable only if the position is on the NavMesh.
        foreach (float r in NavSnapRadii)
        {
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, r, NavMesh.AllAreas))
            {
                target.position = hit.position;
                agent.enabled   = true;
                return;
            }
        }

        // 3. Position is genuinely off every baked NavMesh surface.
        //    Leave agent disabled; runtime recovery only teleports enemies that
        //    actually fall out of the world.
        Debug.LogWarning($"[LevelBuilder] Could not snap enemy to NavMesh near {spawnPosition}. " +
                         "Agent left disabled; EnemyController will retry at runtime.");
    }

    private static void Shuffle<T>(T[] values)
    {
        if (values == null) return;
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = values[i];
            values[i] = values[j];
            values[j] = temp;
        }
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  EDITOR-READY CLEANUP / STABILIZATION (Inspector-callable)
    // ════════════════════════════════════════════════════════════════════════
    //
    // Why these live here: the user can change the level index in the
    // Inspector and immediately see the new map preview in the Scene view
    // (because LevelBuilder is [ExecuteAlways]). But stale prefabs from the
    // previous level — anything tagged "Environment", "LevelContent", or
    // "Map" — would otherwise pile up on top of the new geometry, blocking
    // a clean NavMesh bake. ClearExistingLevel() wipes them. After the new
    // map is instantiated, StabilizeGround() walks the geometry and forces
    // floors to be static, on the Environment layer, and not kinematic-free.
    //
    // PrepareForBake() is the one-shot entry point the user calls from a
    // context-menu before pressing the Navigation window's Bake button.
    // ════════════════════════════════════════════════════════════════════════

    private static readonly string[] LevelContentTags = { "Environment", "LevelContent", "Map" };

    /// <summary>
    /// Destroys every loose GameObject in the active scene that carries one of
    /// the level-content tags. Runs in Editor and at runtime.
    /// </summary>
    [ContextMenu("Level/Clear Existing Level")]
    public void ClearExistingLevel()
    {
        int destroyed = 0;
        foreach (string tag in LevelContentTags)
        {
            GameObject[] tagged;
            try { tagged = GameObject.FindGameObjectsWithTag(tag); }
            catch { continue; } // Tag not defined in this project — skip.

            for (int i = 0; i < tagged.Length; i++)
            {
                if (tagged[i] == null) continue;
                if (tagged[i].transform.IsChildOf(transform)) continue; // never nuke ourselves
                DestroyObjectSafe(tagged[i]);
                destroyed++;
            }
        }

        // Also wipe the procedural roots so the next build starts clean.
        Transform arena = GameObject.Find(ArenaRootName)?.transform;
        Transform enemies = GameObject.Find(EnemyRootName)?.transform;
        if (arena != null)   ClearChildren(arena);
        if (enemies != null) ClearChildren(enemies);

        Debug.Log($"[LevelBuilder] ClearExistingLevel: removed {destroyed} tagged objects + procedural roots.");
    }

    /// <summary>
    /// Walks every renderer under <paramref name="root"/>, snaps anything that
    /// looks like a floor or wall onto the Environment layer, marks it static, and
    /// neutralises stray Rigidbodies that were causing the floor to drift.
    /// </summary>
    public void StabilizeGround(Transform root)
    {
        if (root == null) return;

        int envLayer = LayerMask.NameToLayer("Environment");
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            GameObject go = r.gameObject;
            string n = go.name.ToLowerInvariant();
            bool looksLikeEnvironment =
                n.Contains("floor") || n.Contains("ground") ||
                n.Contains("plane") || n.Contains("terrain") ||
                n.Contains("wall") || n.Contains("ceiling") ||
                n.Contains("concrete") || n.Contains("road");

            if (!looksLikeEnvironment) continue;

            // Layer
            if (envLayer >= 0) SetLayerRecursively(go, envLayer);

            // Static flags — marks for batching, navigation, occlusion bake.
#if UNITY_EDITOR
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(
                go, (UnityEditor.StaticEditorFlags)~0);
#endif
            go.isStatic = true;

            // Collider — guarantee one exists so NavMesh + physics work.
            if (go.GetComponent<Collider>() == null)
            {
                if (go.GetComponent<MeshFilter>() != null)
                    go.AddComponent<MeshCollider>();
                else
                    go.AddComponent<BoxCollider>();
            }

            // Rigidbody — environment must never move. Prefer to remove; if other
            // scripts depend on it, force-kinematic.
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                // If you'd rather delete it outright, uncomment:
                // DestroyObjectSafe(rb);
            }
        }
    }

    /// <summary>
    /// Editor-callable: ensures every level-content root is active, geometry
    /// is stabilised, and tags/layers are correct so the Navigation window's
    /// Bake button picks up the new map perfectly.
    /// </summary>
    [ContextMenu("Level/Prepare For Bake")]
    public void PrepareForBake()
    {
        // 1. Force every relevant root active so its renderers contribute.
        SetRootActive(GameplayRootName);
        SetRootActive(ArenaRootName);
        SetRootActive(EnemyRootName);

        Transform gameplay = GameObject.Find(GameplayRootName)?.transform;
        Transform arena = GameObject.Find(ArenaRootName)?.transform;
        Transform enemies = GameObject.Find(EnemyRootName)?.transform;

        if (gameplay != null)
            TagObjectIfDefined(gameplay.gameObject, "LevelContent");

        if (arena != null)
        {
            TagObjectIfDefined(arena.gameObject, "LevelContent");
            EnsureHierarchyActive(arena);
            TagHierarchyByName(arena);
        }

        if (enemies != null)
            EnsureHierarchyActive(enemies);

        // 2. Activate every tagged level-content object.
        foreach (string tag in LevelContentTags)
        {
            GameObject[] tagged;
            try { tagged = GameObject.FindGameObjectsWithTag(tag); }
            catch { continue; }
            foreach (GameObject go in tagged)
                if (go != null && !go.activeSelf) go.SetActive(true);
        }

        // 3. Stabilise the floor across both procedural and tagged content.
        if (arena != null) StabilizeGround(arena);

        foreach (string tag in LevelContentTags)
        {
            GameObject[] tagged;
            try { tagged = GameObject.FindGameObjectsWithTag(tag); }
            catch { continue; }
            foreach (GameObject go in tagged)
            {
                if (go == null) continue;
                EnsureHierarchyActive(go.transform);
                TagHierarchyByName(go.transform);
                StabilizeGround(go.transform);
            }
        }

#if UNITY_EDITOR
        // Mark scene dirty so the bake button sees the static-flag changes.
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
#endif
        Debug.Log("[LevelBuilder] PrepareForBake: scene is ready for NavMesh bake.");
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void SetRootActive(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return;
        GameObject obj = GameObject.Find(objectName);
        if (obj != null && !obj.activeSelf)
            obj.SetActive(true);
    }

    private static void EnsureHierarchyActive(Transform root)
    {
        if (root == null) return;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.SetActive(true);
    }

    private static void TagHierarchyByName(Transform root)
    {
        if (root == null) return;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == null) continue;

            string lowerName = child.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("floor") || lowerName.Contains("ground") ||
                lowerName.Contains("plane") || lowerName.Contains("terrain"))
                TagObjectIfDefined(child.gameObject, "Environment");
        }
    }

    private static void TagObjectIfDefined(GameObject go, string tag)
    {
        if (go == null || string.IsNullOrWhiteSpace(tag))
            return;

        try
        {
            go.tag = tag;
        }
        catch
        {
            // Tag is not configured in the project yet; skip safely.
        }
    }
}

public class DoorPassThroughOpen : MonoBehaviour
{
    public bool hideOnOpen = true;
    public Vector3 moveOffset = Vector3.up * 3f;

    public void OpenPassable()
    {
        int disabledCount = 0;
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.enabled) continue;
            c.enabled = false;
            disabledCount++;
        }

        string doorName = name;
        if (hideOnOpen)
            gameObject.SetActive(false);
        else
            transform.position += moveOffset;

        Debug.Log($"[DoorFix] OPENED_PASSABLE door={doorName} collidersDisabled={disabledCount}");
    }
}
