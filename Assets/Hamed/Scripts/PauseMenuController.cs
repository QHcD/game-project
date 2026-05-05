using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    private GameObject pauseCanvas;
    private GameObject mainPanel;
    private GameObject optionsPanel;
    private GameObject settingsPanel;
    private bool isPaused;
    private TMP_FontAsset prismFont;

    private readonly string[] graphicsLabels = { "LOW", "MEDIUM", "HIGH" };

    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider mouseSensitivitySlider;

    private void Awake()
    {
        SettingsManager.ApplyDisplayPreferences();
        AudioSettingsRuntime.ApplyListenerVolume();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "GameScene")
        {
            if (isPaused)
                ResumeGame();
            return;
        }

        HUDManager hudManager = HUDManager.Instance;
        if (hudManager != null && hudManager.IsMatchFinished)
            return;

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (isPaused) ResumeGame();
        else ShowPauseMenu();
    }

    private void ShowPauseMenu()
    {
        EnsureEventSystem();
        BuildPauseMenu();
        ShowPanel(mainPanel);

        Time.timeScale = 0f;
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseCanvas != null)
        {
            Destroy(pauseCanvas);
            pauseCanvas = null;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        GameManager.Instance?.ReplayCurrentLevel();
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameManager.Instance?.GoToMainMenu();
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildPauseMenu()
    {
        if (pauseCanvas != null)
            Destroy(pauseCanvas);

        prismFont = ResolvePrismFont();

        pauseCanvas = new GameObject("PauseCanvas");
        Canvas canvas = pauseCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = pauseCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        pauseCanvas.AddComponent<GraphicRaycaster>();

        Image overlay = new GameObject("PauseOverlay").AddComponent<Image>();
        overlay.transform.SetParent(pauseCanvas.transform, false);
        Stretch(overlay.rectTransform);
        overlay.color = new Color(0.02f, 0.02f, 0.06f, 0.72f);

        mainPanel = CreatePausePanel("PausePanel_Main", new Vector2(760f, 700f));
        optionsPanel = CreatePausePanel("PausePanel_Options", new Vector2(860f, 720f));
        settingsPanel = CreatePausePanel("PausePanel_Settings", new Vector2(860f, 720f));

        BuildMainPanel(mainPanel.transform);
        BuildOptionsPanel(optionsPanel.transform);
        BuildSettingsPanel(settingsPanel.transform);
    }

    private GameObject CreatePausePanel(string name, Vector2 size)
    {
        Image panel = new GameObject(name).AddComponent<Image>();
        panel.transform.SetParent(pauseCanvas.transform, false);
        panel.color = new Color(0.16f, 0.20f, 0.30f, 0.36f);

        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = new Vector2(0f, -10f);

        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.26f, 0.42f, 0.68f, 0.22f);
        panelOutline.effectDistance = new Vector2(2f, -2f);
        return panel.gameObject;
    }

    private void BuildMainPanel(Transform parent)
    {
        CreateLabel(parent, "PAUSED", 62f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 250f), new Vector2(420f, 80f), true);
        CreateLabel(parent, "TAKE A BREATH. JUMP BACK IN WHEN YOU'RE READY.", 22f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 198f), new Vector2(620f, 36f), false);

        CreateButton(parent, "RESUME", new Vector2(0f, 92f), ResumeGame);
        CreateButton(parent, "RESTART", new Vector2(0f, 2f), RestartGame);
        CreateButton(parent, "OPTIONS", new Vector2(0f, -88f), () => ShowPanel(optionsPanel));
        CreateButton(parent, "SETTINGS", new Vector2(0f, -178f), () => ShowPanel(settingsPanel));
        CreateButton(parent, "QUIT", new Vector2(0f, -268f), QuitGame);
    }

    private void BuildOptionsPanel(Transform parent)
    {
        CreateLabel(parent, "OPTIONS", 56f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 258f), new Vector2(500f, 76f), true);
        CreateLabel(parent, "ADJUST THE CURRENT MATCH WITHOUT LEAVING GAMEPLAY.", 20f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 208f), new Vector2(620f, 34f), false);

        CreateCycleRow(parent, "DIFFICULTY", new Vector2(0f, 92f), GetDifficultyLabel, CycleDifficulty);
        CreateCycleRow(parent, "CAMERA VIEW", new Vector2(0f, 4f), GetPerspectiveLabel, CyclePerspective);
        CreateCycleRow(parent, "MOVE STYLE", new Vector2(0f, -84f), GetMovementLabel, CycleMovement);
        mouseSensitivitySlider = CreateSensitivityRow(parent, "MOUSE SENS", new Vector2(0f, -166f));

        CreateButton(parent, "RETURN", new Vector2(0f, -286f), () => ShowPanel(mainPanel));
    }

    private void BuildSettingsPanel(Transform parent)
    {
        CreateLabel(parent, "SETTINGS", 56f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 258f), new Vector2(500f, 76f), true);
        CreateLabel(parent, "TUNE DISPLAY AND AUDIO, THEN DROP RIGHT BACK INTO THE MATCH.", 20f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 208f), new Vector2(690f, 34f), false);

        masterVolumeSlider = CreateSliderRow(parent, "MASTER VOL", new Vector2(0f, 104f), AudioSettingsRuntime.MasterKey, value =>
        {
            AudioSettingsRuntime.ApplyListenerVolume();
        });

        musicVolumeSlider = CreateSliderRow(parent, "MUSIC VOL", new Vector2(0f, 24f), AudioSettingsRuntime.MusicKey, null);
        sfxVolumeSlider = CreateSliderRow(parent, "SFX VOL", new Vector2(0f, -56f), AudioSettingsRuntime.SfxKey, null);

        CreateCycleRow(parent, "GRAPHICS", new Vector2(0f, -136f), GetGraphicsLabel, CycleGraphics);
        CreateCycleRow(parent, "FULLSCREEN", new Vector2(0f, -214f), GetFullscreenLabel, ToggleFullscreen);

        CreateButton(parent, "RETURN", new Vector2(0f, -292f), () => ShowPanel(mainPanel));
    }

    private void CreateCycleRow(Transform parent, string label, Vector2 position, System.Func<string> getValue, UnityEngine.Events.UnityAction onCycle)
    {
        GameObject row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(700f, 64f);
        rowRect.anchoredPosition = position;

        CreateLabel(row.transform, label, 26f, Color.white, new Vector2(-190f, 0f), new Vector2(320f, 42f), false, TextAlignmentOptions.MidlineRight);
        CreateButton(row.transform, getValue(), new Vector2(148f, 0f), onCycle, new Vector2(320f, 60f), getValue);
    }

    private Slider CreateSliderRow(
        Transform parent,
        string label,
        Vector2 position,
        string prefKey,
        UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(700f, 60f);
        rowRect.anchoredPosition = position;

        CreateLabel(row.transform, label, 24f, Color.white, new Vector2(-190f, 0f), new Vector2(320f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject sliderObj = new GameObject("Slider_" + label);
        sliderObj.transform.SetParent(row.transform, false);
        Image sliderBackground = sliderObj.AddComponent<Image>();
        sliderBackground.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(320f, 18f);
        sliderRect.anchoredPosition = new Vector2(150f, 0f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderObj.transform, false);
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
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(sliderObj.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 28f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat(prefKey, 0.8f)));
        slider.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetFloat(prefKey, value);
            PlayerPrefs.Save();
            onChanged?.Invoke(value);
        });

        TextMeshProUGUI valueLabel = new GameObject("Val_" + label).AddComponent<TextMeshProUGUI>();
        valueLabel.transform.SetParent(row.transform, false);
        valueLabel.fontSize = 20f;
        valueLabel.color = new Color(0.82f, 0.90f, 1f, 0.92f);
        valueLabel.alignment = TextAlignmentOptions.MidlineLeft;
        if (prismFont != null)
            valueLabel.font = prismFont;
        RectTransform valueRect = valueLabel.rectTransform;
        valueRect.anchorMin = new Vector2(0.5f, 0.5f);
        valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.sizeDelta = new Vector2(90f, 30f);
        valueRect.anchoredPosition = new Vector2(320f, 0f);
        valueLabel.text = Mathf.RoundToInt(slider.value * 100f) + "%";
        slider.onValueChanged.AddListener(value =>
        {
            valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        });

        return slider;
    }

    private Slider CreateSensitivityRow(Transform parent, string label, Vector2 position)
    {
        GameObject row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(700f, 60f);
        rowRect.anchoredPosition = position;

        CreateLabel(row.transform, label, 24f, Color.white, new Vector2(-190f, 0f), new Vector2(320f, 42f), false, TextAlignmentOptions.MidlineRight);

        GameObject sliderObj = new GameObject("Slider_" + label);
        sliderObj.transform.SetParent(row.transform, false);
        Image sliderBackground = sliderObj.AddComponent<Image>();
        sliderBackground.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(320f, 18f);
        sliderRect.anchoredPosition = new Vector2(150f, 0f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = LookSensitivityRuntime.MinSlider;
        slider.maxValue = LookSensitivityRuntime.MaxSlider;
        slider.wholeNumbers = false;

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderObj.transform, false);
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
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(sliderObj.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 28f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        TextMeshProUGUI valueLabel = new GameObject("Val_" + label).AddComponent<TextMeshProUGUI>();
        valueLabel.transform.SetParent(row.transform, false);
        valueLabel.fontSize = 20f;
        valueLabel.color = new Color(0.82f, 0.90f, 1f, 0.92f);
        valueLabel.alignment = TextAlignmentOptions.MidlineLeft;
        if (prismFont != null)
            valueLabel.font = prismFont;
        RectTransform valueRect = valueLabel.rectTransform;
        valueRect.anchorMin = new Vector2(0.5f, 0.5f);
        valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.sizeDelta = new Vector2(90f, 30f);
        valueRect.anchoredPosition = new Vector2(320f, 0f);

        LookSensitivityRuntime.LoadFromPrefs();
        slider.SetValueWithoutNotify(LookSensitivityRuntime.SliderValue);
        valueLabel.text = LookSensitivityRuntime.SliderValue.ToString("0.0");
        slider.onValueChanged.AddListener(value =>
        {
            LookSensitivityRuntime.SetSliderValue(value, persist: true);
            valueLabel.text = value.ToString("0.0");
        });

        return slider;
    }

    private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        CreateButton(parent, text, position, action, new Vector2(300f, 72f), null);
    }

    private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action, Vector2 size, System.Func<string> dynamicText)
    {
        Image buttonImage = new GameObject("Btn_" + text).AddComponent<Image>();
        buttonImage.transform.SetParent(parent, false);
        buttonImage.color = Color.white;

        RectTransform rect = buttonImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.38f, 0.30f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);

        TextMeshProUGUI label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(buttonImage.transform, false);
        label.text = dynamicText != null ? dynamicText() : text;
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.10f, 0.10f, 0.14f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        if (prismFont != null)
            label.font = prismFont;

        Stretch(label.rectTransform);

        if (dynamicText != null)
        {
            PauseDynamicLabel dynamicLabel = buttonImage.gameObject.AddComponent<PauseDynamicLabel>();
            dynamicLabel.label = label;
            dynamicLabel.getText = dynamicText;
        }
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size, bool bold)
    {
        CreateLabel(parent, text, fontSize, color, position, size, bold, TextAlignmentOptions.Center);
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size, bool bold, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = new GameObject("Txt_" + text).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        if (prismFont != null)
            label.font = prismFont;
        if (bold)
            label.fontStyle = FontStyles.Bold;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void ShowPanel(GameObject targetPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(targetPanel == mainPanel);
        if (optionsPanel != null) optionsPanel.SetActive(targetPanel == optionsPanel);
        if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);
    }

    private string GetDifficultyLabel()
    {
        return (GameManager.Instance != null ? GameManager.Instance.difficulty : PlayerPrefs.GetString("Difficulty", "Normal")).ToUpperInvariant();
    }

    private void CycleDifficulty()
    {
        string current = GameManager.Instance != null ? GameManager.Instance.difficulty : PlayerPrefs.GetString("Difficulty", "Normal");
        string next = current == "Easy" ? "Normal" : current == "Normal" ? "Hard" : "Easy";
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDifficulty(next);
        }
        else
        {
            PlayerPrefs.SetString("Difficulty", next);
            PlayerPrefs.Save();
        }
    }

    private string GetPerspectiveLabel()
    {
        // Third-person only.
        return "THIRD PERSON";
    }

    private void CyclePerspective()
    {
        // Third-person only: keep the button functional (re-applies runtime prefs),
        // but do not allow toggling to first-person.
        if (GameManager.Instance != null)
            GameManager.Instance.SetPerspectiveMode(GameManager.PerspectiveMode.ThirdPerson);
        else
        {
            PlayerPrefs.SetInt("PerspectiveMode", (int)GameManager.PerspectiveMode.ThirdPerson);
            PlayerPrefs.Save();
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.RefreshGameplayPreferences();
    }

    private string GetMovementLabel()
    {
        GameManager.MovementScheme scheme = GameManager.Instance != null
            ? GameManager.Instance.GetMovementScheme()
            : (GameManager.MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
        return scheme == GameManager.MovementScheme.ArrowKeys ? "ARROWS + MOUSE" : "WASD + MOUSE";
    }

    private void CycleMovement()
    {
        GameManager.MovementScheme current = GameManager.Instance != null
            ? GameManager.Instance.GetMovementScheme()
            : (GameManager.MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
        GameManager.MovementScheme next = current == GameManager.MovementScheme.Wasd
            ? GameManager.MovementScheme.ArrowKeys
            : GameManager.MovementScheme.Wasd;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMovementScheme(next);
        }
        else
        {
            PlayerPrefs.SetInt("MovementScheme", (int)next);
            PlayerPrefs.Save();
        }
    }

    private string GetGraphicsLabel()
    {
        int tier = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, graphicsLabels.Length - 1);
        return graphicsLabels[tier];
    }

    private void CycleGraphics()
    {
        int tier = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, graphicsLabels.Length - 1);
        int nextTier = (tier + 1) % graphicsLabels.Length;
        PlayerPrefs.SetInt("GraphicsTier", nextTier);
        PlayerPrefs.Save();

        int qualityLevel = nextTier == 0 ? 0 :
            nextTier == 1 ? Mathf.Max(0, (QualitySettings.names.Length - 1) / 2) :
            Mathf.Max(0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel);
    }

    private string GetFullscreenLabel()
    {
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        return fullscreen ? "ON" : "OFF";
    }

    private void ToggleFullscreen()
    {
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        fullscreen = !fullscreen;
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        Screen.fullScreen = fullscreen;
    }

    private TMP_FontAsset ResolvePrismFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            TMP_FontAsset font = fonts[i];
            if (font != null && font.name.Contains("Azonix"))
                return font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        isPaused = false;
    }
}

public class PauseDynamicLabel : MonoBehaviour
{
    public TextMeshProUGUI label;
    public System.Func<string> getText;

    private void Update()
    {
        if (label != null && getText != null)
            label.text = getText();
    }
}
