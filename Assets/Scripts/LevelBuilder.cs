using UnityEngine;

public class LevelBuilder : MonoBehaviour
{
    void Start()
    {
        SetupManagers();
        SetupPlayer();
        BuildGround();
        BuildWalls();
        BuildCovers();
        BuildSpawnPoints();
        BuildChest();
        SetupCameras();
        SetupLighting();
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

        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(7.5f, 1f, 7.5f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.12f, 0.13f, 0.16f);
        ground.GetComponent<Renderer>().material = mat;

        CreateProp("CenterPlatform", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(12f, 0.7f, 12f), new Color(0.18f, 0.2f, 0.24f));
        CreateProp("LaneNorth", PrimitiveType.Cube, new Vector3(0f, 0.05f, 18f), new Vector3(20f, 0.1f, 4f), new Color(0.22f, 0.23f, 0.27f));
        CreateProp("LaneSouth", PrimitiveType.Cube, new Vector3(0f, 0.05f, -18f), new Vector3(20f, 0.1f, 4f), new Color(0.22f, 0.23f, 0.27f));
        CreateProp("LaneEast", PrimitiveType.Cube, new Vector3(18f, 0.05f, 0f), new Vector3(4f, 0.1f, 20f), new Color(0.22f, 0.23f, 0.27f));
        CreateProp("LaneWest", PrimitiveType.Cube, new Vector3(-18f, 0.05f, 0f), new Vector3(4f, 0.1f, 20f), new Color(0.22f, 0.23f, 0.27f));
    }

    void BuildWalls()
    {
        CreateProp("Wall_North", PrimitiveType.Cube, new Vector3(0, 3, 37), new Vector3(74, 6, 1.5f), new Color(0.16f, 0.16f, 0.2f));
        CreateProp("Wall_South", PrimitiveType.Cube, new Vector3(0, 3, -37), new Vector3(74, 6, 1.5f), new Color(0.16f, 0.16f, 0.2f));
        CreateProp("Wall_East", PrimitiveType.Cube, new Vector3(37, 3, 0), new Vector3(1.5f, 6, 74), new Color(0.16f, 0.16f, 0.2f));
        CreateProp("Wall_West", PrimitiveType.Cube, new Vector3(-37, 3, 0), new Vector3(1.5f, 6, 74), new Color(0.16f, 0.16f, 0.2f));

        CreateProp("GateNorth", PrimitiveType.Cube, new Vector3(0f, 4.8f, 35f), new Vector3(16f, 3f, 0.6f), new Color(0.28f, 0.32f, 0.38f));
        CreateProp("GateSouth", PrimitiveType.Cube, new Vector3(0f, 4.8f, -35f), new Vector3(16f, 3f, 0.6f), new Color(0.28f, 0.32f, 0.38f));
    }

    void BuildCovers()
    {
        Vector3[] positions = {
            new Vector3(10, 1.2f, 10), new Vector3(-10, 1.2f, 10),
            new Vector3(10, 1.2f, -10), new Vector3(-10, 1.2f, -10),
            new Vector3(0, 1.2f, 22), new Vector3(0, 1.2f, -22),
            new Vector3(22, 1.2f, 0), new Vector3(-22, 1.2f, 0),
            new Vector3(18, 1.2f, 18), new Vector3(-18, 1.2f, 18),
            new Vector3(18, 1.2f, -18), new Vector3(-18, 1.2f, -18)
        };

        Vector3[] scales = {
            new Vector3(5, 2.4f, 1.3f), new Vector3(5, 2.4f, 1.3f),
            new Vector3(5, 2.4f, 1.3f), new Vector3(5, 2.4f, 1.3f),
            new Vector3(7, 2.4f, 1.3f), new Vector3(7, 2.4f, 1.3f),
            new Vector3(1.3f, 2.4f, 7), new Vector3(1.3f, 2.4f, 7),
            new Vector3(3, 2.4f, 3), new Vector3(3, 2.4f, 3),
            new Vector3(3, 2.4f, 3), new Vector3(3, 2.4f, 3)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            CreateProp("Cover_" + (i + 1), PrimitiveType.Cube, positions[i], scales[i], new Color(0.28f, 0.24f, 0.2f));
        }
    }

    void BuildSpawnPoints()
    {
        Vector3[] points = {
            new Vector3(28f, 0f, 20f), new Vector3(28f, 0f, 0f), new Vector3(28f, 0f, -20f),
            new Vector3(-28f, 0f, 20f), new Vector3(-28f, 0f, 0f), new Vector3(-28f, 0f, -20f),
            new Vector3(20f, 0f, 28f), new Vector3(0f, 0f, 28f), new Vector3(-20f, 0f, 28f),
            new Vector3(20f, 0f, -28f), new Vector3(0f, 0f, -28f), new Vector3(-20f, 0f, -28f)
        };

        EnemySpawner spawner = GameObject.Find("EnemySpawner")?.GetComponent<EnemySpawner>();
        Transform[] spawnTransforms = new Transform[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            string pointName = "SpawnPoint_" + (i + 1);
            GameObject point = GameObject.Find(pointName);
            if (point == null)
                point = new GameObject(pointName);

            point.transform.position = points[i];
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

        chest.transform.position = new Vector3(0f, 0.8f, 8f);
        chest.transform.localScale = new Vector3(1.6f, 1.2f, 1.2f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.78f, 0.1f);
        chest.GetComponent<Renderer>().material = mat;

        if (chest.GetComponent<WeaponChest>() == null)
            chest.AddComponent<WeaponChest>();
    }

    void SetupPlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1f, -8f);
        player.transform.rotation = Quaternion.identity;

        if (player.GetComponent<CharacterController>() == null)
            player.AddComponent<CharacterController>();

        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();

        if (player.GetComponent<PlayerHealth>() == null)
            player.AddComponent<PlayerHealth>();

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
        if (player == null) return;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null) return;

        GameObject tpCam = GameObject.Find("ThirdPersonCam");
        if (tpCam != null)
        {
            CameraController cc = tpCam.GetComponent<CameraController>();
            if (cc == null) cc = tpCam.AddComponent<CameraController>();
            cc.target = player.transform;
            cc.offset = new Vector3(0f, 5f, -7f);
            cc.smoothSpeed = 8f;
            pc.thirdPersonCam = tpCam.GetComponent<Camera>();
        }

        GameObject fpCam = GameObject.Find("FirstPersonCam");
        if (fpCam != null)
        {
            fpCam.transform.SetParent(player.transform);
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
        if (es.GetComponent<EnemySpawner>() == null)
            es.AddComponent<EnemySpawner>();

        GameObject hud = GameObject.Find("HUDManager");
        if (hud == null)
            hud = new GameObject("HUDManager");
        if (hud.GetComponent<HUDManager>() == null)
            hud.AddComponent<HUDManager>();
    }

    void SetupLighting()
    {
        GameObject lightObj = GameObject.Find("Directional Light");
        if (lightObj == null) return;

        lightObj.transform.rotation = Quaternion.Euler(45f, -40f, 0f);
        Light lightComp = lightObj.GetComponent<Light>();
        if (lightComp != null)
        {
            lightComp.intensity = 1.2f;
            lightComp.color = new Color(0.95f, 0.96f, 1f);
        }
    }

    void CreateProp(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = GameObject.CreatePrimitive(type);
            obj.name = name;
        }

        obj.transform.position = position;
        obj.transform.localScale = scale;

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.material = mat;
        }
    }
}
