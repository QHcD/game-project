using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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

    // Kill counter (CoD-style)
    private TextMeshProUGUI killCountText;
    private int killCount;
    private float killFeedTimer;
    private TextMeshProUGUI killFeedText;

    // Damage flash
    private Image damageFlashImage;
    private float damageFlashAlpha;
    private const float DamageFlashDecay = 2.2f;

    // Low-health vignette
    private Image lowHealthImage;
    private float lowHealthPulse;

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
        EnsureDamageFlashLayer();
        EnsureLowHealthLayer();
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

        TickDamageFlash();
        TickLowHealthVignette();

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
        TickKillFeed();
    }

    public void UpdateHealth(float current, float max)
    {
        float ratio = Mathf.Clamp01(current / Mathf.Max(1f, max));

        if (healthText != null)
        {
            healthText.text = "HP  " + Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        }

        // Drive the fill image directly — this is the most reliable approach
        if (healthFillVisual != null)
        {
            healthFillVisual.fillAmount = ratio;
            // Shift colour from green → red as health drops
            healthFillVisual.color = Color.Lerp(new Color(0.92f, 0.18f, 0.18f), new Color(0.18f, 0.92f, 0.45f), ratio);
        }

        if (healthBar != null)
        {
            healthBar.value = ratio;
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

    public void RegisterKill()
    {
        killCount++;

        if (killCountText != null)
        {
            killCountText.text = "KILLS  " + killCount;
        }

        // Show kill feed popup
        if (killFeedText != null)
        {
            string[] messages = { "ENEMY DOWN", "ELIMINATED", "NEUTRALIZED", "TARGET DOWN", "HOSTILE KILLED" };
            killFeedText.text = messages[killCount % messages.Length];
            killFeedText.alpha = 1f;
            killFeedTimer = 2f;
        }
    }

    public void ShowXpPopup(int xpAmount, string eventLabel = "")
    {
        if (killFeedText == null)
            return;

        string suffix = string.IsNullOrWhiteSpace(eventLabel) ? string.Empty : " " + eventLabel.ToUpperInvariant();
        killFeedText.text = $"+{Mathf.Max(0, xpAmount)} XP{suffix}";
        killFeedText.alpha = 1f;
        killFeedTimer = 1.8f;
    }

    private void TickKillFeed()
    {
        if (killFeedText == null || killFeedTimer <= 0f)
        {
            return;
        }

        killFeedTimer -= Time.deltaTime;
        if (killFeedTimer <= 0.5f)
        {
            killFeedText.alpha = Mathf.Max(0f, killFeedTimer / 0.5f);
        }
    }

    public void ShowDamageFlash(float damageAmount)
    {
        // Scale flash intensity with damage (cap at 1.0)
        float intensity = Mathf.Clamp01(damageAmount / 40f);
        damageFlashAlpha = Mathf.Max(damageFlashAlpha, 0.25f + intensity * 0.55f);
        if (damageFlashImage != null)
        {
            damageFlashImage.color = new Color(0.85f, 0.05f, 0.05f, damageFlashAlpha);
        }
    }

    private void EnsureDamageFlashLayer()
    {
        GameObject canvasObject = GameObject.Find("HUDCanvas");
        if (canvasObject == null) return;

        GameObject existing = canvasObject.transform.Find("DamageFlash")?.gameObject;
        if (existing != null)
        {
            damageFlashImage = existing.GetComponent<Image>();
            return;
        }

        GameObject flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(canvasObject.transform, false);
        damageFlashImage = flashObj.AddComponent<Image>();
        damageFlashImage.color = new Color(0.85f, 0.05f, 0.05f, 0f);
        damageFlashImage.raycastTarget = false;
        Stretch(flashObj.GetComponent<RectTransform>());

        // Put it above HUD but below pause menu
        flashObj.GetComponent<Canvas>();
        flashObj.transform.SetAsLastSibling();
    }

    private void TickDamageFlash()
    {
        if (damageFlashImage == null) return;
        damageFlashAlpha = Mathf.MoveTowards(damageFlashAlpha, 0f, DamageFlashDecay * Time.deltaTime);
        damageFlashImage.color = new Color(0.85f, 0.05f, 0.05f, damageFlashAlpha);
    }

    private void EnsureLowHealthLayer()
    {
        GameObject canvasObject = GameObject.Find("HUDCanvas");
        if (canvasObject == null) return;

        GameObject existing = canvasObject.transform.Find("LowHealthVignette")?.gameObject;
        if (existing != null)
        {
            lowHealthImage = existing.GetComponent<Image>();
            return;
        }

        GameObject vigObj = new GameObject("LowHealthVignette");
        vigObj.transform.SetParent(canvasObject.transform, false);
        lowHealthImage = vigObj.AddComponent<Image>();
        lowHealthImage.color = new Color(0.75f, 0.02f, 0.02f, 0f);
        lowHealthImage.raycastTarget = false;
        Stretch(vigObj.GetComponent<RectTransform>());
        vigObj.transform.SetSiblingIndex(1); // just above bottom
    }

    private void TickLowHealthVignette()
    {
        if (lowHealthImage == null || playerHealth == null) return;

        float ratio = playerHealth.currentHealth / Mathf.Max(1f, playerHealth.maxHealth);
        if (ratio > 0.35f)
        {
            lowHealthImage.color = new Color(0.75f, 0.02f, 0.02f, 0f);
            return;
        }

        // Pulse faster as health drops
        float pulseSpeed = Mathf.Lerp(3.5f, 6.5f, 1f - ratio / 0.35f);
        lowHealthPulse += Time.deltaTime * pulseSpeed;
        float pulse = (Mathf.Sin(lowHealthPulse) + 1f) * 0.5f;
        float maxAlpha = Mathf.Lerp(0.12f, 0.52f, 1f - ratio / 0.35f);
        lowHealthImage.color = new Color(0.75f, 0.02f, 0.02f, pulse * maxAlpha);
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

        // ── Health panel (bottom-left) ──────────────────────────────────────────
        // Outer panel: anchored bottom-left, fixed size
        GameObject bottomPanel = CreateImage(canvasObject.transform, "BottomHealthPanel",
            new Color(0.05f, 0.07f, 0.11f, 0.78f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(240f, 62f), new Vector2(440f, 80f));

        // ── Bar track (dark background behind the green bar) ────────────────────
        // Positioned inside the panel, bottom half
        GameObject healthTrack = CreateImage(bottomPanel.transform, "HealthTrack",
            new Color(0.08f, 0.10f, 0.14f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f),   // anchored at bottom of panel
            new Vector2(0f, 10f),                         // 10 px from bottom edge, centred
            new Vector2(-24f, 18f));                      // full width minus 12 px margin each side, 18 px tall

        // ── Green fill — simple Image that we scale via fillAmount ───────────────
        // Anchored LEFT inside the track so width shrinks rightward as HP drops
        GameObject healthFillObj = new GameObject("HealthFill");
        healthFillObj.transform.SetParent(healthTrack.transform, false);
        Image healthFillImage = healthFillObj.AddComponent<Image>();
        healthFillImage.color = new Color(0.18f, 0.92f, 0.45f, 1f);
        healthFillImage.type = Image.Type.Filled;
        healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        healthFillImage.fillOrigin = 0;
        healthFillImage.fillAmount = 1f;
        healthFillVisual = healthFillImage;
        RectTransform fillRect = healthFillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        // Dummy Slider kept for legacy UpdateHealth path
        if (healthBar == null)
        {
            healthBar = healthTrack.AddComponent<Slider>();
            healthBar.fillRect = fillRect;
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

        // Kill counter — bottom-left, above health panel
        killCountText ??= CreateText(canvasObject.transform, "KillCountText",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(240f, 152f), new Vector2(300f, 34f),
            26f, FontStyles.Bold, TextAlignmentOptions.Left);
        killCountText.text = "KILLS  0";
        killCountText.color = new Color(1f, 0.85f, 0.3f, 1f);

        // Kill feed popup — centre screen, slightly above middle
        killFeedText ??= CreateText(canvasObject.transform, "KillFeedText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(500f, 50f),
            32f, FontStyles.Bold, TextAlignmentOptions.Center);
        killFeedText.color = new Color(1f, 0.35f, 0.25f, 1f);
        killFeedText.alpha = 0f;

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
            tmp.font = prismFont;
        if (tmp.font == null)
        {
            TMP_FontAsset lib = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (lib != null)
                tmp.font = lib;
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

        // Only one Graphic (Image OR RawImage) per GameObject — remove Image before adding RawImage.
        minimapImage = minimapContent.GetComponent<RawImage>();
        if (minimapImage == null)
        {
            if (minimapContentImage != null)
                DestroyImmediate(minimapContentImage);
            minimapImage = minimapContent.AddComponent<RawImage>();
        }
        minimapImage.color = Color.white;

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

            // Update minimap camera position HERE before rendering so it tracks the player this frame
            if (playerController != null && !minimap.lockToArenaCenter)
            {
                minimap.transform.position = new Vector3(
                    playerController.transform.position.x,
                    minimap.height,
                    playerController.transform.position.z);
                minimap.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            Camera minimapCamera = minimap.GetComponent<Camera>();
            if (minimapCamera != null)
            {
                minimapCamera.Render();
            }
        }

        if (minimapArrow != null && playerController != null)
        {
            // Camera now follows the player, so player is always at minimap centre
            minimapArrow.anchoredPosition = Vector2.zero;
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

        // Guarantee the canvas can receive pointer events
        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            canvasObject.AddComponent<GraphicRaycaster>();

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

        GameObject restartBtnObj = CreateActionButton(panel.transform, "RESTART", new Vector2(-116f, -66f), () =>
        {
            Time.timeScale = 1f;
            GameManager.Instance?.ReplayCurrentLevel();
        });

        CreateActionButton(panel.transform, "MAIN MENU", new Vector2(116f, -66f), () =>
        {
            Time.timeScale = 1f;
            GameManager.Instance?.GoToMainMenu();
        });

        // ── Ensure an EventSystem exists with a NEW-INPUT-SYSTEM-compatible module ──
        //
        // The project uses the Input System Package (new). The legacy
        // StandaloneInputModule reads UnityEngine.Input.* internally, which
        // throws InvalidOperationException under the new Input System and
        // prevents any button click from being dispatched. That is exactly
        // what was killing the RESTART / MAIN MENU buttons on the Match
        // Finished overlay (levels 4/7/9 were reproducible because a match
        // would end without the player ever opening the pause menu — so
        // PauseMenuController had never created a correct EventSystem, and
        // this method silently fell back to StandaloneInputModule).
        //
        // Fix:
        //   1. Find or create the EventSystem.
        //   2. Strip any stale StandaloneInputModule that was shipped with
        //      the scene or added by older code.
        //   3. Guarantee exactly one InputSystemUIInputModule is attached.
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            eventSystem = esObj.AddComponent<EventSystem>();
        }

        GameObject eventSystemGO = eventSystem.gameObject;

        // 2. Remove the broken legacy module if present.
        StandaloneInputModule legacyModule = eventSystemGO.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
            Destroy(legacyModule);

        // 3. Ensure the new-Input-System UI module is attached exactly once.
        InputSystemUIInputModule uiModule = eventSystemGO.GetComponent<InputSystemUIInputModule>();
        if (uiModule == null)
            uiModule = eventSystemGO.AddComponent<InputSystemUIInputModule>();
        uiModule.enabled = true;

        eventSystemGO.SetActive(true);

        // Focus the Restart button so keyboard/gamepad navigation works immediately
        if (restartBtnObj != null)
        {
            Button firstBtn = restartBtnObj.GetComponent<Button>();
            if (firstBtn != null)
                eventSystem.SetSelectedGameObject(firstBtn.gameObject);
        }
    }

    /// <summary>
    /// Public alias for ShowMatchFinishedOverlay so external callers (and the
    /// user's UIManager references) can trigger the end-game screen directly.
    /// Guarantees: timeScale=0, cursor unlocked, EventSystem active.
    /// </summary>
    public void ShowGameFinishedMenu() => ShowMatchFinishedOverlay();

    private GameObject CreateActionButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateImage(parent, "Btn_" + label,
            Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(200f, 60f));

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();

        // Ensure the Image is set as the button's targetGraphic so it's clickable
        Image btnImage = buttonObject.GetComponent<Image>();
        if (btnImage != null)
        {
            btnImage.raycastTarget = true;
            button.targetGraphic   = btnImage;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        TextMeshProUGUI labelText = CreateText(buttonObject.transform, "Txt_" + label,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            24f, FontStyles.Bold, TextAlignmentOptions.Center);
        labelText.text  = label;
        labelText.color = new Color(0.10f, 0.10f, 0.14f, 1f);
        labelText.raycastTarget = false;   // let the Image underneath catch the click

        return buttonObject;
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
        // ── Priority 1: "aizona fx" font (drag it into a Resources/Fonts folder) ──
        // Try common Resource paths for the custom font asset.
        string[] aizonaPaths = {
            "Fonts/aizona fx SDF",
            "Fonts/aizona fx",
            "Fonts & Materials/aizona fx SDF",
            "Fonts & Materials/aizona fx",
        };
        foreach (string path in aizonaPaths)
        {
            TMP_FontAsset aizona = Resources.Load<TMP_FontAsset>(path);
            if (aizona != null) return aizona;
        }

        // ── Priority 2: any loaded font whose name contains "aizona" ──────────
        TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (TMP_FontAsset font in allFonts)
        {
            if (font == null) continue;
            string lower = font.name.ToLowerInvariant();
            if (lower.Contains("aizona") || lower.Contains("arizona") || lower.Contains("azonix"))
                return font;
        }

        // ── Priority 3: TMP default ──────────────────────────────────────────
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        // ── Priority 4: LiberationSans bundled with TMP ──────────────────────
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }
}
