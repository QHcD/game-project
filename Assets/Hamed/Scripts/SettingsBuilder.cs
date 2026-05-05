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

    readonly string[] graphicsOptions = { "LOW", "MEDIUM", "HIGH" };
    readonly string[] difficultyOptions = { "EASY", "NORMAL", "HARD" };
    readonly string[] perspectiveOptions = { "THIRD PERSON" };
    readonly string[] controlOptions = { "WASD + MOUSE", "ARROWS + MOUSE" };

    Vector2Int[] resolutionOptions;
    int currentResIndex;
    int currentGraphicsIndex;
    bool isFullscreen;

    GameObject canvasObj;
    GameObject panelObj;
    RectTransform tabContentBody;
    readonly List<Button> tabButtons = new List<Button>(4);
    readonly List<Image> tabButtonImages = new List<Image>(4);
    readonly List<Outline> tabButtonOutlines = new List<Outline>(4);
    readonly List<GameObject> tabRoots = new List<GameObject>(4);
    int selectedTab;

    TMP_Dropdown resolutionDropdown;
    TMP_Dropdown graphicsDropdown;
    Toggle fullscreenToggle;
    Slider masterSlider;
    Slider musicSlider;
    Slider sfxSlider;
    TextMeshProUGUI masterVolumeValueLabel;
    TextMeshProUGUI musicVolumeValueLabel;
    TextMeshProUGUI sfxVolumeValueLabel;

    TMP_Dropdown difficultyDropdown;
    TMP_Dropdown perspectiveDropdown;
    TMP_Dropdown controlDropdown;
    Slider sensitivitySlider;
    TextMeshProUGUI sensitivityValueLabel;

    Button returnBtn;
    Button resetBtn;

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
        isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        ApplyGraphicsQuality();
        Screen.fullScreen = isFullscreen;
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
                break;
            case 1:
                if (resolutionDropdown != null) nav.Add(resolutionDropdown);
                if (graphicsDropdown != null) nav.Add(graphicsDropdown);
                if (fullscreenToggle != null) nav.Add(fullscreenToggle);
                break;
            case 2:
                if (controlDropdown != null) nav.Add(controlDropdown);
                if (sensitivitySlider != null) nav.Add(sensitivitySlider);
                break;
            case 3:
                if (difficultyDropdown != null) nav.Add(difficultyDropdown);
                if (perspectiveDropdown != null) nav.Add(perspectiveDropdown);
                break;
        }

        if (returnBtn != null) nav.Add(returnBtn);
        if (resetBtn != null) nav.Add(resetBtn);

        MenuNavigationManager.AttachLinear(canvasObj, nav);
    }

    GameObject BuildAudioTab(Transform parent)
    {
        GameObject root = new GameObject("TabRoot_Audio", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        VerticalLayoutGroup v = root.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(40, 40, 18, 18);
        v.spacing = 26f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        masterSlider = MakeSliderRowLayout(root.transform, "MASTER VOLUME:", AudioSettingsRuntime.MasterKey,
            out masterVolumeValueLabel, val => { AudioSettingsRuntime.ApplyListenerVolume(); });
        musicSlider = MakeSliderRowLayout(root.transform, "MUSIC VOLUME:", AudioSettingsRuntime.MusicKey,
            out musicVolumeValueLabel, _ => { AudioSettingsRuntime.RefreshMenuLobbyMusicIfPresent(); });
        sfxSlider = MakeSliderRowLayout(root.transform, "SFX VOLUME:", AudioSettingsRuntime.SfxKey,
            out sfxVolumeValueLabel, null);

        return root;
    }

    GameObject BuildVideoTab(Transform parent)
    {
        GameObject root = new GameObject("TabRoot_Video", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        VerticalLayoutGroup v = root.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(40, 40, 18, 18);
        v.spacing = 26f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        resolutionDropdown = MakeDropdownRowLayout(root.transform, "RESOLUTION:",
            BuildResolutionLabels(), currentResIndex, OnResolutionChanged);
        graphicsDropdown = MakeDropdownRowLayout(root.transform, "GRAPHICS:",
            new List<string>(graphicsOptions), currentGraphicsIndex, OnGraphicsChanged);
        fullscreenToggle = MakeFullscreenRowLayout(root.transform);

        return root;
    }

    GameObject BuildControlsTab(Transform parent)
    {
        GameObject root = new GameObject("TabRoot_Controls", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        VerticalLayoutGroup v = root.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(40, 40, 18, 18);
        v.spacing = 26f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        controlDropdown = MakeDropdownRowLayout(root.transform, "MOVE STYLE:",
            new List<string>(controlOptions), ControlIndex(), OnControlChanged);
        sensitivitySlider = MakeSensitivityRowLayout(root.transform);

        return root;
    }

    GameObject BuildGameplayTab(Transform parent)
    {
        GameObject root = new GameObject("TabRoot_Gameplay", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        VerticalLayoutGroup v = root.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(40, 40, 18, 18);
        v.spacing = 26f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        difficultyDropdown = MakeDropdownRowLayout(root.transform, "DIFFICULTY:",
            new List<string>(difficultyOptions), DifficultyIndex(), OnDifficultyChanged);
        perspectiveDropdown = MakeDropdownRowLayout(root.transform, "CAMERA VIEW:",
            new List<string>(perspectiveOptions), 0, OnPerspectiveChanged);

        return root;
    }

    Slider MakeSliderRowLayout(Transform parent, string label, string prefKey,
        out TextMeshProUGUI valueLabel, UnityAction<float> onChanged)
    {
        GameObject row = CreateLayoutRow(parent, "Row_" + label);
        MakeText(row.transform, label, 24, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-220f, 0f), new Vector2(300f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject barObj = new GameObject("SliderBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.28f, 0.38f, 0.62f, 0.72f);
        Outline barOl = barObj.AddComponent<Outline>();
        barOl.effectColor = new Color(0.35f, 0.72f, 1f, 0.42f);
        barOl.effectDistance = new Vector2(1f, -1f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(300f, 20f), new Vector2(140f, 0f));

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
        handleRect.sizeDelta = new Vector2(18f, 30f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        float initial = PlayerPrefs.GetFloat(prefKey, 0.8f);
        slider.value = initial;

        valueLabel = MakeText(row.transform, SettingsManager.FormatVolumePercent(initial), 22,
            new Color(0.92f, 0.94f, 1f, 1f), new Vector2(320f, 0f), new Vector2(84f, 42f),
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
        le.preferredHeight = 56f;
        le.minHeight = 52f;
        le.flexibleWidth = 1f;
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 56f);
        return row;
    }

    TMP_Dropdown MakeDropdownRowLayout(Transform parent, string caption, IList<string> options,
        int selectedIndex, UnityEngine.Events.UnityAction<int> onChanged)
    {
        GameObject row = CreateLayoutRow(parent, "Row_" + caption);
        MakeText(row.transform, caption, 24, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-220f, 0f), new Vector2(300f, 42f), false, TextAlignmentOptions.MidlineRight);

        TMP_Dropdown dropdown = CreateDropdown(row.transform, options, selectedIndex, new Vector2(175f, 0f), onChanged);
        SetRect(dropdown.GetComponent<RectTransform>(), new Vector2(340f, 52f), new Vector2(175f, 0f));

        Outline dol = dropdown.gameObject.GetComponent<Outline>();
        if (dol == null)
            dol = dropdown.gameObject.AddComponent<Outline>();
        dol.effectColor = new Color(0.32f, 0.68f, 1f, 0.45f);
        dol.effectDistance = new Vector2(1f, -1f);

        StyleDropdownSelectable(dropdown);
        dropdown.onValueChanged.AddListener(_ => UIAnimationHelper.PunchScale(dropdown.transform, 0.1f, 1.045f));

        return dropdown;
    }

    static void StyleDropdownSelectable(TMP_Dropdown dropdown)
    {
        if (dropdown?.targetGraphic == null || dropdown.captionText == null) return;

        Image img = (Image)dropdown.targetGraphic;
        img.color = new Color(0.12f, 0.20f, 0.42f, 0.94f);
        dropdown.captionText.color = new Color(0.88f, 0.93f, 1f, 1f);

        MenuButtonHoverEffect h = dropdown.gameObject.GetComponent<MenuButtonHoverEffect>();
        if (h != null)
            Destroy(h);
        h = dropdown.gameObject.AddComponent<MenuButtonHoverEffect>();
        TextMeshProUGUI cap = dropdown.captionText as TextMeshProUGUI;
        if (cap == null) return;
        h.label = cap;
        h.background = img;
        h.normalTextColor = cap.color;
        h.hoverTextColor = Color.white;
        h.normalBackgroundColor = img.color;
        h.hoverBackgroundColor = new Color(0.18f, 0.38f, 0.72f, 1f);
        h.hoverScale = new Vector3(1.035f, 1.035f, 1f);
        Outline ol = dropdown.GetComponent<Outline>();
        if (ol != null)
        {
            h.neonOutline = ol;
            h.normalOutlineColor = ol.effectColor;
            h.hoverOutlineColor = TabSelectedOutline;
        }
    }

    Toggle MakeFullscreenRowLayout(Transform parent)
    {
        GameObject row = CreateLayoutRow(parent, "Row_FULLSCREEN");
        MakeText(row.transform, "FULLSCREEN:", 24, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-40f, 0f), new Vector2(260f, 42f), false, TextAlignmentOptions.Center);

        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        SetRect(toggleObj.AddComponent<RectTransform>(), new Vector2(40f, 40f), new Vector2(175f, 0f));

        Image tbg = toggleObj.AddComponent<Image>();
        tbg.color = new Color(0.12f, 0.20f, 0.42f, 0.94f);
        Outline tol = toggleObj.AddComponent<Outline>();
        tol.effectColor = new Color(0.32f, 0.68f, 1f, 0.35f);
        tol.effectDistance = new Vector2(1f, -1f);

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        GameObject checkmarkObj = new GameObject("Checkmark");
        checkmarkObj.transform.SetParent(toggleObj.transform, false);
        TextMeshProUGUI mark = checkmarkObj.AddComponent<TextMeshProUGUI>();
        Stretch(checkmarkObj.GetComponent<RectTransform>());
        if (prismFont != null)
            mark.font = prismFont;

        toggle.targetGraphic = tbg;
        toggle.isOn = isFullscreen;
        SettingsManager.ApplyFullscreenToggleGraphic(toggle, mark, prismFont);
        toggle.onValueChanged.AddListener(value =>
        {
            isFullscreen = value;
            ApplySettings();
            UIAnimationHelper.PunchScale(toggleObj.transform, 0.1f, 1.06f);
        });

        MenuButtonHoverEffect mh = toggleObj.AddComponent<MenuButtonHoverEffect>();
        mh.label = mark;
        mh.background = tbg;
        mh.normalBackgroundColor = tbg.color;
        mh.hoverBackgroundColor = new Color(0.2f, 0.42f, 0.76f, 1f);

        return toggle;
    }

    Slider MakeSensitivityRowLayout(Transform parent)
    {
        GameObject row = CreateLayoutRow(parent, "Row_SENSITIVITY");
        MakeText(row.transform, "MOUSE SENSITIVITY:", 24, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-220f, 0f), new Vector2(300f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject barObj = new GameObject("SensitivityBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.28f, 0.38f, 0.62f, 0.72f);
        Outline bo = barObj.AddComponent<Outline>();
        bo.effectColor = new Color(0.35f, 0.72f, 1f, 0.42f);
        bo.effectDistance = new Vector2(1f, -1f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(300f, 20f), new Vector2(140f, 0f));

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
        handleRect.sizeDelta = new Vector2(18f, 30f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        sensitivityValueLabel = MakeText(row.transform, "3.5", 22,
            new Color(0.92f, 0.94f, 1f, 1f), new Vector2(320f, 0f), new Vector2(72f, 42f),
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

    List<string> BuildResolutionLabels()
    {
        List<string> labels = new List<string>(resolutionOptions.Length);
        for (int i = 0; i < resolutionOptions.Length; i++)
            labels.Add(ResolutionLabel(resolutionOptions[i]));
        return labels;
    }

    void OnResolutionChanged(int selectedIndex)
    {
        currentResIndex = Mathf.Clamp(selectedIndex, 0, resolutionOptions.Length - 1);
        ApplySettings();
    }

    void OnGraphicsChanged(int selectedIndex)
    {
        currentGraphicsIndex = Mathf.Clamp(selectedIndex, 0, graphicsOptions.Length - 1);
        ApplySettings();
    }

    void ResetSettings()
    {
        currentResIndex = resolutionOptions.Length - 1;
        currentGraphicsIndex = 2;
        isFullscreen = true;

        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(0.8f);
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(0.8f);
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(0.8f);

        PlayerPrefs.SetFloat(AudioSettingsRuntime.MasterKey, 0.8f);
        PlayerPrefs.SetFloat(AudioSettingsRuntime.MusicKey, 0.8f);
        PlayerPrefs.SetFloat(AudioSettingsRuntime.SfxKey, 0.8f);
        AudioSettingsRuntime.ApplyListenerVolume();

        RefreshVolumeValueLabels();

        if (resolutionDropdown != null)
        {
            resolutionDropdown.SetValueWithoutNotify(currentResIndex);
            resolutionDropdown.RefreshShownValue();
        }

        if (graphicsDropdown != null)
        {
            graphicsDropdown.SetValueWithoutNotify(currentGraphicsIndex);
            graphicsDropdown.RefreshShownValue();
        }

        if (fullscreenToggle != null)
            fullscreenToggle.isOn = isFullscreen;
        else
            ApplySettings();

        if (difficultyDropdown != null)
        {
            difficultyDropdown.SetValueWithoutNotify(1);
            difficultyDropdown.RefreshShownValue();
            OnDifficultyChanged(1);
        }

        if (perspectiveDropdown != null)
        {
            perspectiveDropdown.SetValueWithoutNotify(0);
            perspectiveDropdown.RefreshShownValue();
            OnPerspectiveChanged(0);
        }

        if (controlDropdown != null)
        {
            controlDropdown.SetValueWithoutNotify(0);
            controlDropdown.RefreshShownValue();
            OnControlChanged(0);
        }

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
    }

    void ApplySettings()
    {
        Vector2Int resolution = resolutionOptions[currentResIndex];
        Screen.SetResolution(resolution.x, resolution.y, isFullscreen);
        ApplyGraphicsQuality();

        PlayerPrefs.SetInt("ResIndex", currentResIndex);
        PlayerPrefs.SetInt("GraphicsTier", currentGraphicsIndex);
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    void ApplyGraphicsQuality()
    {
        int targetQuality = currentGraphicsIndex == 0 ? 0 :
            currentGraphicsIndex == 1 ? Mathf.Max(0, (QualitySettings.names.Length - 1) / 2) :
            Mathf.Max(0, QualitySettings.names.Length - 1);

        QualitySettings.SetQualityLevel(targetQuality);
    }

    int DifficultyIndex()
    {
        string difficulty = PlayerPrefs.GetString("Difficulty", "Normal");
        if (difficulty == "Easy") return 0;
        if (difficulty == "Hard") return 2;
        return 1;
    }

    int ControlIndex()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
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

    TMP_Dropdown CreateDropdown(Transform parent, IList<string> options, int selectedIndex, Vector2 pos, UnityEngine.Events.UnityAction<int> onChanged)
    {
        GameObject dropdownObj = new GameObject("Dropdown");
        dropdownObj.transform.SetParent(parent, false);
        Image dimg = dropdownObj.AddComponent<Image>();
        dimg.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        SetRect(dropdownObj.GetComponent<RectTransform>(), new Vector2(360f, 52f), pos);

        TMP_Dropdown dropdown = dropdownObj.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = dimg;

        TextMeshProUGUI caption = MakeDropdownText(dropdownObj.transform, "Caption", TextAlignmentOptions.Center, new Vector2(16f, 0f), new Vector2(-40f, 0f));
        dropdown.captionText = caption;

        RectTransform templateRect = CreateDropdownTemplate(dropdownObj.transform, out TextMeshProUGUI itemLabel);
        dropdown.template = templateRect;
        dropdown.itemText = itemLabel;

        List<TMP_Dropdown.OptionData> dropdownOptions = new List<TMP_Dropdown.OptionData>(options.Count);
        for (int i = 0; i < options.Count; i++)
            dropdownOptions.Add(new TMP_Dropdown.OptionData(options[i]));

        dropdown.options = dropdownOptions;
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, options.Count - 1));
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(onChanged);
        return dropdown;
    }

    RectTransform CreateDropdownTemplate(Transform parent, out TextMeshProUGUI itemLabel)
    {
        GameObject templateObj = new GameObject("Template");
        templateObj.transform.SetParent(parent, false);
        Image templateBg = templateObj.AddComponent<Image>();
        templateBg.color = new Color(0.94f, 0.94f, 0.98f, 0.98f);
        ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
        RectTransform templateRect = templateObj.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -56f);
        templateRect.sizeDelta = new Vector2(0f, 136f);
        templateObj.SetActive(false);

        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(templateObj.transform, false);
        Image viewportBg = viewportObj.AddComponent<Image>();
        viewportBg.color = new Color(1f, 1f, 1f, 0.02f);
        viewportObj.AddComponent<RectMask2D>();
        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        Stretch(viewportRect);

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 2f;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject itemObj = new GameObject("Item");
        itemObj.transform.SetParent(contentObj.transform, false);
        LayoutElement element = itemObj.AddComponent<LayoutElement>();
        element.preferredHeight = 42f;
        Image itemBg = itemObj.AddComponent<Image>();
        itemBg.color = new Color(1f, 1f, 1f, 0.98f);
        Toggle itemToggle = itemObj.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBg;
        itemToggle.graphic = null;

        itemLabel = MakeDropdownText(itemObj.transform, "Item Label", TextAlignmentOptions.MidlineLeft, new Vector2(16f, 0f), new Vector2(-12f, 0f));

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        return templateRect;
    }

    TextMeshProUGUI MakeDropdownText(Transform parent, string name, TextAlignmentOptions alignment, Vector2 leftOffset, Vector2 rightOffset)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 24f;
        tmp.color = new Color(0.88f, 0.91f, 1f, 1f);
        tmp.alignment = alignment;
        if (prismFont != null)
            tmp.font = prismFont;

        RectTransform rect = tmp.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = leftOffset;
        rect.offsetMax = rightOffset;
        return tmp;
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
