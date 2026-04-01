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

    private float elapsed;
    private float timeLimit = 120f;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private RawImage minimapImage;
    private RectTransform minimapArrow;
    private Texture2D minimapCircleTexture;
    private GameObject matchFinishedOverlay;
    private bool matchFinished;
    private TMP_FontAsset prismFont;
    private Image healthFillVisual;

    public bool IsMatchFinished => matchFinished;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        prismFont = ResolvePrismFont();
        EnsureRuntimeHud();
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        playerController = FindFirstObjectByType<PlayerController>();

        int level = GameManager.Instance.currentLevel;
        if (levelText != null)
        {
            levelText.text = "LEVEL " + level;
        }

        if (weaponText != null)
        {
            weaponText.text = playerController != null
                ? playerController.equippedWeaponName.ToUpperInvariant()
                : GameManager.Instance.GetWeaponNameForLevel(level).ToUpperInvariant();
        }

        UpdateScore(GameManager.Instance.score);
        UpdateEnemyCount(GameManager.Instance.enemiesRemaining);

        if (playerHealth != null)
        {
            UpdateHealth(playerHealth.currentHealth, playerHealth.maxHealth);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float remaining = Mathf.Max(0f, timeLimit - elapsed);

        if (timerText != null)
        {
            timerText.text = "TIME  " + Mathf.CeilToInt(remaining);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.levelTime = elapsed;
        }

        if (!matchFinished && remaining <= 0.001f)
        {
            ShowMatchFinishedOverlay();
        }

        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (playerHealth != null)
        {
            UpdateHealth(playerHealth.currentHealth, playerHealth.maxHealth);
        }

        if (playerController != null && weaponText != null)
        {
            weaponText.text = playerController.equippedWeaponName.ToUpperInvariant();
        }

        UpdateMinimap();
    }

    public void UpdateHealth(float current, float max)
    {
        if (healthText != null)
        {
            healthText.text = "HEALTH  " + Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        }

        if (healthBar != null)
        {
            healthBar.value = Mathf.Clamp01(current / Mathf.Max(1f, max));
        }

        if (healthFillVisual != null)
        {
            healthFillVisual.fillAmount = Mathf.Clamp01(current / Mathf.Max(1f, max));
        }
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = "SCORE  " + score;
        }
    }

    public void UpdateEnemyCount(int count)
    {
        if (enemyCountText != null)
        {
            enemyCountText.text = "ENEMIES  " + count;
        }
    }

    private void EnsureRuntimeHud()
    {
        if (healthText != null && scoreText != null && levelText != null &&
            weaponText != null && enemyCountText != null && timerText != null && healthBar != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find("HUDCanvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("HUDCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject topBar = CreateImage(canvasObject.transform, "TopBar",
            new Color(0.05f, 0.07f, 0.11f, 0.52f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -58f), new Vector2(0f, 116f));

        CreateImage(topBar.transform, "TopBarAccent",
            new Color(0.30f, 0.76f, 1f, 0.35f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f));

        GameObject bottomPanel = CreateImage(canvasObject.transform, "BottomHealthPanel",
            new Color(0.05f, 0.07f, 0.11f, 0.66f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(252f, 72f), new Vector2(476f, 104f));

        GameObject healthBackground = CreateImage(bottomPanel.transform, "HealthBg",
            new Color(0.12f, 0.16f, 0.20f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 18f), new Vector2(428f, 24f));

        GameObject fillArea = EnsureRectObject(healthBackground.transform, "FillArea",
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.offsetMin = new Vector2(10f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);
        if (fillArea.GetComponent<RectMask2D>() == null)
        {
            fillArea.AddComponent<RectMask2D>();
        }

        GameObject healthFill = CreateImage(fillArea.transform, "HealthFill",
            new Color(0.18f, 0.92f, 0.45f, 1f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image healthFillImage = healthFill.GetComponent<Image>();
        healthFillImage.type = Image.Type.Filled;
        healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        healthFillImage.fillOrigin = 0;
        healthFillImage.fillAmount = 1f;
        healthFillVisual = healthFillImage;

        if (healthBar == null)
        {
            healthBar = healthBackground.GetComponent<Slider>();
            if (healthBar == null)
            {
                healthBar = healthBackground.AddComponent<Slider>();
            }

            healthBar.fillRect = healthFill.GetComponent<RectTransform>();
            healthBar.minValue = 0f;
            healthBar.maxValue = 1f;
            healthBar.value = 1f;
            healthBar.direction = Slider.Direction.LeftToRight;
            healthBar.interactable = false;
            healthBar.targetGraphic = healthFillImage;
        }

        scoreText ??= CreateText(topBar.transform, "ScoreText",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(205f, -2f), new Vector2(380f, 44f),
            34f, FontStyles.Bold, TextAlignmentOptions.Left);

        levelText ??= CreateText(topBar.transform, "LevelText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(320f, 44f),
            36f, FontStyles.Bold, TextAlignmentOptions.Center);

        weaponText ??= CreateText(topBar.transform, "WeaponText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(500f, 34f),
            24f, FontStyles.Normal, TextAlignmentOptions.Center);

        enemyCountText ??= CreateText(topBar.transform, "EnemyText",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-215f, 14f), new Vector2(380f, 40f),
            28f, FontStyles.Bold, TextAlignmentOptions.Right);

        timerText ??= CreateText(topBar.transform, "TimerText",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-215f, -18f), new Vector2(300f, 34f),
            24f, FontStyles.Normal, TextAlignmentOptions.Right);

        healthText ??= CreateText(bottomPanel.transform, "HPText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(238f, -28f), new Vector2(420f, 30f),
            24f, FontStyles.Bold, TextAlignmentOptions.Left);

        scoreText.color = new Color(0.97f, 0.98f, 1f, 1f);
        levelText.color = new Color(0.97f, 0.98f, 1f, 1f);
        weaponText.color = new Color(0.66f, 0.88f, 1f, 1f);
        enemyCountText.color = new Color(0.97f, 0.98f, 1f, 1f);
        timerText.color = new Color(0.86f, 0.92f, 1f, 1f);
        healthText.color = Color.white;

        EnsureMinimap(canvasObject.transform);
    }

    private GameObject CreateImage(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject existing = parent.Find(name)?.gameObject;
        if (existing != null)
        {
            return existing;
        }

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
        Vector2 position, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject existing = parent.Find(name)?.gameObject;
        if (existing == null)
        {
            existing = new GameObject(name);
            existing.transform.SetParent(parent, false);
        }

        TextMeshProUGUI tmp = existing.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = existing.AddComponent<TextMeshProUGUI>();
        }

        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (prismFont != null)
        {
            tmp.font = prismFont;
        }

        RectTransform rect = existing.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return tmp;
    }

    private void EnsureMinimap(Transform canvasTransform)
    {
        GameObject minimapRoot = CreateImage(canvasTransform, "MinimapRoot",
            new Color(0.10f, 0.08f, 0.05f, 0.82f),
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-130f, 128f), new Vector2(188f, 188f));
        Image minimapRootImage = minimapRoot.GetComponent<Image>();
        minimapRootImage.sprite = GetOrCreateCircleSprite();
        minimapRootImage.type = Image.Type.Simple;
        minimapRootImage.preserveAspect = true;
        if (minimapRoot.GetComponent<Mask>() == null)
        {
            minimapRoot.AddComponent<Mask>().showMaskGraphic = true;
        }

        GameObject minimapContent = CreateImage(minimapRoot.transform, "MinimapContent",
            Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(172f, 172f));
        Image minimapContentImage = minimapContent.GetComponent<Image>();
        minimapContentImage.sprite = GetOrCreateCircleSprite();
        minimapContentImage.color = Color.white;

        minimapImage = minimapContent.GetComponent<RawImage>();
        if (minimapImage == null)
        {
            Destroy(minimapContentImage);
            minimapImage = minimapContent.AddComponent<RawImage>();
        }

        GameObject arrowObject = CreateImage(minimapRoot.transform, "PlayerArrow",
            new Color(1f, 0.96f, 0.92f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 28f));
        arrowObject.GetComponent<Image>().sprite = GetOrCreateTriangleSprite();
        minimapArrow = arrowObject.GetComponent<RectTransform>();
    }

    private void UpdateMinimap()
    {
        if (minimapImage == null)
        {
            return;
        }

        MinimapCameraFollow minimap = FindFirstObjectByType<MinimapCameraFollow>();
        if (minimap != null)
        {
            RenderTexture texture = minimap.EnsureRenderTexture();
            if (minimapImage.texture != texture)
            {
                minimapImage.texture = texture;
            }

            Camera minimapCamera = minimap.GetComponent<Camera>();
            if (minimapCamera != null)
            {
                minimapCamera.Render();
            }
        }

        if (minimapArrow != null && playerController != null)
        {
            float normalizedX = Mathf.Clamp(playerController.transform.position.x / 26f, -1f, 1f);
            float normalizedZ = Mathf.Clamp(playerController.transform.position.z / 26f, -1f, 1f);
            float travelRadius = 62f;
            minimapArrow.anchoredPosition = new Vector2(normalizedX * travelRadius, normalizedZ * travelRadius);
            minimapArrow.localEulerAngles = new Vector3(0f, 0f, -playerController.transform.eulerAngles.y);
        }
    }

    private void ShowMatchFinishedOverlay()
    {
        matchFinished = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameObject canvasObject = GameObject.Find("HUDCanvas");
        if (canvasObject == null)
        {
            return;
        }

        if (matchFinishedOverlay != null)
        {
            Destroy(matchFinishedOverlay);
        }

        matchFinishedOverlay = new GameObject("MatchFinishedOverlay");
        matchFinishedOverlay.transform.SetParent(canvasObject.transform, false);

        Image overlay = matchFinishedOverlay.AddComponent<Image>();
        overlay.color = new Color(0.02f, 0.03f, 0.06f, 0.76f);
        Stretch(matchFinishedOverlay.GetComponent<RectTransform>());

        GameObject panel = CreateImage(matchFinishedOverlay.transform, "FinishedPanel",
            new Color(0.13f, 0.17f, 0.28f, 0.90f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 360f));
        panel.AddComponent<Outline>().effectColor = new Color(0.26f, 0.42f, 0.68f, 0.22f);

        TextMeshProUGUI title = CreateText(panel.transform, "FinishedTitle",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 74f), new Vector2(560f, 88f),
            44f, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = "MATCH FINISHED";

        TextMeshProUGUI subtitle = CreateText(panel.transform, "FinishedSubtitle",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(620f, 64f),
            24f, FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.text = "The timer reached zero. Choose what you want to do next.";
        subtitle.color = new Color(0.78f, 0.88f, 1f, 0.92f);
        subtitle.textWrappingMode = TextWrappingModes.Normal;

        CreateActionButton(panel.transform, "RESTART", new Vector2(-116f, -66f), () =>
        {
            Time.timeScale = 1f;
            GameManager.Instance?.ReplayCurrentLevel();
        });

        CreateActionButton(panel.transform, "MAIN MENU", new Vector2(116f, -66f), () =>
        {
            Time.timeScale = 1f;
            GameManager.Instance?.GoToMainMenu();
        });
    }

    private void CreateActionButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateImage(parent, "Btn_" + label,
            Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(200f, 60f));

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        TextMeshProUGUI labelText = CreateText(buttonObject.transform, "Txt_" + label,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            24f, FontStyles.Bold, TextAlignmentOptions.Center);
        labelText.text = label;
        labelText.color = new Color(0.10f, 0.10f, 0.14f, 1f);
    }

    private Sprite GetOrCreateCircleSprite()
    {
        if (minimapCircleTexture == null)
        {
            minimapCircleTexture = new Texture2D(256, 256, TextureFormat.ARGB32, false);
            minimapCircleTexture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(127.5f, 127.5f);
            float radius = 120f;
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = distance <= radius ? 1f : 0f;
                    minimapCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            minimapCircleTexture.Apply();
        }

        return Sprite.Create(minimapCircleTexture, new Rect(0f, 0f, 256f, 256f), new Vector2(0.5f, 0.5f));
    }

    private Sprite GetOrCreateTriangleSprite()
    {
        Texture2D triangleTexture = new Texture2D(64, 64, TextureFormat.ARGB32, false);
        triangleTexture.wrapMode = TextureWrapMode.Clamp;

        Vector2 a = new Vector2(32f, 60f);
        Vector2 b = new Vector2(10f, 10f);
        Vector2 c = new Vector2(54f, 10f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Vector2 p = new Vector2(x, y);
                bool inside = SameSide(p, a, b, c) && SameSide(p, b, a, c) && SameSide(p, c, a, b);
                triangleTexture.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }

        triangleTexture.Apply();
        return Sprite.Create(triangleTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f));
    }

    private bool SameSide(Vector2 p1, Vector2 p2, Vector2 a, Vector2 b)
    {
        Vector3 cp1 = Vector3.Cross(b - a, p1 - a);
        Vector3 cp2 = Vector3.Cross(b - a, p2 - a);
        return Vector3.Dot(cp1, cp2) >= 0f;
    }

    private GameObject EnsureRectObject(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject existing = parent.Find(name)?.gameObject;
        if (existing != null)
        {
            return existing;
        }

        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return obj;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private TMP_FontAsset ResolvePrismFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            TMP_FontAsset font = fonts[i];
            if (font != null && (font.name.Contains("Arizona") || font.name.Contains("Azonix")))
            {
                return font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }
}
