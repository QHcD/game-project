using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    public TextMeshProUGUI healthText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI weaponText;
    public TextMeshProUGUI enemyCountText;
    public TextMeshProUGUI timerText;
    public Slider healthBar;

    private float elapsed = 0f;
    private float timeLimit = 120f;
    private PlayerHealth playerHealth;
    private PlayerController playerController;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (GameManager.Instance == null) return;

        EnsureRuntimeHud();
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        playerController = FindFirstObjectByType<PlayerController>();

        int level = GameManager.Instance.currentLevel;
        if (levelText != null)
            levelText.text = "Level " + level;

        if (weaponText != null)
            weaponText.text = playerController != null ? playerController.equippedWeaponName : GameManager.Instance.GetWeaponNameForLevel(level);

        UpdateScore(GameManager.Instance.score);
        UpdateEnemyCount(GameManager.Instance.enemiesRemaining);
        if (playerHealth != null)
            UpdateHealth(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float remaining = Mathf.Max(0, timeLimit - elapsed);
        if (timerText != null)
            timerText.text = "Time: " + Mathf.CeilToInt(remaining);

        if (GameManager.Instance != null)
            GameManager.Instance.levelTime = elapsed;

        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (playerHealth != null)
            UpdateHealth(playerHealth.currentHealth, playerHealth.maxHealth);
        if (playerController != null && weaponText != null)
            weaponText.text = playerController.equippedWeaponName;
    }

    public void UpdateHealth(float current, float max)
    {
        if (healthText != null) healthText.text = "HP: " + Mathf.CeilToInt(current);
        if (healthBar != null) healthBar.value = Mathf.Clamp01(current / Mathf.Max(1f, max));
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
    }

    public void UpdateEnemyCount(int count)
    {
        if (enemyCountText != null) enemyCountText.text = "Enemies: " + count;
    }

    private void EnsureRuntimeHud()
    {
        if (healthText != null && scoreText != null && levelText != null &&
            weaponText != null && enemyCountText != null && timerText != null && healthBar != null)
            return;

        GameObject canvasObj = GameObject.Find("HUDCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("HUDCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        GameObject healthBg = CreateImage(canvasObj.transform, "HealthBg", new Color(0.1f, 0.1f, 0.1f, 0.85f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(25f, 25f), new Vector2(320f, 30f));
        GameObject healthFill = CreateImage(healthBg.transform, "HealthFill", new Color(0.2f, 0.85f, 0.35f, 1f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        if (healthBar == null)
        {
            healthBar = healthBg.GetComponent<Slider>();
            if (healthBar == null)
                healthBar = healthBg.AddComponent<Slider>();
            healthBar.fillRect = healthFill.GetComponent<RectTransform>();
            healthBar.minValue = 0f;
            healthBar.maxValue = 1f;
            healthBar.value = 1f;
            healthBar.interactable = false;
        }

        healthText ??= CreateText(canvasObj.transform, "HPText", new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(25f, 62f), new Vector2(220f, 28f));
        scoreText ??= CreateText(canvasObj.transform, "ScoreText", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(25f, -40f), new Vector2(250f, 30f));
        levelText ??= CreateText(canvasObj.transform, "LevelText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-100f, -40f), new Vector2(200f, 30f));
        weaponText ??= CreateText(canvasObj.transform, "WeaponText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-170f, -76f), new Vector2(340f, 26f));
        enemyCountText ??= CreateText(canvasObj.transform, "EnemyText", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-220f, -40f), new Vector2(200f, 30f));
        timerText ??= CreateText(canvasObj.transform, "TimerText", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-220f, -76f), new Vector2(200f, 26f));
    }

    private GameObject CreateImage(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject existing = parent.Find(name)?.gameObject;
        if (existing != null)
            return existing;

        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return obj;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size)
    {
        GameObject existing = parent.Find(name)?.gameObject;
        if (existing == null)
        {
            existing = new GameObject(name);
            existing.transform.SetParent(parent, false);
        }

        TextMeshProUGUI tmp = existing.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = existing.AddComponent<TextMeshProUGUI>();

        tmp.fontSize = 22;
        tmp.color = Color.white;

        RectTransform rect = existing.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return tmp;
    }
}
