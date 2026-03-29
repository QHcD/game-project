using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RuntimeMenuBuilder : MonoBehaviour
{
    public Sprite backgroundImage;
    public TMP_FontAsset customFont;

    void Start()
    {
        EnsureEventSystem();
        BuildCurrentScreen();
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<InputSystemUIInputModule>();
    }

    void BuildCurrentScreen()
    {
        GameObject existingCanvas = GameObject.Find("NeonCanvas");
        if (existingCanvas != null)
            Destroy(existingCanvas);

        GameObject canvasObj = new GameObject("NeonCanvas");
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
        if (backgroundImage != null)
        {
            bg.sprite = backgroundImage;
            bg.color = Color.white;
        }

        Image overlay = new GameObject("Overlay").AddComponent<Image>();
        overlay.transform.SetParent(canvasObj.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.48f);

        if (GameManager.Instance == null || GameManager.PendingMenuScreen == GameManager.MenuScreen.MainMenu)
        {
            BuildMainMenu(canvasObj.transform);
            return;
        }

        BuildResultsMenu(canvasObj.transform);
    }

    // ── ORIGINAL MAIN MENU — NOT TOUCHED ─────────────────────────────────────────
    void BuildMainMenu(Transform root)
    {
        MakeText(root, "PRISM-7", 120, new Color(0.92f, 0.92f, 1f, 1f), new Vector2(0.18f, 0.66f), new Vector2(0.82f, 0.88f), true);
        MakeText(root, "WEAPON TRIALS", 74, new Color(0.45f, 0.20f, 0.75f, 0.24f), new Vector2(0.14f, 0.54f), new Vector2(0.86f, 0.74f), true);

        int continueLevel = GameManager.Instance != null ? GameManager.Instance.GetContinueLevel() : 1;
        MakeMenuButton(root, "CONTINUE", new Vector2(0.34f, 0.45f), new Vector2(0.66f, 0.52f), () => GameManager.Instance?.StartRun(continueLevel));
        MakeMenuButton(root, "SELECT LEVEL", new Vector2(0.34f, 0.36f), new Vector2(0.66f, 0.43f), () => ToggleLevelSelect(root));
        MakeMenuButton(root, "SETTINGS", new Vector2(0.34f, 0.27f), new Vector2(0.66f, 0.34f), () => SceneManager.LoadScene("Settings"));
        MakeMenuButton(root, "CREDITS", new Vector2(0.34f, 0.18f), new Vector2(0.66f, 0.25f), () => SceneManager.LoadScene("Credits"));
        MakeMenuButton(root, "QUIT", new Vector2(0.34f, 0.09f), new Vector2(0.66f, 0.16f), Application.Quit);
    }

    // ── RESULTS MENU — NOT TOUCHED ────────────────────────────────────────────────
    void BuildResultsMenu(Transform root)
    {
        string title = "MISSION RESULT";
        string subtitle = "Back to the Prism.";
        string primaryButton = "MAIN MENU";
        UnityEngine.Events.UnityAction primaryAction = () => GameManager.Instance?.GoToMainMenu();

        if (GameManager.PendingMenuScreen == GameManager.MenuScreen.LevelComplete)
        {
            int stars = GameManager.Instance.CalculateStars(120f);
            title = "LEVEL COMPLETE";
            subtitle = "Stars: " + new string('*', stars) + new string('-', 3 - stars) +
                "\nScore: " + GameManager.Instance.score +
                "\nLevel: " + GameManager.Instance.currentLevel;
            primaryButton = "NEXT LEVEL";
            primaryAction = () => GameManager.Instance?.LoadNextLevel();
        }
        else if (GameManager.PendingMenuScreen == GameManager.MenuScreen.GameOver)
        {
            title = "MISSION FAILED";
            subtitle = "Level: " + GameManager.Instance.currentLevel + "\nScore: " + GameManager.Instance.score;
            primaryButton = "RETRY";
            primaryAction = () => GameManager.Instance?.ReplayCurrentLevel();
        }
        else if (GameManager.PendingMenuScreen == GameManager.MenuScreen.Victory)
        {
            title = "PRISM CLEARED";
            subtitle = "All 20 trials completed.\nFinal Score: " + GameManager.Instance.score;
            primaryButton = "PLAY AGAIN";
            primaryAction = () => GameManager.Instance?.StartRun(1);
        }

        MakeText(root, title, 92, new Color(0.92f, 0.92f, 1f, 1f), new Vector2(0.20f, 0.62f), new Vector2(0.80f, 0.82f), true);
        MakeText(root, subtitle, 42, Color.white, new Vector2(0.20f, 0.42f), new Vector2(0.80f, 0.58f));
        MakePanelButton(root, primaryButton, new Vector2(0.39f, 0.21f), new Vector2(0.61f, 0.28f), primaryAction);
        MakePanelButton(root, "MAIN MENU", new Vector2(0.39f, 0.11f), new Vector2(0.61f, 0.18f), () => GameManager.Instance?.GoToMainMenu());
    }

    // ── LEVEL SELECT — 6 columns grid (left) + Map Choice panel (right) ──────────
    void ToggleLevelSelect(Transform root)
    {
        Transform existing = root.Find("LevelSelectOverlay");
        if (existing != null)
        {
            Destroy(existing.gameObject);
            return;
        }

        // Full-screen dark backdrop
        GameObject overlayObj = new GameObject("LevelSelectOverlay");
        overlayObj.transform.SetParent(root, false);
        Image backdrop = overlayObj.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.01f, 0.06f, 0.95f);
        Stretch(backdrop.rectTransform);

        // ── "SELECT LEVEL" title — above the grid ──
        MakeText(overlayObj.transform, "SELECT LEVEL", 72, new Color(0.94f, 0.94f, 1f, 1f),
            new Vector2(0.02f, 0.87f), new Vector2(0.72f, 0.98f), true);

        // ── "MAP CHOICE" title — above the map panel ──
        MakeText(overlayObj.transform, "MAP CHOICE", 36, new Color(0.94f, 0.94f, 1f, 0.85f),
            new Vector2(0.74f, 0.87f), new Vector2(0.98f, 0.98f), false);

        // ── Level Grid — 6 columns, no background panel ──
        GameObject gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(overlayObj.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.02f, 0.10f);
        gridRect.anchorMax = new Vector2(0.72f, 0.86f);
        gridRect.offsetMin = gridRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(178f, 148f);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.UpperLeft;

        int unlockedLevels = GameManager.Instance != null ? GameManager.Instance.GetUnlockedLevelCount() : 1;

        for (int i = 1; i <= GameManager.TotalLevels; i++)
        {
            int level = i;
            bool isUnlocked = level <= unlockedLevels;
            bool isCurrent = GameManager.Instance != null && level == GameManager.Instance.currentLevel;
            MakeLevelButton(gridObj.transform, level, isUnlocked, isCurrent,
                () => GameManager.Instance?.StartRun(level));
        }

        // ── Map Choice Panel — right side ──
        GameObject mapPanel = new GameObject("MapChoicePanel");
        mapPanel.transform.SetParent(overlayObj.transform, false);
        Image mapPanelImg = mapPanel.AddComponent<Image>();
        mapPanelImg.color = new Color(0.10f, 0.06f, 0.18f, 0.60f);
        RectTransform mapPanelRect = mapPanel.GetComponent<RectTransform>();
        mapPanelRect.anchorMin = new Vector2(0.74f, 0.10f);
        mapPanelRect.anchorMax = new Vector2(0.98f, 0.86f);
        mapPanelRect.offsetMin = mapPanelRect.offsetMax = Vector2.zero;

        Outline mapOutline = mapPanel.AddComponent<Outline>();
        mapOutline.effectColor = new Color(0.70f, 0.20f, 0.90f, 0.50f);
        mapOutline.effectDistance = new Vector2(3f, -3f);

        // Map tiles
        string[] mapNames = { "BLACKSITE\nFACILITY", "CYBERRUINS\nNEON", "CONTAINER\nPORT YARD" };
        float[] minY = { 0.66f, 0.35f, 0.04f };
        float[] maxY = { 0.94f, 0.63f, 0.32f };

        for (int m = 0; m < mapNames.Length; m++)
        {
            GameObject mapBtn = new GameObject("MapBtn_" + m);
            mapBtn.transform.SetParent(mapPanel.transform, false);

            Image mapBtnImg = mapBtn.AddComponent<Image>();
            mapBtnImg.color = m == 0
                ? new Color(0.90f, 0.40f, 0.90f, 1f)  // selected — pink
                : new Color(0.94f, 0.94f, 0.96f, 1f);  // unselected — white

            Button mapBtnComp = mapBtn.AddComponent<Button>();
            mapBtnComp.targetGraphic = mapBtnImg;

            RectTransform mapBtnRect = mapBtn.GetComponent<RectTransform>();
            mapBtnRect.anchorMin = new Vector2(0.07f, minY[m]);
            mapBtnRect.anchorMax = new Vector2(0.93f, maxY[m]);
            mapBtnRect.offsetMin = mapBtnRect.offsetMax = Vector2.zero;

            Color txtColor = m == 0
                ? new Color(0.15f, 0.08f, 0.24f, 1f)   // dark on pink
                : new Color(0.45f, 0.44f, 0.55f, 1f);  // muted on white

            TextMeshProUGUI lbl = CreateCenteredLabel(mapBtn.transform, mapNames[m], 22, txtColor, true);
            AttachHoverEffect(mapBtn, lbl, mapBtnImg,
                mapBtnImg.color,
                new Color(1f, 0.80f, 1f, 1f),
                new Color(0.15f, 0.08f, 0.24f, 1f));
        }

        // ── RETURN button — bottom left, white bg with DARK text ──
        MakePanelButton(overlayObj.transform, "RETURN",
            new Vector2(0.02f, 0.02f), new Vector2(0.14f, 0.09f),
            () => Destroy(overlayObj));
    }

    // ── SHARED HELPERS ────────────────────────────────────────────────────────────

    void MakeText(Transform parent, string text, float size, Color color,
        Vector2 anchorMin, Vector2 anchorMax, bool isTitle = false)
    {
        GameObject obj = new GameObject("Text_" + text.Substring(0, Mathf.Min(text.Length, 5)));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (customFont != null) tmp.font = customFont;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        if (isTitle)
        {
            tmp.fontStyle = FontStyles.Bold;
            Outline outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.12f, 0.62f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    void MakeMenuButton(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject(label + "_Btn");
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = CreateCenteredLabel(obj.transform, label, 40, new Color(0.95f, 0.95f, 1f, 1f), true);
        AttachHoverEffect(obj, labelText, img,
            new Color(1f, 1f, 1f, 0f),
            new Color(0.55f, 0.22f, 0.82f, 0.18f),
            Color.white);
    }

    void MakePanelButton(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject(label + "_Btn");
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        // Dark text — visible on the light button background
        TextMeshProUGUI labelText = CreateCenteredLabel(obj.transform, label, 24, new Color(0.10f, 0.10f, 0.14f, 1f), true);
        AttachHoverEffect(obj, labelText, img,
            new Color(0.94f, 0.94f, 0.96f, 1f),
            new Color(1f, 0.88f, 1f, 1f),
            new Color(0.10f, 0.10f, 0.14f, 1f));
    }

    void MakeLevelButton(Transform parent, int level, bool isUnlocked, bool isCurrent,
        UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject("Level_" + level);
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.color = isCurrent
            ? new Color(0.84f, 0.29f, 0.82f, 1f)
            : isUnlocked
                ? new Color(0.94f, 0.94f, 0.96f, 1f)
                : new Color(0.94f, 0.94f, 0.96f, 0.72f);

        Button btn = obj.AddComponent<Button>();
        btn.interactable = isUnlocked;
        if (isUnlocked) btn.onClick.AddListener(action);

        Outline glow = obj.AddComponent<Outline>();
        glow.effectColor = isCurrent
            ? new Color(0.86f, 0.32f, 0.86f, 0.45f)
            : new Color(0f, 0f, 0f, 0.18f);
        glow.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI labelText = CreateCenteredLabel(obj.transform, level.ToString(), 48,
            isCurrent ? new Color(0.24f, 0.22f, 0.38f, 1f)
            : isUnlocked ? new Color(0.32f, 0.32f, 0.40f, 1f)
                         : new Color(0.32f, 0.32f, 0.40f, 0.58f),
            false);

        if (isUnlocked)
            AttachHoverEffect(obj, labelText, img,
                img.color,
                new Color(1f, 0.82f, 1f, 1f),
                new Color(0.18f, 0.18f, 0.24f, 1f));
    }

    TextMeshProUGUI CreateCenteredLabel(Transform parent, string text,
        float size, Color color, bool bold)
    {
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        if (bold) label.fontStyle = FontStyles.Bold;
        if (customFont != null) label.font = customFont;
        Stretch(label.GetComponent<RectTransform>());
        return label;
    }

    void AttachHoverEffect(GameObject target, TextMeshProUGUI label, Image image,
        Color normalBackground, Color hoverBackground, Color hoverTextColor)
    {
        MenuButtonHoverEffect hover = target.AddComponent<MenuButtonHoverEffect>();
        hover.label = label;
        hover.background = image;
        hover.normalTextColor = label.color;
        hover.hoverTextColor = hoverTextColor;
        hover.normalBackgroundColor = normalBackground;
        hover.hoverBackgroundColor = hoverBackground;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
