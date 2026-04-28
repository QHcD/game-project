using TMPro;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime loading screen UI (DontDestroyOnLoad) with animated footer text.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    private Canvas _canvas;
    private TextMeshProUGUI _loadingLabel;
    private Image _backgroundImage;
    private Image _dimOverlay;
    private float _startTime;
    private static Sprite _cachedLoadingSprite;

    public static LoadingScreenUI CreateOrGet()
    {
        LoadingScreenUI existing = FindFirstObjectByType<LoadingScreenUI>();
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing;
        }

        GameObject root = new GameObject("RuntimeLoadingScreen");
        DontDestroyOnLoad(root);
        LoadingScreenUI ui = root.AddComponent<LoadingScreenUI>();
        ui.BuildUi();
        return ui;
    }

    public void SetLabel(string label)
    {
        if (_loadingLabel != null)
            _loadingLabel.text = label;
    }

    public void DestroySelf()
    {
        if (gameObject != null)
            Destroy(gameObject);
    }

    private void BuildUi()
    {
        _startTime = Time.unscaledTime;

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 2000;
        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject overlayObject = new GameObject("Overlay");
        overlayObject.transform.SetParent(transform, false);
        _backgroundImage = overlayObject.AddComponent<Image>();
        _backgroundImage.color = Color.white;
        _backgroundImage.sprite = LoadLoadingSprite();
        _backgroundImage.preserveAspect = false;
        Stretch(_backgroundImage.rectTransform);

        GameObject dimObject = new GameObject("DimOverlay");
        dimObject.transform.SetParent(transform, false);
        _dimOverlay = dimObject.AddComponent<Image>();
        _dimOverlay.color = _backgroundImage.sprite != null
            ? new Color(0.02f, 0.03f, 0.05f, 0.34f)
            : new Color(0.02f, 0.03f, 0.05f, 0.98f);
        Stretch(_dimOverlay.rectTransform);

        // Soft vignette-ish accent
        GameObject accentObject = new GameObject("Accent");
        accentObject.transform.SetParent(transform, false);
        Image accent = accentObject.AddComponent<Image>();
        accent.color = new Color(0.12f, 0.18f, 0.30f, 0.22f);
        Stretch(accent.rectTransform);

        GameObject labelObject = new GameObject("LoadingLabel");
        labelObject.transform.SetParent(transform, false);
        _loadingLabel = labelObject.AddComponent<TextMeshProUGUI>();
        _loadingLabel.text = "LOADING";
        _loadingLabel.fontSize = 34f;
        _loadingLabel.fontStyle = FontStyles.Bold;
        _loadingLabel.alignment = TextAlignmentOptions.BottomRight;
        _loadingLabel.color = new Color(0.90f, 0.95f, 1f, 0.92f);

        RectTransform labelRect = _loadingLabel.rectTransform;
        labelRect.anchorMin = new Vector2(1f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(1f, 0f);
        labelRect.sizeDelta = new Vector2(420f, 90f);
        labelRect.anchoredPosition = new Vector2(-36f, 24f);
    }

    private void Update()
    {
        if (_loadingLabel == null)
            return;

        float elapsed = Time.unscaledTime - _startTime;
        int dots = ((int)(elapsed * 2.8f)) % 4;
        string dotSuffix = dots == 0 ? string.Empty : new string('.', dots);
        _loadingLabel.text = "LOADING" + dotSuffix;

        float pulse = 0.72f + 0.28f * Mathf.Sin(elapsed * 3.2f);
        Color c = _loadingLabel.color;
        c.a = Mathf.Clamp01(pulse);
        _loadingLabel.color = c;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite LoadLoadingSprite()
    {
        if (_cachedLoadingSprite != null)
            return _cachedLoadingSprite;

        string path = Path.Combine(Application.dataPath, "loading.png");
        if (!File.Exists(path))
            return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            texture.name = "loading.png";
            _cachedLoadingSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _cachedLoadingSprite;
        }
        catch
        {
            return null;
        }
    }
}
