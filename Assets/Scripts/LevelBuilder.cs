using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class LevelBuilder : MonoBehaviour
{
    private const string RuntimeObjectName = "__LevelBuilderRuntime";
    private const string GameplayRootName = "GameplayRoot";
    private const string ArenaRootName = "UrbanArenaRoot";
    private const string EnemyRootName = "EnemiesRoot";
    private const string MinimapCameraName = "MinimapCamera";
    private static LevelBuilder instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<LevelBuilder>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        HandleScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleScene(scene);
    }

    private void HandleScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

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
        Transform arenaRoot = GetOrCreateChildRoot(gameplayRoot, ArenaRootName);
        Transform enemyRoot = GetOrCreateChildRoot(gameplayRoot, EnemyRootName);

        ClearChildren(arenaRoot);
        ClearChildren(enemyRoot);

        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(6f, 1f, 6f);
        }

        BuildArena(arenaRoot);
        ConfigurePlayer();
        EnsureMinimapCamera();

        NavMeshSurface navMeshSurface = EnsureNavMeshSurface();
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }

        SpawnEnemies(enemyRoot);
        EnsureHud();
    }

    private IEnumerator CleanupMainMenuNextFrame()
    {
        yield return null;

        GameObject urbanArenaRoot = GameObject.Find(ArenaRootName);
        if (urbanArenaRoot != null)
        {
            urbanArenaRoot.SetActive(false);
        }

        GameObject enemiesRoot = GameObject.Find(EnemyRootName);
        if (enemiesRoot != null)
        {
            enemiesRoot.SetActive(false);
        }
    }

    private static void EnsureGameManager()
    {
        if (GameManager.Instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("GameManager");
        managerObject.AddComponent<GameManager>();
    }

    private static Transform GetOrCreateRoot(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject created = new GameObject(objectName);
        return created.transform;
    }

    private static Transform GetOrCreateChildRoot(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void BuildArena(Transform arenaRoot)
    {
        GameManager.ArenaMap map = GameManager.Instance != null
            ? GameManager.Instance.GetSelectedMap()
            : GameManager.ArenaMap.BlacksiteFacility;

        CreateArenaFloor(arenaRoot, map);
        CreateArenaWalls(arenaRoot, map);
        CreateArenaObstacles(arenaRoot, map);
    }

    private void CreateArenaFloor(Transform parent, GameManager.ArenaMap map)
    {
        Color floorColor = map switch
        {
            GameManager.ArenaMap.CyberRuinsNeon => new Color(0.11f, 0.12f, 0.18f),
            GameManager.ArenaMap.ContainerPortYard => new Color(0.26f, 0.24f, 0.22f),
            _ => new Color(0.18f, 0.19f, 0.22f)
        };

        CreatePrimitive(parent, "ArenaFloor", PrimitiveType.Cube, Vector3.zero, new Vector3(44f, 0.5f, 44f), floorColor);
    }

    private void CreateArenaWalls(Transform parent, GameManager.ArenaMap map)
    {
        Color wallColor = map switch
        {
            GameManager.ArenaMap.CyberRuinsNeon => new Color(0.20f, 0.18f, 0.30f),
            GameManager.ArenaMap.ContainerPortYard => new Color(0.38f, 0.34f, 0.30f),
            _ => new Color(0.23f, 0.25f, 0.29f)
        };

        CreatePrimitive(parent, "NorthWall", PrimitiveType.Cube, new Vector3(0f, 2.4f, 22f), new Vector3(44f, 4.8f, 1f), wallColor);
        CreatePrimitive(parent, "SouthWall", PrimitiveType.Cube, new Vector3(0f, 2.4f, -22f), new Vector3(44f, 4.8f, 1f), wallColor);
        CreatePrimitive(parent, "EastWall", PrimitiveType.Cube, new Vector3(22f, 2.4f, 0f), new Vector3(1f, 4.8f, 44f), wallColor);
        CreatePrimitive(parent, "WestWall", PrimitiveType.Cube, new Vector3(-22f, 2.4f, 0f), new Vector3(1f, 4.8f, 44f), wallColor);
    }

    private void CreateArenaObstacles(Transform parent, GameManager.ArenaMap map)
    {
        Color pillarColor = map switch
        {
            GameManager.ArenaMap.CyberRuinsNeon => new Color(0.45f, 0.35f, 0.70f),
            GameManager.ArenaMap.ContainerPortYard => new Color(0.55f, 0.47f, 0.38f),
            _ => new Color(0.55f, 0.58f, 0.63f)
        };

        Color crateColor = map switch
        {
            GameManager.ArenaMap.CyberRuinsNeon => new Color(0.14f, 0.22f, 0.35f),
            GameManager.ArenaMap.ContainerPortYard => new Color(0.44f, 0.30f, 0.18f),
            _ => new Color(0.27f, 0.28f, 0.31f)
        };

        Color metalColor = map switch
        {
            GameManager.ArenaMap.CyberRuinsNeon => new Color(0.22f, 0.18f, 0.32f),
            GameManager.ArenaMap.ContainerPortYard => new Color(0.42f, 0.35f, 0.28f),
            _ => new Color(0.30f, 0.32f, 0.36f)
        };

        Color burntColor = map switch
        {
            GameManager.ArenaMap.CyberRuinsNeon => new Color(0.12f, 0.10f, 0.18f),
            GameManager.ArenaMap.ContainerPortYard => new Color(0.18f, 0.14f, 0.10f),
            _ => new Color(0.14f, 0.13f, 0.12f)
        };

        Color accentColor = map switch
        {
            GameManager.ArenaMap.CyberRuinsNeon => new Color(0.55f, 0.25f, 0.85f),
            GameManager.ArenaMap.ContainerPortYard => new Color(0.72f, 0.42f, 0.18f),
            _ => new Color(0.85f, 0.45f, 0.15f)
        };

        // ── Pillars ──
        Vector3[] pillarPositions =
        {
            new Vector3(-14f, 2f, -12f),
            new Vector3(14f, 2f, -12f),
            new Vector3(-14f, 2f, 12f),
            new Vector3(14f, 2f, 12f),
            new Vector3(0f, 2f, 0f)
        };
        for (int i = 0; i < pillarPositions.Length; i++)
            CreatePrimitive(parent, "Pillar_" + i, PrimitiveType.Cylinder, pillarPositions[i], new Vector3(2f, 2f, 2f), pillarColor);

        // ── Cover crates ──
        Vector3[] cratePositions =
        {
            new Vector3(-8f, 1f, 6f),
            new Vector3(8f, 1f, 6f),
            new Vector3(-8f, 1f, -6f),
            new Vector3(8f, 1f, -6f)
        };
        for (int i = 0; i < cratePositions.Length; i++)
            CreatePrimitive(parent, "Cover_" + i, PrimitiveType.Cube, cratePositions[i], new Vector3(3f, 2f, 2.4f), crateColor);

        // ── Burnt-out cars (body + 4 wheels) ──
        CreateBurntCar(parent, "BurntCar_1", new Vector3(-17f, 0f, 6f), 25f, burntColor, metalColor);
        CreateBurntCar(parent, "BurntCar_2", new Vector3(16f, 0f, -8f), -15f, burntColor, metalColor);

        // ── Jersey barriers / concrete barricades ──
        CreatePrimitive(parent, "Barrier_1", PrimitiveType.Cube, new Vector3(-5f, 0.55f, 16f), new Vector3(4.5f, 1.1f, 0.7f), metalColor);
        CreatePrimitive(parent, "Barrier_2", PrimitiveType.Cube, new Vector3(5f, 0.55f, -16f), new Vector3(4.5f, 1.1f, 0.7f), metalColor);
        CreatePrimitive(parent, "Barrier_3", PrimitiveType.Cube, new Vector3(18f, 0.55f, 4f), new Vector3(0.7f, 1.1f, 4.5f), metalColor);
        CreatePrimitive(parent, "Barrier_4", PrimitiveType.Cube, new Vector3(-18f, 0.55f, -4f), new Vector3(0.7f, 1.1f, 4.5f), metalColor);

        // ── Oil drums / barrels ──
        CreatePrimitive(parent, "Barrel_1", PrimitiveType.Cylinder, new Vector3(-4f, 0.7f, -18f), new Vector3(0.8f, 0.7f, 0.8f), accentColor);
        CreatePrimitive(parent, "Barrel_2", PrimitiveType.Cylinder, new Vector3(-3f, 0.7f, -17.5f), new Vector3(0.8f, 0.7f, 0.8f), accentColor);
        CreatePrimitive(parent, "Barrel_3", PrimitiveType.Cylinder, new Vector3(12f, 0.7f, 17f), new Vector3(0.8f, 0.7f, 0.8f), accentColor);

        // ── Debris / rubble piles ──
        CreatePrimitive(parent, "Rubble_1", PrimitiveType.Cube, new Vector3(11f, 0.3f, -2f), new Vector3(1.8f, 0.6f, 1.4f), burntColor);
        CreatePrimitive(parent, "Rubble_2", PrimitiveType.Cube, new Vector3(-12f, 0.25f, 3f), new Vector3(1.5f, 0.5f, 1.2f), burntColor);
        CreatePrimitive(parent, "Rubble_3", PrimitiveType.Sphere, new Vector3(3f, 0.4f, 12f), new Vector3(1.2f, 0.8f, 1.2f), burntColor);
        CreatePrimitive(parent, "Rubble_4", PrimitiveType.Sphere, new Vector3(-6f, 0.35f, -11f), new Vector3(1f, 0.7f, 1f), burntColor);

        // ── Sandbag stacks (low cover) ──
        CreatePrimitive(parent, "Sandbag_1", PrimitiveType.Cube, new Vector3(0f, 0.4f, 8f), new Vector3(2.8f, 0.8f, 1f), new Color(0.48f, 0.42f, 0.32f));
        CreatePrimitive(parent, "Sandbag_2", PrimitiveType.Cube, new Vector3(0f, 0.4f, -8f), new Vector3(2.8f, 0.8f, 1f), new Color(0.48f, 0.42f, 0.32f));

        // ── Tall scaffolding / watchtower posts ──
        CreatePrimitive(parent, "Tower_1", PrimitiveType.Cube, new Vector3(-19f, 2.8f, 18f), new Vector3(1.2f, 5.6f, 1.2f), metalColor);
        CreatePrimitive(parent, "TowerTop_1", PrimitiveType.Cube, new Vector3(-19f, 5.8f, 18f), new Vector3(2.6f, 0.3f, 2.6f), metalColor);
        CreatePrimitive(parent, "Tower_2", PrimitiveType.Cube, new Vector3(19f, 2.8f, -18f), new Vector3(1.2f, 5.6f, 1.2f), metalColor);
        CreatePrimitive(parent, "TowerTop_2", PrimitiveType.Cube, new Vector3(19f, 5.8f, -18f), new Vector3(2.6f, 0.3f, 2.6f), metalColor);

        // ── Tyre stacks ──
        CreatePrimitive(parent, "Tyre_1", PrimitiveType.Cylinder, new Vector3(6f, 0.25f, 18f), new Vector3(1.2f, 0.25f, 1.2f), new Color(0.12f, 0.12f, 0.12f));
        CreatePrimitive(parent, "Tyre_2", PrimitiveType.Cylinder, new Vector3(6.6f, 0.7f, 18f), new Vector3(1.1f, 0.25f, 1.1f), new Color(0.12f, 0.12f, 0.12f));
        CreatePrimitive(parent, "Tyre_3", PrimitiveType.Cylinder, new Vector3(-10f, 0.25f, 15f), new Vector3(1.2f, 0.25f, 1.2f), new Color(0.12f, 0.12f, 0.12f));
    }

    private void CreateBurntCar(Transform parent, string baseName, Vector3 position, float yRotation, Color bodyColor, Color wheelColor)
    {
        // Car body
        GameObject body = CreatePrimitive(parent, baseName + "_Body", PrimitiveType.Cube,
            position + new Vector3(0f, 0.75f, 0f), new Vector3(3.8f, 1.2f, 1.8f), bodyColor);
        body.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        // Roof / cabin
        GameObject cabin = CreatePrimitive(parent, baseName + "_Cabin", PrimitiveType.Cube,
            position + new Vector3(0f, 1.55f, 0f), new Vector3(1.8f, 0.8f, 1.6f), bodyColor);
        cabin.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        // 4 wheels
        Quaternion rot = Quaternion.Euler(0f, yRotation, 0f);
        Vector3 fl = position + rot * new Vector3(-1.3f, 0.3f, 0.85f);
        Vector3 fr = position + rot * new Vector3(-1.3f, 0.3f, -0.85f);
        Vector3 rl = position + rot * new Vector3(1.3f, 0.3f, 0.85f);
        Vector3 rr = position + rot * new Vector3(1.3f, 0.3f, -0.85f);

        CreatePrimitive(parent, baseName + "_WheelFL", PrimitiveType.Cylinder, fl, new Vector3(0.6f, 0.12f, 0.6f), wheelColor);
        CreatePrimitive(parent, baseName + "_WheelFR", PrimitiveType.Cylinder, fr, new Vector3(0.6f, 0.12f, 0.6f), wheelColor);
        CreatePrimitive(parent, baseName + "_WheelRL", PrimitiveType.Cylinder, rl, new Vector3(0.6f, 0.12f, 0.6f), wheelColor);
        CreatePrimitive(parent, baseName + "_WheelRR", PrimitiveType.Cylinder, rr, new Vector3(0.6f, 0.12f, 0.6f), wheelColor);
    }

    private static GameObject CreatePrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localScale = scale;

        Renderer rendererComponent = primitive.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                litShader = Shader.Find("Standard");
            }

            Material material = new Material(litShader);
            material.color = color;
            rendererComponent.material = material;
        }

        return primitive;
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
            return;
        }

        playerController.gameObject.tag = "Player";
        playerController.transform.position = new Vector3(0f, 0.01f, -14f);
        playerController.transform.rotation = Quaternion.identity;

        CharacterController characterController = playerController.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.height = 2f;
            characterController.radius = 0.4f;
        }

        EnsureComponent<PlayerHealth>(playerController.gameObject);
        playerController.RefreshGameplayPreferences();
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
        // Physics colliders avoid "mesh does not allow read access" on imported / default Plane meshes in builds.
        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        return navMeshSurface;
    }

    private void SpawnEnemies(Transform enemyRoot)
    {
        int enemyCount = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 10;
        float chaseSpeed = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() : 3.8f;
        float enemyDamage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() : 10f;

        Vector3[] spawnPoints =
        {
            new Vector3(-15f, 0.01f, -15f),
            new Vector3(0f, 0.01f, -15f),
            new Vector3(15f, 0.01f, -15f),
            new Vector3(-15f, 0.01f, 0f),
            new Vector3(15f, 0.01f, 0f),
            new Vector3(-15f, 0.01f, 15f),
            new Vector3(0f, 0.01f, 15f),
            new Vector3(15f, 0.01f, 15f),
            new Vector3(-8f, 0.01f, 10f),
            new Vector3(8f, 0.01f, 10f),
            new Vector3(-8f, 0.01f, -10f),
            new Vector3(8f, 0.01f, -10f)
        };

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPosition = spawnPoints[i % spawnPoints.Length];
            GameObject enemyObject = CreatePrimitive(enemyRoot, "Enemy_" + (i + 1), PrimitiveType.Capsule, spawnPosition, new Vector3(1f, 1.1f, 1f), new Color(0.60f, 0.10f, 0.16f));
            enemyObject.tag = "Enemy";

            NavMeshAgent agent = EnsureComponent<NavMeshAgent>(enemyObject);
            agent.speed = chaseSpeed;
            agent.acceleration = 14f;
            agent.angularSpeed = 360f;
            // stoppingDistance must match EnemyController.attackRadius so enemies
            // don't slide through the player before swinging
            agent.stoppingDistance = 1.5f;
            agent.radius = 0.45f;
            agent.height = 2f;
            // Each enemy gets a unique avoidance priority (30–69) so they negotiate
            // who yields when paths cross — prevents the "frozen cluster" bug
            agent.avoidancePriority = 30 + (i * 3) % 40;
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            EnemyController controller = EnsureComponent<EnemyController>(enemyObject);
            controller.moveSpeed = Mathf.Max(3f, chaseSpeed - 0.6f);
            controller.chaseSpeed = chaseSpeed;
            controller.attackDamage = enemyDamage;
            controller.maxHealth = 55 + Mathf.RoundToInt((GameManager.Instance.currentLevel - 1) * 5f);
        }

        GameManager.Instance.enemiesRemaining = enemyCount;
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
        if (FindFirstObjectByType<MinimapCameraFollow>() != null)
        {
            return;
        }

        GameObject minimapObject = new GameObject(MinimapCameraName);
        Camera minimapCamera = minimapObject.AddComponent<Camera>();
        minimapCamera.transform.position = new Vector3(0f, 35f, 0f);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        MinimapCameraFollow follow = minimapObject.AddComponent<MinimapCameraFollow>();
        follow.lockToArenaCenter = false;
        follow.height = 35f;
        follow.viewRadius = 26f;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }
}
