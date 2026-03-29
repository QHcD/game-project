using UnityEngine;

public class LevelBuilder : MonoBehaviour
{
    enum ArenaTheme
    {
        BlacksiteFacility,
        CyberRuinsNeon,
        ContainerPortYard
    }

    void Start()
    {
        SetupManagers();
        BuildGround();
        BuildWalls();
        BuildPlatforms();
        BuildThemeProps();
        BuildCovers();
        SetupPlayer();
        BuildSpawnPoints();
        BuildChest();
        SetupCameras();
        SetupLighting();
    }

    ArenaTheme GetTheme()
    {
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        if (level <= 7) return ArenaTheme.BlacksiteFacility;
        if (level <= 14) return ArenaTheme.CyberRuinsNeon;
        return ArenaTheme.ContainerPortYard;
    }

    void BuildGround()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
            ground = GameObject.Find("Plane");

        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }

        ArenaTheme theme = GetTheme();
        Color groundColor = GetGroundColor(theme);
        Color laneColor = GetLaneColor(theme);

        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(7.5f, 1f, 7.5f);
        SetLitColor(ground, groundColor);

        CreateProp("CenterPlatform", PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(20f, 1.6f, 18f), laneColor);
        CreateProp("NorthLane", PrimitiveType.Cube, new Vector3(0f, 0.08f, 22f), new Vector3(26f, 0.16f, 5f), laneColor);
        CreateProp("SouthLane", PrimitiveType.Cube, new Vector3(0f, 0.08f, -22f), new Vector3(26f, 0.16f, 5f), laneColor);
        CreateProp("EastLane", PrimitiveType.Cube, new Vector3(22f, 0.08f, 0f), new Vector3(5f, 0.16f, 26f), laneColor);
        CreateProp("WestLane", PrimitiveType.Cube, new Vector3(-22f, 0.08f, 0f), new Vector3(5f, 0.16f, 26f), laneColor);
        CreateProp("MidLane_North", PrimitiveType.Cube, new Vector3(0f, 0.04f, 10f), new Vector3(12f, 0.08f, 3f), laneColor);
        CreateProp("MidLane_South", PrimitiveType.Cube, new Vector3(0f, 0.04f, -10f), new Vector3(12f, 0.08f, 3f), laneColor);
    }

    void BuildWalls()
    {
        Color wallColor = GetWallColor(GetTheme());
        CreateProp("Wall_North", PrimitiveType.Cube, new Vector3(0f, 3f, 37f), new Vector3(74f, 6f, 1.5f), wallColor);
        CreateProp("Wall_South", PrimitiveType.Cube, new Vector3(0f, 3f, -37f), new Vector3(74f, 6f, 1.5f), wallColor);
        CreateProp("Wall_East", PrimitiveType.Cube, new Vector3(37f, 3f, 0f), new Vector3(1.5f, 6f, 74f), wallColor);
        CreateProp("Wall_West", PrimitiveType.Cube, new Vector3(-37f, 3f, 0f), new Vector3(1.5f, 6f, 74f), wallColor);
    }

    void BuildPlatforms()
    {
        Color platformColor = GetPlatformColor(GetTheme());

        CreateProp("Tower_NE", PrimitiveType.Cube, new Vector3(28f, 2.6f, 28f), new Vector3(5f, 5.2f, 5f), platformColor);
        CreateProp("Tower_NW", PrimitiveType.Cube, new Vector3(-28f, 2.6f, 28f), new Vector3(5f, 5.2f, 5f), platformColor);
        CreateProp("Tower_SE", PrimitiveType.Cube, new Vector3(28f, 2.6f, -28f), new Vector3(5f, 5.2f, 5f), platformColor);
        CreateProp("Tower_SW", PrimitiveType.Cube, new Vector3(-28f, 2.6f, -28f), new Vector3(5f, 5.2f, 5f), platformColor);

        CreateProp("Bridge_N", PrimitiveType.Cube, new Vector3(0f, 2.2f, 28f), new Vector3(16f, 0.45f, 3f), platformColor);
        CreateProp("Bridge_S", PrimitiveType.Cube, new Vector3(0f, 2.2f, -28f), new Vector3(16f, 0.45f, 3f), platformColor);
        CreateProp("Bridge_E", PrimitiveType.Cube, new Vector3(28f, 2.2f, 0f), new Vector3(3f, 0.45f, 16f), platformColor);
        CreateProp("Bridge_W", PrimitiveType.Cube, new Vector3(-28f, 2.2f, 0f), new Vector3(3f, 0.45f, 16f), platformColor);
    }

    void BuildThemeProps()
    {
        ArenaTheme theme = GetTheme();

        if (theme == ArenaTheme.BlacksiteFacility)
        {
            CreateProp("Blacksite_CoreWallA", PrimitiveType.Cube, new Vector3(-6f, 1.5f, 0f), new Vector3(4f, 3f, 10f), new Color(0.22f, 0.23f, 0.27f));
            CreateProp("Blacksite_CoreWallB", PrimitiveType.Cube, new Vector3(6f, 1.5f, 0f), new Vector3(4f, 3f, 10f), new Color(0.22f, 0.23f, 0.27f));
            CreateProp("Blacksite_PipeNorth", PrimitiveType.Cylinder, new Vector3(-10f, 2.3f, 14f), new Vector3(0.9f, 6f, 0.9f), new Color(0.34f, 0.16f, 0.16f));
            CreateProp("Blacksite_PipeSouth", PrimitiveType.Cylinder, new Vector3(10f, 2.3f, -14f), new Vector3(0.9f, 6f, 0.9f), new Color(0.34f, 0.16f, 0.16f));
            CreateProp("Blacksite_ServerA", PrimitiveType.Cube, new Vector3(-15f, 1.4f, 0f), new Vector3(3f, 2.8f, 6f), new Color(0.10f, 0.12f, 0.16f));
            CreateProp("Blacksite_ServerB", PrimitiveType.Cube, new Vector3(15f, 1.4f, 0f), new Vector3(3f, 2.8f, 6f), new Color(0.10f, 0.12f, 0.16f));
            CreateSceneLight("Blacksite_Light_N", new Vector3(0f, 5.5f, 15f), new Color(1f, 0.28f, 0.22f), 20f, 2.2f);
            CreateSceneLight("Blacksite_Light_S", new Vector3(0f, 5.5f, -15f), new Color(1f, 0.28f, 0.22f), 20f, 2.2f);
        }
        else if (theme == ArenaTheme.CyberRuinsNeon)
        {
            CreateProp("Cyber_CentralRuinA", PrimitiveType.Cube, new Vector3(-7f, 2.2f, -2f), new Vector3(5f, 4.4f, 7f), new Color(0.20f, 0.22f, 0.29f), Quaternion.Euler(0f, 18f, 0f));
            CreateProp("Cyber_CentralRuinB", PrimitiveType.Cube, new Vector3(8f, 1.8f, 3f), new Vector3(4f, 3.6f, 8f), new Color(0.17f, 0.19f, 0.27f), Quaternion.Euler(0f, -16f, 0f));
            CreateProp("Cyber_SignEast", PrimitiveType.Cube, new Vector3(16f, 2.8f, 12f), new Vector3(5f, 0.4f, 2f), new Color(0.28f, 0.88f, 1f));
            CreateProp("Cyber_SignWest", PrimitiveType.Cube, new Vector3(-16f, 2.8f, -12f), new Vector3(5f, 0.4f, 2f), new Color(1f, 0.28f, 0.86f));
            CreateProp("Cyber_RoofA", PrimitiveType.Cube, new Vector3(-18f, 3.6f, 18f), new Vector3(6f, 0.4f, 8f), new Color(0.20f, 0.22f, 0.29f), Quaternion.Euler(0f, 12f, 0f));
            CreateProp("Cyber_RoofB", PrimitiveType.Cube, new Vector3(18f, 3.4f, -18f), new Vector3(6f, 0.4f, 8f), new Color(0.20f, 0.22f, 0.29f), Quaternion.Euler(0f, -12f, 0f));
            CreateSceneLight("Cyber_Light_Cyan", new Vector3(-12f, 5f, 6f), new Color(0.18f, 0.88f, 1f), 24f, 3.1f);
            CreateSceneLight("Cyber_Light_Magenta", new Vector3(12f, 5f, -6f), new Color(1f, 0.26f, 0.84f), 24f, 3.1f);
        }
        else
        {
            CreateProp("Port_ContainerA", PrimitiveType.Cube, new Vector3(-11f, 1.7f, 3f), new Vector3(8f, 3.2f, 3.2f), new Color(0.66f, 0.38f, 0.20f), Quaternion.Euler(0f, 90f, 0f));
            CreateProp("Port_ContainerB", PrimitiveType.Cube, new Vector3(11f, 1.7f, -3f), new Vector3(8f, 3.2f, 3.2f), new Color(0.18f, 0.42f, 0.58f), Quaternion.Euler(0f, 90f, 0f));
            CreateProp("Port_ContainerC", PrimitiveType.Cube, new Vector3(-18f, 1.7f, -16f), new Vector3(8f, 3.2f, 3.2f), new Color(0.52f, 0.32f, 0.18f));
            CreateProp("Port_ContainerD", PrimitiveType.Cube, new Vector3(18f, 1.7f, 16f), new Vector3(8f, 3.2f, 3.2f), new Color(0.20f, 0.46f, 0.62f));
            CreateProp("Port_CraneBaseA", PrimitiveType.Cube, new Vector3(-24f, 4f, 0f), new Vector3(2f, 8f, 2f), new Color(0.30f, 0.30f, 0.24f));
            CreateProp("Port_CraneArmA", PrimitiveType.Cube, new Vector3(-18f, 7.2f, 0f), new Vector3(12f, 0.5f, 1.2f), new Color(0.30f, 0.30f, 0.24f));
            CreateProp("Port_CraneBaseB", PrimitiveType.Cube, new Vector3(24f, 4f, 0f), new Vector3(2f, 8f, 2f), new Color(0.30f, 0.30f, 0.24f));
            CreateProp("Port_CraneArmB", PrimitiveType.Cube, new Vector3(18f, 7.2f, 0f), new Vector3(12f, 0.5f, 1.2f), new Color(0.30f, 0.30f, 0.24f));
            CreateSceneLight("Port_FogLight_A", new Vector3(-10f, 5f, 12f), new Color(0.96f, 0.84f, 0.58f), 22f, 2.4f);
            CreateSceneLight("Port_FogLight_B", new Vector3(10f, 5f, -12f), new Color(0.96f, 0.84f, 0.58f), 22f, 2.4f);
        }
    }

    void BuildCovers()
    {
        Color coverColor = GetCoverColor(GetTheme());
        Vector3[] positions = {
            new Vector3(0f, 1.2f, 15f), new Vector3(0f, 1.2f, -15f),
            new Vector3(15f, 1.2f, 0f), new Vector3(-15f, 1.2f, 0f),
            new Vector3(18f, 1.2f, 14f), new Vector3(-18f, 1.2f, 14f),
            new Vector3(18f, 1.2f, -14f), new Vector3(-18f, 1.2f, -14f),
            new Vector3(10f, 1.2f, 22f), new Vector3(-10f, 1.2f, 22f),
            new Vector3(10f, 1.2f, -22f), new Vector3(-10f, 1.2f, -22f)
        };

        Vector3[] scales = {
            new Vector3(8f, 2.4f, 2f), new Vector3(8f, 2.4f, 2f),
            new Vector3(2f, 2.4f, 8f), new Vector3(2f, 2.4f, 8f),
            new Vector3(4.5f, 2.4f, 4.5f), new Vector3(4.5f, 2.4f, 4.5f),
            new Vector3(4.5f, 2.4f, 4.5f), new Vector3(4.5f, 2.4f, 4.5f),
            new Vector3(6f, 2.4f, 2f), new Vector3(6f, 2.4f, 2f),
            new Vector3(6f, 2.4f, 2f), new Vector3(6f, 2.4f, 2f)
        };

        for (int i = 0; i < positions.Length; i++)
            CreateProp("Cover_" + (i + 1), PrimitiveType.Cube, positions[i], scales[i], coverColor);
    }

    void BuildSpawnPoints()
    {
        Vector3[] points = {
            new Vector3(0f, 0f, 28f),
            new Vector3(12f, 0f, 24f),
            new Vector3(24f, 0f, 12f),
            new Vector3(28f, 0f, 0f),
            new Vector3(24f, 0f, -12f),
            new Vector3(12f, 0f, -24f),
            new Vector3(0f, 0f, -28f),
            new Vector3(-12f, 0f, -24f),
            new Vector3(-24f, 0f, -12f),
            new Vector3(-28f, 0f, 0f),
            new Vector3(-24f, 0f, 12f),
            new Vector3(-12f, 0f, 24f)
        };

        EnemySpawner spawner = GameObject.Find("EnemySpawner")?.GetComponent<EnemySpawner>();
        Transform[] spawnTransforms = new Transform[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            string pointName = "SpawnPoint_" + (i + 1);
            GameObject point = GameObject.Find(pointName);
            if (point == null)
                point = new GameObject(pointName);

            point.transform.position = GetGroundPosition(points[i]) + Vector3.up * 0.05f;
            spawnTransforms[i] = point.transform;
        }

        if (spawner != null)
            spawner.spawnPoints = spawnTransforms;
    }

    void BuildChest()
    {
        GameObject chest = GameObject.Find("Chest");
        if (chest == null)
        {
            chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "Chest";
        }

        chest.transform.position = new Vector3(0f, 1.9f, 0f);
        chest.transform.localScale = new Vector3(1.8f, 1.4f, 1.4f);
        SetLitColor(chest, new Color(1f, 0.78f, 0.1f));

        if (chest.GetComponent<WeaponChest>() == null)
            chest.AddComponent<WeaponChest>();
    }

    void SetupPlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        player.tag = "Player";
        player.transform.rotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
            controller = player.AddComponent<CharacterController>();

        bool controllerEnabled = controller.enabled;
        controller.enabled = false;
        player.transform.position = GetGroundPosition(new Vector3(0f, 0f, -24f)) + Vector3.up * 0.05f;
        controller.enabled = controllerEnabled;

        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();
        if (player.GetComponent<PlayerHealth>() == null)
            player.AddComponent<PlayerHealth>();

        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.35f;

        foreach (Animator animator in player.GetComponentsInChildren<Animator>(true))
            animator.applyRootMotion = false;

        Transform playerBase = FindChildRecursive(player.transform, "Player_Base");
        if (playerBase != null)
        {
            playerBase.localPosition = Vector3.zero;
            playerBase.localRotation = Quaternion.identity;
            playerBase.localScale = Vector3.one;
        }

        if (player.transform.Find("WeaponHoldPoint") == null)
        {
            GameObject hp = new GameObject("WeaponHoldPoint");
            hp.transform.SetParent(player.transform);
            hp.transform.localPosition = new Vector3(0.4f, 1.1f, 0.6f);
        }
    }

    void SetupCameras()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null)
            return;

        GameObject tpCam = GameObject.Find("ThirdPersonCam");
        if (tpCam != null)
        {
            tpCam.transform.SetParent(null);
            tpCam.transform.localScale = Vector3.one;

            CameraController cc = tpCam.GetComponent<CameraController>();
            if (cc == null)
                cc = tpCam.AddComponent<CameraController>();

            cc.target = player.transform;
            cc.offset = new Vector3(0f, 7f, -10f);
            cc.smoothSpeed = 8f;
            pc.thirdPersonCam = tpCam.GetComponent<Camera>();
        }

        GameObject fpCam = GameObject.Find("FirstPersonCam");
        if (fpCam != null)
        {
            fpCam.transform.SetParent(player.transform);
            fpCam.transform.localScale = Vector3.one;
            fpCam.transform.localPosition = new Vector3(0f, 1.6f, 0.15f);
            fpCam.transform.localRotation = Quaternion.identity;
            fpCam.SetActive(false);
            pc.firstPersonCam = fpCam.GetComponent<Camera>();
        }
    }

    void SetupManagers()
    {
        GameObject gm = GameObject.Find("GameManager");
        if (gm == null)
            gm = new GameObject("GameManager");
        if (gm.GetComponent<GameManager>() == null)
            gm.AddComponent<GameManager>();

        GameObject es = GameObject.Find("EnemySpawner");
        if (es == null)
            es = new GameObject("EnemySpawner");
        EnemySpawner spawner = es.GetComponent<EnemySpawner>();
        if (spawner == null)
            spawner = es.AddComponent<EnemySpawner>();
        spawner.spawnDelay = 1.25f;

        GameObject hud = GameObject.Find("HUDManager");
        if (hud == null)
            hud = new GameObject("HUDManager");
        if (hud.GetComponent<HUDManager>() == null)
            hud.AddComponent<HUDManager>();
    }

    void SetupLighting()
    {
        GameObject lightObj = GameObject.Find("Directional Light");
        if (lightObj != null)
        {
            lightObj.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            Light lightComp = lightObj.GetComponent<Light>();
            if (lightComp != null)
            {
                lightComp.intensity = 1.35f;
                lightComp.color = new Color(0.95f, 0.96f, 1f);
            }
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        if (GetTheme() == ArenaTheme.BlacksiteFacility)
        {
            RenderSettings.fogColor = new Color(0.10f, 0.08f, 0.09f);
            RenderSettings.fogDensity = 0.012f;
        }
        else if (GetTheme() == ArenaTheme.CyberRuinsNeon)
        {
            RenderSettings.fogColor = new Color(0.06f, 0.08f, 0.12f);
            RenderSettings.fogDensity = 0.010f;
        }
        else
        {
            RenderSettings.fogColor = new Color(0.16f, 0.18f, 0.18f);
            RenderSettings.fogDensity = 0.016f;
        }
    }

    void CreateProp(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        CreateProp(name, type, position, scale, color, Quaternion.identity);
    }

    void CreateProp(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Quaternion rotation)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = GameObject.CreatePrimitive(type);
            obj.name = name;
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.transform.localScale = scale;
        SetLitColor(obj, color);
    }

    void CreateSceneLight(string name, Vector3 position, Color color, float range, float intensity)
    {
        GameObject lightObj = GameObject.Find(name);
        if (lightObj == null)
        {
            lightObj = new GameObject(name);
            lightObj.AddComponent<Light>().type = LightType.Point;
        }

        lightObj.transform.position = position;
        Light pointLight = lightObj.GetComponent<Light>();
        if (pointLight == null)
            pointLight = lightObj.AddComponent<Light>();

        pointLight.type = LightType.Point;
        pointLight.color = color;
        pointLight.range = range;
        pointLight.intensity = intensity;
    }

    Vector3 GetGroundPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * 20f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return new Vector3(desiredPosition.x, 0f, desiredPosition.z);
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }

        return null;
    }

    void SetLitColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        renderer.material = mat;
    }

    Color GetGroundColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.08f, 0.09f, 0.11f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.09f, 0.11f, 0.16f);
        return new Color(0.14f, 0.15f, 0.16f);
    }

    Color GetLaneColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.17f, 0.17f, 0.20f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.14f, 0.16f, 0.22f);
        return new Color(0.22f, 0.22f, 0.24f);
    }

    Color GetWallColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.12f, 0.12f, 0.15f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.12f, 0.14f, 0.20f);
        return new Color(0.18f, 0.19f, 0.20f);
    }

    Color GetPlatformColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.22f, 0.23f, 0.27f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.18f, 0.19f, 0.27f);
        return new Color(0.28f, 0.27f, 0.24f);
    }

    Color GetCoverColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.28f, 0.22f, 0.22f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.24f, 0.24f, 0.30f);
        return new Color(0.42f, 0.29f, 0.20f);
    }
}
