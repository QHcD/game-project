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
        customFont = ResolveMenuFont();
        EnsureEventSystem();
        BuildCurrentScreen();
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
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

    // ─── MAIN MENU ────────────────────────────────────────────────────────────────
    void BuildMainMenu(Transform root)
    {
        MakeText(root, "PRISM-7", 120, new Color(0.92f, 0.92f, 1f, 1f),
            new Vector2(0.18f, 0.66f), new Vector2(0.82f, 0.88f), true);
        MakeText(root, "WEAPON TRIALS", 74, new Color(0.45f, 0.20f, 0.75f, 0.24f),
            new Vector2(0.14f, 0.54f), new Vector2(0.86f, 0.74f), true);

        int continueLevel = GameManager.Instance != null ? GameManager.Instance.GetContinueLevel() : 1;
        MakeMenuButton(root, "CONTINUE", new Vector2(0.34f, 0.49f), new Vector2(0.66f, 0.56f), () => GameManager.Instance?.StartRun(continueLevel));
        MakeMenuButton(root, "SELECT LEVEL", new Vector2(0.34f, 0.40f), new Vector2(0.66f, 0.47f), () => ToggleLevelSelect(root));
        MakeMenuButton(root, "OPTIONS", new Vector2(0.34f, 0.31f), new Vector2(0.66f, 0.38f), () => SceneManager.LoadScene("Options"));
        MakeMenuButton(root, "SETTINGS", new Vector2(0.34f, 0.22f), new Vector2(0.66f, 0.29f), () => SceneManager.LoadScene("Settings"));
        MakeMenuButton(root, "CREDITS", new Vector2(0.34f, 0.13f), new Vector2(0.66f, 0.20f), () => SceneManager.LoadScene("Credits"));
        MakeMenuButton(root, "QUIT", new Vector2(0.34f, 0.04f), new Vector2(0.66f, 0.11f), QuitFromMainMenu);
    }

    // ─── RESULTS MENU ─────────────────────────────────────────────────────────────
    void BuildResultsMenu(Transform root)
    {
        // Ensure GameManager exists so button callbacks never silently fail
        EnsureGameManager();

        string title = "MISSION RESULT", subtitle = "Back to the Prism.", primaryButton = "MAIN MENU";
        UnityEngine.Events.UnityAction primaryAction = GoToMainMenuSafe;

        if (GameManager.PendingMenuScreen == GameManager.MenuScreen.LevelComplete)
        {
            int stars = GameManager.Instance != null ? GameManager.Instance.CalculateStars(120f) : 1;
            int score  = GameManager.Instance != null ? GameManager.Instance.score : 0;
            int level  = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
            title = "LEVEL COMPLETE";
            subtitle = "STARS  " + stars + " / 3"
                     + "\nSCORE  " + score
                     + "\nLEVEL  " + level;
            primaryButton = "NEXT LEVEL";
            primaryAction = () => { if (GameManager.Instance != null) GameManager.Instance.LoadNextLevel(); else GoToMainMenuSafe(); };
        }
        else if (GameManager.PendingMenuScreen == GameManager.MenuScreen.GameOver)
        {
            int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
            int score = GameManager.Instance != null ? GameManager.Instance.score : 0;
            title = "MISSION FAILED";
            subtitle = "LEVEL  " + level + "\nSCORE  " + score;
            primaryButton = "RETRY";
            primaryAction = () => { if (GameManager.Instance != null) GameManager.Instance.ReplayCurrentLevel(); else UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene"); };
        }
        else if (GameManager.PendingMenuScreen == GameManager.MenuScreen.Victory)
        {
            int score = GameManager.Instance != null ? GameManager.Instance.score : 0;
            title = "PRISM CLEARED";
            subtitle = "All 20 trials completed.\nFinal Score: " + score;
            primaryButton = "PLAY AGAIN";
            primaryAction = () => { if (GameManager.Instance != null) GameManager.Instance.StartRun(1); else UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene"); };
        }

        // Title
        MakeText(root, title, 92, new Color(0.92f, 0.92f, 1f, 1f),
            new Vector2(0.20f, 0.62f), new Vector2(0.80f, 0.82f), true);

        // Subtitle panel
        MakeText(root, subtitle, 42, new Color(0.94f, 0.94f, 1f, 1f),
            new Vector2(0.25f, 0.40f), new Vector2(0.75f, 0.60f));

        // Primary action button (NEXT LEVEL / RETRY / PLAY AGAIN) — large, bright
        MakeActiveButton(root, primaryButton, new Vector2(0.32f, 0.24f), new Vector2(0.68f, 0.33f),
            primaryAction, new Color(0.60f, 0.22f, 0.88f, 1f), Color.white);

        // MAIN MENU button — slightly smaller, secondary style
        MakeActiveButton(root, "MAIN MENU", new Vector2(0.35f, 0.11f), new Vector2(0.65f, 0.20f),
            GoToMainMenuSafe, new Color(0.18f, 0.18f, 0.28f, 1f), new Color(0.88f, 0.88f, 1f, 1f));
    }

    void EnsureGameManager()
    {
        if (GameManager.Instance != null) return;
        GameObject gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
    }

    void GoToMainMenuSafe()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.GoToMainMenu();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    // A clearly active, prominent button with solid background
    void MakeActiveButton(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction action,
        Color bgColor, Color textColor)
    {
        GameObject obj = new GameObject("ActiveBtn_" + label);
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = true;
        btn.onClick.AddListener(action);

        // Ensure button is unblocked
        btn.onClick.AddListener(action); // double-register is harmless but let's remove dupe
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(textColor.r * 0.6f, textColor.g * 0.6f, textColor.b * 0.6f, 0.5f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI lbl = CreateCenteredLabel(obj.transform, label, 32, textColor, true);
        lbl.fontStyle = FontStyles.Bold;

        AttachHoverEffect(obj, lbl, img, bgColor,
            new Color(Mathf.Min(1f, bgColor.r + 0.18f), Mathf.Min(1f, bgColor.g + 0.08f), Mathf.Min(1f, bgColor.b + 0.18f), 1f),
            textColor);
    }

    void ToggleLevelSelect(Transform root)
    {
        Transform existing = root.Find("LevelSelectOverlay");
        if (existing != null)
        {
            Destroy(existing.gameObject);
            SetMainMenuElementsVisible(root, true);
            return;
        }

        SetMainMenuElementsVisible(root, false);

        GameObject overlayObj = new GameObject("LevelSelectOverlay");
        overlayObj.transform.SetParent(root, false);
        Image overlay = overlayObj.AddComponent<Image>();
        Stretch(overlay.rectTransform);
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.10f);

        GameObject panelObj = new GameObject("LevelSelectPanel");
        panelObj.transform.SetParent(overlayObj.transform, false);
        Image panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0.16f, 0.20f, 0.30f, 0.30f);
        Outline panelOutline = panelObj.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.26f, 0.42f, 0.68f, 0.18f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        SetCenteredRect(panelObj.GetComponent<RectTransform>(), new Vector2(1140f, 760f), new Vector2(0f, -6f));

        MakeText(panelObj.transform, "SELECT LEVEL", 64, new Color(0.94f, 0.94f, 1f, 1f),
            new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.98f), true);

        MakeText(panelObj.transform, "MAP CHOICE", 30, new Color(0.94f, 0.94f, 1f, 0.90f),
            new Vector2(0.72f, 0.72f), new Vector2(0.95f, 0.82f), false);

        GameObject gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(panelObj.transform, false);
        RectTransform gridRT = gridObj.AddComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0.05f, 0.22f);
        gridRT.anchorMax = new Vector2(0.66f, 0.82f);
        gridRT.offsetMin = gridRT.offsetMax = Vector2.zero;

        GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(140f, 108f);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(6, 6, 6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperCenter;

        int unlockedLevels = GameManager.Instance != null ? GameManager.Instance.GetUnlockedLevelCount() : 1;
        for (int i = 1; i <= GameManager.TotalLevels; i++)
        {
            int level = i;
            bool isUnlocked = level <= unlockedLevels;
            bool isCurrent = GameManager.Instance != null && level == GameManager.Instance.currentLevel;
            MakeLevelTile(gridObj.transform, level, isUnlocked, isCurrent,
                () => GameManager.Instance?.StartRun(level));
        }

        GameObject mapLayoutObj = new GameObject("MapLayout");
        mapLayoutObj.transform.SetParent(panelObj.transform, false);
        RectTransform mapLayoutRect = mapLayoutObj.AddComponent<RectTransform>();
        mapLayoutRect.anchorMin = new Vector2(0.72f, 0.22f);
        mapLayoutRect.anchorMax = new Vector2(0.95f, 0.77f);
        mapLayoutRect.offsetMin = mapLayoutRect.offsetMax = Vector2.zero;

        string[] mapNames = { "BLACKSITE\nFACILITY", "CYBERRUINS\nNEON", "CONTAINER\nPORT YARD" };

        float[] btnMinY = { 0.72f, 0.42f, 0.12f };
        float[] btnMaxY = { 0.88f, 0.58f, 0.28f };

        Image[] mapImages = new Image[3];
        TextMeshProUGUI[] mapLabels = new TextMeshProUGUI[3];
        MenuButtonHoverEffect[] mapHovers = new MenuButtonHoverEffect[3];

        int selectedMapIndex = PlayerPrefs.GetInt("SelectedMap", 0);

        for (int m = 0; m < mapNames.Length; m++)
        {
            int currentIndex = m;

            GameObject mapBtn = new GameObject("MapBtn_" + m);
            mapBtn.transform.SetParent(mapLayoutObj.transform, false);

            mapImages[m] = mapBtn.AddComponent<Image>();
            mapImages[m].color = m == selectedMapIndex
                ? new Color(0.90f, 0.40f, 0.90f, 1f)
                : Color.white; // تم تغيير الخلفية إلى الأبيض كما طلبت

            Button mapBtnComp = mapBtn.AddComponent<Button>();
            mapBtnComp.targetGraphic = mapImages[m];

            RectTransform mapBtnRect = mapBtn.GetComponent<RectTransform>();
            mapBtnRect.anchorMin = new Vector2(0.00f, btnMinY[m]);
            mapBtnRect.anchorMax = new Vector2(1.00f, btnMaxY[m]);
            mapBtnRect.offsetMin = mapBtnRect.offsetMax = Vector2.zero;

            Color txtColor = m == selectedMapIndex
                ? new Color(0.15f, 0.08f, 0.24f, 1f)
                : new Color(0.15f, 0.12f, 0.22f, 1f); // خط داكن ليكون واضح على الأبيض

            mapLabels[m] = CreateCenteredLabel(mapBtn.transform, mapNames[m], 21, txtColor, true);

            mapHovers[m] = mapBtn.AddComponent<MenuButtonHoverEffect>();
            mapHovers[m].label = mapLabels[m];
            mapHovers[m].background = mapImages[m];
            mapHovers[m].normalTextColor = mapLabels[m].color;
            mapHovers[m].hoverTextColor = new Color(0.15f, 0.08f, 0.24f, 1f);
            mapHovers[m].normalBackgroundColor = mapImages[m].color;
            mapHovers[m].hoverBackgroundColor = new Color(1f, 0.80f, 1f, 1f);

            mapBtnComp.onClick.AddListener(() =>
            {
                PlayerPrefs.SetInt("SelectedMap", currentIndex);
                PlayerPrefs.Save();

                for (int i = 0; i < mapNames.Length; i++)
                {
                    bool isSelected = (i == currentIndex);
                    mapImages[i].color = isSelected ? new Color(0.90f, 0.40f, 0.90f, 1f) : Color.white;
                    mapLabels[i].color = isSelected ? new Color(0.15f, 0.08f, 0.24f, 1f) : new Color(0.15f, 0.12f, 0.22f, 1f);

                    mapHovers[i].normalBackgroundColor = mapImages[i].color;
                    mapHovers[i].normalTextColor = mapLabels[i].color;
                }
            });
        }

        // تم إضافة <b> حول كلمة RETURN لتكون عريضة (Bold) إجبارياً
        MakePanelButton(panelObj.transform, "<b>RETURN</b>",
            new Vector2(0.40f, 0.03f), new Vector2(0.60f, 0.13f),
            () =>
            {
                Destroy(overlayObj);
                SetMainMenuElementsVisible(root, true);
            });
    }

    // ─── LEVEL TILE ───────────────────────────────────────────────────────────────
    void MakeLevelTile(Transform parent, int level, bool isUnlocked, bool isCurrent,
        UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject("Level_" + level);
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.color = isCurrent
            ? new Color(0.84f, 0.29f, 0.82f, 1f)
            : isUnlocked
                ? new Color(0.94f, 0.94f, 0.96f, 1f)
                : new Color(0.22f, 0.18f, 0.30f, 0.55f);

        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = isUnlocked;
        if (isUnlocked) btn.onClick.AddListener(action);

        Outline glow = obj.AddComponent<Outline>();
        glow.effectColor = isCurrent
            ? new Color(0.86f, 0.32f, 0.86f, 0.50f)
            : isUnlocked
                ? new Color(0.60f, 0.18f, 0.88f, 0.22f)
                : new Color(0f, 0f, 0f, 0.12f);
        glow.effectDistance = new Vector2(2f, -2f);

        Color numColor = isCurrent
            ? new Color(0.20f, 0.16f, 0.32f, 1f)
            : isUnlocked
                ? new Color(0.28f, 0.26f, 0.38f, 1f)
                : new Color(0.50f, 0.48f, 0.58f, 0.50f);

        TextMeshProUGUI lbl = CreateCenteredLabel(obj.transform, level.ToString(), 44, numColor, true);

        if (isUnlocked)
            AttachHoverEffect(obj, lbl, img,
                img.color,
                new Color(0.76f, 0.18f, 0.95f, 0.85f),
                Color.white);
    }

    // ─── SHARED HELPERS ───────────────────────────────────────────────────────────

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
        img.color = Color.white;
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.38f, 0.30f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI labelText = CreateCenteredLabel(obj.transform, label, 26,
            new Color(0.08f, 0.08f, 0.12f, 1f), true);
        labelText.fontStyle = FontStyles.Bold;
        labelText.fontSize = 30f;
        labelText.color = new Color(0.08f, 0.08f, 0.12f, 1f);

        AttachHoverEffect(obj, labelText, img,
            Color.white,
            new Color(0.98f, 0.98f, 1f, 1f),
            new Color(0.08f, 0.08f, 0.12f, 1f));
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
        label.raycastTarget = false;
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

    void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    void SetMainMenuElementsVisible(Transform root, bool isVisible)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string childName = child.name;
            if (childName == "Background" || childName == "Overlay" || childName == "LevelSelectOverlay")
                continue;

            child.gameObject.SetActive(isVisible);
        }
    }

    void QuitFromMainMenu()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    TMP_FontAsset ResolveMenuFont()
    {
        if (customFont != null)
            return customFont;

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
