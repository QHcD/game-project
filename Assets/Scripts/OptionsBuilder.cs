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

    private void Start()
    {
        EnsureEventSystem();
        BuildOptionsMenu();
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
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

        MakeText(canvasObject.transform, "OPTIONS", 62f, new Color(0.78f, 0.84f, 1f, 1f),
            new Vector2(0f, 300f), new Vector2(720f, 84f), true, TextAlignmentOptions.Center);
        MakeText(canvasObject.transform, "MATCH THE RUN TO YOUR STYLE", 22f, new Color(0.72f, 0.84f, 1f, 0.88f),
            new Vector2(0f, 248f), new Vector2(720f, 34f), false, TextAlignmentOptions.Center);

        GameObject panelObject = new GameObject("CentralPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.16f, 0.20f, 0.30f, 0.30f);
        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.26f, 0.42f, 0.68f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(980f, 460f), new Vector2(0f, -10f));

        difficultyDropdown = MakeDropdownRow(panel.transform, "DIFFICULTY:", new List<string>(difficultyOptions),
            DifficultyIndex(), 80f, OnDifficultyChanged);
        perspectiveDropdown = MakeDropdownRow(panel.transform, "CAMERA VIEW:", new List<string>(perspectiveOptions),
            PerspectiveIndex(), 0f, OnPerspectiveChanged);
        controlDropdown = MakeDropdownRow(panel.transform, "MOVE STYLE:", new List<string>(controlOptions),
            ControlIndex(), -80f, OnControlChanged);

        // FIX: Moved info text lower so it doesn't overlap dropdowns
        MakeInfoText(panel.transform, "Choose how intense the enemies are, how the camera follows you, and whether movement uses WASD or Arrow keys.",
            new Vector2(0f, -170f), new Vector2(760f, 70f));

        MakePrismButton(canvasObject.transform, "RETURN", new Vector2(-150f, -305f), () => SceneManager.LoadScene("MainMenu"));
        MakePrismButton(canvasObject.transform, "RESET", new Vector2(150f, -305f), ResetOptions);
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
        return Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", (int)GameManager.PerspectiveMode.ThirdPerson), 0, 1);
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
        GameManager.PerspectiveMode perspective = selectedIndex == 1
            ? GameManager.PerspectiveMode.ThirdPerson
            : GameManager.PerspectiveMode.FirstPerson;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPerspectiveMode(perspective);
        }
        else
        {
            PlayerPrefs.SetInt("PerspectiveMode", (int)perspective);
            PlayerPrefs.Save();
        }
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
            perspectiveDropdown.SetValueWithoutNotify((int)GameManager.PerspectiveMode.ThirdPerson);
            perspectiveDropdown.RefreshShownValue();
        }

        if (controlDropdown != null)
        {
            controlDropdown.SetValueWithoutNotify(0);
            controlDropdown.RefreshShownValue();
        }

        OnDifficultyChanged(1);
        OnPerspectiveChanged((int)GameManager.PerspectiveMode.ThirdPerson);
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

        Image dropdownBackground = dropdownObject.AddComponent<Image>();
        dropdownBackground.color = new Color(0.94f, 0.94f, 0.98f, 0.98f);

        TMP_Dropdown dropdown = dropdownObject.AddComponent<TMP_Dropdown>();
        SetRect(dropdownObject.GetComponent<RectTransform>(), new Vector2(340f, 52f), position);

        TextMeshProUGUI captionText = MakeDropdownText(dropdownObject.transform, "Label", TextAlignmentOptions.MidlineLeft, new Vector2(16f, 0f), new Vector2(-40f, 0f));
        dropdown.captionText = captionText;

        GameObject arrowObject = new GameObject("Arrow");
        arrowObject.transform.SetParent(dropdownObject.transform, false);
        TextMeshProUGUI arrow = arrowObject.AddComponent<TextMeshProUGUI>();
        arrow.text = "▼";
        arrow.fontSize = 18f;
        arrow.color = new Color(0.24f, 0.22f, 0.38f, 1f);
        arrow.alignment = TextAlignmentOptions.Center;
        if (prismFont != null) arrow.font = prismFont;
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.sizeDelta = new Vector2(36f, 0f);
        arrowRect.anchoredPosition = new Vector2(-4f, 0f);

        TextMeshProUGUI itemLabel;
        RectTransform templateRect = CreateDropdownTemplate(dropdownObject.transform, out itemLabel);
        dropdown.template = templateRect;
        dropdown.itemText = itemLabel;

        List<TMP_Dropdown.OptionData> dropdownOptions = new List<TMP_Dropdown.OptionData>();
        foreach (string option in options)
        {
            dropdownOptions.Add(new TMP_Dropdown.OptionData(option));
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

        GameObject checkmarkObject = new GameObject("Item Checkmark");
        checkmarkObject.transform.SetParent(itemObject.transform, false);
        TextMeshProUGUI checkmark = checkmarkObject.AddComponent<TextMeshProUGUI>();
        checkmark.text = "✓";  // FIX: proper checkmark character instead of "v"
        checkmark.fontSize = 20f;
        checkmark.color = new Color(0.24f, 0.22f, 0.38f, 1f);
        checkmark.alignment = TextAlignmentOptions.Center;
        if (prismFont != null)
        {
            checkmark.font = prismFont;
        }

        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 0f);
        checkRect.anchorMax = new Vector2(0f, 1f);
        checkRect.pivot = new Vector2(0.5f, 0.5f);
        checkRect.sizeDelta = new Vector2(28f, 0f);
        checkRect.anchoredPosition = new Vector2(18f, 0f);
        itemToggle.graphic = checkmark;

        itemLabel = MakeDropdownText(itemObject.transform, "Item Label", TextAlignmentOptions.MidlineLeft, new Vector2(40f, 0f), new Vector2(-12f, 0f));

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

    private void MakePrismButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        Image buttonImage = new GameObject("Btn_" + label).AddComponent<Image>();
        buttonImage.transform.SetParent(parent, false);
        buttonImage.color = Color.white;
        SetRect(buttonImage.GetComponent<RectTransform>(), new Vector2(190f, 58f), position);

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.38f, 0.30f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);

        TextMeshProUGUI labelText = MakeText(buttonImage.transform, label, 24f, new Color(0.10f, 0.10f, 0.14f, 1f), Vector2.zero, new Vector2(190f, 58f), false, TextAlignmentOptions.Center);
        labelText.fontStyle = FontStyles.Bold;
        AttachHoverEffect(buttonImage.gameObject, labelText, buttonImage);
    }

    private void AttachHoverEffect(GameObject target, TextMeshProUGUI label, Image image)
    {
        MenuButtonHoverEffect hover = target.AddComponent<MenuButtonHoverEffect>();
        hover.label = label;
        hover.background = image;
        hover.normalTextColor = new Color(0.10f, 0.10f, 0.14f, 1f);
        // FIX: hover text color was identical to normal — now gives visible feedback
        hover.hoverTextColor = new Color(0.25f, 0.45f, 0.95f, 1f);
        hover.normalBackgroundColor = Color.white;
        hover.hoverBackgroundColor = new Color(0.88f, 0.92f, 1f, 1f);
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
}
