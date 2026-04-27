using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime loading overlay (DontDestroyOnLoad): flat dark fill and static text only.
/// Heavy visuals (sprite bleed, RawImage, 3D katana stage, pulsing labels) were removed for performance and clarity.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    private Canvas _canvas;
    private TextMeshProUGUI _loadingLabel;
    private Image _backgroundImage;
    private Image _dimOverlay;
    private Coroutine _dotsRoutine;

    public static LoadingScreenUI CreateOrGet()
    {
        StripLegacyBlueLoadingArtifacts();

        LoadingScreenUI existing = UnityEngine.Object.FindFirstObjectByType<LoadingScreenUI>();
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.ApplyMinimalLook();
            existing.StopAnyAnimations();
            return existing;
        }

        GameObject root = new GameObject("RuntimeLoadingScreen");
        DontDestroyOnLoad(root);
        LoadingScreenUI ui = root.AddComponent<LoadingScreenUI>();
        ui.BuildUi();
        ui.ApplyMinimalLook();
        ui.StopAnyAnimations();
        return ui;
    }

    /// <summary>
    /// Kills any legacy blue background objects and animators from older
    /// loading-screen prefabs that may still be in the scene at startup.
    /// Called every time we show the screen so a stale prefab in a freshly
    /// loaded scene can't paint over our flat dark fill.
    /// </summary>
    private static void StripLegacyBlueLoadingArtifacts()
    {
        string[] staleNames =
        {
            "LoadingStage", "BlueBackground", "BlueOverlay", "LoadingBlue",
            "LoadingBackground", "LoadingAccent", "HeroLabel"
        };

        for (int i = 0; i < staleNames.Length; i++)
        {
            GameObject stale = GameObject.Find(staleNames[i]);
            if (stale != null)
                UnityEngine.Object.Destroy(stale);
        }
    }

    private void StopAnyAnimations()
    {
        StopAllCoroutines();
        _dotsRoutine = null;

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            if (animators[i] != null) animators[i].enabled = false;

        Animation[] legacyAnims = GetComponentsInChildren<Animation>(true);
        for (int i = 0; i < legacyAnims.Length; i++)
            if (legacyAnims[i] != null) { legacyAnims[i].Stop(); legacyAnims[i].enabled = false; }
    }

    public void SetLabel(string label)
    {
        if (_loadingLabel != null)
            _loadingLabel.text = string.IsNullOrEmpty(label) ? "LOADING" : label;
    }

    public void DestroySelf()
    {
        DestroyLoadingStageIfAny();
        if (gameObject != null)
            UnityEngine.Object.Destroy(gameObject);
    }

    private void OnEnable()
    {
        DestroyLoadingStageIfAny();
        if (_dotsRoutine == null && isActiveAndEnabled)
            _dotsRoutine = StartCoroutine(LoadingDotsLoop());
    }

    private IEnumerator LoadingDotsLoop()
    {
        int i = 0;
        while (true)
        {
            if (_loadingLabel != null)
            {
                string dots = (i % 4) switch { 0 => "", 1 => ".", 2 => "..", _ => "..." };
                _loadingLabel.text = "LOADING" + dots;
            }
            i++;
            yield return new WaitForSecondsRealtime(0.22f);
        }
    }

    private static void DestroyLoadingStageIfAny()
    {
        GameObject stage = GameObject.Find("LoadingStage");
        if (stage != null)
            UnityEngine.Object.Destroy(stage);
    }

    /// <summary>Flat fallback or <c>Resources/loading</c> art + dim; strips legacy junk.</summary>
    private void ApplyMinimalLook()
    {
        DestroyLoadingStageIfAny();

        // Prefer Sprite import, but fall back to Texture2D->Sprite so the
        // loading image always renders even if import settings are wrong.
        Sprite spr = Resources.Load<Sprite>("loading");
        if (spr == null)
        {
            Texture2D tex = Resources.Load<Texture2D>("loading");
            if (tex != null)
                spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
#if UNITY_EDITOR
        if (spr == null)
            spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/loading.png");
#endif

        if (spr != null && _backgroundImage != null)
        {
            _backgroundImage.sprite = spr;
            _backgroundImage.type = Image.Type.Simple;
            // Full-screen: never letterbox over the main menu background.
            _backgroundImage.preserveAspect = false;
            _backgroundImage.color = Color.white;
            if (_dimOverlay != null)
                _dimOverlay.color = new Color(0f, 0f, 0f, 0.78f);
        }
        else if (_backgroundImage != null)
        {
            _backgroundImage.sprite = null;
            _backgroundImage.color = new Color(0.02f, 0.02f, 0.03f, 1f);
            if (_dimOverlay != null)
                _dimOverlay.color = new Color(0f, 0f, 0f, 0.12f);
        }

        Transform accent = transform.Find("Accent");
        if (accent != null)
            accent.gameObject.SetActive(false);

        Transform hero = transform.Find("HeroLabel");
        if (hero != null)
            hero.gameObject.SetActive(false);

        Transform overlay = transform.Find("Overlay");
        if (overlay != null)
        {
            var img = overlay.GetComponent<Image>();
            if (img != null && spr == null)
            {
                img.sprite = null;
                img.color = new Color(0.02f, 0.02f, 0.03f, 1f);
            }
        }
    }

    private void BuildUi()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 2000;
        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("SolidBackground");
        panel.transform.SetParent(transform, false);
        _backgroundImage = panel.AddComponent<Image>();
        _backgroundImage.sprite = null;
        _backgroundImage.color = new Color(0.02f, 0.02f, 0.03f, 1f);
        Stretch(_backgroundImage.rectTransform);

        GameObject dimObject = new GameObject("DimOverlay");
        dimObject.transform.SetParent(transform, false);
        _dimOverlay = dimObject.AddComponent<Image>();
        _dimOverlay.color = new Color(0f, 0f, 0f, 0.12f);
        Stretch(_dimOverlay.rectTransform);

        GameObject labelObject = new GameObject("LoadingLabel");
        labelObject.transform.SetParent(transform, false);
        _loadingLabel = labelObject.AddComponent<TextMeshProUGUI>();
        _loadingLabel.text = "LOADING";
        _loadingLabel.fontSize = 34f;
        _loadingLabel.fontStyle = FontStyles.Bold;
        _loadingLabel.alignment = TextAlignmentOptions.BottomRight;
        _loadingLabel.color = new Color(0.90f, 0.95f, 1f, 0.96f);
        TMP_FontAsset azonix = Resources.Load<TMP_FontAsset>("Fonts/Azonix SDF");
        if (azonix != null) _loadingLabel.font = azonix;

        RectTransform labelRect = _loadingLabel.rectTransform;
        labelRect.anchorMin = new Vector2(1f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(1f, 0f);
        labelRect.sizeDelta = new Vector2(520f, 100f);
        labelRect.anchoredPosition = new Vector2(-36f, 22f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
