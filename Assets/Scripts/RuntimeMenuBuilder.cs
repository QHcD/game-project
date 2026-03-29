using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RuntimeMenuBuilder : MonoBehaviour
{
    public Sprite backgroundImage;
    public TMP_FontAsset customFont;

    void Start()
    {
        BuildCurrentScreen();
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

    void BuildMainMenu(Transform root)
    {
        MakeText(root, "PRISM-7", 120, new Color(0.92f, 0.92f, 1f, 1f), new Vector2(0.18f, 0.66f), new Vector2(0.82f, 0.88f), true);
        MakeText(root, "WEAPON TRIALS", 74, new Color(0.45f, 0.20f, 0.75f, 0.28f), new Vector2(0.14f, 0.54f), new Vector2(0.86f, 0.74f), true);

        MakeMenuButton(root, "START GAME", new Vector2(0.34f, 0.45f), new Vector2(0.66f, 0.52f), () => GameManager.Instance?.StartRun(1));
        MakeMenuButton(root, "SELECT LEVEL", new Vector2(0.34f, 0.36f), new Vector2(0.66f, 0.43f), () => ToggleLevelSelect(root));
        MakeMenuButton(root, "SETTINGS", new Vector2(0.34f, 0.27f), new Vector2(0.66f, 0.34f), () => SceneManager.LoadScene("Settings"));
        MakeMenuButton(root, "CREDITS", new Vector2(0.34f, 0.18f), new Vector2(0.66f, 0.25f), () => SceneManager.LoadScene("Credits"));
        MakeMenuButton(root, "QUIT", new Vector2(0.34f, 0.09f), new Vector2(0.66f, 0.16f), Application.Quit);
    }

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
        MakeText(root, subtitle, 36, Color.white, new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.56f));
        MakePanelButton(root, primaryButton, new Vector2(0.39f, 0.21f), new Vector2(0.61f, 0.28f), primaryAction);
        MakePanelButton(root, "MAIN MENU", new Vector2(0.39f, 0.11f), new Vector2(0.61f, 0.18f), () => GameManager.Instance?.GoToMainMenu());
    }

    void ToggleLevelSelect(Transform root)
    {
        Transform existing = root.Find("LevelSelectOverlay");
        if (existing != null)
        {
            Destroy(existing.gameObject);
            return;
        }

        GameObject overlayObj = new GameObject("LevelSelectOverlay");
        overlayObj.transform.SetParent(root, false);
        Image overlay = overlayObj.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.14f);
        Stretch(overlay.GetComponent<RectTransform>());

        GameObject panelObj = new GameObject("LevelSelectPanel");
        panelObj.transform.SetParent(overlayObj.transform, false);
        Image panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0.05f, 0.04f, 0.14f, 0.42f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.23f, 0.23f);
        panelRect.anchorMax = new Vector2(0.77f, 0.77f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        Outline outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.22f, 0.82f, 0.45f);
        outline.effectDistance = new Vector2(4f, -4f);

        MakeText(panelObj.transform, "SELECT LEVEL", 54, new Color(0.94f, 0.94f, 1f, 1f),
            new Vector2(0.14f, 0.84f), new Vector2(0.86f, 0.97f), true);

        GameObject gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(panelObj.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.08f, 0.14f);
        gridRect.anchorMax = new Vector2(0.92f, 0.76f);
        gridRect.offsetMin = gridRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(148f, 108f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        int unlockedLevels = GameManager.Instance != null ? GameManager.Instance.GetUnlockedLevelCount() : 1;
        for (int i = 1; i <= GameManager.TotalLevels; i++)
        {
            int level = i;
            bool isUnlocked = level <= unlockedLevels;
            bool isCurrent = GameManager.Instance != null && level == GameManager.Instance.currentLevel;
            MakeLevelButton(gridObj.transform, level, isUnlocked, isCurrent, () => GameManager.Instance?.StartRun(level));
        }

        MakePanelButton(overlayObj.transform, "RETURN", new Vector2(0.23f, 0.13f), new Vector2(0.31f, 0.18f), () => Destroy(overlayObj));
    }

    void MakeText(Transform parent, string text, float size, Color color, Vector2 anchorMin, Vector2 anchorMax, bool isTitle = false)
    {
        GameObject obj = new GameObject("Text_" + text.Substring(0, Mathf.Min(text.Length, 5)));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (customFont != null)
            tmp.font = customFont;

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

    void MakeMenuButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject(label + "_Btn");
        obj.transform.SetParent(parent, false);
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        MakeText(obj.transform, label, 40, new Color(0.95f, 0.95f, 1f, 1f), Vector2.zero, Vector2.one, true);
    }

    void MakePanelButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action)
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

        MakeText(obj.transform, label, 24, new Color(0.24f, 0.22f, 0.38f, 1f), Vector2.zero, Vector2.one);
    }

    void MakeLevelButton(Transform parent, int level, bool isUnlocked, bool isCurrent, UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject("Level_" + level);
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.color = isCurrent
            ? new Color(0.85f, 0.30f, 0.82f, 1f)
            : isUnlocked ? new Color(0.94f, 0.94f, 0.96f, 1f) : new Color(0.94f, 0.94f, 0.96f, 0.72f);
        Button btn = obj.AddComponent<Button>();
        btn.interactable = isUnlocked;
        if (isUnlocked)
            btn.onClick.AddListener(action);

        TextMeshProUGUI label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(obj.transform, false);
        label.text = level.ToString();
        label.fontSize = 34;
        label.color = isCurrent
            ? new Color(0.24f, 0.22f, 0.38f, 1f)
            : isUnlocked ? new Color(0.32f, 0.32f, 0.40f, 1f) : new Color(0.32f, 0.32f, 0.40f, 0.58f);
        label.alignment = TextAlignmentOptions.Center;
        if (customFont != null)
            label.font = customFont;
        Stretch(label.GetComponent<RectTransform>());
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
