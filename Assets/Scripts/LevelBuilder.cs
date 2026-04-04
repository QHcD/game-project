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

        Vector3[] pillarPositions =
        {
            new Vector3(-14f, 2f, -12f),
            new Vector3(14f, 2f, -12f),
            new Vector3(-14f, 2f, 12f),
            new Vector3(14f, 2f, 12f),
            new Vector3(0f, 2f, 0f)
        };

        for (int i = 0; i < pillarPositions.Length; i++)
        {
            CreatePrimitive(parent, "Pillar_" + i, PrimitiveType.Cylinder, pillarPositions[i], new Vector3(2f, 2f, 2f), pillarColor);
        }

        Vector3[] cratePositions =
        {
            new Vector3(-8f, 1f, 6f),
            new Vector3(8f, 1f, 6f),
            new Vector3(-8f, 1f, -6f),
            new Vector3(8f, 1f, -6f)
        };

        for (int i = 0; i < cratePositions.Length; i++)
        {
            CreatePrimitive(parent, "Cover_" + i, PrimitiveType.Cube, cratePositions[i], new Vector3(3f, 2f, 2.4f), crateColor);
        }
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
            return navMeshSurface;
        }

        GameObject surfaceObject = new GameObject("NavMesh Surface");
        navMeshSurface = surfaceObject.AddComponent<NavMeshSurface>();
        navMeshSurface.collectObjects = CollectObjects.All;
        navMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
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
            agent.acceleration = 12f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 1.4f;
            agent.radius = 0.4f;
            agent.height = 2f;

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
