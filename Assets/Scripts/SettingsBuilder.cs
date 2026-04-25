using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    readonly string[] graphicsOptions = { "LOW", "MEDIUM", "HIGH" };

    Vector2Int[] resolutionOptions;
    int currentResIndex;
    int currentGraphicsIndex;
    bool isFullscreen;

    TMP_Dropdown resolutionDropdown;
    TMP_Dropdown graphicsDropdown;
    Toggle fullscreenToggle;
    Slider masterSlider;
    Slider musicSlider;
    Slider sfxSlider;

    void Start()
    {
        prismFont = ResolvePrismFont();
        EnsureEventSystem();
        LoadSettingsData();
        BuildSettingsMenu();
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<InputSystemUIInputModule>();
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

        GameObject canvasObj = new GameObject("Prism7Canvas_Settings");
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

        MakeText(canvasObj.transform, "SETTINGS", 62, new Color(0.78f, 0.84f, 1f, 1f),
            new Vector2(0f, 300f), new Vector2(720f, 84f), true, TextAlignmentOptions.Center);

        GameObject panelObj = new GameObject("CentralPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0.16f, 0.20f, 0.30f, 0.30f);
        Outline outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.26f, 0.42f, 0.68f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(980f, 520f), new Vector2(0f, -10f));

        masterSlider = MakeSliderRow(panel.transform, "MASTER VOLUME:", 150f, AudioSettingsRuntime.MasterKey, value =>
        {
            AudioSettingsRuntime.ApplyListenerVolume();
        });
        musicSlider = MakeSliderRow(panel.transform, "MUSIC VOLUME:", 75f, AudioSettingsRuntime.MusicKey, null);
        sfxSlider = MakeSliderRow(panel.transform, "SFX VOLUME:", 0f, AudioSettingsRuntime.SfxKey, null);

        resolutionDropdown = MakeDropdownRow(panel.transform, "RESOLUTION:", BuildResolutionLabels(), currentResIndex, -80f, OnResolutionChanged);
        graphicsDropdown = MakeDropdownRow(panel.transform, "GRAPHICS:", new List<string>(graphicsOptions), currentGraphicsIndex, -155f, OnGraphicsChanged);
        fullscreenToggle = MakeFullscreenRow(panel.transform, -230f);

        MakePrismButton(canvasObj.transform, "RETURN", new Vector2(-150f, -325f), () => SceneManager.LoadScene("MainMenu"));
        MakePrismButton(canvasObj.transform, "RESET", new Vector2(150f, -325f), ResetSettings);
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

    Slider MakeSliderRow(Transform parent, string label, float yPos, string prefKey, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = CreateRow(parent, "Row_" + label, yPos);
        MakeText(row.transform, label, 25, new Color(0.95f, 0.95f, 1f, 1f), new Vector2(-210f, 0f), new Vector2(340f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject barObj = new GameObject("SliderBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.74f, 0.74f, 0.74f, 0.95f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(360f, 18f), new Vector2(175f, 0f));

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
        fillImage.color = new Color(0.58f, 0.32f, 0.94f, 1f);
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
        slider.value = PlayerPrefs.GetFloat(prefKey, 0.8f);
        slider.onValueChanged.AddListener(val =>
        {
            PlayerPrefs.SetFloat(prefKey, val);
            PlayerPrefs.Save();
            onChanged?.Invoke(val);
        });

        return slider;
    }

    TMP_Dropdown MakeDropdownRow(Transform parent, string label, IList<string> options, int selectedIndex, float yPos, UnityEngine.Events.UnityAction<int> onChanged)
    {
        GameObject row = CreateRow(parent, "Row_" + label, yPos);
        MakeText(row.transform, label, 25, new Color(0.95f, 0.95f, 1f, 1f), new Vector2(-210f, 0f), new Vector2(340f, 42f), false, TextAlignmentOptions.MidlineRight);
        return CreateDropdown(row.transform, options, selectedIndex, new Vector2(175f, 0f), onChanged);
    }

    TMP_Dropdown CreateDropdown(Transform parent, IList<string> options, int selectedIndex, Vector2 pos, UnityEngine.Events.UnityAction<int> onChanged)
    {
        GameObject dropdownObj = new GameObject("Dropdown");
        dropdownObj.transform.SetParent(parent, false);
        Image bg = dropdownObj.AddComponent<Image>();
        bg.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        SetRect(dropdownObj.GetComponent<RectTransform>(), new Vector2(360f, 52f), pos);

        TMP_Dropdown dropdown = dropdownObj.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = bg;

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

    Toggle MakeFullscreenRow(Transform parent, float yPos)
    {
        GameObject row = CreateRow(parent, "Row_FULLSCREEN", yPos);
        MakeText(row.transform, "FULLSCREEN", 25, new Color(0.95f, 0.95f, 1f, 1f), new Vector2(-40f, 0f), new Vector2(260f, 42f), false, TextAlignmentOptions.Center);

        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        SetRect(toggleObj.AddComponent<RectTransform>(), new Vector2(32f, 32f), new Vector2(175f, 0f));

        Image bg = toggleObj.AddComponent<Image>();
        bg.color = Color.white;

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        GameObject checkmarkObj = new GameObject("Checkmark");
        checkmarkObj.transform.SetParent(toggleObj.transform, false);
        TextMeshProUGUI mark = checkmarkObj.AddComponent<TextMeshProUGUI>();
        mark.text = "v";
        mark.fontSize = 24f;
        mark.alignment = TextAlignmentOptions.Center;
        mark.color = new Color(0.18f, 0.22f, 0.34f, 1f);
        if (prismFont != null)
            mark.font = prismFont;
        Stretch(checkmarkObj.GetComponent<RectTransform>());

        toggle.graphic = mark;
        toggle.targetGraphic = bg;
        toggle.isOn = isFullscreen;
        toggle.onValueChanged.AddListener(value =>
        {
            isFullscreen = value;
            ApplySettings();
        });
        return toggle;
    }

    GameObject CreateRow(Transform parent, string name, float yPos)
    {
        GameObject row = new GameObject(name);
        row.transform.SetParent(parent, false);
        SetRect(row.AddComponent<RectTransform>(), new Vector2(820f, 56f), new Vector2(0f, yPos));
        return row;
    }

    TextMeshProUGUI MakeText(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 sizeDelta, bool addOutline, TextAlignmentOptions align)
    {
        TextMeshProUGUI tmp = new GameObject("Txt_" + text).AddComponent<TextMeshProUGUI>();
        tmp.transform.SetParent(parent, false);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        if (prismFont != null)
            tmp.font = prismFont;
        if (addOutline)
        {
            tmp.fontStyle = FontStyles.Bold;
            Outline outline = tmp.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.12f, 0.42f, 1f);
        }
        SetRect(tmp.GetComponent<RectTransform>(), sizeDelta, pos);
        return tmp;
    }

    TextMeshProUGUI MakeDropdownText(Transform parent, string name, TextAlignmentOptions alignment, Vector2 leftOffset, Vector2 rightOffset)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 24f;
        tmp.color = new Color(0.23f, 0.22f, 0.38f, 1f);
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

    void MakePrismButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        Image buttonImage = new GameObject("Btn_" + label).AddComponent<Image>();
        buttonImage.transform.SetParent(parent, false);
        buttonImage.color = Color.white;
        SetRect(buttonImage.GetComponent<RectTransform>(), new Vector2(190f, 58f), pos);

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.38f, 0.30f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);
        TextMeshProUGUI labelText = MakeText(buttonImage.transform, label, 22, new Color(0.10f, 0.10f, 0.14f, 1f), Vector2.zero, new Vector2(190f, 58f), false, TextAlignmentOptions.Center);
        labelText.fontStyle = FontStyles.Bold;
        labelText.fontSize = 24f;
        labelText.color = new Color(0.10f, 0.10f, 0.14f, 1f);
        AttachHoverEffect(buttonImage.gameObject, labelText, buttonImage);
    }

    void AttachHoverEffect(GameObject target, TextMeshProUGUI label, Image image)
    {
        MenuButtonHoverEffect hover = target.AddComponent<MenuButtonHoverEffect>();
        hover.label = label;
        hover.background = image;
        hover.normalTextColor = new Color(0.10f, 0.10f, 0.14f, 1f);
        hover.hoverTextColor = new Color(0.10f, 0.10f, 0.14f, 1f);
        hover.normalBackgroundColor = Color.white;
        hover.hoverBackgroundColor = new Color(0.98f, 0.98f, 1f, 1f);
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

    void Stretch(RectTransform rect)
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

        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return null;
    }

}
