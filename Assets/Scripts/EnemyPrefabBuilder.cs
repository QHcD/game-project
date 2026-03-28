using UnityEngine;

public class EnemyPrefabBuilder : MonoBehaviour
{
    void Start()
    {
        CreateEnemyPrefabs();
        CreateHUD();
    }

    void CreateEnemyPrefabs()
    {
        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner == null) return;

        spawner.gruntPrefab = BuildEnemy("Grunt", Color.green, 60f);
        spawner.soldierPrefab = BuildEnemy("Soldier", Color.blue, 100f);
        spawner.elitePrefab = BuildEnemy("Elite", Color.red, 160f);
    }

    GameObject BuildEnemy(string enemyName, Color color, float health)
    {
        GameObject root = new GameObject(enemyName);

        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0, 1, 0);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        // Remove collider from child — root has its own
        Destroy(body.GetComponent<CapsuleCollider>());
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        body.GetComponent<Renderer>().material = mat;

        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform);
        head.transform.localPosition = new Vector3(0, 2.2f, 0);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Destroy(head.GetComponent<SphereCollider>());
        head.GetComponent<Renderer>().material = mat;

        // Root collider
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.4f;
        col.center = new Vector3(0, 1, 0);

        // Rigidbody — no rotation freeze on Y so enemy can turn
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Enemy script
        EnemyController ec = root.AddComponent<EnemyController>();
        ec.maxHealth = health;
        switch (enemyName)
        {
            case "Grunt": ec.enemyType = EnemyController.EnemyType.Grunt; break;
            case "Soldier": ec.enemyType = EnemyController.EnemyType.Soldier; break;
            case "Elite": ec.enemyType = EnemyController.EnemyType.Elite; break;
        }

        root.tag = "Enemy";
        root.SetActive(false);
        return root;
    }

    void CreateHUD()
    {
        // Don't create if already exists
        if (GameObject.Find("HUDCanvas") != null) return;

        GameObject canvasObj = new GameObject("HUDCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Health bar BG
        GameObject healthBg = MakeImage(canvasObj.transform, "HealthBg",
            new Color(0.1f, 0.1f, 0.1f, 0.8f),
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(20f, 20f), new Vector2(320f, 40f));

        // Health fill
        GameObject healthFill = MakeImage(healthBg.transform, "HealthFill",
            new Color(0.2f, 0.8f, 0.2f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero);

        var healthSlider = healthBg.AddComponent<UnityEngine.UI.Slider>();
        healthSlider.fillRect = healthFill.GetComponent<RectTransform>();
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.value = 1f;
        healthSlider.interactable = false;

        var hpText = MakeText(canvasObj.transform, "HPText", "HP: 100",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 65f), new Vector2(200f, 28f));
        var scoreText = MakeText(canvasObj.transform, "ScoreText", "Score: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -45f), new Vector2(250f, 30f));
        var levelText = MakeText(canvasObj.transform, "LevelText", "Level 1",
            new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(-100f, -45f), new Vector2(200f, 30f));
        var weaponText = MakeText(canvasObj.transform, "WeaponText", "Combat Knife",
            new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(-150f, -80f), new Vector2(300f, 26f));
        var enemyText = MakeText(canvasObj.transform, "EnemyText", "Enemies: 0",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-220f, -45f), new Vector2(200f, 30f));
        var timerText = MakeText(canvasObj.transform, "TimerText", "Time: 120",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-220f, -80f), new Vector2(200f, 26f));

        // Wire HUDManager
        HUDManager hud = FindObjectOfType<HUDManager>();
        if (hud != null)
        {
            hud.healthBar = healthSlider;
            hud.healthText = hpText;
            hud.scoreText = scoreText;
            hud.levelText = levelText;
            hud.weaponText = weaponText;
            hud.enemyCountText = enemyText;
            hud.timerText = timerText;
        }
    }

    GameObject MakeImage(Transform parent, string name, Color color,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return obj;
    }

    TMPro.TextMeshProUGUI MakeText(Transform parent, string name, string text,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 22; tmp.color = Color.white;
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return tmp;
    }
}
