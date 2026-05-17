// Loading-screen / full-screen transition animations are not driven from HUDManager
// (see LoadingScreenUI + RuntimeMenuBuilder — motion overlays disabled project-wide).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    private sealed class ScoreboardRowUi
    {
        public GameObject Root;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI KillsText;
        public TextMeshProUGUI StatusText;
    }

    public static HUDManager Instance;

    public TextMeshProUGUI healthText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI weaponText;
    public TextMeshProUGUI enemyCountText;
    public TextMeshProUGUI timerText;
    public Slider healthBar;

    private float elapsed;
    private float timeLimit = 300f;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private RawImage minimapImage;
    private RectTransform minimapArrow;
    private RectTransform fullMapPlayerArrow;
    private RectTransform minimapRootRect;
    private RectTransform fullMapFrameRect;
    private readonly List<RectTransform> enemyMinimapArrows = new List<RectTransform>();
    private readonly List<RectTransform> enemyFullMapArrows = new List<RectTransform>();
    private Texture2D minimapCircleTexture;
    private GameObject fullMapOverlay;
    private RawImage fullMapImage;
    private TextMeshProUGUI fullMapHintText;
    private GameObject scoreboardOverlay;
    private TextMeshProUGUI scoreboardTitleText;
    private TextMeshProUGUI scoreboardSummaryText;
    private ScoreboardRowUi[] scoreboardRows;
    private GameObject matchFinishedOverlay;
    private bool matchFinished;
    private TMP_FontAsset prismFont;
    private Image healthFillVisual;
    private bool isFullMapVisible;
    private bool isScoreboardVisible;

    // Kill counter (CoD-style)
    private TextMeshProUGUI killCountText;
    private int killCount;
    private CombatUIManager combatUIManager;

    // Damage flash
    private Image damageFlashImage;
    private float damageFlashAlpha;
    private const float DamageFlashDecay = 2.2f;

    // Low-health vignette
    private Image lowHealthImage;
    private float lowHealthPulse;

    public bool IsMatchFinished => matchFinished;

    /// <summary>Elapsed match seconds (drives the on-screen timer). Prefer this over duplicating time state.</summary>
    public float MatchElapsedSeconds => elapsed;

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

    private void OnDestroy()
    {
        if (MatchStatsManager.Instance != null)
            MatchStatsManager.Instance.StatsChanged -= HandleMatchStatsChanged;
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

        if (!MultiplayerMode.IsMultiplayer)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            playerController = FindFirstObjectByType<PlayerController>();
        }
        // In multiplayer, player refs are injected by NetworkPlayerSpawner
        // via InitForMultiplayerLocalPlayer() after the local player spawns.

        int level = GameManager.Instance.currentLevel;
        timeLimit = GameManager.Instance.LevelTimeLimitSeconds;
        elapsed = 0f;

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
            UpdateHealth(playerHealth.currentHealth, playerHealth.maxHealth);

        // ── Register the local player in MatchStatsManager ──────────────────
        // Skipped in multiplayer — InitForMultiplayerLocalPlayer() registers
        // the confirmed local player after PhotonNetwork.Instantiate succeeds,
        // avoiding accidental registration of a remote player's PlayerHealth.
        if (!MultiplayerMode.IsMultiplayer && MatchStatsManager.Instance != null && playerHealth != null)
        {
            string playerId = MatchStatsManager.BuildCombatantId(playerHealth);
            string playerLabel = PlayerProfile.HasUsername ? PlayerProfile.Username : "YOU";
            MatchStatsManager.Instance.RegisterCombatant(playerId, playerLabel, isPlayer: true, transform: playerHealth.transform);
            MatchStatsManager.Instance.StatsChanged -= HandleMatchStatsChanged;
            MatchStatsManager.Instance.StatsChanged += HandleMatchStatsChanged;
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

        if (GameManager.Instance != null && !MultiplayerMode.IsMultiplayer)
            GameManager.Instance.levelTime = elapsed;

        // In multiplayer, never freeze time — clients each run their own
        // HUD timer but the match end is server-driven, not timer-driven.
        if (!matchFinished && remaining <= 0.001f && !MultiplayerMode.IsMultiplayer)
            ShowMatchFinishedOverlay();

        TickDamageFlash();
        TickLowHealthVignette();

        if (playerHealth == null && !MultiplayerMode.IsMultiplayer)
            playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerController == null && !MultiplayerMode.IsMultiplayer)
            playerController = FindFirstObjectByType<PlayerController>();

        if (playerHealth != null)
        {
            UpdateHealth(playerHealth.currentHealth, playerHealth.maxHealth);
        }

        if (playerController != null && weaponText != null)
        {
            weaponText.text = playerController.equippedWeaponName.ToUpperInvariant();
        }

        HandleOverlayInput();
        UpdateMinimap();
        RefreshScoreboard();
    }

    private void HandleMatchStatsChanged()
    {
        RefreshScoreboard(force: false);
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
            killCountText.text = "KILLS  " + killCount;

        // Live-refresh the leaderboard row (visible or not) so the sort
        // order updates the moment the kill is confirmed.
        RefreshScoreboard(force: false);
    }

    public void ShowXpPopup(int xpAmount, string eventLabel = "")
    {
        combatUIManager ??= CombatUIManager.CreateOrGet(GameObject.Find("HUDCanvas")?.transform, prismFont);
        combatUIManager?.ShowXpPopup(xpAmount, eventLabel);
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
        GameObject canvasObject = GameObject.Find("HUDCanvas");
        if (healthText != null && scoreText != null && levelText != null &&
            weaponText != null && enemyCountText != null && timerText != null && healthBar != null)
        {
            if (canvasObject != null)
            {
                EnsureCombatUi(canvasObject.transform);
                EnsureMinimap(canvasObject.transform);
                EnsureFullMapOverlay(canvasObject.transform);
                EnsureScoreboardOverlay(canvasObject.transform);
                EnsurePlayerIdentityChip(canvasObject.transform);
            }
            return;
        }

        if (canvasObject == null)
        {
            canvasObject = new GameObject("HUDCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler newScaler = canvasObject.AddComponent<CanvasScaler>();
            newScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            newScaler.referenceResolution = new Vector2(1920f, 1080f);
            newScaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
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
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(320f, 40f),
            36f, FontStyles.Bold, TextAlignmentOptions.Center);

        weaponText ??= CreateText(topBar.transform, "WeaponText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(500f, 30f),
            24f, FontStyles.Normal, TextAlignmentOptions.Center);

        // Restore classic layout: top-right ENEMIES + TIME (pre-regression).
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

        scoreText.color = new Color(0.97f, 0.98f, 1f, 1f);
        levelText.color = new Color(0.97f, 0.98f, 1f, 1f);
        weaponText.color = new Color(0.66f, 0.88f, 1f, 1f);
        enemyCountText.color = new Color(0.97f, 0.98f, 1f, 1f);
        timerText.color = new Color(0.86f, 0.92f, 1f, 1f);
        healthText.color = Color.white;

        EnsureCombatUi(canvasObject.transform);
        EnsureMinimap(canvasObject.transform);
        EnsureFullMapOverlay(canvasObject.transform);
        EnsureScoreboardOverlay(canvasObject.transform);
        EnsurePlayerIdentityChip(canvasObject.transform);
    }

    /// <summary>Small top-left name strip — semi-transparent, sized to the handle.</summary>
    private void EnsurePlayerIdentityChip(Transform canvasTransform)
    {
        if (canvasTransform == null) return;

        string hudName = PlayerProfile.HasUsername ? PlayerProfile.Username : "OPERATIVE";
        GameObject chip = CreateImage(canvasTransform, "PlayerIdentityChip",
            new Color(0.12f, 0.12f, 0.14f, 0.5f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -68f), new Vector2(268f, 46f));

        TextMeshProUGUI nameTmp = CreateText(chip.transform, "PlayerHudName",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            20f, FontStyles.Bold, TextAlignmentOptions.Left);
        nameTmp.text = "  " + hudName;
        nameTmp.color = new Color(0.96f, 0.97f, 1f, 1f);
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (prismFont != null) nameTmp.font = prismFont;
    }

    private void EnsureCombatUi(Transform canvasTransform)
    {
        combatUIManager = CombatUIManager.CreateOrGet(canvasTransform, prismFont);
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
        minimapRootRect = minimapRoot.GetComponent<RectTransform>();
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
            new Color(0.25f, 1f, 0.35f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 42f));
        Image playerArrowImage = arrowObject.GetComponent<Image>();
        playerArrowImage.sprite = GetOrCreateTriangleSprite();
        playerArrowImage.color = new Color(0.25f, 1f, 0.35f, 1f);
        minimapArrow = arrowObject.GetComponent<RectTransform>();
        minimapArrow.sizeDelta = new Vector2(40f, 50f);
        EnsureEnemyArrowPool(minimapRoot.transform, enemyMinimapArrows, "EnemyMiniArrow_", 24f);
    }

    private void EnsureFullMapOverlay(Transform canvasTransform)
    {
        fullMapOverlay = canvasTransform.Find("FullMapOverlay")?.gameObject;
        if (fullMapOverlay == null)
        {
            fullMapOverlay = new GameObject("FullMapOverlay");
            fullMapOverlay.transform.SetParent(canvasTransform, false);
            Image backdrop = fullMapOverlay.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.04f, 0.08f, 0.68f);
            Stretch(fullMapOverlay.GetComponent<RectTransform>());

            GameObject frame = CreateImage(fullMapOverlay.transform, "FullMapFrame",
                new Color(0.08f, 0.12f, 0.18f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 760f));
            fullMapFrameRect = frame.GetComponent<RectTransform>();
            frame.AddComponent<Outline>().effectColor = new Color(0.32f, 0.78f, 1f, 0.24f);

            GameObject mapImageObject = new GameObject("FullMapImage");
            mapImageObject.transform.SetParent(frame.transform, false);
            fullMapImage = mapImageObject.AddComponent<RawImage>();
            fullMapImage.color = Color.white;
            RectTransform mapRect = mapImageObject.GetComponent<RectTransform>();
            mapRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.anchoredPosition = new Vector2(0f, 18f);
            mapRect.sizeDelta = new Vector2(920f, 620f);

            fullMapHintText = CreateText(frame.transform, "FullMapHint",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(760f, 40f),
                24f, FontStyles.Bold, TextAlignmentOptions.Center);
            fullMapHintText.text = "TAB  TOGGLE FULL MAP";
            fullMapHintText.color = new Color(0.76f, 0.90f, 1f, 0.92f);

            GameObject fullPlayerArrow = CreateImage(frame.transform, "FullMapPlayerArrow",
                new Color(0.25f, 1f, 0.35f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 54f));
            fullPlayerArrow.GetComponent<Image>().sprite = GetOrCreateTriangleSprite();
            fullMapPlayerArrow = fullPlayerArrow.GetComponent<RectTransform>();
        }
        else
        {
            fullMapFrameRect = fullMapOverlay.transform.Find("FullMapFrame")?.GetComponent<RectTransform>();
            fullMapImage = fullMapOverlay.transform.Find("FullMapFrame/FullMapImage")?.GetComponent<RawImage>();
            fullMapHintText = fullMapOverlay.transform.Find("FullMapFrame/FullMapHint")?.GetComponent<TextMeshProUGUI>();
            fullMapPlayerArrow = fullMapOverlay.transform.Find("FullMapFrame/FullMapPlayerArrow")?.GetComponent<RectTransform>();
        }

        Transform frameTransform = fullMapFrameRect != null ? fullMapFrameRect.transform : null;
        if (frameTransform != null && fullMapPlayerArrow == null)
        {
            GameObject fullPlayerArrow = CreateImage(frameTransform, "FullMapPlayerArrow",
                new Color(0.25f, 1f, 0.35f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 54f));
            fullPlayerArrow.GetComponent<Image>().sprite = GetOrCreateTriangleSprite();
            fullMapPlayerArrow = fullPlayerArrow.GetComponent<RectTransform>();
        }
        if (frameTransform != null)
            EnsureEnemyArrowPool(frameTransform, enemyFullMapArrows, "EnemyFullMapArrow_", 34f);

        fullMapOverlay.SetActive(false);
    }

    private void EnsureScoreboardOverlay(Transform canvasTransform)
    {
        scoreboardOverlay = canvasTransform.Find("ScoreboardOverlay")?.gameObject;
        if (scoreboardOverlay == null)
        {
            scoreboardOverlay = new GameObject("ScoreboardOverlay");
            scoreboardOverlay.transform.SetParent(canvasTransform, false);
            Image backdrop = scoreboardOverlay.AddComponent<Image>();
            backdrop.color = new Color(0.01f, 0.03f, 0.06f, 0.72f);
            Stretch(scoreboardOverlay.GetComponent<RectTransform>());

            // ── Panel — centred on screen (anchor + pivot both at 0.5,0.5) ─────
            // Previously anchored to the right edge (1,0.5), which caused the
            // panel to be off-centre and resolution-dependent.
            GameObject panel = CreateImage(scoreboardOverlay.transform, "ScoreboardPanel",
                new Color(0.05f, 0.08f, 0.14f, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 560f));

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0.5f);

            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.32f, 0.78f, 1f, 0.30f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // ── Title ────────────────────────────────────────────────────────
            scoreboardTitleText = CreateText(panel.transform, "ScoreboardTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(480f, 46f),
                34f, FontStyles.Bold, TextAlignmentOptions.Center);
            scoreboardTitleText.text = "MATCH STATS";
            scoreboardTitleText.color = Color.white;

            // Thin separator line under title
            GameObject sep = new GameObject("TitleSeparator");
            sep.transform.SetParent(panel.transform, false);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(0.32f, 0.78f, 1f, 0.35f);
            RectTransform sepRect = sep.GetComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.5f, 1f);
            sepRect.anchorMax = new Vector2(0.5f, 1f);
            sepRect.pivot     = new Vector2(0.5f, 1f);
            sepRect.anchoredPosition = new Vector2(0f, -84f);
            sepRect.sizeDelta        = new Vector2(480f, 2f);

            // ── Column headers ───────────────────────────────────────────────
            scoreboardSummaryText = CreateText(panel.transform, "ScoreboardSummary",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(480f, 28f),
                17f, FontStyles.Bold, TextAlignmentOptions.Left);
            scoreboardSummaryText.text = "LIVE MATCH LEADERBOARD";
            scoreboardSummaryText.color = new Color(0.50f, 0.75f, 1f, 0.80f);

            // ── Entry rows — 5 rows, 82 px apart ────────────────────────────
            CreateScoreboardHeader(panel.transform);
            CreateScoreboardRows(panel.transform);
            // Footer intentionally removed — the "CAPS LOCK TOGGLE LEADERBOARD"
            // hint is no longer shown.
        }
        else
        {
            scoreboardTitleText = scoreboardOverlay.transform.Find("ScoreboardPanel/ScoreboardTitle")?.GetComponent<TextMeshProUGUI>();
            scoreboardSummaryText = scoreboardOverlay.transform.Find("ScoreboardPanel/ScoreboardSummary")?.GetComponent<TextMeshProUGUI>();
            CacheScoreboardRows();
            if (scoreboardRows == null || scoreboardRows.Length == 0 || scoreboardRows[0] == null)
            {
                Transform panel = scoreboardOverlay.transform.Find("ScoreboardPanel");
                if (panel != null)
                {
                    CreateScoreboardHeader(panel);
                    CreateScoreboardRows(panel);
                }
            }
        }

        scoreboardOverlay.SetActive(false);
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

            if (fullMapImage != null && fullMapImage.texture != texture)
                fullMapImage.texture = texture;

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

        if (minimapArrow != null)
            minimapArrow.gameObject.SetActive(!isFullMapVisible);

        UpdateMapArrows(minimap);
    }

    private const int MaxEnemyMapArrows = 32;

    private void EnsureEnemyArrowPool(Transform parent, List<RectTransform> pool, string prefix, float size)
    {
        if (parent == null || pool == null) return;

        for (int i = pool.Count; i < MaxEnemyMapArrows; i++)
        {
            GameObject arrow = CreateImage(parent, prefix + i,
                new Color(1f, 0.18f, 0.16f, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size * 1.25f));
            arrow.GetComponent<Image>().sprite = GetOrCreateTriangleSprite();
            arrow.SetActive(false);
            pool.Add(arrow.GetComponent<RectTransform>());
        }
    }

    private void UpdateMapArrows(MinimapCameraFollow minimap)
    {
        if (minimap == null || playerController == null)
            return;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        UpdateFullMapPlayerArrow(minimap);
        UpdateEnemyArrowPool(enemyMinimapArrows, enemies, minimap, minimapRootRect, false);
        UpdateEnemyArrowPool(enemyFullMapArrows, enemies, minimap, fullMapFrameRect, true);
    }

    private void UpdateFullMapPlayerArrow(MinimapCameraFollow minimap)
    {
        if (fullMapPlayerArrow == null || fullMapFrameRect == null || playerController == null)
            return;

        Camera mapCamera = minimap.GetComponent<Camera>();
        if (mapCamera == null)
            return;

        float halfSize = Mathf.Min(fullMapFrameRect.rect.width, fullMapFrameRect.rect.height) * 0.5f;
        Vector2 pos = WorldToMapPosition(playerController.transform.position, minimap.transform.position, mapCamera.orthographicSize, halfSize);
        fullMapPlayerArrow.anchoredPosition = pos;
        fullMapPlayerArrow.localEulerAngles = new Vector3(0f, 0f, -playerController.transform.eulerAngles.y);
        fullMapPlayerArrow.gameObject.SetActive(isFullMapVisible);
    }

    private void UpdateEnemyArrowPool(List<RectTransform> pool, EnemyController[] enemies, MinimapCameraFollow minimap, RectTransform mapRect, bool fullMap)
    {
        if (pool == null || mapRect == null)
            return;

        Camera mapCamera = minimap.GetComponent<Camera>();
        if (mapCamera == null)
            return;

        float halfSize = Mathf.Min(mapRect.rect.width, mapRect.rect.height) * 0.5f;
        int used = 0;

        for (int i = 0; i < enemies.Length && used < pool.Count; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
                continue;

            RectTransform arrow = pool[used++];
            Vector2 pos = WorldToMapPosition(enemy.transform.position, minimap.transform.position, mapCamera.orthographicSize, halfSize);
            bool inside = pos.sqrMagnitude <= halfSize * halfSize;
            arrow.gameObject.SetActive(inside && (fullMap ? isFullMapVisible : !isFullMapVisible));
            if (!inside) continue;

            arrow.anchoredPosition = pos;
            arrow.localEulerAngles = new Vector3(0f, 0f, -enemy.transform.eulerAngles.y);
        }

        for (int i = used; i < pool.Count; i++)
            if (pool[i] != null) pool[i].gameObject.SetActive(false);
    }

    private static Vector2 WorldToMapPosition(Vector3 world, Vector3 mapCenter, float orthographicSize, float halfSize)
    {
        float worldRadius = Mathf.Max(1f, orthographicSize);
        float x = (world.x - mapCenter.x) / worldRadius * halfSize;
        float y = (world.z - mapCenter.z) / worldRadius * halfSize;
        return new Vector2(x, y);
    }

    private void HandleOverlayInput()
    {
        if (Keyboard.current == null)
            return;

        SetFullMapVisible(Keyboard.current.tabKey.isPressed);

        if (Keyboard.current.capsLockKey.wasPressedThisFrame)
            ToggleScoreboard();
    }

    private void SetFullMapVisible(bool visible)
    {
        if (isFullMapVisible == visible)
            return;

        isFullMapVisible = visible;
        if (fullMapOverlay != null)
            fullMapOverlay.SetActive(isFullMapVisible);

        MinimapCameraFollow minimap = FindFirstObjectByType<MinimapCameraFollow>();
        if (minimap != null)
            minimap.SetFullMapMode(isFullMapVisible, playerController != null ? playerController.transform : null);
    }

    private void ToggleScoreboard()
    {
        isScoreboardVisible = !isScoreboardVisible;
        if (scoreboardOverlay != null)
            scoreboardOverlay.SetActive(isScoreboardVisible);

        RefreshScoreboard(force: true);
    }

    private void RefreshScoreboard(bool force = false)
    {
        // Always update data even when hidden — kill counts must be live.
        // Only update visibility when explicitly toggled (force = true from ToggleScoreboard).
        if (force && scoreboardOverlay != null)
            scoreboardOverlay.SetActive(isScoreboardVisible);

        if (scoreboardRows == null || scoreboardRows.Length == 0) return;

        MatchStatsManager stats = MatchStatsManager.Instance;
        if (stats == null) return;

        // Header row stays as a fixed column guide.
        if (scoreboardSummaryText != null)
            scoreboardSummaryText.text = $"LIVE MATCH LEADERBOARD  ({stats.GetRegisteredCombatantCount()} COMBATANTS)";

        var entries = stats.GetTopCombatants(scoreboardRows.Length);
        for (int i = 0; i < scoreboardRows.Length; i++)
        {
            ScoreboardRowUi row = scoreboardRows[i];
            if (row == null || row.Root == null) continue;

            if (i >= entries.Count)
            {
                row.NameText.text = $"{i + 1}. ---";
                row.KillsText.text = "0";
                row.StatusText.text = "---";
                ApplyScoreboardRowStyle(row, false, false, true);
                continue;
            }

            MatchStatsManager.CombatantSnapshot entry = entries[i];
            row.NameText.text = $"{i + 1}. {entry.DisplayName}";
            row.KillsText.text = entry.Kills.ToString();
            row.StatusText.text = entry.IsAlive ? "ALIVE" : "DEAD";
            ApplyScoreboardRowStyle(row, entry.IsPlayer, entry.IsAlive, false);
        }
    }

    private void CreateScoreboardHeader(Transform panelTransform)
    {
        GameObject headerRow = new GameObject("ScoreboardHeaderRow");
        headerRow.transform.SetParent(panelTransform, false);
        RectTransform headerRect = headerRow.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 1f);
        headerRect.anchorMax = new Vector2(0.5f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -128f);
        headerRect.sizeDelta = new Vector2(480f, 30f);

        HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.spacing = 12f;

        CreateScoreboardColumn(headerRow.transform, "HeaderName", "NAME", 256f, TextAlignmentOptions.Left, true);
        CreateScoreboardColumn(headerRow.transform, "HeaderKills", "KILLS", 84f, TextAlignmentOptions.Center, true);
        CreateScoreboardColumn(headerRow.transform, "HeaderStatus", "STATUS", 104f, TextAlignmentOptions.Center, true);
    }

    private void CreateScoreboardRows(Transform panelTransform)
    {
        GameObject rowsRoot = new GameObject("ScoreboardRows");
        rowsRoot.transform.SetParent(panelTransform, false);
        RectTransform rowsRect = rowsRoot.AddComponent<RectTransform>();
        rowsRect.anchorMin = new Vector2(0.5f, 1f);
        rowsRect.anchorMax = new Vector2(0.5f, 1f);
        rowsRect.pivot = new Vector2(0.5f, 1f);
        rowsRect.anchoredPosition = new Vector2(0f, -168f);
        rowsRect.sizeDelta = new Vector2(480f, 320f);

        // English comment: VerticalLayoutGroup keeps each leaderboard row aligned automatically.
        VerticalLayoutGroup layout = rowsRoot.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);

        scoreboardRows = new ScoreboardRowUi[5];
        for (int i = 0; i < scoreboardRows.Length; i++)
        {
            GameObject rowObject = new GameObject($"ScoreboardRow_{i + 1}");
            rowObject.transform.SetParent(rowsRoot.transform, false);

            RectTransform rowRect = rowObject.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(480f, 52f);

            LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 52f;

            Image rowBg = rowObject.AddComponent<Image>();
            rowBg.color = i % 2 == 0 ? new Color(1f, 1f, 1f, 0.035f) : new Color(1f, 1f, 1f, 0.015f);

            HorizontalLayoutGroup rowHorizontal = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowHorizontal.childAlignment = TextAnchor.MiddleLeft;
            rowHorizontal.childControlHeight = true;
            rowHorizontal.childControlWidth = true;
            rowHorizontal.childForceExpandHeight = true;
            rowHorizontal.childForceExpandWidth = false;
            rowHorizontal.spacing = 12f;
            rowHorizontal.padding = new RectOffset(12, 12, 0, 0);

            scoreboardRows[i] = new ScoreboardRowUi
            {
                Root = rowObject,
                NameText = CreateScoreboardColumn(rowObject.transform, $"RowName_{i + 1}", $"{i + 1}. ---", 256f, TextAlignmentOptions.Left, false),
                KillsText = CreateScoreboardColumn(rowObject.transform, $"RowKills_{i + 1}", "0", 84f, TextAlignmentOptions.Center, false),
                StatusText = CreateScoreboardColumn(rowObject.transform, $"RowStatus_{i + 1}", "---", 104f, TextAlignmentOptions.Center, false)
            };
        }
    }

    private void CacheScoreboardRows()
    {
        Transform rowsRoot = scoreboardOverlay != null
            ? scoreboardOverlay.transform.Find("ScoreboardPanel/ScoreboardRows")
            : null;
        if (rowsRoot == null)
            return;

        scoreboardRows = new ScoreboardRowUi[5];
        for (int i = 0; i < scoreboardRows.Length; i++)
        {
            Transform row = rowsRoot.Find($"ScoreboardRow_{i + 1}");
            if (row == null)
                continue;

            scoreboardRows[i] = new ScoreboardRowUi
            {
                Root = row.gameObject,
                NameText = row.Find($"RowName_{i + 1}")?.GetComponent<TextMeshProUGUI>(),
                KillsText = row.Find($"RowKills_{i + 1}")?.GetComponent<TextMeshProUGUI>(),
                StatusText = row.Find($"RowStatus_{i + 1}")?.GetComponent<TextMeshProUGUI>()
            };
        }
    }

    private TextMeshProUGUI CreateScoreboardColumn(Transform parent, string name, string text, float width, TextAlignmentOptions alignment, bool isHeader)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = isHeader ? 17f : 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.color = isHeader ? new Color(0.50f, 0.75f, 1f, 0.80f) : Color.white;
        if (prismFont != null)
            tmp.font = prismFont;
        return tmp;
    }

    private static void ApplyScoreboardRowStyle(ScoreboardRowUi row, bool isPlayer, bool isAlive, bool isEmpty)
    {
        Color nameColor = isEmpty
            ? new Color(0.45f, 0.55f, 0.68f, 0.55f)
            : isPlayer
                ? new Color(1f, 0.84f, 0.34f, 1f)
                : new Color(0.92f, 0.97f, 1f, isAlive ? 1f : 0.60f);

        Color valueColor = isEmpty
            ? new Color(0.45f, 0.55f, 0.68f, 0.55f)
            : new Color(0.92f, 0.97f, 1f, isAlive ? 0.95f : 0.60f);

        Color statusColor = isEmpty
            ? new Color(0.45f, 0.55f, 0.68f, 0.55f)
            : isAlive
                ? new Color(0.54f, 0.95f, 0.64f, 0.95f)
                : new Color(1f, 0.32f, 0.32f, 0.92f);

        if (row.NameText != null) row.NameText.color = nameColor;
        if (row.KillsText != null) row.KillsText.color = valueColor;
        if (row.StatusText != null) row.StatusText.color = statusColor;
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

    /// <summary>
    /// Called by NetworkPlayerSpawner after the local Photon player is confirmed
    /// spawned. Wires HUD to the correct local player, hides any stale overlays,
    /// locks the cursor, and registers the combatant in MatchStatsManager.
    /// </summary>
    public void InitForMultiplayerLocalPlayer(PlayerController pc, PlayerHealth ph)
    {
        playerController = pc;
        playerHealth = ph;

        // Ensure scoreboard is hidden during active gameplay.
        if (scoreboardOverlay != null)
        {
            scoreboardOverlay.SetActive(false);
            isScoreboardVisible = false;
        }

        // Destroy any stale match-finished overlay that may have carried over.
        if (matchFinishedOverlay != null)
        {
            Destroy(matchFinishedOverlay);
            matchFinishedOverlay = null;
            matchFinished = false;
        }

        // Lock cursor so the player can look around immediately.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Register local player in leaderboard with confirmed identity.
        if (MatchStatsManager.Instance != null && ph != null)
        {
            string playerId = MatchStatsManager.BuildCombatantId(ph);
            string playerLabel = PlayerProfile.HasUsername ? PlayerProfile.Username : "YOU";
            MatchStatsManager.Instance.RegisterCombatant(playerId, playerLabel, isPlayer: true, transform: ph.transform);
            MatchStatsManager.Instance.StatsChanged -= HandleMatchStatsChanged;
            MatchStatsManager.Instance.StatsChanged += HandleMatchStatsChanged;
        }

        if (pc != null && weaponText != null)
            weaponText.text = pc.equippedWeaponName.ToUpperInvariant();

        if (ph != null)
            UpdateHealth(ph.currentHealth, ph.maxHealth);

        Debug.Log("[MPFlow] local player ready");
        Debug.Log("[MPFlow] gameplay state active");
        Debug.Log("[MPFlow] hiding match stats");
        Debug.Log("[MPFlow] input enabled");
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
