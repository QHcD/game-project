using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    static readonly Color PanelFill = new Color(0.05f, 0.08f, 0.18f, 0.95f);
    static readonly Color TabNormalBlue = new Color(0.10f, 0.26f, 0.48f, 0.62f);
    static readonly Color TabSelectedFill = new Color(0.12f, 0.62f, 1f, 0.92f);
    static readonly Color TabNormalOutline = new Color(0.22f, 0.52f, 0.92f, 0.42f);
    static readonly Color TabSelectedOutline = new Color(0.45f, 0.92f, 1f, 0.96f);

    readonly string[] graphicsOptions = { "LOW", "MEDIUM", "HIGH", "ULTRA" };
    readonly string[] difficultyOptions = { "EASY", "NORMAL", "HARD" };
    readonly string[] perspectiveOptions = { "THIRD PERSON" };
    readonly string[] controlOptions = { "WASD + MOUSE", "ARROWS + MOUSE" };
    readonly string[] fpsLimitOptions = { "30", "60", "120", "UNLIMITED" };
    readonly int[] fpsLimitValues = { 30, 60, 120, -1 };
    readonly string[] sprintModeOptions = { "HOLD", "TOGGLE" };

    Vector2Int[] resolutionOptions;
    int currentResIndex;
    int currentGraphicsIndex;
    int currentFpsLimitIndex;
    bool isFullscreen;
    bool isVSync;
    bool isMuteAll;
    bool isInvertY;
    bool minimapEnabled;
    bool damageNumbersEnabled;
    bool tutorialTipsEnabled;
    int sprintModeIndex;

    GameObject canvasObj;
    GameObject panelObj;
    RectTransform tabContentBody;
    readonly List<Button> tabButtons = new List<Button>(4);
    readonly List<Image> tabButtonImages = new List<Image>(4);
    readonly List<Outline> tabButtonOutlines = new List<Outline>(4);
    readonly List<GameObject> tabRoots = new List<GameObject>(4);
    int selectedTab;

    Slider masterSlider;
    Slider musicSlider;
    Slider sfxSlider;
    Slider uiSlider;
    Slider brightnessSlider;
    Slider hudScaleSlider;
    Slider screenShakeSlider;
    TextMeshProUGUI masterVolumeValueLabel;
    TextMeshProUGUI musicVolumeValueLabel;
    TextMeshProUGUI sfxVolumeValueLabel;
    TextMeshProUGUI uiVolumeValueLabel;
    TextMeshProUGUI brightnessValueLabel;
    TextMeshProUGUI hudScaleValueLabel;
    TextMeshProUGUI screenShakeValueLabel;

    Button resolutionButton;
    Button graphicsButton;
    Button fullscreenButton;
    Button vsyncButton;
    Button fpsLimitButton;
    Button muteAllButton;
    Button testSfxButton;
    Button controlButton;
    Button invertYButton;
    Button sprintModeButton;
    Button difficultyButton;
    Button perspectiveButton;
    Button minimapButton;
    Button damageNumbersButton;
    Button tutorialTipsButton;
    Button playerNameSaveButton;
    TextMeshProUGUI resolutionValueLabel;
    TextMeshProUGUI graphicsValueLabel;
    TextMeshProUGUI fullscreenValueLabel;
    TextMeshProUGUI vsyncValueLabel;
    TextMeshProUGUI fpsLimitValueLabel;
    TextMeshProUGUI muteAllValueLabel;
    TextMeshProUGUI controlValueLabel;
    TextMeshProUGUI invertYValueLabel;
    TextMeshProUGUI sprintModeValueLabel;
    TextMeshProUGUI difficultyValueLabel;
    TextMeshProUGUI perspectiveValueLabel;
    TextMeshProUGUI minimapValueLabel;
    TextMeshProUGUI damageNumbersValueLabel;
    TextMeshProUGUI tutorialTipsValueLabel;
    // Kept for backwards compatibility with older layouts; not used anymore.
    TextMeshProUGUI playerNameStatusLabel;

    Slider sensitivitySlider;
    TextMeshProUGUI sensitivityValueLabel;
    TMP_InputField playerNameInput;

    Button returnBtn;
    Button resetBtn;

    TextMeshProUGUI globalSavedLabel;
    CanvasGroup globalSavedCg;
    Coroutine globalSavedRoutine;

    void Start()
    {
        prismFont = ResolvePrismFont();
        EnsureEventSystem();
        LoadSettingsData();
        BuildSettingsMenu();
    }

    void EnsureEventSystem()
    {
        UIManager.EnsureInputSystemEventSystem();
    }

    void LoadSettingsData()
    {
        resolutionOptions = BuildResolutionOptions();
        currentResIndex = Mathf.Clamp(PlayerPrefs.GetInt("ResIndex", resolutionOptions.Length - 1), 0, resolutionOptions.Length - 1);
        currentGraphicsIndex = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, graphicsOptions.Length - 1);
        currentFpsLimitIndex = FpsLimitIndex();
        isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        isVSync = PlayerPrefs.GetInt(SettingsManager.VSyncKey, 1) == 1;
        isMuteAll = PlayerPrefs.GetInt(AudioSettingsRuntime.MuteAllKey, 0) == 1;
        isInvertY = PlayerPrefs.GetInt("InvertY", 0) == 1;
        sprintModeIndex = Mathf.Clamp(PlayerPrefs.GetInt("SprintMode", 0), 0, sprintModeOptions.Length - 1);
        minimapEnabled = PlayerPrefs.GetInt("MinimapEnabled", 1) == 1;
        damageNumbersEnabled = PlayerPrefs.GetInt("DamageNumbersEnabled", 1) == 1;
        tutorialTipsEnabled = PlayerPrefs.GetInt("TutorialTipsEnabled", 1) == 1;

        ApplyGraphicsQuality();
        Screen.fullScreen = isFullscreen;
        QualitySettings.vSyncCount = isVSync ? 1 : 0;
        Application.targetFrameRate = isVSync ? -1 : fpsLimitValues[currentFpsLimitIndex];
        BrightnessRuntime.ApplyNow(PlayerPrefs.GetFloat(SettingsManager.BrightnessKey, 1f));
        AudioSettingsRuntime.ApplyListenerVolume();
    }

    Vector2Int[] BuildResolutionOptions()
    {
        List<Vector2Int> options = new List<Vector2Int>
        {
            new Vector2Int(640, 480),
            new Vector2Int(800, 600),
            new Vector2Int(1024, 768),
            new Vector2Int(1280, 720),
            new Vector2Int(1366, 768),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080)
        };

        Vector2Int current = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        if (!options.Contains(current))
            options.Add(current);

        return options.ToArray();
    }

    void BuildSettingsMenu()
    {
        GameObject existing = GameObject.Find("Prism7Canvas_Settings");
        if (existing != null)
            Destroy(existing);

        canvasObj = new GameObject("Prism7Canvas_Settings");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        Image bg = new GameObject("Background").AddComponent<Image>();
        bg.transform.SetParent(canvasObj.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.color = new Color(0.03f, 0.04f, 0.08f, 1f);
        if (prismBackground != null)
        {
            bg.sprite = prismBackground;
            bg.color = Color.white;
        }

        Image overlay = new GameObject("Overlay").AddComponent<Image>();
        overlay.transform.SetParent(canvasObj.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.22f);

        panelObj = new GameObject("CentralPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panel = panelObj.AddComponent<Image>();
        Outline outline = panelObj.AddComponent<Outline>();
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(1100f, 700f), new Vector2(0f, 6f));
        PrismOrganizedMenuChrome.ApplyPanelSurface(panel, outline);
        panel.color = PanelFill;

        CanvasGroup panelCg = panelObj.AddComponent<CanvasGroup>();
        panelCg.alpha = 0f;
        UIAnimationHelper.FadeIn(panelCg, 0.24f);

        MakeText(panelObj.transform, "SETTINGS", 60, new Color(0.82f, 0.88f, 1f, 1f),
            new Vector2(0f, 302f), new Vector2(820f, 76f), true, TextAlignmentOptions.Center);

        BuildTabBar(panelObj.transform);
        tabContentBody = CreateTabBody(panelObj.transform);

        tabRoots.Add(BuildAudioTab(tabContentBody));
        tabRoots.Add(BuildVideoTab(tabContentBody));
        tabRoots.Add(BuildControlsTab(tabContentBody));
        tabRoots.Add(BuildGameplayTab(tabContentBody));

        for (int i = 0; i < tabRoots.Count; i++)
            tabRoots[i].SetActive(i == 0);

        RectTransform footerRt = PrismOrganizedMenuChrome.CreateFooterRow(panelObj.transform);
        returnBtn = PrismOrganizedMenuChrome.AddFooterChipButton(
            footerRt, "RETURN",
            new Color(0.12f, 0.20f, 0.42f, 0.92f), PrismOrganizedMenuChrome.ButtonOutlineBlue,
            () => SceneManager.LoadScene("MainMenu"), prismFont);
        resetBtn = PrismOrganizedMenuChrome.AddFooterChipButton(
            footerRt, "RESET",
            new Color(0.22f, 0.12f, 0.38f, 0.92f), PrismOrganizedMenuChrome.ButtonOutlinePurple,
            ResetSettings, prismFont);

        WireFooterPulse(returnBtn);
        WireFooterPulse(resetBtn);

        // Global confirmation label lives above footer and never overlaps it.
        globalSavedLabel = MakeText(panelObj.transform, string.Empty, 18,
            new Color(0.42f, 0.92f, 1f, 1f), new Vector2(0f, -250f), new Vector2(720f, 24f),
            false, TextAlignmentOptions.Center);
        globalSavedLabel.raycastTarget = false;
        globalSavedCg = globalSavedLabel.gameObject.AddComponent<CanvasGroup>();
        globalSavedCg.alpha = 0f;

        SelectTab(0, false);
        RefreshLinearNavigation();
    }

    void WireFooterPulse(Button b)
    {
        if (b == null) return;
        b.onClick.AddListener(() => UIAnimationHelper.PunchScale(b.transform, 0.12f, 1.06f));
    }

    void BuildTabBar(Transform parent)
    {
        GameObject row = new GameObject("TabBar", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.06f, 0.78f);
        rt.anchorMax = new Vector2(0.94f, 0.90f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 14f;
        h.padding = new RectOffset(8, 8, 6, 6);
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        string[] labels = { "AUDIO", "VIDEO", "CONTROLS", "GAMEPLAY" };
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            Button tabBtn = CreateTabButton(row.transform, labels[i], () => SelectTab(idx, true));
            tabButtons.Add(tabBtn);
            tabButtonImages.Add(tabBtn.GetComponent<Image>());
            tabButtonOutlines.Add(tabBtn.GetComponent<Outline>());
        }
    }

    RectTransform CreateTabBody(Transform parent)
    {
        GameObject body = new GameObject("TabContent", typeof(RectTransform));
        body.transform.SetParent(parent, false);
        RectTransform brt = body.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.05f, 0.11f);
        brt.anchorMax = new Vector2(0.95f, 0.76f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        return brt;
    }

    Button CreateTabButton(Transform parent, string label, UnityAction onClick)
    {
        GameObject go = new GameObject("Tab_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 52f;
        le.minHeight = 48f;
        le.flexibleWidth = 1f;

        Image img = go.AddComponent<Image>();
        img.color = TabNormalBlue;

        Outline ol = go.AddComponent<Outline>();
        ol.effectColor = TabNormalOutline;
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        TextMeshProUGUI tmp = MakeText(go.transform, label, 23, Color.white,
            Vector2.zero, Vector2.zero, false, TextAlignmentOptions.Center);
        Stretch(tmp.rectTransform);
        tmp.fontStyle = FontStyles.Bold;

        MenuButtonHoverEffect hov = go.AddComponent<MenuButtonHoverEffect>();
        hov.label = tmp;
        hov.background = img;
        hov.normalTextColor = Color.white;
        hov.hoverTextColor = new Color(0.88f, 0.96f, 1f, 1f);
        hov.normalBackgroundColor = TabNormalBlue;
        hov.hoverBackgroundColor = new Color(0.16f, 0.42f, 0.72f, 0.82f);
        hov.hoverScale = new Vector3(1.05f, 1.05f, 1f);
        hov.neonOutline = ol;
        hov.normalOutlineColor = TabNormalOutline;
        hov.hoverOutlineColor = TabSelectedOutline;

        return btn;
    }

    void SelectTab(int index, bool animate)
    {
        selectedTab = Mathf.Clamp(index, 0, tabRoots.Count - 1);
        for (int i = 0; i < tabRoots.Count; i++)
        {
            bool on = i == selectedTab;
            tabRoots[i].SetActive(on);
            bool sel = i == selectedTab;
            if (i < tabButtonImages.Count)
            {
                tabButtonImages[i].color = sel ? TabSelectedFill : TabNormalBlue;
                if (i < tabButtonOutlines.Count)
                {
                    tabButtonOutlines[i].effectColor = sel ? TabSelectedOutline : TabNormalOutline;
                    tabButtonOutlines[i].effectDistance = sel
                        ? new Vector2(2.2f, -2.2f)
                        : new Vector2(1.5f, -1.5f);
                }
                MenuButtonHoverEffect mh = tabButtons[i].GetComponent<MenuButtonHoverEffect>();
                if (mh != null)
                {
                    mh.normalBackgroundColor = sel ? TabSelectedFill : TabNormalBlue;
                    mh.hoverBackgroundColor = sel
                        ? new Color(0.18f, 0.74f, 1f, 1f)
                        : new Color(0.16f, 0.42f, 0.72f, 0.82f);
                    mh.normalOutlineColor = sel ? TabSelectedOutline : TabNormalOutline;
                }
            }
        }

        if (animate && tabRoots.Count > selectedTab)
            StartCoroutine(CoTabContentScale(tabRoots[selectedTab].transform as RectTransform));

        RefreshLinearNavigation();
    }

    IEnumerator CoTabContentScale(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 tgt = rt.localScale;
        rt.localScale = tgt * 0.982f;
        for (float t = 0f; t < 0.14f; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.Lerp(rt.localScale, tgt, 18f * Time.unscaledDeltaTime);
            yield return null;
        }
        rt.localScale = tgt;
    }

    void RefreshLinearNavigation()
    {
        if (canvasObj == null) return;

        MenuNavigationManager oldNav = canvasObj.GetComponent<MenuNavigationManager>();
        if (oldNav != null)
            Destroy(oldNav);

        List<Selectable> nav = new List<Selectable>();
        for (int i = 0; i < tabButtons.Count; i++)
            if (tabButtons[i] != null) nav.Add(tabButtons[i]);

        switch (selectedTab)
        {
            case 0:
                if (masterSlider != null) nav.Add(masterSlider);
                if (musicSlider != null) nav.Add(musicSlider);
                if (sfxSlider != null) nav.Add(sfxSlider);
                if (uiSlider != null) nav.Add(uiSlider);
                if (muteAllButton != null) nav.Add(muteAllButton);
                if (testSfxButton != null) nav.Add(testSfxButton);
                break;
            case 1:
                if (resolutionButton != null) nav.Add(resolutionButton);
                if (graphicsButton != null) nav.Add(graphicsButton);
                if (fullscreenButton != null) nav.Add(fullscreenButton);
                if (vsyncButton != null) nav.Add(vsyncButton);
                if (fpsLimitButton != null) nav.Add(fpsLimitButton);
                if (brightnessSlider != null) nav.Add(brightnessSlider);
                break;
            case 2:
                if (controlButton != null) nav.Add(controlButton);
                if (sensitivitySlider != null) nav.Add(sensitivitySlider);
                if (invertYButton != null) nav.Add(invertYButton);
                if (sprintModeButton != null) nav.Add(sprintModeButton);
                break;
            case 3:
                if (difficultyButton != null) nav.Add(difficultyButton);
                if (perspectiveButton != null) nav.Add(perspectiveButton);
                if (hudScaleSlider != null) nav.Add(hudScaleSlider);
                if (minimapButton != null) nav.Add(minimapButton);
                if (damageNumbersButton != null) nav.Add(damageNumbersButton);
                if (screenShakeSlider != null) nav.Add(screenShakeSlider);
                if (tutorialTipsButton != null) nav.Add(tutorialTipsButton);
                if (playerNameInput != null) nav.Add(playerNameInput);
                if (playerNameSaveButton != null) nav.Add(playerNameSaveButton);
                break;
        }

        if (returnBtn != null) nav.Add(returnBtn);
        if (resetBtn != null) nav.Add(resetBtn);

        MenuNavigationManager.AttachLinear(canvasObj, nav);
    }

    GameObject BuildAudioTab(Transform parent)
    {
        GameObject root = CreateCompactTabRoot(parent, "TabRoot_Audio", 8f);

        masterSlider = MakeSliderRowLayout(root.transform, "MASTER VOLUME:", AudioSettingsRuntime.MasterKey,
            out masterVolumeValueLabel, val => { AudioSettingsRuntime.ApplyListenerVolume(); });
        musicSlider = MakeSliderRowLayout(root.transform, "MUSIC VOLUME:", AudioSettingsRuntime.MusicKey,
            out musicVolumeValueLabel, _ => { AudioSettingsRuntime.RefreshMenuLobbyMusicIfPresent(); });
        sfxSlider = MakeSliderRowLayout(root.transform, "SFX VOLUME:", AudioSettingsRuntime.SfxKey,
            out sfxVolumeValueLabel, null);
        uiSlider = MakeSliderRowLayout(root.transform, "UI SOUND VOLUME:", AudioSettingsRuntime.UiKey,
            out uiVolumeValueLabel, null);
        muteAllButton = MakeCycleButtonRow(root.transform, "MUTE ALL:", OnOffLabel(isMuteAll),
            out muteAllValueLabel, ToggleMuteAll);
        testSfxButton = MakeActionButtonRow(root.transform, "TEST SFX:", "TEST SFX", TestSfx);

        return root;
    }

    GameObject BuildVideoTab(Transform parent)
    {
        GameObject root = CreateCompactTabRoot(parent, "TabRoot_Video", 8f);

        resolutionButton = MakeCycleButtonRow(root.transform, "RESOLUTION:", ResolutionLabel(resolutionOptions[currentResIndex]),
            out resolutionValueLabel, CycleResolution);
        graphicsButton = MakeCycleButtonRow(root.transform, "GRAPHICS:", graphicsOptions[currentGraphicsIndex],
            out graphicsValueLabel, CycleGraphics);
        fullscreenButton = MakeCycleButtonRow(root.transform, "FULLSCREEN:", OnOffLabel(isFullscreen),
            out fullscreenValueLabel, ToggleFullscreen);
        vsyncButton = MakeCycleButtonRow(root.transform, "VSYNC:", OnOffLabel(isVSync),
            out vsyncValueLabel, ToggleVSync);
        fpsLimitButton = MakeCycleButtonRow(root.transform, "FPS LIMIT:", fpsLimitOptions[currentFpsLimitIndex],
            out fpsLimitValueLabel, CycleFpsLimit);
        brightnessSlider = MakePercentSliderRowLayout(root.transform, "BRIGHTNESS:", SettingsManager.BrightnessKey,
            0.5f, 1.25f, 1f, out brightnessValueLabel, val => BrightnessRuntime.ApplyNow(val));

        return root;
    }

    GameObject BuildControlsTab(Transform parent)
    {
        GameObject root = CreateCompactTabRoot(parent, "TabRoot_Controls", 18f);

        controlButton = MakeCycleButtonRow(root.transform, "MOVE STYLE:", controlOptions[ControlIndex()],
            out controlValueLabel, CycleControl);
        sensitivitySlider = MakeSensitivityRowLayout(root.transform);
        invertYButton = MakeCycleButtonRow(root.transform, "INVERT Y:", OnOffLabel(isInvertY),
            out invertYValueLabel, ToggleInvertY);
        sprintModeButton = MakeCycleButtonRow(root.transform, "SPRINT MODE:", sprintModeOptions[sprintModeIndex],
            out sprintModeValueLabel, ToggleSprintMode);

        return root;
    }

    GameObject BuildGameplayTab(Transform parent)
    {
        GameObject root = CreateCompactTabRoot(parent, "TabRoot_Gameplay", 5f);

        difficultyButton = MakeCycleButtonRow(root.transform, "DIFFICULTY:", difficultyOptions[DifficultyIndex()],
            out difficultyValueLabel, CycleDifficulty);
        perspectiveButton = MakeCycleButtonRow(root.transform, "CAMERA VIEW:", perspectiveOptions[0],
            out perspectiveValueLabel, () => OnPerspectiveChanged(0));
        hudScaleSlider = MakePercentSliderRowLayout(root.transform, "HUD SCALE:", "HUDScale",
            0.75f, 1.25f, 1f, out hudScaleValueLabel, null);
        minimapButton = MakeCycleButtonRow(root.transform, "MINIMAP:", OnOffLabel(minimapEnabled),
            out minimapValueLabel, ToggleMinimap);
        damageNumbersButton = MakeCycleButtonRow(root.transform, "DAMAGE NUMBERS:", OnOffLabel(damageNumbersEnabled),
            out damageNumbersValueLabel, ToggleDamageNumbers);
        screenShakeSlider = MakePercentSliderRowLayout(root.transform, "SCREEN SHAKE:", "ScreenShake",
            0f, 1f, 0.8f, out screenShakeValueLabel, null);
        tutorialTipsButton = MakeCycleButtonRow(root.transform, "TUTORIAL TIPS:", OnOffLabel(tutorialTipsEnabled),
            out tutorialTipsValueLabel, ToggleTutorialTips);
        MakePlayerNameRow(root.transform);

        return root;
    }

    GameObject CreateCompactTabRoot(Transform parent, string name, float topPadding)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        VerticalLayoutGroup v = root.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(34, 34, Mathf.RoundToInt(topPadding), 8);
        v.spacing = 5f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return root;
    }

    Slider MakeSliderRowLayout(Transform parent, string label, string prefKey,
        out TextMeshProUGUI valueLabel, UnityAction<float> onChanged)
    {
        GameObject row = CreateLayoutRow(parent, "Row_" + label);
        MakeText(row.transform, label, 22, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-258f, 0f), new Vector2(322f, 42f), false, TextAlignmentOptions.Right);

        GameObject barObj = new GameObject("SliderBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.28f, 0.38f, 0.62f, 0.72f);
        Outline barOl = barObj.AddComponent<Outline>();
        barOl.effectColor = new Color(0.35f, 0.72f, 1f, 0.42f);
        barOl.effectDistance = new Vector2(1f, -1f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(280f, 20f), new Vector2(118f, 0f));

        Slider slider = barObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(barObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = new Vector2(-18f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.22f, 0.74f, 1f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(barObj.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 32f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        float initial = PlayerPrefs.GetFloat(prefKey, 0.8f);
        slider.value = initial;

        valueLabel = MakeText(row.transform, SettingsManager.FormatVolumePercent(initial), 22,
            new Color(0.42f, 0.92f, 1f, 1f), new Vector2(323f, 0f), new Vector2(104f, 42f),
            false, TextAlignmentOptions.Center);
        valueLabel.fontStyle = FontStyles.Bold;

        TextMeshProUGUI volumeReadout = valueLabel;
        slider.onValueChanged.AddListener(val =>
        {
            PlayerPrefs.SetFloat(prefKey, val);
            PlayerPrefs.Save();
            volumeReadout.text = SettingsManager.FormatVolumePercent(val);
            onChanged?.Invoke(val);
        });

        return slider;
    }

    GameObject CreateLayoutRow(Transform parent, string name)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 46f;
        le.minHeight = 44f;
        le.flexibleWidth = 1f;
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 46f);
        return row;
    }

    Button MakeCycleButtonRow(Transform parent, string label, string value, out TextMeshProUGUI valueLabel, UnityAction onClick)
    {
        GameObject row = CreateLayoutRow(parent, "Row_" + label);
        MakeText(row.transform, label, 22, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-258f, 0f), new Vector2(322f, 42f), false, TextAlignmentOptions.Right);

        Button button = CreateValueButton(row.transform, "Btn_" + label, new Vector2(292f, 42f), new Vector2(155f, 0f),
            value, out valueLabel);
        button.onClick.AddListener(onClick);
        button.onClick.AddListener(() => UIAnimationHelper.PunchScale(button.transform, 0.1f, 1.045f));
        return button;
    }

    Button MakeActionButtonRow(Transform parent, string label, string buttonText, UnityAction onClick)
    {
        GameObject row = CreateLayoutRow(parent, "Row_" + label);
        MakeText(row.transform, label, 22, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-258f, 0f), new Vector2(322f, 42f), false, TextAlignmentOptions.Right);

        Button button = CreateValueButton(row.transform, "Btn_" + label, new Vector2(292f, 42f), new Vector2(155f, 0f),
            buttonText, out _);
        button.onClick.AddListener(onClick);
        button.onClick.AddListener(() => UIAnimationHelper.PunchScale(button.transform, 0.1f, 1.045f));
        return button;
    }

    Button CreateValueButton(Transform parent, string name, Vector2 size, Vector2 pos, string text, out TextMeshProUGUI label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), size, pos);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.20f, 0.42f, 0.94f);
        Outline ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0.32f, 0.78f, 1f, 0.58f);
        ol.effectDistance = new Vector2(1.2f, -1.2f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;

        label = MakeText(go.transform, text, 21, new Color(0.42f, 0.92f, 1f, 1f),
            Vector2.zero, Vector2.zero, false, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 21f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(label.rectTransform);

        MenuButtonHoverEffect h = go.AddComponent<MenuButtonHoverEffect>();
        h.label = label;
        h.background = img;
        h.normalTextColor = label.color;
        h.hoverTextColor = Color.white;
        h.normalBackgroundColor = img.color;
        h.hoverBackgroundColor = new Color(0.18f, 0.38f, 0.72f, 1f);
        h.hoverScale = new Vector3(1.035f, 1.035f, 1f);
        h.neonOutline = ol;
        h.normalOutlineColor = ol.effectColor;
        h.hoverOutlineColor = TabSelectedOutline;

        return button;
    }

    Slider MakePercentSliderRowLayout(Transform parent, string label, string prefKey, float minValue, float maxValue,
        float defaultValue, out TextMeshProUGUI valueLabel, UnityAction<float> onChanged)
    {
        GameObject row = CreateLayoutRow(parent, "Row_" + label);
        MakeText(row.transform, label, 22, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-258f, 0f), new Vector2(322f, 42f), false, TextAlignmentOptions.Right);

        GameObject barObj = new GameObject("SliderBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.28f, 0.38f, 0.62f, 0.72f);
        Outline barOl = barObj.AddComponent<Outline>();
        barOl.effectColor = new Color(0.35f, 0.72f, 1f, 0.42f);
        barOl.effectDistance = new Vector2(1f, -1f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(280f, 20f), new Vector2(118f, 0f));

        Slider slider = barObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(barObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = new Vector2(-18f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.22f, 0.74f, 1f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(barObj.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 32f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        float initial = Mathf.Clamp(PlayerPrefs.GetFloat(prefKey, defaultValue), minValue, maxValue);
        slider.value = initial;

        valueLabel = MakeText(row.transform, PercentFromRange(initial, minValue, maxValue), 22,
            new Color(0.42f, 0.92f, 1f, 1f), new Vector2(323f, 0f), new Vector2(104f, 42f),
            false, TextAlignmentOptions.Center);
        valueLabel.fontStyle = FontStyles.Bold;

        TextMeshProUGUI readout = valueLabel;
        slider.onValueChanged.AddListener(val =>
        {
            PlayerPrefs.SetFloat(prefKey, val);
            PlayerPrefs.Save();
            readout.text = PercentFromRange(val, minValue, maxValue);
            onChanged?.Invoke(val);
        });

        return slider;
    }

    void MakePlayerNameRow(Transform parent)
    {
        GameObject row = CreateLayoutRow(parent, "Row_PLAYER_NAME");
        MakeText(row.transform, "PLAYER NAME:", 22, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-258f, 0f), new Vector2(322f, 42f), false, TextAlignmentOptions.Right);

        GameObject inputObj = new GameObject("PlayerNameInput", typeof(RectTransform));
        inputObj.transform.SetParent(row.transform, false);
        SetRect(inputObj.GetComponent<RectTransform>(), new Vector2(178f, 42f), new Vector2(64f, 0f));
        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.07f, 0.12f, 0.28f, 0.94f);
        Outline inputOutline = inputObj.AddComponent<Outline>();
        inputOutline.effectColor = new Color(0.32f, 0.78f, 1f, 0.42f);
        inputOutline.effectDistance = new Vector2(1f, -1f);

        playerNameInput = inputObj.AddComponent<TMP_InputField>();
        PlayerProfile.Reload();
        string savedName = PlayerProfile.Username;

        TextMeshProUGUI text = MakeText(inputObj.transform, savedName,
            20, new Color(0.88f, 0.93f, 1f, 1f), Vector2.zero, Vector2.zero, false, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        TextMeshProUGUI placeholder = MakeText(inputObj.transform, PlayerProfile.DefaultUsername, 20, new Color(0.45f, 0.55f, 0.72f, 0.72f),
            Vector2.zero, Vector2.zero, false, TextAlignmentOptions.Center);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        playerNameInput.textComponent = text;
        playerNameInput.placeholder = placeholder;
        playerNameInput.text = savedName;
        playerNameInput.characterLimit = PlayerProfile.MaxNameLength;

        playerNameSaveButton = CreateValueButton(row.transform, "Btn_SAVE_PLAYER_NAME",
            new Vector2(166f, 42f), new Vector2(268f, 0f), "SAVE CHANGES", out _);
        playerNameSaveButton.onClick.AddListener(SavePlayerName);
        playerNameSaveButton.onClick.AddListener(() => UIAnimationHelper.PunchScale(playerNameSaveButton.transform, 0.1f, 1.045f));

        // Per-row status label removed: we now show a global confirmation above the footer
        // to guarantee it never overlaps Return/Reset buttons.
    }


    Slider MakeSensitivityRowLayout(Transform parent)
    {
        GameObject row = CreateLayoutRow(parent, "Row_SENSITIVITY");
        MakeText(row.transform, "MOUSE SENSITIVITY:", 22, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-258f, 0f), new Vector2(322f, 42f), false, TextAlignmentOptions.Right);

        GameObject barObj = new GameObject("SensitivityBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.28f, 0.38f, 0.62f, 0.72f);
        Outline bo = barObj.AddComponent<Outline>();
        bo.effectColor = new Color(0.35f, 0.72f, 1f, 0.42f);
        bo.effectDistance = new Vector2(1f, -1f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(280f, 20f), new Vector2(118f, 0f));

        Slider slider = barObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = LookSensitivityRuntime.MinSlider;
        slider.maxValue = LookSensitivityRuntime.MaxSlider;
        slider.wholeNumbers = false;

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(barObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = new Vector2(-18f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.22f, 0.74f, 1f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(barObj.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 32f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        sensitivityValueLabel = MakeText(row.transform, "3.5", 22,
            new Color(0.42f, 0.92f, 1f, 1f), new Vector2(323f, 0f), new Vector2(104f, 42f),
            false, TextAlignmentOptions.Center);
        sensitivityValueLabel.fontStyle = FontStyles.Bold;

        LookSensitivityRuntime.LoadFromPrefs();
        slider.SetValueWithoutNotify(LookSensitivityRuntime.SliderValue);
        sensitivityValueLabel.text = LookSensitivityRuntime.SliderValue.ToString("0.0");

        slider.onValueChanged.AddListener(value =>
        {
            LookSensitivityRuntime.SetSliderValue(value, persist: true);
            sensitivityValueLabel.text = value.ToString("0.0");
        });

        return slider;
    }

    void CycleResolution()
    {
        currentResIndex = (currentResIndex + 1) % resolutionOptions.Length;
        if (resolutionValueLabel != null)
            resolutionValueLabel.text = ResolutionLabel(resolutionOptions[currentResIndex]);
        ApplySettings();
    }

    void CycleGraphics()
    {
        currentGraphicsIndex = (currentGraphicsIndex + 1) % graphicsOptions.Length;
        if (graphicsValueLabel != null)
            graphicsValueLabel.text = graphicsOptions[currentGraphicsIndex];
        ApplySettings();
    }

    void ToggleFullscreen()
    {
        isFullscreen = !isFullscreen;
        if (fullscreenValueLabel != null)
            fullscreenValueLabel.text = OnOffLabel(isFullscreen);
        ApplySettings();
    }

    void ToggleVSync()
    {
        isVSync = !isVSync;
        if (vsyncValueLabel != null)
            vsyncValueLabel.text = OnOffLabel(isVSync);
        ApplySettings();
    }

    void CycleFpsLimit()
    {
        currentFpsLimitIndex = (currentFpsLimitIndex + 1) % fpsLimitOptions.Length;
        if (fpsLimitValueLabel != null)
            fpsLimitValueLabel.text = fpsLimitOptions[currentFpsLimitIndex];
        ApplySettings();
    }

    void ToggleMuteAll()
    {
        isMuteAll = !isMuteAll;
        PlayerPrefs.SetInt(AudioSettingsRuntime.MuteAllKey, isMuteAll ? 1 : 0);
        PlayerPrefs.Save();
        if (muteAllValueLabel != null)
            muteAllValueLabel.text = OnOffLabel(isMuteAll);
        AudioSettingsRuntime.ApplyListenerVolume();
    }

    void TestSfx()
    {
        AudioSettingsRuntime.ApplyListenerVolume();
    }

    void CycleControl()
    {
        int next = (ControlIndex() + 1) % controlOptions.Length;
        if (controlValueLabel != null)
            controlValueLabel.text = controlOptions[next];
        OnControlChanged(next);
    }

    void ToggleInvertY()
    {
        isInvertY = !isInvertY;
        PlayerPrefs.SetInt("InvertY", isInvertY ? 1 : 0);
        PlayerPrefs.Save();
        if (invertYValueLabel != null)
            invertYValueLabel.text = OnOffLabel(isInvertY);
    }

    void ToggleSprintMode()
    {
        sprintModeIndex = (sprintModeIndex + 1) % sprintModeOptions.Length;
        PlayerPrefs.SetInt("SprintMode", sprintModeIndex);
        PlayerPrefs.Save();
        if (sprintModeValueLabel != null)
            sprintModeValueLabel.text = sprintModeOptions[sprintModeIndex];
    }

    void CycleDifficulty()
    {
        int next = (DifficultyIndex() + 1) % difficultyOptions.Length;
        if (difficultyValueLabel != null)
            difficultyValueLabel.text = difficultyOptions[next];
        OnDifficultyChanged(next);
    }

    void ToggleMinimap()
    {
        minimapEnabled = !minimapEnabled;
        PlayerPrefs.SetInt("MinimapEnabled", minimapEnabled ? 1 : 0);
        PlayerPrefs.Save();
        if (minimapValueLabel != null)
            minimapValueLabel.text = OnOffLabel(minimapEnabled);
    }

    void ToggleDamageNumbers()
    {
        damageNumbersEnabled = !damageNumbersEnabled;
        PlayerPrefs.SetInt("DamageNumbersEnabled", damageNumbersEnabled ? 1 : 0);
        PlayerPrefs.Save();
        if (damageNumbersValueLabel != null)
            damageNumbersValueLabel.text = OnOffLabel(damageNumbersEnabled);
    }

    void ToggleTutorialTips()
    {
        tutorialTipsEnabled = !tutorialTipsEnabled;
        PlayerPrefs.SetInt("TutorialTipsEnabled", tutorialTipsEnabled ? 1 : 0);
        PlayerPrefs.Save();
        if (tutorialTipsValueLabel != null)
            tutorialTipsValueLabel.text = OnOffLabel(tutorialTipsEnabled);
    }

    void SavePlayerName()
    {
        string raw = playerNameInput != null ? playerNameInput.text : string.Empty;
        string cleaned = PlayerProfile.Sanitize(raw);
        if (string.IsNullOrEmpty(cleaned))
            cleaned = PlayerProfile.DefaultUsername;

        PlayerProfile.SetUsername(cleaned); // persists to PlayerPrefs.PlayerName

        if (playerNameInput != null)
            playerNameInput.SetTextWithoutNotify(cleaned);

        ShowGlobalSaved("Changes Saved");
    }

    void ShowGlobalSaved(string message)
    {
        if (globalSavedLabel == null || globalSavedCg == null) return;
        if (globalSavedRoutine != null) StopCoroutine(globalSavedRoutine);
        globalSavedRoutine = StartCoroutine(CoGlobalSaved(message));
    }

    IEnumerator CoGlobalSaved(string message)
    {
        globalSavedLabel.text = message ?? string.Empty;
        globalSavedCg.alpha = 0f;

        // Fade in
        for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
        {
            globalSavedCg.alpha = Mathf.Lerp(0f, 1f, t / 0.12f);
            yield return null;
        }
        globalSavedCg.alpha = 1f;

        yield return new WaitForSecondsRealtime(1.1f);

        // Fade out
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            globalSavedCg.alpha = Mathf.Lerp(1f, 0f, t / 0.22f);
            yield return null;
        }
        globalSavedCg.alpha = 0f;
    }


    void ResetSettings()
    {
        currentResIndex = resolutionOptions.Length - 1;
        currentGraphicsIndex = 2;
        currentFpsLimitIndex = 1;
        isFullscreen = true;
        isVSync = true;
        isMuteAll = false;
        isInvertY = false;
        sprintModeIndex = 0;
        minimapEnabled = true;
        damageNumbersEnabled = true;
        tutorialTipsEnabled = true;

        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(0.8f);
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(0.8f);
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(0.8f);
        if (uiSlider != null)
            uiSlider.SetValueWithoutNotify(0.8f);
        if (brightnessSlider != null)
            brightnessSlider.SetValueWithoutNotify(1f);
        if (hudScaleSlider != null)
            hudScaleSlider.SetValueWithoutNotify(1f);
        if (screenShakeSlider != null)
            screenShakeSlider.SetValueWithoutNotify(0.8f);

        PlayerPrefs.SetFloat(AudioSettingsRuntime.MasterKey, 0.8f);
        PlayerPrefs.SetFloat(AudioSettingsRuntime.MusicKey, 0.8f);
        PlayerPrefs.SetFloat(AudioSettingsRuntime.SfxKey, 0.8f);
        PlayerPrefs.SetFloat(AudioSettingsRuntime.UiKey, 0.8f);
        PlayerPrefs.SetInt(AudioSettingsRuntime.MuteAllKey, 0);
        PlayerPrefs.SetInt(SettingsManager.VSyncKey, 1);
        PlayerPrefs.SetInt(SettingsManager.FpsLimitKey, 60);
        PlayerPrefs.SetFloat(SettingsManager.BrightnessKey, 1f);
        PlayerPrefs.SetInt("InvertY", 0);
        PlayerPrefs.SetInt("SprintMode", 0);
        PlayerPrefs.SetFloat("HUDScale", 1f);
        PlayerPrefs.SetInt("MinimapEnabled", 1);
        PlayerPrefs.SetInt("DamageNumbersEnabled", 1);
        PlayerPrefs.SetFloat("ScreenShake", 0.8f);
        PlayerPrefs.SetInt("TutorialTipsEnabled", 1);
        // Do not wipe the player's name on reset.
        PlayerPrefs.Save();
        PlayerProfile.Reload();
        AudioSettingsRuntime.ApplyListenerVolume();

        RefreshVolumeValueLabels();
        RefreshCompactValueLabels();

        ApplySettings();
        OnDifficultyChanged(1);
        OnPerspectiveChanged(0);
        OnControlChanged(0);
        RefreshCompactValueLabels();

        if (sensitivitySlider != null)
        {
            sensitivitySlider.SetValueWithoutNotify(LookSensitivityRuntime.DefaultSlider);
            if (sensitivityValueLabel != null)
                sensitivityValueLabel.text = LookSensitivityRuntime.DefaultSlider.ToString("0.0");
            LookSensitivityRuntime.SetSliderValue(LookSensitivityRuntime.DefaultSlider, persist: true);
        }

        RefreshLinearNavigation();
    }

    void RefreshVolumeValueLabels()
    {
        if (masterVolumeValueLabel != null && masterSlider != null)
            masterVolumeValueLabel.text = SettingsManager.FormatVolumePercent(masterSlider.value);
        if (musicVolumeValueLabel != null && musicSlider != null)
            musicVolumeValueLabel.text = SettingsManager.FormatVolumePercent(musicSlider.value);
        if (sfxVolumeValueLabel != null && sfxSlider != null)
            sfxVolumeValueLabel.text = SettingsManager.FormatVolumePercent(sfxSlider.value);
        if (uiVolumeValueLabel != null && uiSlider != null)
            uiVolumeValueLabel.text = SettingsManager.FormatVolumePercent(uiSlider.value);
    }

    void RefreshCompactValueLabels()
    {
        if (resolutionValueLabel != null)
            resolutionValueLabel.text = ResolutionLabel(resolutionOptions[currentResIndex]);
        if (graphicsValueLabel != null)
            graphicsValueLabel.text = graphicsOptions[currentGraphicsIndex];
        if (fullscreenValueLabel != null)
            fullscreenValueLabel.text = OnOffLabel(isFullscreen);
        if (vsyncValueLabel != null)
            vsyncValueLabel.text = OnOffLabel(isVSync);
        if (fpsLimitValueLabel != null)
            fpsLimitValueLabel.text = fpsLimitOptions[currentFpsLimitIndex];
        if (muteAllValueLabel != null)
            muteAllValueLabel.text = OnOffLabel(isMuteAll);
        if (brightnessValueLabel != null && brightnessSlider != null)
            brightnessValueLabel.text = PercentFromRange(brightnessSlider.value, brightnessSlider.minValue, brightnessSlider.maxValue);
        if (controlValueLabel != null)
            controlValueLabel.text = controlOptions[ControlIndex()];
        if (invertYValueLabel != null)
            invertYValueLabel.text = OnOffLabel(isInvertY);
        if (sprintModeValueLabel != null)
            sprintModeValueLabel.text = sprintModeOptions[sprintModeIndex];
        if (difficultyValueLabel != null)
            difficultyValueLabel.text = difficultyOptions[DifficultyIndex()];
        if (perspectiveValueLabel != null)
            perspectiveValueLabel.text = perspectiveOptions[0];
        if (hudScaleValueLabel != null && hudScaleSlider != null)
            hudScaleValueLabel.text = PercentFromRange(hudScaleSlider.value, hudScaleSlider.minValue, hudScaleSlider.maxValue);
        if (minimapValueLabel != null)
            minimapValueLabel.text = OnOffLabel(minimapEnabled);
        if (damageNumbersValueLabel != null)
            damageNumbersValueLabel.text = OnOffLabel(damageNumbersEnabled);
        if (screenShakeValueLabel != null && screenShakeSlider != null)
            screenShakeValueLabel.text = PercentFromRange(screenShakeSlider.value, screenShakeSlider.minValue, screenShakeSlider.maxValue);
        if (tutorialTipsValueLabel != null)
            tutorialTipsValueLabel.text = OnOffLabel(tutorialTipsEnabled);
        if (playerNameInput != null)
        {
            string savedName = PlayerPrefs.GetString(PlayerProfile.PlayerNameKey, PlayerProfile.DefaultUsername).Trim();
            playerNameInput.text = string.IsNullOrEmpty(savedName) ? PlayerProfile.DefaultUsername : savedName;
        }
        if (playerNameStatusLabel != null)
            playerNameStatusLabel.text = string.Empty;
    }

    void ApplySettings()
    {
        Vector2Int resolution = resolutionOptions[currentResIndex];
        Screen.SetResolution(resolution.x, resolution.y, isFullscreen);
        ApplyGraphicsQuality();
        QualitySettings.vSyncCount = isVSync ? 1 : 0;
        Application.targetFrameRate = isVSync ? -1 : fpsLimitValues[currentFpsLimitIndex];
        if (brightnessSlider != null)
            BrightnessRuntime.ApplyNow(brightnessSlider.value);

        PlayerPrefs.SetInt("ResIndex", currentResIndex);
        PlayerPrefs.SetInt("GraphicsTier", currentGraphicsIndex);
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(SettingsManager.VSyncKey, isVSync ? 1 : 0);
        PlayerPrefs.SetInt(SettingsManager.FpsLimitKey, fpsLimitValues[currentFpsLimitIndex]);
        PlayerPrefs.Save();
    }

    void ApplyGraphicsQuality()
    {
        int maxQuality = Mathf.Max(0, QualitySettings.names.Length - 1);
        int targetQuality = currentGraphicsIndex == 0 ? 0 :
            currentGraphicsIndex == 1 ? Mathf.Max(0, maxQuality / 2) :
            currentGraphicsIndex == 2 ? Mathf.Max(0, maxQuality - 1) :
            maxQuality;

        QualitySettings.SetQualityLevel(targetQuality);
    }

    int DifficultyIndex()
    {
        string difficulty = PlayerPrefs.GetString("Difficulty", "Normal");
        if (difficulty == "Easy") return 0;
        if (difficulty == "Hard") return 2;
        return 1;
    }

    int FpsLimitIndex()
    {
        int saved = PlayerPrefs.GetInt(SettingsManager.FpsLimitKey, 60);
        for (int i = 0; i < fpsLimitValues.Length; i++)
            if (fpsLimitValues[i] == saved)
                return i;
        return 1;
    }

    int ControlIndex()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
    }

    string OnOffLabel(bool value)
    {
        return value ? "ON" : "OFF";
    }

    string PercentFromRange(float value, float minValue, float maxValue)
    {
        float normalized = Mathf.InverseLerp(minValue, maxValue, value);
        return Mathf.RoundToInt(normalized * 100f) + "%";
    }

    void OnDifficultyChanged(int selectedIndex)
    {
        string difficulty = selectedIndex == 0 ? "Easy" : selectedIndex == 2 ? "Hard" : "Normal";
        if (GameManager.Instance != null)
            GameManager.Instance.SetDifficulty(difficulty);
        else
        {
            PlayerPrefs.SetString("Difficulty", difficulty);
            PlayerPrefs.Save();
        }
    }

    void OnPerspectiveChanged(int selectedIndex)
    {
        GameManager.PerspectiveMode perspective = GameManager.PerspectiveMode.ThirdPerson;

        if (GameManager.Instance != null)
            GameManager.Instance.SetPerspectiveMode(perspective);
        else
        {
            PlayerPrefs.SetInt("PerspectiveMode", (int)perspective);
            PlayerPrefs.Save();
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.RefreshGameplayPreferences();
    }

    void OnControlChanged(int selectedIndex)
    {
        GameManager.MovementScheme scheme = selectedIndex == 1
            ? GameManager.MovementScheme.ArrowKeys
            : GameManager.MovementScheme.Wasd;

        if (GameManager.Instance != null)
            GameManager.Instance.SetMovementScheme(scheme);
        else
        {
            PlayerPrefs.SetInt("MovementScheme", (int)scheme);
            PlayerPrefs.Save();
        }
    }

    TextMeshProUGUI MakeText(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 sizeDelta, bool addOutline, TextAlignmentOptions align)
    {
        string shortKey = text.Length <= 24 ? text : text.Substring(0, 24);
        GameObject txtObj = new GameObject("Txt_" + shortKey.GetHashCode());
        txtObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        if (prismFont != null)
            tmp.font = prismFont;
        if (addOutline)
        {
            tmp.fontStyle = FontStyles.Bold;
            Outline outline = txtObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.12f, 0.42f, 1f);
        }

        RectTransform rt = txtObj.GetComponent<RectTransform>();
        if (parent is RectTransform && sizeDelta != Vector2.zero)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
        }
        else
        {
            Stretch(rt);
        }
        return tmp;
    }

    string ResolutionLabel(Vector2Int resolution)
    {
        return resolution.x + " x " + resolution.y;
    }

    void SetRect(RectTransform rect, Vector2 size, Vector2 pos)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    TMP_FontAsset ResolvePrismFont()
    {
        if (prismFont != null)
            return prismFont;

        TMP_FontAsset lib = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (lib != null)
            return lib;

        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            TMP_FontAsset font = fonts[i];
            if (font != null && (font.name.Contains("Arizona") || font.name.Contains("Azonix")))
                return font;
        }

        return TMP_Settings.defaultFontAsset;
    }
}
