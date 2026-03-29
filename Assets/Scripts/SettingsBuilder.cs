using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    private int currentQualityIndex;
    private bool isFullscreen;
    private Resolution[] resolutions;
    private int currentResIndex;

    private TextMeshProUGUI resText;
    private TextMeshProUGUI gfxText;
    private Toggle fullscreenToggle;

    void Start()
    {
        LoadSettingsData();
        BuildSettingsMenu();
    }

    void LoadSettingsData()
    {
        resolutions = Screen.resolutions;
        if (resolutions == null || resolutions.Length == 0)
            resolutions = new[] { Screen.currentResolution };

        currentResIndex = Mathf.Clamp(PlayerPrefs.GetInt("ResIndex", resolutions.Length - 1), 0, resolutions.Length - 1);
        currentQualityIndex = Mathf.Clamp(PlayerPrefs.GetInt("QualityIndex", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        QualitySettings.SetQualityLevel(currentQualityIndex);
        Screen.fullScreen = isFullscreen;
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
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.42f);

        MakeText(canvasObj.transform, "SETTINGS", 62, new Color(0.78f, 0.84f, 1f, 1f),
            new Vector2(0f, 355f), new Vector2(720f, 84f), true, TextAlignmentOptions.Center);

        GameObject panelObj = new GameObject("CentralPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0.05f, 0.07f, 0.12f, 0.32f);
        Outline outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.40f, 0.18f, 0.75f, 0.35f);
        outline.effectDistance = new Vector2(3f, -3f);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(980f, 520f), new Vector2(0f, 10f));

        MakeSliderRow(panel.transform, "MASTER VOLUME:", 150f, "MasterVol");
        MakeSliderRow(panel.transform, "MUSIC VOLUME:", 75f, "MusicVol");
        MakeSliderRow(panel.transform, "SFX VOLUME:", 0f, "SFXVol");

        resText = MakeCycleRow(panel.transform, "RESOLUTION:", ResolutionLabel(resolutions[currentResIndex]), -80f, CycleResolution);
        gfxText = MakeCycleRow(panel.transform, "GRAPHICS:", QualitySettings.names[currentQualityIndex].ToUpper(), -155f, CycleGraphics);
        fullscreenToggle = MakeFullscreenRow(panel.transform, -230f);

        MakePrismButton(canvasObj.transform, "RETURN", new Vector2(-150f, -325f), () => SceneManager.LoadScene("MainMenu"));
        MakePrismButton(canvasObj.transform, "RESET", new Vector2(150f, -325f), ResetSettings);
    }

    void CycleResolution()
    {
        currentResIndex = (currentResIndex + 1) % resolutions.Length;
        resText.text = ResolutionLabel(resolutions[currentResIndex]);
    }

    void CycleGraphics()
    {
        currentQualityIndex = (currentQualityIndex + 1) % QualitySettings.names.Length;
        gfxText.text = QualitySettings.names[currentQualityIndex].ToUpper();
    }

    void ResetSettings()
    {
        currentResIndex = resolutions.Length - 1;
        currentQualityIndex = Mathf.Clamp(QualitySettings.names.Length - 1, 0, QualitySettings.names.Length - 1);
        isFullscreen = true;

        resText.text = ResolutionLabel(resolutions[currentResIndex]);
        gfxText.text = QualitySettings.names[currentQualityIndex].ToUpper();
        fullscreenToggle.isOn = isFullscreen;
        ApplySettings();
    }

    void ApplySettings()
    {
        Resolution res = resolutions[currentResIndex];
        Screen.SetResolution(res.width, res.height, isFullscreen);
        QualitySettings.SetQualityLevel(currentQualityIndex);

        PlayerPrefs.SetInt("ResIndex", currentResIndex);
        PlayerPrefs.SetInt("QualityIndex", currentQualityIndex);
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    void MakeSliderRow(Transform parent, string label, float yPos, string prefKey)
    {
        GameObject row = CreateRow(parent, "Row_" + label, yPos);
        MakeText(row.transform, label, 25, new Color(0.95f, 0.95f, 1f, 1f), new Vector2(-210f, 0f), new Vector2(340f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject barObj = new GameObject("SliderBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.62f, 0.62f, 0.62f, 0.95f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(310f, 18f), new Vector2(165f, 0f));

        Slider slider = barObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(barObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(0f, 0f);
        fillAreaRect.offsetMax = new Vector2(-18f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.55f, 0.35f, 0.95f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(barObj.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 26f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.value = PlayerPrefs.GetFloat(prefKey, 0.8f);
        slider.onValueChanged.AddListener(val => PlayerPrefs.SetFloat(prefKey, val));
    }

    TextMeshProUGUI MakeCycleRow(Transform parent, string label, string value, float yPos, UnityEngine.Events.UnityAction action)
    {
        GameObject row = CreateRow(parent, "Row_" + label, yPos);
        MakeText(row.transform, label, 25, new Color(0.95f, 0.95f, 1f, 1f), new Vector2(-210f, 0f), new Vector2(340f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject buttonObj = new GameObject("ValueButton");
        buttonObj.transform.SetParent(row.transform, false);
        Image box = buttonObj.AddComponent<Image>();
        box.color = new Color(0.92f, 0.92f, 0.94f, 1f);
        SetRect(buttonObj.GetComponent<RectTransform>(), new Vector2(310f, 52f), new Vector2(165f, 0f));
        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            action.Invoke();
            ApplySettings();
        });

        return MakeText(buttonObj.transform, value, 24, new Color(0.22f, 0.22f, 0.35f, 1f), Vector2.zero, new Vector2(280f, 40f), false, TextAlignmentOptions.Center);
    }

    Toggle MakeFullscreenRow(Transform parent, float yPos)
    {
        GameObject row = CreateRow(parent, "Row_FULLSCREEN", yPos);
        MakeText(row.transform, "FULLSCREEN", 25, new Color(0.95f, 0.95f, 1f, 1f), new Vector2(-20f, 0f), new Vector2(260f, 42f), false, TextAlignmentOptions.Center);

        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        SetRect(toggleObj.AddComponent<RectTransform>(), new Vector2(32f, 32f), new Vector2(175f, 0f));

        Image bg = toggleObj.AddComponent<Image>();
        bg.color = Color.white;

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        GameObject checkmarkObj = new GameObject("Checkmark");
        checkmarkObj.transform.SetParent(toggleObj.transform, false);
        TextMeshProUGUI mark = checkmarkObj.AddComponent<TextMeshProUGUI>();
        mark.text = "X";
        mark.fontSize = 24f;
        mark.alignment = TextAlignmentOptions.Center;
        mark.color = new Color(0.18f, 0.22f, 0.34f, 1f);
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
        SetRect(row.AddComponent<RectTransform>(), new Vector2(760f, 56f), new Vector2(0f, yPos));
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

    void MakePrismButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        Image buttonImage = new GameObject("Btn_" + label).AddComponent<Image>();
        buttonImage.transform.SetParent(parent, false);
        buttonImage.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        SetRect(buttonImage.GetComponent<RectTransform>(), new Vector2(180f, 56f), pos);
        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        MakeText(buttonImage.transform, label, 22, new Color(0.23f, 0.22f, 0.38f, 1f), Vector2.zero, new Vector2(180f, 56f), false, TextAlignmentOptions.Center);
    }

    string ResolutionLabel(Resolution resolution)
    {
        return resolution.width + " x " + resolution.height;
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
}
