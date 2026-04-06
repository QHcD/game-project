using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class LevelBuilder : MonoBehaviour
{
    private const string RuntimeObjectName  = "__LevelBuilderRuntime";
    private const string GameplayRootName   = "GameplayRoot";
    private const string ArenaRootName      = "UrbanArenaRoot";
    private const string EnemyRootName      = "EnemiesRoot";
    private const string MinimapCameraName  = "MinimapCamera";
    private static LevelBuilder instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<LevelBuilder>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void Start() { HandleScene(SceneManager.GetActiveScene()); }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { HandleScene(scene); }

    private void HandleScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        StopAllCoroutines();
        if (scene.name == "GameScene")
        {
            EnsureGameManager();
            GameManager.Instance.SetPerspectiveMode(GameManager.PerspectiveMode.ThirdPerson);
            StartCoroutine(BuildGameSceneNextFrame());
        }
        else if (scene.name == "MainMenu")
        {
            StartCoroutine(CleanupMainMenuNextFrame());
        }
    }

    private IEnumerator BuildGameSceneNextFrame()
    {
        yield return null;
        EnsureGameManager();

        Transform gameplayRoot = GetOrCreateRoot(GameplayRootName);
        Transform arenaRoot    = GetOrCreateChildRoot(gameplayRoot, ArenaRootName);
        Transform enemyRoot    = GetOrCreateChildRoot(gameplayRoot, EnemyRootName);

        ClearChildren(arenaRoot);
        ClearChildren(enemyRoot);

        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.transform.position   = Vector3.zero;
            plane.transform.localScale = new Vector3(6f, 1f, 6f);
        }

        BuildArena(arenaRoot);
        EnsureMinimapCamera();

        // Build NavMesh FIRST — both player and enemies need it to find
        // the actual ground level on the auto-scaled FBX map.
        NavMeshSurface navMeshSurface = EnsureNavMeshSurface();
        if (navMeshSurface != null)
            navMeshSurface.BuildNavMesh();

        // Now place player and enemies ON the baked NavMesh.
        ConfigurePlayer();
        SpawnEnemies(enemyRoot);
        EnsureHud();
    }

    private IEnumerator CleanupMainMenuNextFrame()
    {
        yield return null;
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

        // Always create invisible physics bounds (floor + walls) for NavMesh & collision
        CreatePhysicsBounds(arenaRoot);

        // Load the FBX map as visual layer
        LoadFbxMap(arenaRoot, map);
    }

    /// <summary>Creates invisible floor + walls. These drive the NavMesh and keep
    /// characters inside the arena, but are not rendered.</summary>
    private void CreatePhysicsBounds(Transform parent)
    {
        // Floor — wide and flat for NavMesh baking
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "PhysicsFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, -0.26f, 0f);
        floor.transform.localScale    = new Vector3(44f, 0.5f, 44f);
        Renderer floorRend = floor.GetComponent<Renderer>();
        if (floorRend != null) floorRend.enabled = false; // invisible

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
    //  ENEMY SPAWNING — Battle Royale (12 combatants + player)
    // ════════════════════════════════════════════════════════════════════════

    private void SpawnEnemies(Transform enemyRoot)
    {
        int   enemyCount  = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 11;
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
            }
            else
            {
                // Fallback: coloured capsule
                enemyObject = CreatePrimitive(enemyRoot, "Enemy_" + (i + 1),
                    PrimitiveType.Capsule, spawnPos, new Vector3(1f, 1.1f, 1f),
                    new Color(0.60f, 0.10f, 0.16f));
            }

            enemyObject.tag = "Enemy";

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

            // Main collider — ensure a CapsuleCollider is present for hit detection
            if (enemyObject.GetComponent<Collider>() == null)
            {
                CapsuleCollider cap = enemyObject.AddComponent<CapsuleCollider>();
                cap.height = 2f;
                cap.radius = 0.45f;
                cap.center = new Vector3(0f, 1f, 0f);
            }

            EnemyController controller = EnsureComponent<EnemyController>(enemyObject);
            controller.moveSpeed   = Mathf.Max(3f, chaseSpeed - 0.6f);
            controller.chaseSpeed  = chaseSpeed;
            controller.attackDamage = enemyDamage;
            controller.maxHealth   = 55 + Mathf.RoundToInt((currentLvl - 1) * 5f);

            // Snap to actual NavMesh surface — fixes enemies floating above the map.
            // Search up to 8 units above/below the spawn point so scaled FBX maps work.
            NavMeshHit navHit;
            Vector3 sampleOrigin = spawnPos + Vector3.up * 4f;
            if (NavMesh.SamplePosition(sampleOrigin, out navHit, 8f, NavMesh.AllAreas))
            {
                enemyObject.transform.position = navHit.position;
                agent.Warp(navHit.position);
            }
            else
            {
                // Fallback: place slightly above origin so CharacterController settles
                enemyObject.transform.position = spawnPos + Vector3.up * 0.05f;
                Debug.LogWarning($"[LevelBuilder] No NavMesh found near {spawnPos} for Enemy_{i + 1}. " +
                                 "Ensure the NavMesh covers all spawn areas.");
            }

            // Attach the same melee weapon the player is using
            AttachWeaponToEnemy(enemyObject, currentLvl);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.enemiesRemaining = enemyCount;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ENEMY WEAPON ATTACHMENT
    // ════════════════════════════════════════════════════════════════════════

    private void AttachWeaponToEnemy(GameObject enemy, int level)
    {
        string weaponPath = GetWeaponResourcePath(level);
        if (string.IsNullOrEmpty(weaponPath)) return;

        GameObject weaponPrefab = Resources.Load<GameObject>(weaponPath);
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[LevelBuilder] Enemy weapon not found: {weaponPath}");
            return;
        }

        // Find best right-hand bone on the enemy model
        Transform handBone = FindRightHandBone(enemy.transform);
        Transform attachPoint = handBone != null ? handBone : enemy.transform;

        GameObject weapon = Instantiate(weaponPrefab, attachPoint);
        weapon.name = "EnemyWeapon";
        weapon.layer = enemy.layer;

        // Scale weapon to a natural hand-held size
        float targetSize = GetWeaponTargetSize(level);
        NormalizeWeaponScale(weapon, targetSize);

        // Orient weapon so it points forward from the hand (melee grip)
        weapon.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        weapon.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        // Remove any colliders from the weapon so it doesn't interfere with NavMesh
        foreach (Collider col in weapon.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    /// <summary>Searches the transform hierarchy for a right-hand bone.</summary>
    private Transform FindRightHandBone(Transform root)
    {
        string[] exactNames = {
            "Bip01 R Hand", "Bip001 R Hand",
            "mixamorig:RightHand", "RightHand", "Hand_R",
            "right_hand", "R_Hand", "HandRight", "Wrist_R",
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
    //  WEAPON RESOURCE PATHS  (mirrors PlayerController)
    // ════════════════════════════════════════════════════════════════════════

    private static string GetWeaponResourcePath(int level)
    {
        switch (level)
        {
            case  1: return "Weapons/Imported/tactical-knife(level1)/source/TacticalKnife/Tactical Knife";
            case  2: return "Weapons/Imported/Katana(level2)/source/melee";
            case  3: return "Weapons/Imported/shovel(level3)/source/Shovel/Shovel";
            case  4: return "Weapons/Imported/baseball-bat(level4)/source/baseball_bat_1k";
            case  5: return "Weapons/Imported/nunchucks(level5)/Nunchucks";
            case  6: return "Weapons/Imported/Wrench(level6)/source/PipeWrenchUnreal";
            case  7: return "Weapons/Imported/crowbar(level7)/source/CrowbarV2";
            case  8: return "Weapons/Imported/Hammer(level8)l/source/Sledgehammer/Sledge hammer";
            case  9: return "Weapons/Imported/axe(level9)/source/axe";
            case 10: return "Weapons/Imported/Spear(level10)/source/Spear/Spear";
            case 11: return "Weapons/Imported/nailed-plank(level11)/source/NailedPlank/NailedPlank";
            case 12: return "Weapons/Imported/saw(level12)/source/extracted/saw_low";
            case 13: return "Weapons/Imported/sickle(level13)/source/Sickle";
            case 14: return "Weapons/Imported/medieval(level14)/source/Medieval_morgenstern_low2 scene";
            case 15: return "Weapons/Imported/l3fte(level15)/source/L3FT_E";
            case 16: return "Weapons/Imported/shield(level16)/source/RiotShield/Riot Shield";
            default: return null;
        }
    }

    private static float GetWeaponTargetSize(int level)
    {
        float[] sizes = {
            0.30f, 0.95f, 1.00f, 0.85f, 0.30f,
            0.40f, 0.55f, 0.85f, 0.70f, 1.40f,
            1.00f, 0.40f, 0.35f, 0.50f, 0.60f,
            0.90f
        };
        return sizes[Mathf.Clamp(level - 1, 0, sizes.Length - 1)];
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

    /// <summary>Scales weapon so its largest dimension equals targetSize.</summary>
    private static void NormalizeWeaponScale(GameObject weapon, float targetSize)
    {
        Transform saved = weapon.transform.parent;
        weapon.transform.SetParent(null, false);
        weapon.transform.localScale = Vector3.one;

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;

        foreach (MeshFilter mf in weapon.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 wc = mf.transform.TransformPoint(mb.center);
            Vector3 we = Vector3.Scale(mb.extents, mf.transform.lossyScale);
            Bounds wb  = new Bounds(wc, we * 2f);
            if (!any) { b = wb; any = true; }
            else b.Encapsulate(wb);
        }

        weapon.transform.SetParent(saved, false);
        if (!any) return;

        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maxDim < 0.001f) return;

        weapon.transform.localScale = Vector3.one * (targetSize / maxDim);
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
            Destroy(root.GetChild(i).gameObject);
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
        if (playerController == null) return;

        if (!playerController.gameObject.CompareTag("Player"))
            playerController.gameObject.tag = "Player";

        // ── CharacterController must be DISABLED to set transform.position ──
        // When a CC is enabled, Unity blocks direct position writes. If the
        // player spawns inside geometry, all Move() calls silently fail →
        // frozen player, camera staring at a wall, instant death.
        CharacterController cc = playerController.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // ── Find safe spawn on the NavMesh ──────────────────────────────────
        // Try the preferred spawn first; fall back to centre of map if no
        // NavMesh triangle exists near the preferred point.
        Vector3 preferredSpawn = new Vector3(0f, 5f, -14f);
        Vector3 safeSpawn = preferredSpawn;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(preferredSpawn, out navHit, 15f, NavMesh.AllAreas))
        {
            safeSpawn = navHit.position + Vector3.up * 0.1f;
            Debug.Log($"[LevelBuilder] Player spawn snapped to NavMesh at {safeSpawn}");
        }
        else if (NavMesh.SamplePosition(Vector3.up * 5f, out navHit, 20f, NavMesh.AllAreas))
        {
            // Fallback: centre of the arena
            safeSpawn = navHit.position + Vector3.up * 0.1f;
            Debug.LogWarning("[LevelBuilder] Preferred spawn off-NavMesh — using map centre.");
        }
        else
        {
            safeSpawn = new Vector3(0f, 2f, 0f);
            Debug.LogWarning("[LevelBuilder] No NavMesh found for player — using raw fallback.");
        }

        playerController.transform.position = safeSpawn;
        playerController.transform.rotation = Quaternion.identity;

        // ── Configure CharacterController dimensions, then re-enable ────────
        if (cc != null)
        {
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.enabled = true;
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
                // Snap immediately instead of lerping from origin
                Vector3 orbitOffset = camCtrl.offset;
                Quaternion orbitRot = Quaternion.Euler(0f, playerController.transform.eulerAngles.y, 0f);
                tpCam.transform.position = playerController.transform.position + orbitRot * orbitOffset;
                tpCam.transform.LookAt(playerController.transform.position + Vector3.up * 1.35f);
            }
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

    private void EnsureHud()
    {
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject("HUDManager");
            hud = hudObject.AddComponent<HUDManager>();
        }
        hud.UpdateEnemyCount(GameManager.Instance.enemiesRemaining);
        hud.UpdateScore(GameManager.Instance.score);
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

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
