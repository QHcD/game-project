using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class SettingsBuilder : MonoBehaviour
{
    [Header("Prism-7 UI Assets")]
    public Sprite prismBackground;
    public TMP_FontAsset prismFont;

    // متغيرات لحفظ حالة الإعدادات
    private int currentQualityIndex;
    private bool isFullscreen;
    private Resolution[] resolutions;
    private int currentResIndex;

    private TextMeshProUGUI resText;
    private TextMeshProUGUI gfxText;
    private TextMeshProUGUI fullScreenText;

    void Start()
    {
        LoadSettingsData();
        BuildPrism7SettingsMenu();
    }

    // --- 1. تحميل الإعدادات من جهاز اللاعب (فكرة صديقك) ---
    void LoadSettingsData()
    {
        resolutions = Screen.resolutions;
        currentResIndex = PlayerPrefs.GetInt("ResIndex", resolutions.Length - 1);
        currentQualityIndex = PlayerPrefs.GetInt("QualityIndex", QualitySettings.GetQualityLevel());
        isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        QualitySettings.SetQualityLevel(currentQualityIndex);
        Screen.fullScreen = isFullscreen;
    }

    // --- 2. بناء الواجهة الشفافة الفخمة ---
    void BuildPrism7SettingsMenu()
    {
        GameObject existing = GameObject.Find("Prism7Canvas_Settings");
        if (existing != null)
            Destroy(existing);

        GameObject canvasObj = new GameObject("Prism7Canvas_Settings");
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
        if (prismBackground != null)
        {
            bg.sprite = prismBackground;
            bg.color = Color.white;
        }
        else
        {
            bg.color = new Color(0.03f, 0.03f, 0.08f, 1f);
        }

        MakeText(canvasObj.transform, "SETTINGS", 50, new Color(0.6f, 0.2f, 1f, 1f), new Vector2(0, 380), new Vector2(800, 100), true);

        GameObject panelObj = new GameObject("CentralPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0.02f, 0.02f, 0.06f, 0.3f); // خلفية شفافة جداً
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(900, 550), new Vector2(0, -20));
        panelObj.AddComponent<Outline>().effectColor = Color.white;

        // السطور
        MakeSliderRow(panel.transform, "MASTER VOLUME:", 200f, "MasterVol");
        MakeSliderRow(panel.transform, "MUSIC VOLUME:", 120f, "MusicVol");
        MakeSliderRow(panel.transform, "SFX VOLUME:", 40f, "SFXVol");

        resText = MakeCycleRow(panel.transform, "RESOLUTION:", resolutions[currentResIndex].width + " x " + resolutions[currentResIndex].height, -40f, CycleResolution);
        gfxText = MakeCycleRow(panel.transform, "GRAPHICS:", QualitySettings.names[currentQualityIndex].ToUpper(), -120f, CycleGraphics);
        fullScreenText = MakeCycleRow(panel.transform, "FULLSCREEN:", isFullscreen ? "ON" : "OFF", -200f, ToggleFullscreen);

        MakePrismButton(canvasObj.transform, "RETURN", new Vector2(-150, -380), () => SceneManager.LoadScene("MainMenu"));
        MakePrismButton(canvasObj.transform, "APPLY", new Vector2(150, -380), ApplySettings);
    }

    // --- 3. وظائف الإعدادات الفعلية (اللوجيك) ---
    void CycleResolution()
    {
        currentResIndex++;
        if (currentResIndex >= resolutions.Length) currentResIndex = 0;
        resText.text = resolutions[currentResIndex].width + " x " + resolutions[currentResIndex].height;
    }

    void CycleGraphics()
    {
        currentQualityIndex++;
        if (currentQualityIndex >= QualitySettings.names.Length) currentQualityIndex = 0;
        gfxText.text = QualitySettings.names[currentQualityIndex].ToUpper();
    }

    void ToggleFullscreen()
    {
        isFullscreen = !isFullscreen;
        fullScreenText.text = isFullscreen ? "ON" : "OFF";
    }

    void ApplySettings()
    {
        // تطبيق التغييرات وحفظها
        Resolution res = resolutions[currentResIndex];
        Screen.SetResolution(res.width, res.height, isFullscreen);
        QualitySettings.SetQualityLevel(currentQualityIndex);

        PlayerPrefs.SetInt("ResIndex", currentResIndex);
        PlayerPrefs.SetInt("QualityIndex", currentQualityIndex);
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log("PRISM-7 Settings Saved & Applied!");
    }

    // --- دوال بناء العناصر (UI Helpers) ---
    void SetRect(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    void MakeSliderRow(Transform parent, string label, float yPos, string prefKey)
    {
        GameObject row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        SetRect(row.AddComponent<RectTransform>(), new Vector2(800, 50), new Vector2(0, yPos));

        MakeText(row.transform, label, 24, new Color(0.7f, 0.4f, 1f, 1f), new Vector2(-120, 0), new Vector2(400, 50), false, TextAlignmentOptions.Right);

        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        Slider slider = sliderObj.AddComponent<Slider>();
        SetRect(sliderObj.GetComponent<RectTransform>(), new Vector2(300, 15), new Vector2(200, 0));

        Image bg = new GameObject("BG").AddComponent<Image>();
        bg.transform.SetParent(sliderObj.transform, false); bg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        Stretch(bg.GetComponent<RectTransform>());

        Image fill = new GameObject("Fill").AddComponent<Image>();
        fill.transform.SetParent(sliderObj.transform, false); fill.color = new Color(0.6f, 0.2f, 1f, 1f);
        Stretch(fill.GetComponent<RectTransform>());
        slider.fillRect = fill.GetComponent<RectTransform>();

        slider.value = PlayerPrefs.GetFloat(prefKey, 0.8f);
        slider.onValueChanged.AddListener((val) => PlayerPrefs.SetFloat(prefKey, val));
    }

    TextMeshProUGUI MakeCycleRow(Transform parent, string label, string val, float yPos, UnityEngine.Events.UnityAction action)
    {
        GameObject row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        SetRect(row.AddComponent<RectTransform>(), new Vector2(800, 50), new Vector2(0, yPos));

        MakeText(row.transform, label, 24, new Color(0.7f, 0.4f, 1f, 1f), new Vector2(-120, 0), new Vector2(400, 50), false, TextAlignmentOptions.Right);

        // جعل القيمة قابلة للضغط (زر)
        GameObject btnObj = new GameObject("Btn_" + label);
        btnObj.transform.SetParent(row.transform, false);
        Image boxImg = btnObj.AddComponent<Image>(); boxImg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        SetRect(btnObj.GetComponent<RectTransform>(), new Vector2(300, 40), new Vector2(200, 0));
        Button btn = btnObj.AddComponent<Button>(); btn.onClick.AddListener(action);

        return MakeText(btnObj.transform, val, 20, Color.white, Vector2.zero, new Vector2(300, 40));
    }

    TextMeshProUGUI MakeText(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 sizeDelta, bool addOutline = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        TextMeshProUGUI tmp = new GameObject("Txt_" + text).AddComponent<TextMeshProUGUI>();
        tmp.transform.SetParent(parent, false); tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
        if (prismFont) tmp.font = prismFont;
        if (addOutline) { tmp.fontStyle = FontStyles.Bold; tmp.gameObject.AddComponent<Outline>().effectColor = Color.white; }
        SetRect(tmp.GetComponent<RectTransform>(), sizeDelta, pos);
        return tmp;
    }

    void MakePrismButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        Image btnImg = new GameObject("Btn_" + label).AddComponent<Image>();
        btnImg.transform.SetParent(parent, false); btnImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        SetRect(btnImg.GetComponent<RectTransform>(), new Vector2(180, 50), pos);
        btnImg.gameObject.AddComponent<Button>().onClick.AddListener(action);
        MakeText(btnImg.transform, label, 20, new Color(0.1f, 0.1f, 0.3f, 1f), Vector2.zero, new Vector2(180, 50));
    }

    void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
