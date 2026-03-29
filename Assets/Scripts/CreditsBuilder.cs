using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreditsBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    void Start()
    {
        GameObject existing = GameObject.Find("CreditsCanvas");
        if (existing != null)
            Destroy(existing);

        GameObject canvasObj = new GameObject("CreditsCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        Image bg = new GameObject("Background").AddComponent<Image>();
        bg.transform.SetParent(canvasObj.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.color = new Color(0.03f, 0.03f, 0.08f, 1f);
        if (prismBackground != null)
        {
            bg.sprite = prismBackground;
            bg.color = Color.white;
        }

        Image panel = new GameObject("Panel").AddComponent<Image>();
        panel.transform.SetParent(canvasObj.transform, false);
        panel.color = new Color(0.03f, 0.05f, 0.09f, 0.82f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.17f, 0.18f);
        panelRect.anchorMax = new Vector2(0.83f, 0.78f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        MakeText(canvasObj.transform, "PRISM-7 CREDITS", 62, new Color(0.7f, 0.9f, 1f, 1f),
            new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.92f), true, TextAlignmentOptions.Center);

        string roles =
            "UI / MENUS\n" +
            "WEAPON SYSTEM\n" +
            "LEVEL DESIGN & CORE\n" +
            "AI & ENEMIES\n" +
            "AUDIO & VFX";

        string names =
            "MOHAMED AMAN\n" +
            "MURTADHA SARHAN\n" +
            "MOHAMED ALTAJER\n" +
            "ALI ALHAWAJ\n" +
            "HAMED AHMED";

        MakeText(panel.transform, roles, 28, new Color(0.72f, 0.55f, 1f, 1f),
            new Vector2(0.08f, 0.12f), new Vector2(0.46f, 0.86f), false, TextAlignmentOptions.MidlineRight);

        MakeText(panel.transform, names, 28, new Color(0.35f, 0.85f, 1f, 1f),
            new Vector2(0.54f, 0.12f), new Vector2(0.92f, 0.86f), false, TextAlignmentOptions.MidlineLeft);

        MakeButton(canvasObj.transform, "RETURN", new Vector2(0.06f, 0.05f), new Vector2(0.20f, 0.11f),
            () => SceneManager.LoadScene("MainMenu"));
    }

    void MakeText(Transform parent, string text, float size, Color color, Vector2 aMin, Vector2 aMax, bool isTitle, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject("Txt");
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.lineSpacing = 8f;
        if (prismFont != null)
            tmp.font = prismFont;
        if (isTitle)
        {
            tmp.fontStyle = FontStyles.Bold;
            obj.AddComponent<Outline>().effectColor = Color.white;
        }

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = aMin;
        rect.anchorMax = aMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject("Btn_" + label);
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.94f, 0.96f, 1f, 1f);
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txt = new GameObject("Txt");
        txt.transform.SetParent(obj.transform, false);
        TextMeshProUGUI tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.color = new Color(0.08f, 0.12f, 0.24f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont != null)
            tmp.font = prismFont;
        Stretch(txt.GetComponent<RectTransform>());

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = aMin;
        rect.anchorMax = aMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
