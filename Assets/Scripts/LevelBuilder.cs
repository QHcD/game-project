using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LevelBuilder : MonoBehaviour
{
    private const float ArenaRadius = 26f;
    private const int ArenaSegments = 20;

    private enum ArenaTheme
    {
        BlacksiteFacility,
        CyberRuinsNeon,
        ContainerPortYard
    }

    private void Start()
    {
        SetupManagers();
        BuildArena();
        SetupPlayer();
        SetupEnemies();
        SetupCameras();
        SetupMinimap();
        SetupLighting();
    }

    private ArenaTheme GetTheme()
    {
        if (GameManager.Instance == null)
        {
            return ArenaTheme.BlacksiteFacility;
        }

        switch (GameManager.Instance.GetSelectedMap())
        {
            case GameManager.ArenaMap.CyberRuinsNeon:
                return ArenaTheme.CyberRuinsNeon;
            case GameManager.ArenaMap.ContainerPortYard:
                return ArenaTheme.ContainerPortYard;
            default:
                return ArenaTheme.BlacksiteFacility;
        }
    }

    private void BuildArena()
    {
        GameObject existingRoot = GameObject.Find("ArenaRoot");
        if (existingRoot != null)
        {
            Destroy(existingRoot);
        }

        GameObject arenaRoot = new GameObject("ArenaRoot");

        BuildGround(arenaRoot.transform);
        BuildBoundary(arenaRoot.transform);
        BuildInnerCover(arenaRoot.transform);
        BuildThemeProps(arenaRoot.transform);
    }

    private void BuildGround(Transform root)
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Ground";
        }

        ground.transform.SetParent(root, false);
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.rotation = Quaternion.identity;
        ground.transform.localScale = new Vector3(6f, 0.5f, 6f);

        SetLitColor(ground, GetGroundColor(GetTheme()));

        GameObject centerDisk = CreateArenaProp(root, "ArenaCenterDisk", PrimitiveType.Cylinder,
            new Vector3(0f, -0.44f, 0f), new Vector3(2.4f, 0.18f, 2.4f), GetLaneColor(GetTheme()));
        centerDisk.transform.rotation = Quaternion.identity;

        GameObject ringDisk = CreateArenaProp(root, "ArenaRingDisk", PrimitiveType.Cylinder,
            new Vector3(0f, -0.46f, 0f), new Vector3(4.6f, 0.08f, 4.6f), new Color(0.32f, 0.36f, 0.42f));
        ringDisk.transform.rotation = Quaternion.identity;
    }

    private void BuildBoundary(Transform root)
    {
        Color wallColor = GetWallColor(GetTheme());
        Color pillarColor = GetPlatformColor(GetTheme());

        for (int i = 0; i < ArenaSegments; i++)
        {
            float angle = (Mathf.PI * 2f / ArenaSegments) * i;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            GameObject wallSegment = CreateArenaProp(root, "ArenaWall_" + i, PrimitiveType.Cube,
                direction * ArenaRadius + Vector3.up * 2.4f,
                new Vector3(7f, 4.8f, 1.4f), wallColor);
            wallSegment.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            GameObject pillar = CreateArenaProp(root, "ArenaPillar_" + i, PrimitiveType.Cylinder,
                direction * (ArenaRadius - 1.5f) + Vector3.up * 2.8f,
                new Vector3(0.8f, 2.8f, 0.8f), pillarColor);
            pillar.transform.rotation = Quaternion.identity;
        }
    }

    private void BuildInnerCover(Transform root)
    {
        Color coverColor = GetCoverColor(GetTheme());

        for (int i = 0; i < 8; i++)
        {
            float angle = (Mathf.PI * 2f / 8f) * i;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float radius = i % 2 == 0 ? 11f : 14f;

            GameObject cover = CreateArenaProp(root, "ArenaCover_" + i, PrimitiveType.Cube,
                direction * radius + Vector3.up * 1.15f,
                new Vector3(4.2f, 2.3f, 2.2f), coverColor);
            cover.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        CreateArenaProp(root, "ArenaCentralBlock_A", PrimitiveType.Cube,
            new Vector3(-4.5f, 1.2f, 0f), new Vector3(3f, 2.4f, 9f), new Color(0.27f, 0.30f, 0.36f));
        CreateArenaProp(root, "ArenaCentralBlock_B", PrimitiveType.Cube,
            new Vector3(4.5f, 1.2f, 0f), new Vector3(3f, 2.4f, 9f), new Color(0.27f, 0.30f, 0.36f));
    }

    private void BuildThemeProps(Transform root)
    {
        ArenaTheme theme = GetTheme();

        if (theme == ArenaTheme.BlacksiteFacility)
        {
            CreateArenaProp(root, "AccentNorth", PrimitiveType.Cube,
                new Vector3(0f, 4.6f, 18f), new Vector3(10f, 0.35f, 1.2f), new Color(0.92f, 0.28f, 0.24f));
            CreateArenaProp(root, "AccentSouth", PrimitiveType.Cube,
                new Vector3(0f, 4.6f, -18f), new Vector3(10f, 0.35f, 1.2f), new Color(0.28f, 0.78f, 1f));
            BuildDestroyedCar(root, "WreckNorth", new Vector3(-11f, 0f, 8f), 22f);
            BuildDestroyedCar(root, "WreckEast", new Vector3(12f, 0f, -6f), -35f);
            BuildBarricade(root, "BarrierA", new Vector3(6f, 0f, 15f), 16f);
            BuildBarricade(root, "BarrierB", new Vector3(-14f, 0f, -12f), -22f);
        }
        else if (theme == ArenaTheme.CyberRuinsNeon)
        {
            CreateArenaProp(root, "AccentEast", PrimitiveType.Cube,
                new Vector3(18f, 4.4f, 0f), new Vector3(1f, 0.35f, 10f), new Color(1f, 0.24f, 0.78f));
            CreateArenaProp(root, "AccentWest", PrimitiveType.Cube,
                new Vector3(-18f, 4.4f, 0f), new Vector3(1f, 0.35f, 10f), new Color(0.18f, 0.86f, 1f));
        }
        else
        {
            CreateArenaProp(root, "ContainerA", PrimitiveType.Cube,
                new Vector3(10f, 1.7f, 10f), new Vector3(7f, 3.4f, 3.2f), new Color(0.72f, 0.38f, 0.20f));
            CreateArenaProp(root, "ContainerB", PrimitiveType.Cube,
                new Vector3(-10f, 1.7f, -10f), new Vector3(7f, 3.4f, 3.2f), new Color(0.20f, 0.46f, 0.62f));
        }
    }

    private void BuildDestroyedCar(Transform root, string name, Vector3 position, float yRotation)
    {
        GameObject carRoot = new GameObject(name);
        carRoot.transform.SetParent(root, false);
        carRoot.transform.position = position;
        carRoot.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        CreatePrimitiveChild(carRoot.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 0.55f, 0f), new Vector3(2.4f, 0.7f, 1.2f), new Color(0.26f, 0.23f, 0.22f));
        CreatePrimitiveChild(carRoot.transform, "Cabin", PrimitiveType.Cube,
            new Vector3(-0.2f, 1.05f, 0f), new Vector3(1.15f, 0.55f, 1.05f), new Color(0.35f, 0.34f, 0.36f));
        CreatePrimitiveChild(carRoot.transform, "Hood", PrimitiveType.Cube,
            new Vector3(0.95f, 0.82f, 0f), new Vector3(0.8f, 0.22f, 1.0f), new Color(0.42f, 0.18f, 0.16f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_FL", PrimitiveType.Cylinder,
            new Vector3(0.8f, 0.3f, 0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_FR", PrimitiveType.Cylinder,
            new Vector3(0.8f, 0.3f, -0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_BL", PrimitiveType.Cylinder,
            new Vector3(-0.8f, 0.3f, 0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_BR", PrimitiveType.Cylinder,
            new Vector3(-0.8f, 0.3f, -0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
    }

    private void BuildBarricade(Transform root, string name, Vector3 position, float yRotation)
    {
        GameObject barricade = new GameObject(name);
        barricade.transform.SetParent(root, false);
        barricade.transform.position = position;
        barricade.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        CreatePrimitiveChild(barricade.transform, "Base", PrimitiveType.Cube,
            new Vector3(0f, 0.35f, 0f), new Vector3(2.4f, 0.5f, 0.5f), new Color(0.54f, 0.50f, 0.42f));
        CreatePrimitiveChild(barricade.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 0.95f, 0f), new Vector3(2.0f, 0.22f, 0.42f), new Color(0.80f, 0.64f, 0.18f));
    }

    private void SetupPlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player == null)
        {
            player = SpawnFirstPersonPlayer();
        }

        if (player == null)
        {
            return;
        }

        player.tag = "Player";
        player.transform.localScale = Vector3.one;
        player.transform.position = GetGroundPosition(new Vector3(0f, 0f, -18f)) + Vector3.up * 1.05f;
        player.transform.rotation = Quaternion.identity;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = player.AddComponent<CharacterController>();
        }

        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.35f;

        if (player.GetComponent<PlayerHealth>() == null)
        {
            player.AddComponent<PlayerHealth>();
        }

        foreach (Animator animator in player.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
        }
    }

    private void SetupCameras()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player == null)
        {
            return;
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        playerController.cam = playerCamera;
        playerController.firstPersonCam = playerCamera;
        playerController.thirdPersonCam = null;
    }

    private void SetupMinimap()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        GameObject minimapCameraObject = GameObject.Find("MinimapCamera");
        if (minimapCameraObject == null)
        {
            minimapCameraObject = new GameObject("MinimapCamera");
        }

        Camera minimapCamera = minimapCameraObject.GetComponent<Camera>();
        if (minimapCamera == null)
        {
            minimapCamera = minimapCameraObject.AddComponent<Camera>();
        }

        MinimapCameraFollow minimapFollow = minimapCameraObject.GetComponent<MinimapCameraFollow>();
        if (minimapFollow == null)
        {
            minimapFollow = minimapCameraObject.AddComponent<MinimapCameraFollow>();
        }

        minimapFollow.target = player.transform;
        minimapFollow.height = 32f;
        minimapFollow.orthographicSize = 18f;
        minimapFollow.offset = Vector3.zero;
        minimapFollow.lockToArenaCenter = true;
        minimapFollow.EnsureRenderTexture();
    }

    private void SetupManagers()
    {
        GameObject gameManager = GameObject.Find("GameManager");
        if (gameManager == null)
        {
            gameManager = new GameObject("GameManager");
        }

        if (gameManager.GetComponent<GameManager>() == null)
        {
            gameManager.AddComponent<GameManager>();
        }

        GameObject hud = GameObject.Find("HUDManager");
        if (hud == null)
        {
            hud = new GameObject("HUDManager");
        }

        if (hud.GetComponent<HUDManager>() == null)
        {
            hud.AddComponent<HUDManager>();
        }

        if (hud.GetComponent<PauseMenuController>() == null)
        {
            hud.AddComponent<PauseMenuController>();
        }
    }

    private void SetupEnemies()
    {
        GameObject existingRoot = GameObject.Find("EnemyRoot");
        if (existingRoot != null)
        {
            Destroy(existingRoot);
        }

        GameObject enemyRoot = new GameObject("EnemyRoot");
        int enemyCount = GameManager.Instance != null ? Mathf.Max(1, Mathf.Min(3, GameManager.Instance.GetEnemyCount() / 4)) : 2;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.enemiesRemaining = enemyCount;
        }

        for (int i = 0; i < enemyCount; i++)
        {
            float angle = (Mathf.PI * 2f / enemyCount) * i;
            Vector3 spawnPoint = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 14f;
            CreateEnemy(enemyRoot.transform, i, spawnPoint);
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateEnemyCount(enemyCount);
        }
    }

    private void CreateEnemy(Transform root, int index, Vector3 position)
    {
        GameObject enemy = new GameObject("Enemy_" + index);
        enemy.transform.SetParent(root, false);
        enemy.transform.position = GetGroundPosition(position) + Vector3.up * 0.95f;

        CapsuleCollider collider = enemy.AddComponent<CapsuleCollider>();
        collider.height = 1.9f;
        collider.radius = 0.38f;
        collider.center = new Vector3(0f, 0.95f, 0f);

        Rigidbody body = enemy.AddComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.mass = 55f;

        PrototypeEnemy prototypeEnemy = enemy.AddComponent<PrototypeEnemy>();
        prototypeEnemy.maxHealth = 90;

        BuildEnemyVisual(enemy.transform);
    }

    private void BuildEnemyVisual(Transform root)
    {
        GameObject knightPrefab = Resources.Load<GameObject>("ThirdPersonKnight/Paladin WProp J Nordstrom");
        if (knightPrefab != null)
        {
            GameObject visual = Instantiate(knightPrefab, root);
            visual.name = "EnemyVisual";
            visual.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);

            foreach (Animator childAnimator in visual.GetComponentsInChildren<Animator>(true))
            {
                childAnimator.enabled = false;
            }

            return;
        }

        CreatePrimitiveChild(root, "Torso", PrimitiveType.Capsule,
            new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 1.0f, 0.72f), new Color(0.46f, 0.18f, 0.18f));
        CreatePrimitiveChild(root, "Head", PrimitiveType.Sphere,
            new Vector3(0f, 1.82f, 0f), new Vector3(0.42f, 0.42f, 0.42f), new Color(0.86f, 0.76f, 0.66f));
        CreatePrimitiveChild(root, "LeftArm", PrimitiveType.Cylinder,
            new Vector3(-0.52f, 1.08f, 0f), new Vector3(0.16f, 0.52f, 0.16f), new Color(0.30f, 0.12f, 0.12f), new Vector3(0f, 0f, 26f));
        CreatePrimitiveChild(root, "RightArm", PrimitiveType.Cylinder,
            new Vector3(0.52f, 1.08f, 0f), new Vector3(0.16f, 0.52f, 0.16f), new Color(0.30f, 0.12f, 0.12f), new Vector3(0f, 0f, -26f));
        CreatePrimitiveChild(root, "LeftLeg", PrimitiveType.Cylinder,
            new Vector3(-0.18f, 0.3f, 0f), new Vector3(0.18f, 0.62f, 0.18f), new Color(0.08f, 0.08f, 0.10f));
        CreatePrimitiveChild(root, "RightLeg", PrimitiveType.Cylinder,
            new Vector3(0.18f, 0.3f, 0f), new Vector3(0.18f, 0.62f, 0.18f), new Color(0.08f, 0.08f, 0.10f));
    }

    private GameObject SpawnFirstPersonPlayer()
    {
        GameObject playerPrefab = Resources.Load<GameObject>("FirstPersonMelee/Player");
        if (playerPrefab == null)
        {
            return null;
        }

        GameObject player = Instantiate(playerPrefab);
        player.name = "Player";
        return player;
    }

    private void SetupLighting()
    {
        GameObject lightObject = GameObject.Find("Directional Light");
        if (lightObject != null)
        {
            lightObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
            Light lightComponent = lightObject.GetComponent<Light>();
            if (lightComponent != null)
            {
                lightComponent.intensity = 1.3f;
                lightComponent.color = new Color(0.92f, 0.95f, 1f);
            }
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.10f, 0.12f, 0.16f);
        RenderSettings.fogDensity = 0.0065f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.54f, 0.62f);
    }

    private GameObject CreateArenaProp(Transform root, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(root, false);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        SetLitColor(obj, color);
        return obj;
    }

    private GameObject CreatePrimitiveChild(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
    {
        return CreatePrimitiveChild(parent, name, type, localPosition, localScale, color, Vector3.zero);
    }

    private GameObject CreatePrimitiveChild(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color, Vector3 localRotationEuler)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.Euler(localRotationEuler);
        obj.transform.localScale = localScale;
        SetLitColor(obj, color);

        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        return obj;
    }

    private Vector3 GetGroundPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * 20f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return new Vector3(desiredPosition.x, 0f, desiredPosition.z);
    }

    private void SetLitColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        renderer.material = mat;
    }

    private Color GetGroundColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.18f, 0.20f, 0.24f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.12f, 0.16f, 0.22f);
        return new Color(0.20f, 0.20f, 0.18f);
    }

    private Color GetLaneColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.32f, 0.36f, 0.42f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.20f, 0.28f, 0.40f);
        return new Color(0.34f, 0.28f, 0.22f);
    }

    private Color GetWallColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.10f, 0.11f, 0.14f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.11f, 0.12f, 0.18f);
        return new Color(0.22f, 0.20f, 0.18f);
    }

    private Color GetPlatformColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.26f, 0.30f, 0.36f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.24f, 0.22f, 0.34f);
        return new Color(0.36f, 0.28f, 0.20f);
    }

    private Color GetCoverColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.28f, 0.24f, 0.24f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.22f, 0.24f, 0.32f);
        return new Color(0.40f, 0.30f, 0.22f);
    }
}

