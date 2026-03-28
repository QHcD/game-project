using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class RuntimeMenuBuilder : MonoBehaviour
{
    [Header("UI Customization")]
    public Sprite backgroundImage; // اسحب صورة الخلفية هنا
    public TMP_FontAsset customFont; // اسحب الخط المخصص هنا

    void Start()
    {
        BuildNeonMenu();
    }

    void BuildNeonMenu()
    {
        // 1. إنشاء الـ Canvas مع ضبط المقاسات بدقة (هذا اللي كان مخرب الشاشة)
        GameObject canvasObj = new GameObject("NeonCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); // المقاس السري للترتيب
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. الخلفية (الآن تدعم صورة حقيقية)
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
            bg.color = new Color(0.02f, 0.02f, 0.06f, 1f); // كحلي غامق إذا مافي صورة
        }

        // 3. عنوان اللعبة (بتوسيط دقيق)
        MakeText(canvasObj.transform, "PRISM-7\nWEAPON TRIALS", 120, new Color(0.6f, 0.3f, 1f, 1f),
            new Vector2(0f, 0.65f), new Vector2(1f, 0.95f), true);

        // 4. الأزرار (مرتبة بشكل ممتاز في المنتصف)
        MakeButton(canvasObj.transform, "START GAME", new Vector2(0f, 0.50f), new Vector2(1f, 0.58f), () => SceneManager.LoadScene("GameScene"));

        MakeButton(canvasObj.transform, "SELECT LEVEL", new Vector2(0f, 0.40f), new Vector2(1f, 0.48f), () => Debug.Log("Select Level Menu..."));

        MakeButton(canvasObj.transform, "SETTINGS", new Vector2(0f, 0.30f), new Vector2(1f, 0.38f), () => SceneManager.LoadScene("Settings"));

        MakeButton(canvasObj.transform, "CREDITS", new Vector2(0f, 0.20f), new Vector2(1f, 0.28f), () => SceneManager.LoadScene("Credits"));

        MakeButton(canvasObj.transform, "QUIT", new Vector2(0f, 0.10f), new Vector2(1f, 0.18f), () => Application.Quit());
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

        // استخدام الخط المخصص إذا كان موجوداً
        if (customFont != null) tmp.font = customFont;

        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;

        // إضافة تأثير الأوتلاين لعنوان اللعبة (مثل شغل صديقك)
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
        img.color = new Color(0f, 0f, 0f, 0f); // شفاف

        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;

        // نص الزر
        MakeText(obj.transform, label, 45, new Color(0.4f, 0.8f, 1f, 1f), Vector2.zero, Vector2.one);
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
    }
}