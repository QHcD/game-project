using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class CreditsBuilder : MonoBehaviour
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

        Image bg = new GameObject("Background").AddComponent<Image>();
        bg.transform.SetParent(canvasObj.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        if (prismBackground) { bg.sprite = prismBackground; bg.color = Color.white; }

        MakeText(canvasObj.transform, "PRISM-7\nWEAPON TRIALS", 80, new Color(0.6f, 0.2f, 1f, 1f), new Vector2(0f, 0.75f), new Vector2(1f, 0.95f), true, TextAlignmentOptions.Center);

        // Left Column (Roles)
        string roles = "UI / MENUS\n\nWEAPON SYSTEM\n\nLEVEL DESIGN & CORE\n\nAI & ENEMIES\n\nAUDIO & VFX";
        MakeText(canvasObj.transform, roles, 35, new Color(0.7f, 0.5f, 1f, 1f), new Vector2(0.1f, 0.2f), new Vector2(0.48f, 0.65f), false, TextAlignmentOptions.Right);

        // Right Column (Names)
        string names = "MOHAMED AMAN\n\nMURTADHA SARHAN\n\nMOHAMED ALTAJER\n\nALI ALHAWAJ\n\nHAMED AHMED";
        MakeText(canvasObj.transform, names, 35, new Color(0.3f, 0.8f, 1f, 1f), new Vector2(0.52f, 0.2f), new Vector2(0.9f, 0.65f), false, TextAlignmentOptions.Left);

        MakeButton(canvasObj.transform, "RETURN", new Vector2(0.05f, 0.05f), new Vector2(0.2f, 0.12f), () => SceneManager.LoadScene("MainMenu"));
    }

    // (نفس دوال MakeText و MakeButton و Stretch انسخها هنا)
    void MakeText(Transform parent, string text, float size, Color color, Vector2 aMin, Vector2 aMax, bool isTitle = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var obj = new GameObject("Txt"); obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
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