public class MinimapCameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 32f;
    public float orthographicSize = 18f;
    public Vector3 offset = Vector3.zero;
    public bool lockToArenaCenter = true;

    private Camera minimapCamera;
    private RenderTexture minimapTexture;

    public RenderTexture MinimapTexture => minimapTexture;

    private void Awake()
    {
        minimapCamera = GetComponent<Camera>();
        if (minimapCamera == null)
        {
            minimapCamera = gameObject.AddComponent<Camera>();
        }

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthographicSize;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.74f, 0.66f, 0.48f, 1f);
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = 100f;
        minimapCamera.cullingMask = ~0;

        EnsureRenderTexture();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                return;
            }
        }

        Vector3 targetPosition = lockToArenaCenter ? offset : target.position + offset;
        transform.position = new Vector3(targetPosition.x, height, targetPosition.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void OnDestroy()
    {
        if (minimapTexture != null)
        {
            minimapTexture.Release();
            Destroy(minimapTexture);
        }
    }

    public RenderTexture EnsureRenderTexture()
    {
        if (minimapTexture == null)
        {
            minimapTexture = new RenderTexture(256, 256, 16)
            {
                name = "RuntimeMinimapTexture"
            };
        }

        if (minimapCamera != null)
        {
            minimapCamera.targetTexture = minimapTexture;
        }

        return minimapTexture;
    }
}

