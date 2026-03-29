using UnityEngine;

public class LevelBuilder : MonoBehaviour
{
    void Start()
    {
        BuildGround();
        BuildWalls();
        BuildCovers();
        BuildSpawnPoints();
        BuildChest();
        SetupPlayer();
        SetupCameras();
        SetupManagers();
    }

    void BuildGround()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10, 1, 10);
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0.3f, 0.3f);
        ground.GetComponent<Renderer>().material = mat;
    }

    void BuildWalls()
    {
        CreateWall("Wall_North", new Vector3(0, 2, 50), new Vector3(100, 4, 1));
        CreateWall("Wall_South", new Vector3(0, 2, -50), new Vector3(100, 4, 1));
        CreateWall("Wall_East", new Vector3(50, 2, 0), new Vector3(1, 4, 100));
        CreateWall("Wall_West", new Vector3(-50, 2, 0), new Vector3(1, 4, 100));
    }

    void CreateWall(string name, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.Find(name);
        if (wall == null)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
        }
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.2f, 0.2f, 0.25f);
        wall.GetComponent<Renderer>().material = mat;
    }

    void BuildCovers()
    {
        Vector3[] positions = {
            new Vector3(10, 1, 10),  new Vector3(-10, 1, 10),
            new Vector3(15, 1, -5),  new Vector3(-15, 1, -5),
            new Vector3(0,  1, 20),  new Vector3(5,   1, -15),
            new Vector3(-5, 1, 15),  new Vector3(20,  1, 0),
        };
        Vector3[] scales = {
            new Vector3(4,2,1), new Vector3(1,2,4),
            new Vector3(3,2,1), new Vector3(4,2,2),
            new Vector3(2,2,3), new Vector3(3,1,1),
            new Vector3(1,3,2), new Vector3(5,2,1),
        };
        for (int i = 0; i < positions.Length; i++)
        {
            string coverName = "Cover_" + (i + 1);
            GameObject cover = GameObject.Find(coverName);
            if (cover == null)
            {
                cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = coverName;
            }
            cover.transform.position = positions[i];
            cover.transform.localScale = scales[i];
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.4f, 0.35f, 0.3f);
            cover.GetComponent<Renderer>().material = mat;
        }
    }

    void BuildSpawnPoints()
    {
        Vector3[] points = {
            new Vector3(20, 0, 20),
            new Vector3(-20, 0, 20),
            new Vector3(0, 0, -20),
            new Vector3(20, 0, -20),
            new Vector3(-20, 0, -20),
        };

        EnemySpawner spawner = GameObject.Find("EnemySpawner")?.GetComponent<EnemySpawner>();
        Transform[] spawnTransforms = new Transform[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            string pointName = "SpawnPoint_" + (i + 1);
            GameObject sp = GameObject.Find(pointName);
            if (sp == null)
                sp = new GameObject(pointName);
            sp.transform.position = points[i];
            spawnTransforms[i] = sp.transform;
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
        chest.transform.position = new Vector3(5, 0.5f, 5);
        chest.transform.localScale = Vector3.one;
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.8f, 0f);
        chest.GetComponent<Renderer>().material = mat;
        if (chest.GetComponent<WeaponChest>() == null)
            chest.AddComponent<WeaponChest>();
    }

    void SetupPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        if (player.GetComponent<CharacterController>() == null)
            player.AddComponent<CharacterController>();

        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();

        player.transform.position = new Vector3(0, 1, 0);

        if (player.transform.Find("WeaponHoldPoint") == null)
        {
            GameObject hp = new GameObject("WeaponHoldPoint");
            hp.transform.SetParent(player.transform);
            hp.transform.localPosition = new Vector3(0.5f, 1.2f, 0.8f);
        }
    }

    void SetupCameras()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null) return;

        GameObject tpCam = GameObject.Find("ThirdPersonCam");
        if (tpCam != null)
        {
            CameraController cc = tpCam.GetComponent<CameraController>();
            if (cc == null) cc = tpCam.AddComponent<CameraController>();
            cc.target = player.transform;
            pc.thirdPersonCam = tpCam.GetComponent<Camera>();
        }

        GameObject fpCam = GameObject.Find("FirstPersonCam");
        if (fpCam != null)
        {
            fpCam.transform.localPosition = new Vector3(0, 1.7f, 0.2f);
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
}
