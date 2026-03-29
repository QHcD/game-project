using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RuntimeMenuBuilder : MonoBehaviour
{
    [Header("UI Customization")]
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

        if (backgroundImage != null)
        {
            bg.sprite = backgroundImage;
            bg.color = Color.white;
        }
        else
        {
            bg.color = new Color(0.02f, 0.02f, 0.06f, 1f);
        }

        if (GameManager.Instance == null || GameManager.PendingMenuScreen == GameManager.MenuScreen.MainMenu)
        {
            BuildMainMenu(canvasObj.transform);
            return;
        }

        BuildResultsMenu(canvasObj.transform);
    }

    void BuildMainMenu(Transform root)
    {
        MakeText(root, "PRISM-7\nWEAPON TRIALS", 120, new Color(0.6f, 0.3f, 1f, 1f),
            new Vector2(0f, 0.65f), new Vector2(1f, 0.95f), true);

        MakeButton(root, "START GAME", new Vector2(0f, 0.50f), new Vector2(1f, 0.58f),
            () => GameManager.Instance?.StartRun(1));

        MakeButton(root, "SELECT LEVEL", new Vector2(0f, 0.40f), new Vector2(1f, 0.48f),
            () => ToggleLevelSelect(root));

        MakeButton(root, "SETTINGS", new Vector2(0f, 0.30f), new Vector2(1f, 0.38f),
            () => SceneManager.LoadScene("Settings"));

        MakeButton(root, "CREDITS", new Vector2(0f, 0.20f), new Vector2(1f, 0.28f),
            () => SceneManager.LoadScene("Credits"));

        MakeButton(root, "QUIT", new Vector2(0f, 0.10f), new Vector2(1f, 0.18f),
            () => Application.Quit());
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
            subtitle = "Level: " + GameManager.Instance.currentLevel +
                "\nScore: " + GameManager.Instance.score;
            primaryButton = "RETRY";
            primaryAction = () => GameManager.Instance?.ReplayCurrentLevel();
        }
        else if (GameManager.PendingMenuScreen == GameManager.MenuScreen.Victory)
        {
            title = "PRISM CLEARED";
            subtitle = "All 20 trials completed." +
                "\nFinal Score: " + GameManager.Instance.score;
            primaryButton = "PLAY AGAIN";
            primaryAction = () => GameManager.Instance?.StartRun(1);
        }

        MakeText(root, title, 110, new Color(0.7f, 0.85f, 1f, 1f),
            new Vector2(0f, 0.64f), new Vector2(1f, 0.90f), true);

        MakeText(root, subtitle, 42, Color.white,
            new Vector2(0.15f, 0.40f), new Vector2(0.85f, 0.60f));

        MakeButton(root, primaryButton, new Vector2(0.30f, 0.20f), new Vector2(0.70f, 0.30f), primaryAction);
        MakeButton(root, "MAIN MENU", new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.18f),
            () => GameManager.Instance?.GoToMainMenu());
    }

    void ToggleLevelSelect(Transform root)
    {
        Transform existing = root.Find("LevelSelectPanel");
        if (existing != null)
        {
            Destroy(existing.gameObject);
            return;
        }

        GameObject panelObj = new GameObject("LevelSelectPanel");
        panelObj.transform.SetParent(root, false);
        Image panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0.04f, 0.05f, 0.10f, 0.96f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.12f, 0.15f);
        panelRect.anchorMax = new Vector2(0.88f, 0.82f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        MakeText(panelObj.transform, "SELECT LEVEL", 58, Color.white,
            new Vector2(0.1f, 0.84f), new Vector2(0.9f, 0.97f), true);

        GameObject gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(panelObj.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.06f, 0.08f);
        gridRect.anchorMax = new Vector2(0.94f, 0.80f);
        gridRect.offsetMin = gridRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(120f, 80f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        int unlockedLevels = GameManager.Instance != null ? GameManager.Instance.GetUnlockedLevelCount() : 1;
        for (int i = 1; i <= GameManager.TotalLevels; i++)
        {
            int level = i;
            bool isUnlocked = level <= unlockedLevels;
            MakeLevelButton(gridObj.transform, level, isUnlocked, () => GameManager.Instance?.StartRun(level));
        }
    }

    void MakeText(Transform parent, string text, float size, Color color, Vector2 anchorMin, Vector2 anchorMax, bool isTitle = false)
    {
        var obj = new GameObject("Text_" + text.Substring(0, Mathf.Min(text.Length, 5)));
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        if (customFont != null)
            tmp.font = customFont;

        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;

        if (isTitle)
        {
            tmp.fontStyle = FontStyles.Bold;
            Outline outline = obj.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    void MakeButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject(label + "_Btn");
        obj.transform.SetParent(parent, false);

        var img = obj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);

        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;

        MakeText(obj.transform, label, 45, new Color(0.4f, 0.8f, 1f, 1f), Vector2.zero, Vector2.one);
    }

    void MakeLevelButton(Transform parent, int level, bool isUnlocked, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject("Level_" + level);
        obj.transform.SetParent(parent, false);

        var img = obj.AddComponent<Image>();
        img.color = isUnlocked ? new Color(0.85f, 0.9f, 1f, 0.95f) : new Color(0.25f, 0.25f, 0.30f, 0.9f);

        var btn = obj.AddComponent<Button>();
        btn.interactable = isUnlocked;
        if (isUnlocked)
            btn.onClick.AddListener(action);

        MakeText(obj.transform, isUnlocked ? level.ToString() : "LOCK", 36,
            isUnlocked ? new Color(0.08f, 0.12f, 0.24f, 1f) : new Color(0.8f, 0.8f, 0.85f, 1f),
            Vector2.zero, Vector2.one);
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
