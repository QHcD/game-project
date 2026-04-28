using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreditsBuilder : MonoBehaviour
{
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    void Start()
    {
        prismFont = ResolvePrismFont();
        EnsureEventSystem();

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
        bg.color = new Color(0.03f, 0.04f, 0.08f, 1f);
        if (prismBackground != null)
        {
            bg.sprite = prismBackground;
            bg.color = Color.white;
        }

        Image overlay = new GameObject("Overlay").AddComponent<Image>();
        overlay.transform.SetParent(canvasObj.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.48f);

        MakeText(canvasObj.transform, "PRISM-7", 122, new Color(0.94f, 0.94f, 1f, 1f),
            new Vector2(0.18f, 0.65f), new Vector2(0.82f, 0.85f), true, TextAlignmentOptions.Center);
        MakeText(canvasObj.transform, "CREDITS", 56, new Color(0.94f, 0.94f, 1f, 1f),
            new Vector2(0.31f, 0.54f), new Vector2(0.69f, 0.63f), true, TextAlignmentOptions.Center);

        string roles =
            "UI MENUS\n" +
            "WEAPON SYSTEM\n" +
            "LEVEL DESIGN & CORE\n" +
            "AI & ENEMIES\n" +
            "AUDIO & VFX\n" +
            "SUPERVISOR";

        string names =
            "HAMED AHMED\n" +
            "MURTADHA SARHAN\n" +
            "MOHAMED ALTAJER\n" +
            "ALI ALHAWAJ\n" +
            "MOHAMED AMAN\n" +
            "DR. HAETHAM ALHADDAD";

        MakeText(canvasObj.transform, roles, 34, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(0.19f, 0.17f), new Vector2(0.47f, 0.49f), false, TextAlignmentOptions.MidlineRight);
        MakeText(canvasObj.transform, names, 34, new Color(0.95f, 0.95f, 1f, 1f),
            new Vector2(0.52f, 0.17f), new Vector2(0.81f, 0.49f), false, TextAlignmentOptions.MidlineLeft);

        MakeButton(canvasObj.transform, "RETURN", new Vector2(0.025f, 0.045f), new Vector2(0.14f, 0.105f),
            () => SceneManager.LoadScene("MainMenu"));
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<InputSystemUIInputModule>();
    }

    void MakeText(Transform parent, string text, float size, Color color, Vector2 aMin, Vector2 aMax, bool addOutline, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject("Txt");
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.lineSpacing = 6f;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (prismFont != null)
            tmp.font = prismFont;
        if (addOutline)
        {
            tmp.fontStyle = FontStyles.Bold;
            obj.AddComponent<Outline>().effectColor = new Color(0.30f, 0.12f, 0.62f, 1f);
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
        img.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txt = new GameObject("Txt");
        txt.transform.SetParent(obj.transform, false);
        TextMeshProUGUI tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.24f, 0.22f, 0.38f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        if (prismFont != null)
            tmp.font = prismFont;
        Stretch(txt.GetComponent<RectTransform>());

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = aMin;
        rect.anchorMax = aMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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