public class PauseMenuController : MonoBehaviour
{
    private GameObject pauseCanvas;
    private GameObject mainPanel;
    private GameObject optionsPanel;
    private GameObject settingsPanel;
    private bool isPaused;
    private TMP_FontAsset prismFont;

    private readonly float[] volumeLevels = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    private readonly string[] volumeLabels = { "MUTE", "25%", "50%", "75%", "100%" };
    private readonly string[] graphicsLabels = { "LOW", "MEDIUM", "HIGH" };

    private void Update()
    {
        HUDManager hudManager = HUDManager.Instance;
        if (hudManager != null && hudManager.IsMatchFinished)
        {
            return;
        }

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            ShowPauseMenu();
        }
    }

    private void ShowPauseMenu()
    {
        EnsureEventSystem();
        BuildPauseMenu();
        ShowPanel(mainPanel);

        Time.timeScale = 0f;
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseCanvas != null)
        {
            Destroy(pauseCanvas);
            pauseCanvas = null;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        GameManager.Instance?.ReplayCurrentLevel();
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildPauseMenu()
    {
        if (pauseCanvas != null)
        {
            Destroy(pauseCanvas);
        }

        prismFont = ResolvePrismFont();

        pauseCanvas = new GameObject("PauseCanvas");
        Canvas canvas = pauseCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = pauseCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        pauseCanvas.AddComponent<GraphicRaycaster>();

        Image overlay = new GameObject("PauseOverlay").AddComponent<Image>();
        overlay.transform.SetParent(pauseCanvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.color = new Color(0.02f, 0.02f, 0.06f, 0.72f);

        mainPanel = CreatePausePanel("PausePanel_Main", new Vector2(760f, 700f));
        optionsPanel = CreatePausePanel("PausePanel_Options", new Vector2(860f, 720f));
        settingsPanel = CreatePausePanel("PausePanel_Settings", new Vector2(860f, 720f));

        BuildMainPanel(mainPanel.transform);
        BuildOptionsPanel(optionsPanel.transform);
        BuildSettingsPanel(settingsPanel.transform);
    }

    private GameObject CreatePausePanel(string name, Vector2 size)
    {
        Image panel = new GameObject(name).AddComponent<Image>();
        panel.transform.SetParent(pauseCanvas.transform, false);
        panel.color = new Color(0.16f, 0.20f, 0.30f, 0.36f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = new Vector2(0f, -10f);

        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.26f, 0.42f, 0.68f, 0.22f);
        panelOutline.effectDistance = new Vector2(2f, -2f);
        return panel.gameObject;
    }

    private void BuildMainPanel(Transform parent)
    {
        CreateLabel(parent, "PAUSED", 62f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 250f), new Vector2(420f, 80f), true);
        CreateLabel(parent, "TAKE A BREATH. JUMP BACK IN WHEN YOU'RE READY.", 22f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 198f), new Vector2(620f, 36f), false);

        CreateButton(parent, "RESUME", new Vector2(0f, 92f), ResumeGame);
        CreateButton(parent, "RESTART", new Vector2(0f, 2f), RestartGame);
        CreateButton(parent, "OPTIONS", new Vector2(0f, -88f), () => ShowPanel(optionsPanel));
        CreateButton(parent, "SETTINGS", new Vector2(0f, -178f), () => ShowPanel(settingsPanel));
        CreateButton(parent, "QUIT", new Vector2(0f, -268f), QuitGame);
    }

    private void BuildOptionsPanel(Transform parent)
    {
        CreateLabel(parent, "OPTIONS", 56f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 258f), new Vector2(500f, 76f), true);
        CreateLabel(parent, "ADJUST THE CURRENT MATCH WITHOUT LEAVING GAMEPLAY.", 20f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 208f), new Vector2(620f, 34f), false);

        CreateCycleRow(parent, "DIFFICULTY", new Vector2(0f, 92f), GetDifficultyLabel, CycleDifficulty);
        CreateCycleRow(parent, "CAMERA VIEW", new Vector2(0f, 4f), GetPerspectiveLabel, CyclePerspective);
        CreateCycleRow(parent, "MOVE STYLE", new Vector2(0f, -84f), GetMovementLabel, CycleMovement);

        CreateButton(parent, "RETURN", new Vector2(0f, -238f), () => ShowPanel(mainPanel));
    }

    private void BuildSettingsPanel(Transform parent)
    {
        CreateLabel(parent, "SETTINGS", 56f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 258f), new Vector2(500f, 76f), true);
        CreateLabel(parent, "TUNE DISPLAY AND AUDIO, THEN DROP RIGHT BACK INTO THE MATCH.", 20f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 208f), new Vector2(690f, 34f), false);

        CreateCycleRow(parent, "MASTER VOL", new Vector2(0f, 92f), GetMasterVolumeLabel, CycleMasterVolume);
        CreateCycleRow(parent, "GRAPHICS", new Vector2(0f, 4f), GetGraphicsLabel, CycleGraphics);
        CreateCycleRow(parent, "FULLSCREEN", new Vector2(0f, -84f), GetFullscreenLabel, ToggleFullscreen);

        CreateButton(parent, "RETURN", new Vector2(0f, -238f), () => ShowPanel(mainPanel));
    }

    private void CreateCycleRow(Transform parent, string label, Vector2 position, System.Func<string> getValue, UnityEngine.Events.UnityAction onCycle)
    {
        GameObject row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(700f, 64f);
        rowRect.anchoredPosition = position;

        CreateLabel(row.transform, label, 26f, Color.white, new Vector2(-190f, 0f), new Vector2(320f, 42f), false, TextAlignmentOptions.MidlineRight);
        CreateButton(row.transform, getValue(), new Vector2(148f, 0f), onCycle, new Vector2(320f, 60f), getValue);
    }

    private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        CreateButton(parent, text, position, action, new Vector2(300f, 72f), null);
    }

    private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action, Vector2 size, System.Func<string> dynamicText)
    {
        Image buttonImage = new GameObject("Btn_" + text).AddComponent<Image>();
        buttonImage.transform.SetParent(parent, false);
        buttonImage.color = Color.white;

        RectTransform rect = buttonImage.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.38f, 0.30f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);

        TextMeshProUGUI label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(buttonImage.transform, false);
        label.text = dynamicText != null ? dynamicText() : text;
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.10f, 0.10f, 0.14f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        if (prismFont != null)
        {
            label.font = prismFont;
        }

        RectTransform labelRect = label.GetComponent<RectTransform>();
        Stretch(labelRect);

        if (dynamicText != null)
        {
            PauseDynamicLabel dynamicLabel = buttonImage.gameObject.AddComponent<PauseDynamicLabel>();
            dynamicLabel.label = label;
            dynamicLabel.getText = dynamicText;
        }
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size, bool bold)
    {
        CreateLabel(parent, text, fontSize, color, position, size, bold, TextAlignmentOptions.Center);
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size, bool bold, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = new GameObject("Txt_" + text).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        if (prismFont != null)
        {
            label.font = prismFont;
        }

        if (bold)
        {
            label.fontStyle = FontStyles.Bold;
        }

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void ShowPanel(GameObject targetPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(targetPanel == mainPanel);
        if (optionsPanel != null) optionsPanel.SetActive(targetPanel == optionsPanel);
        if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);
    }

    private string GetDifficultyLabel()
    {
        return (GameManager.Instance != null ? GameManager.Instance.difficulty : PlayerPrefs.GetString("Difficulty", "Normal")).ToUpperInvariant();
    }

    private void CycleDifficulty()
    {
        string current = GameManager.Instance != null ? GameManager.Instance.difficulty : PlayerPrefs.GetString("Difficulty", "Normal");
        string next = current == "Easy" ? "Normal" : current == "Normal" ? "Hard" : "Easy";
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDifficulty(next);
        }
        else
        {
            PlayerPrefs.SetString("Difficulty", next);
            PlayerPrefs.Save();
        }
    }

    private string GetPerspectiveLabel()
    {
        GameManager.PerspectiveMode mode = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : (GameManager.PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", 0), 0, 1);
        return mode == GameManager.PerspectiveMode.ThirdPerson ? "THIRD PERSON" : "FIRST PERSON";
    }

    private void CyclePerspective()
    {
        GameManager.PerspectiveMode current = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : (GameManager.PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", 0), 0, 1);
        GameManager.PerspectiveMode next = current == GameManager.PerspectiveMode.FirstPerson
            ? GameManager.PerspectiveMode.ThirdPerson
            : GameManager.PerspectiveMode.FirstPerson;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPerspectiveMode(next);
        }
        else
        {
            PlayerPrefs.SetInt("PerspectiveMode", (int)next);
            PlayerPrefs.Save();
        }

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.RefreshGameplayPreferences();
        }
    }

    private string GetMovementLabel()
    {
        GameManager.MovementScheme scheme = GameManager.Instance != null
            ? GameManager.Instance.GetMovementScheme()
            : (GameManager.MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
        return scheme == GameManager.MovementScheme.ArrowKeys ? "ARROWS + MOUSE" : "WASD + MOUSE";
    }

    private void CycleMovement()
    {
        GameManager.MovementScheme current = GameManager.Instance != null
            ? GameManager.Instance.GetMovementScheme()
            : (GameManager.MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
        GameManager.MovementScheme next = current == GameManager.MovementScheme.Wasd
            ? GameManager.MovementScheme.ArrowKeys
            : GameManager.MovementScheme.Wasd;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMovementScheme(next);
        }
        else
        {
            PlayerPrefs.SetInt("MovementScheme", (int)next);
            PlayerPrefs.Save();
        }
    }

    private string GetMasterVolumeLabel()
    {
        float volume = PlayerPrefs.GetFloat("MasterVol", 0.8f);
        int index = 0;
        float smallestDifference = float.MaxValue;
        for (int i = 0; i < volumeLevels.Length; i++)
        {
            float difference = Mathf.Abs(volume - volumeLevels[i]);
            if (difference < smallestDifference)
            {
                smallestDifference = difference;
                index = i;
            }
        }

        return volumeLabels[index];
    }

    private void CycleMasterVolume()
    {
        float current = PlayerPrefs.GetFloat("MasterVol", 0.8f);
        int index = 0;
        for (int i = 0; i < volumeLevels.Length; i++)
        {
            if (Mathf.Abs(current - volumeLevels[i]) < 0.01f)
            {
                index = i;
                break;
            }
        }

        int nextIndex = (index + 1) % volumeLevels.Length;
        float next = volumeLevels[nextIndex];
        PlayerPrefs.SetFloat("MasterVol", next);
        PlayerPrefs.Save();
        AudioListener.volume = next;
    }

    private string GetGraphicsLabel()
    {
        int tier = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, graphicsLabels.Length - 1);
        return graphicsLabels[tier];
    }

    private void CycleGraphics()
    {
        int tier = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, graphicsLabels.Length - 1);
        int nextTier = (tier + 1) % graphicsLabels.Length;
        PlayerPrefs.SetInt("GraphicsTier", nextTier);
        PlayerPrefs.Save();

        int qualityLevel = nextTier == 0 ? 0 :
            nextTier == 1 ? Mathf.Max(0, (QualitySettings.names.Length - 1) / 2) :
            Mathf.Max(0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel);
    }

    private string GetFullscreenLabel()
    {
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        return fullscreen ? "ON" : "OFF";
    }

    private void ToggleFullscreen()
    {
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        fullscreen = !fullscreen;
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        Screen.fullScreen = fullscreen;
    }

    private TMP_FontAsset ResolvePrismFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            TMP_FontAsset font = fonts[i];
            if (font != null && font.name.Contains("Azonix"))
            {
                return font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        isPaused = false;
    }
}

public class PauseDynamicLabel : MonoBehaviour
{
    public TextMeshProUGUI label;
    public System.Func<string> getText;

    private void Update()
    {
        if (label != null && getText != null)
        {
            label.text = getText();
        }
    }
}

public class PrototypeEnemy : Actor
{
    public float moveSpeed = 2.2f;
    public float attackRange = 1.7f;
    public float attackDamage = 12f;
    public float attackCooldown = 1.1f;

    private Transform target;
    private float lastAttackTime;

    private void Start()
    {
        if (maxHealth <= 0)
        {
            maxHealth = 90;
        }

        currentHealth = maxHealth;
        if (GameManager.Instance != null)
        {
            moveSpeed = GameManager.Instance.GetEnemySpeed();
            attackDamage = GameManager.Instance.GetEnemyDamage();
        }
    }

    private void Update()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                return;
            }
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance > 0.2f)
        {
            Vector3 direction = toTarget.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);

            if (distance > attackRange)
            {
                transform.position += direction * (moveSpeed * Time.deltaTime);
            }
        }

        if (distance <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }
    }

    protected override void Death()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnemyKilled();
        }

        base.Death();
    }
}
