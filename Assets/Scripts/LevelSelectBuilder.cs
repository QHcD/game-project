using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelSelectBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    // Tracks which map button is "selected" visually
    private Button _map1Btn;
    private Button _map2Btn;

    void Start()
    {
        prismFont = ResolvePrismFont();
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Background
        Image bg = new GameObject("Background").AddComponent<Image>();
        bg.transform.SetParent(canvasObj.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.color = new Color(0.07f, 0.07f, 0.13f, 1f);
        if (prismBackground) { bg.sprite = prismBackground; bg.color = Color.white; }

        // ── Title ─────────────────────────────────────────────────────────────
        MakeText(canvasObj.transform, "SELECT LEVEL", 60, new Color(0.6f, 0.2f, 1f, 1f),
            new Vector2(0f, 0.90f), new Vector2(1f, 0.99f), true);

        // ── Map Selection Panel ───────────────────────────────────────────────
        MakeText(canvasObj.transform, "CHOOSE MAP", 28, new Color(0.8f, 0.8f, 0.8f, 1f),
            new Vector2(0.15f, 0.83f), new Vector2(0.85f, 0.90f), false);

        // Map panel background
        Image mapPanel = new GameObject("MapPanel").AddComponent<Image>();
        mapPanel.transform.SetParent(canvasObj.transform, false);
        mapPanel.color = new Color(0.08f, 0.08f, 0.18f, 0.7f);
        RectTransform mpRect = mapPanel.GetComponent<RectTransform>();
        mpRect.anchorMin = new Vector2(0.15f, 0.74f);
        mpRect.anchorMax = new Vector2(0.85f, 0.83f);
        mpRect.offsetMin = mpRect.offsetMax = Vector2.zero;

        // Map 1 button
        _map1Btn = MakeMapButton(canvasObj.transform, "MAP 1  (NukeTown)",
            new Vector2(0.17f, 0.75f), new Vector2(0.49f, 0.82f),
            () => SelectMap(GameManager.ArenaMap.Map1));

        // Map 2 button
        _map2Btn = MakeMapButton(canvasObj.transform, "MAP 2  (City)",
            new Vector2(0.51f, 0.75f), new Vector2(0.83f, 0.82f),
            () => SelectMap(GameManager.ArenaMap.Map2));

        // Highlight the currently saved map
        RefreshMapHighlight();

        // ── Level Grid Panel ──────────────────────────────────────────────────
        Image panel = new GameObject("Panel").AddComponent<Image>();
        panel.transform.SetParent(canvasObj.transform, false);
        panel.color = new Color(0.1f, 0.1f, 0.2f, 0.5f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.13f);
        panelRect.anchorMax = new Vector2(0.85f, 0.73f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        Outline outl = panel.gameObject.AddComponent<Outline>();
        outl.effectColor = new Color(0.6f, 0.2f, 1f, 1f);
        outl.effectDistance = new Vector2(3, -3);

        GridLayoutGroup grid = panel.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(130, 110);
        grid.spacing = new Vector2(20, 20);
        grid.padding = new RectOffset(40, 40, 30, 30);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        // 16 level buttons (all melee)
        for (int i = 1; i <= 16; i++)
        {
            int lvl = i;
            MakeLevelButton(panel.transform, lvl.ToString(), () =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.currentLevel = lvl;
                    GameManager.Instance.levelTime = 0;
                }
                SceneManager.LoadScene("GameScene");
            });
        }

        // ── Return Button ─────────────────────────────────────────────────────
        MakeButton(canvasObj.transform, "RETURN",
            new Vector2(0.05f, 0.04f), new Vector2(0.22f, 0.11f),
            () => SceneManager.LoadScene("MainMenu"));
    }

    // ── Map selection ─────────────────────────────────────────────────────────

    private void SelectMap(GameManager.ArenaMap map)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SetSelectedMap(map);
        RefreshMapHighlight();
    }

    private void RefreshMapHighlight()
    {
        GameManager.ArenaMap current = GameManager.Instance != null
            ? GameManager.Instance.GetSelectedMap()
            : GameManager.ArenaMap.Map1;

        Color selectedCol  = new Color(0.35f, 0.18f, 0.85f, 1f);
        Color defaultCol   = new Color(0.20f, 0.20f, 0.30f, 1f);

        if (_map1Btn != null)
            _map1Btn.GetComponent<Image>().color = (current == GameManager.ArenaMap.Map1) ? selectedCol : defaultCol;
        if (_map2Btn != null)
            _map2Btn.GetComponent<Image>().color = (current == GameManager.ArenaMap.Map2) ? selectedCol : defaultCol;
    }

    // ── Widget helpers ────────────────────────────────────────────────────────

    Button MakeMapButton(Transform parent, string label, Vector2 aMin, Vector2 aMax,
        UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject("MapBtn_" + label);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.color = new Color(0.20f, 0.20f, 0.30f, 1f);
        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        var outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0.6f, 0.2f, 1f, 0.6f);
        outline.effectDistance = new Vector2(2, -2);

        var txt = new GameObject("Txt");
        txt.transform.SetParent(obj.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont) tmp.font = prismFont;
        Stretch(txt.GetComponent<RectTransform>());

        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = r.offsetMax = Vector2.zero;

        return btn;
    }

    void MakeLevelButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject("Btn_" + label);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        var txt = new GameObject("Txt");
        txt.transform.SetParent(obj.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 45; tmp.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont) tmp.font = prismFont;
        Stretch(txt.GetComponent<RectTransform>());
    }

    void MakeText(Transform parent, string text, float size, Color color,
        Vector2 aMin, Vector2 aMax, bool isTitle = false)
    {
        var obj = new GameObject("Txt"); obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont) tmp.font = prismFont;
        if (isTitle) { tmp.fontStyle = FontStyles.Bold; obj.AddComponent<Outline>().effectColor = Color.white; }
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax,
        UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject("Btn_" + label); obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>(); img.color = Color.white;
        var btn = obj.AddComponent<Button>(); btn.onClick.AddListener(action);
        var txt = new GameObject("Txt"); txt.transform.SetParent(obj.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 28; tmp.color = new Color(0.1f, 0.1f, 0.3f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont) tmp.font = prismFont;
        Stretch(txt.GetComponent<RectTransform>());
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
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
