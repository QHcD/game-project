using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelSelectBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    void Start()
    {
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Background
        Image bg = new GameObject("Background").AddComponent<Image>();
        bg.transform.SetParent(canvasObj.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        if (prismBackground) { bg.sprite = prismBackground; bg.color = Color.white; }

        // Title
        MakeText(canvasObj.transform, "SELECT LEVEL", 70, new Color(0.6f, 0.2f, 1f, 1f), new Vector2(0f, 0.85f), new Vector2(1f, 0.95f), true);

        // Translucent Panel (اللوحة الشفافة خلف الأزرار)
        Image panel = new GameObject("Panel").AddComponent<Image>();
        panel.transform.SetParent(canvasObj.transform, false);
        panel.color = new Color(0.1f, 0.1f, 0.2f, 0.5f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.20f);
        panelRect.anchorMax = new Vector2(0.85f, 0.80f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        // Outline للوحة (مثل صورة صديقك)
        Outline outl = panel.gameObject.AddComponent<Outline>();
        outl.effectColor = new Color(0.6f, 0.2f, 1f, 1f);
        outl.effectDistance = new Vector2(3, -3);

        // Grid (الشبكة)
        GridLayoutGroup grid = panel.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(130, 110);
        grid.spacing = new Vector2(20, 20);
        grid.padding = new RectOffset(40, 40, 40, 40);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;

        // توليد 20 زر
        for (int i = 1; i <= 20; i++)
        {
            int lvl = i;
            MakeLevelButton(panel.transform, lvl.ToString(), () => {
                if (GameManager.Instance != null) { GameManager.Instance.currentLevel = lvl; GameManager.Instance.levelTime = 0; }
                SceneManager.LoadScene("GameScene");
            });
        }

        // Return Button
        MakeButton(canvasObj.transform, "RETURN", new Vector2(0.05f, 0.05f), new Vector2(0.2f, 0.12f), () => SceneManager.LoadScene("MainMenu"));
    }

    void MakeLevelButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject("Btn_" + label);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.color = new Color(0.8f, 0.8f, 0.8f, 1f); // رصاصي فاتح مثل الصورة
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

    void MakeText(Transform parent, string text, float size, Color color, Vector2 aMin, Vector2 aMax, bool isTitle = false)
    {
        var obj = new GameObject("Txt"); obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont) tmp.font = prismFont;
        if (isTitle) { tmp.fontStyle = FontStyles.Bold; obj.AddComponent<Outline>().effectColor = Color.white; }
        var r = obj.GetComponent<RectTransform>(); r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject("Btn_" + label); obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>(); img.color = Color.white;
        var btn = obj.AddComponent<Button>(); btn.onClick.AddListener(action);
        var txt = new GameObject("Txt"); txt.transform.SetParent(obj.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 28; tmp.color = new Color(0.1f, 0.1f, 0.3f, 1f); tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont) tmp.font = prismFont;
        Stretch(txt.GetComponent<RectTransform>());
        var r = obj.GetComponent<RectTransform>(); r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = r.offsetMax = Vector2.zero;
    }
    void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}