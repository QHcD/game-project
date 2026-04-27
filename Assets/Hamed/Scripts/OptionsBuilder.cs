using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OptionsBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    private readonly string[] difficultyOptions = { "EASY", "NORMAL", "HARD" };
    private readonly string[] perspectiveOptions = { "FIRST PERSON", "THIRD PERSON" };
    private readonly string[] controlOptions = { "WASD + MOUSE", "ARROWS + MOUSE" };

    private TMP_Dropdown difficultyDropdown;
    private TMP_Dropdown perspectiveDropdown;
    private TMP_Dropdown controlDropdown;
    private Slider      sensitivitySlider;
    private TextMeshProUGUI sensitivityValueLabel;

    private void Start()
    {
        prismFont = ResolvePrismFont();
        EnsureEventSystem();
        BuildOptionsMenu();
    }

    private void EnsureEventSystem()
    {
        UIManager.EnsureInputSystemEventSystem();
    }

    private void BuildOptionsMenu()
    {
        GameObject existing = GameObject.Find("Prism7Canvas_Options");
        if (existing != null)
        {
            Destroy(existing);
        }

        GameObject canvasObject = new GameObject("Prism7Canvas_Options");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image background = new GameObject("Background").AddComponent<Image>();
        background.transform.SetParent(canvasObject.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        background.color = new Color(0.03f, 0.04f, 0.08f, 1f);
        if (prismBackground != null)
        {
            background.sprite = prismBackground;
            background.color = Color.white;
        }

        Image overlay = new GameObject("Overlay").AddComponent<Image>();
        overlay.transform.SetParent(canvasObject.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.22f);

        GameObject panelObject = new GameObject("CentralPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        Image panel = panelObject.AddComponent<Image>();
        Outline outline = panelObject.AddComponent<Outline>();
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(1100f, 640f), new Vector2(0f, 6f));
        PrismOrganizedMenuChrome.ApplyPanelSurface(panel, outline);

        // Title + subtitle inside the panel (not floating above it).
        MakeText(panelObject.transform, "OPTIONS", 62f, new Color(0.78f, 0.84f, 1f, 1f),
            new Vector2(0f, 268f), new Vector2(720f, 84f), true, TextAlignmentOptions.Center);
        MakeText(panelObject.transform, "MATCH THE RUN TO YOUR STYLE", 22f, new Color(0.72f, 0.84f, 1f, 0.88f),
            new Vector2(0f, 220f), new Vector2(720f, 34f), false, TextAlignmentOptions.Center);

        difficultyDropdown = MakeDropdownRow(panel.transform, "DIFFICULTY:", new List<string>(difficultyOptions),
            DifficultyIndex(), 118f, OnDifficultyChanged);
        perspectiveDropdown = MakeDropdownRow(panel.transform, "CAMERA VIEW:", new List<string>(perspectiveOptions),
            PerspectiveIndex(), 42f, OnPerspectiveChanged);
        controlDropdown = MakeDropdownRow(panel.transform, "MOVE STYLE:", new List<string>(controlOptions),
            ControlIndex(), -34f, OnControlChanged);
        sensitivitySlider = MakeSensitivityRow(panel.transform, -108f);

        MakeInfoText(panel.transform, "Tune difficulty, camera view, movement scheme, and mouse sensitivity. Sensitivity 0 is very slow, 7 is very fast — the change takes effect immediately in-game.",
            new Vector2(0f, -162f), new Vector2(820f, 70f));

        RectTransform footerRt = PrismOrganizedMenuChrome.CreateFooterRow(panelObject.transform);
        Button returnBtn = PrismOrganizedMenuChrome.AddFooterChipButton(
            footerRt, "RETURN",
            new Color(0.12f, 0.20f, 0.42f, 0.92f), PrismOrganizedMenuChrome.ButtonOutlineBlue,
            () => SceneManager.LoadScene("MainMenu"), prismFont);
        Button resetBtn = PrismOrganizedMenuChrome.AddFooterChipButton(
            footerRt, "RESET",
            new Color(0.22f, 0.12f, 0.38f, 0.92f), PrismOrganizedMenuChrome.ButtonOutlinePurple,
            ResetOptions, prismFont);

        List<Selectable> nav = new List<Selectable>();
        if (difficultyDropdown != null)  nav.Add(difficultyDropdown);
        if (perspectiveDropdown != null) nav.Add(perspectiveDropdown);
        if (controlDropdown != null)     nav.Add(controlDropdown);
        if (sensitivitySlider != null)   nav.Add(sensitivitySlider);
        if (returnBtn != null) nav.Add(returnBtn);
        if (resetBtn != null)  nav.Add(resetBtn);
        MenuNavigationManager.AttachLinear(canvasObject, nav);
    }

    /// <summary>
    /// Builds the Mouse Sensitivity row: a 0–7 slider that updates
    /// <see cref="LookSensitivityRuntime"/> on change so the camera responds
    /// the moment the player drags it.
    /// </summary>
    private Slider MakeSensitivityRow(Transform parent, float yPos)
    {
        GameObject row = CreateRow(parent, "Row_SENSITIVITY", yPos);
        MakeText(row.transform, "MOUSE SENSITIVITY:", 25f, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(-210f, 0f), new Vector2(340f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject barObj = new GameObject("SensitivityBar");
        barObj.transform.SetParent(row.transform, false);
        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.74f, 0.78f, 0.94f, 0.95f);
        SetRect(barObj.GetComponent<RectTransform>(), new Vector2(310f, 18f), new Vector2(150f, 0f));

        Slider slider = barObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue  = LookSensitivityRuntime.MinSlider;
        slider.maxValue  = LookSensitivityRuntime.MaxSlider;
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
        fillImage.color = new Color(0.30f, 0.55f, 1f, 1f);
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

        // Live numeric readout on the right of the slider.
        sensitivityValueLabel = MakeText(row.transform, "3.5", 22f, new Color(0.92f, 0.94f, 1f, 1f),
            new Vector2(330f, 0f), new Vector2(60f, 42f), false, TextAlignmentOptions.Center);
        sensitivityValueLabel.fontStyle = FontStyles.Bold;

        // Initialise from PlayerPrefs and write changes through the runtime.
        LookSensitivityRuntime.LoadFromPrefs();
        slider.SetValueWithoutNotify(LookSensitivityRuntime.SliderValue);
        UpdateSensitivityLabel(LookSensitivityRuntime.SliderValue);

        slider.onValueChanged.AddListener(value =>
        {
            LookSensitivityRuntime.SetSliderValue(value, persist: true);
            UpdateSensitivityLabel(value);
        });

        return slider;
    }

    private void UpdateSensitivityLabel(float sliderValue)
    {
        if (sensitivityValueLabel == null) return;
        sensitivityValueLabel.text = sliderValue.ToString("0.0");
    }

    private int DifficultyIndex()
    {
        string difficulty = PlayerPrefs.GetString("Difficulty", "Normal");
        if (difficulty == "Easy") return 0;
        if (difficulty == "Hard") return 2;
        return 1;
    }

    private int PerspectiveIndex()
    {
        GameManager.PerspectiveMode mode = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : (GameManager.PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", (int)GameManager.PerspectiveMode.ThirdPerson), 0, 1);

        return mode == GameManager.PerspectiveMode.FirstPerson ? 0 : 1;
    }

    private int ControlIndex()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
    }

    private void OnDifficultyChanged(int selectedIndex)
    {
        string difficulty = selectedIndex == 0 ? "Easy" : selectedIndex == 2 ? "Hard" : "Normal";
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDifficulty(difficulty);
        }
        else
        {
            PlayerPrefs.SetString("Difficulty", difficulty);
            PlayerPrefs.Save();
        }
    }

    private void OnPerspectiveChanged(int selectedIndex)
    {
        GameManager.PerspectiveMode perspective = selectedIndex == 0
            ? GameManager.PerspectiveMode.FirstPerson
            : GameManager.PerspectiveMode.ThirdPerson;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPerspectiveMode(perspective);
        }
        else
        {
            PlayerPrefs.SetInt("PerspectiveMode", (int)perspective);
            PlayerPrefs.Save();
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.RefreshGameplayPreferences();
    }

    private void OnControlChanged(int selectedIndex)
    {
        GameManager.MovementScheme scheme = selectedIndex == 1
            ? GameManager.MovementScheme.ArrowKeys
            : GameManager.MovementScheme.Wasd;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMovementScheme(scheme);
        }
        else
        {
            PlayerPrefs.SetInt("MovementScheme", (int)scheme);
            PlayerPrefs.Save();
        }
    }

    private void ResetOptions()
    {
        if (difficultyDropdown != null)
        {
            difficultyDropdown.SetValueWithoutNotify(1);
            difficultyDropdown.RefreshShownValue();
        }

        if (perspectiveDropdown != null)
        {
            perspectiveDropdown.SetValueWithoutNotify(0);
            perspectiveDropdown.RefreshShownValue();
        }

        if (controlDropdown != null)
        {
            controlDropdown.SetValueWithoutNotify(0);
            controlDropdown.RefreshShownValue();
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.SetValueWithoutNotify(LookSensitivityRuntime.DefaultSlider);
            UpdateSensitivityLabel(LookSensitivityRuntime.DefaultSlider);
            LookSensitivityRuntime.SetSliderValue(LookSensitivityRuntime.DefaultSlider, persist: true);
        }

        OnDifficultyChanged(1);
        OnPerspectiveChanged(0);
        OnControlChanged(0);
    }

    private TMP_Dropdown MakeDropdownRow(Transform parent, string label, IList<string> options, int selectedIndex, float yPos, UnityEngine.Events.UnityAction<int> onChanged)
    {
        GameObject row = CreateRow(parent, "Row_" + label, yPos);
        MakeText(row.transform, label, 25f, new Color(0.95f, 0.95f, 1f, 1f), new Vector2(-210f, 0f), new Vector2(340f, 42f), false, TextAlignmentOptions.MidlineRight);
        return CreateDropdown(row.transform, options, selectedIndex, new Vector2(175f, 0f), onChanged);
    }

    private TMP_Dropdown CreateDropdown(Transform parent, IList<string> options, int selectedIndex, Vector2 position, UnityEngine.Events.UnityAction<int> onChanged)
    {
        GameObject dropdownObject = new GameObject("Dropdown");
        dropdownObject.transform.SetParent(parent, false);
        Image background = dropdownObject.AddComponent<Image>();
        background.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        SetRect(dropdownObject.GetComponent<RectTransform>(), new Vector2(360f, 52f), position);

        TMP_Dropdown dropdown = dropdownObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = background;

        TextMeshProUGUI caption = MakeDropdownText(dropdownObject.transform, "Caption", TextAlignmentOptions.Center, new Vector2(16f, 0f), new Vector2(-40f, 0f));
        dropdown.captionText = caption;

        RectTransform templateRect = CreateDropdownTemplate(dropdownObject.transform, out TextMeshProUGUI itemLabel);
        dropdown.template = templateRect;
        dropdown.itemText = itemLabel;

        List<TMP_Dropdown.OptionData> dropdownOptions = new List<TMP_Dropdown.OptionData>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            dropdownOptions.Add(new TMP_Dropdown.OptionData(options[i]));
        }

        dropdown.options = dropdownOptions;
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, options.Count - 1));
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(onChanged);
        return dropdown;
    }

    private RectTransform CreateDropdownTemplate(Transform parent, out TextMeshProUGUI itemLabel)
    {
        GameObject templateObject = new GameObject("Template");
        templateObject.transform.SetParent(parent, false);
        Image templateBackground = templateObject.AddComponent<Image>();
        templateBackground.color = new Color(0.94f, 0.94f, 0.98f, 0.98f);
        ScrollRect scrollRect = templateObject.AddComponent<ScrollRect>();
        RectTransform templateRect = templateObject.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -56f);
        templateRect.sizeDelta = new Vector2(0f, 136f);
        templateObject.SetActive(false);

        GameObject viewportObject = new GameObject("Viewport");
        viewportObject.transform.SetParent(templateObject.transform, false);
        Image viewportBackground = viewportObject.AddComponent<Image>();
        viewportBackground.color = new Color(1f, 1f, 1f, 0.02f);
        viewportObject.AddComponent<RectMask2D>();
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect);

        GameObject contentObject = new GameObject("Content");
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 2f;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject itemObject = new GameObject("Item");
        itemObject.transform.SetParent(contentObject.transform, false);
        LayoutElement element = itemObject.AddComponent<LayoutElement>();
        element.preferredHeight = 42f;
        Image itemBackground = itemObject.AddComponent<Image>();
        itemBackground.color = new Color(1f, 1f, 1f, 0.98f);
        Toggle itemToggle = itemObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBackground;

        itemToggle.graphic = null;

        itemLabel = MakeDropdownText(itemObject.transform, "Item Label", TextAlignmentOptions.MidlineLeft, new Vector2(16f, 0f), new Vector2(-12f, 0f));

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        return templateRect;
    }

    private GameObject CreateRow(Transform parent, string name, float yPos)
    {
        GameObject row = new GameObject(name);
        row.transform.SetParent(parent, false);
        SetRect(row.AddComponent<RectTransform>(), new Vector2(820f, 56f), new Vector2(0f, yPos));
        return row;
    }

    private void MakeInfoText(Transform parent, string text, Vector2 position, Vector2 size)
    {
        TextMeshProUGUI info = MakeText(parent, text, 22f, new Color(0.90f, 0.93f, 1f, 0.88f), position, size, false, TextAlignmentOptions.Center);
        info.textWrappingMode = TextWrappingModes.Normal;
    }

    private TextMeshProUGUI MakeText(Transform parent, string text, float size, Color color, Vector2 position, Vector2 sizeDelta, bool addOutline, TextAlignmentOptions align)
    {
        TextMeshProUGUI tmp = new GameObject("Txt_" + text).AddComponent<TextMeshProUGUI>();
        tmp.transform.SetParent(parent, false);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        if (prismFont != null)
        {
            tmp.font = prismFont;
        }

        if (addOutline)
        {
            tmp.fontStyle = FontStyles.Bold;
            Outline outline = tmp.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.12f, 0.42f, 1f);
        }

        SetRect(tmp.GetComponent<RectTransform>(), sizeDelta, position);
        return tmp;
    }

    private TextMeshProUGUI MakeDropdownText(Transform parent, string name, TextAlignmentOptions alignment, Vector2 leftOffset, Vector2 rightOffset)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 24f;
        tmp.color = new Color(0.23f, 0.22f, 0.38f, 1f);
        tmp.alignment = alignment;
        if (prismFont != null)
        {
            tmp.font = prismFont;
        }

        RectTransform rect = tmp.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = leftOffset;
        rect.offsetMax = rightOffset;
        return tmp;
    }

    private Button MakePrismButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        Image buttonImage = new GameObject("Btn_" + label).AddComponent<Image>();
        buttonImage.transform.SetParent(parent, false);
        buttonImage.color = Color.white;
        SetRect(buttonImage.GetComponent<RectTransform>(), new Vector2(190f, 58f), position);

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.38f, 0.30f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);

        TextMeshProUGUI labelText = MakeText(buttonImage.transform, label, 24f, new Color(0.10f, 0.10f, 0.14f, 1f), Vector2.zero, new Vector2(190f, 58f), false, TextAlignmentOptions.Center);
        labelText.fontStyle = FontStyles.Bold;
        AttachHoverEffect(buttonImage.gameObject, labelText, buttonImage);
        return button;
    }

    private void AttachHoverEffect(GameObject target, TextMeshProUGUI label, Image image)
    {
        MenuButtonHoverEffect hover = target.AddComponent<MenuButtonHoverEffect>();
        hover.label = label;
        hover.background = image;
        hover.normalTextColor = new Color(0.10f, 0.10f, 0.14f, 1f);
        hover.hoverTextColor = new Color(0.10f, 0.10f, 0.14f, 1f);
        hover.normalBackgroundColor = Color.white;
        hover.hoverBackgroundColor = new Color(0.98f, 0.98f, 1f, 1f);
    }

    private void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
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
