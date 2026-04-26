using TMPro;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime loading screen UI (DontDestroyOnLoad) with animated footer text
/// AND a procedural 3D "preparing for combat" stage shown behind the panel —
/// a slowly rotating katana, accent particles and rim lighting. All built
/// programmatically so no prefab dependencies are required.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    private Canvas _canvas;
    private TextMeshProUGUI _loadingLabel;
    private TextMeshProUGUI _heroLabel;
    private Image _backgroundImage;
    private Image _dimOverlay;
    private float _startTime;
    private static Sprite _cachedLoadingSprite;

    // 3D stage (created on demand and torn down with the loading screen).
    private GameObject _stageRoot;
    private Camera     _stageCamera;
    private Transform  _stageBlade;
    private Transform  _stageRing;

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
        if (_stageRoot != null) Destroy(_stageRoot);
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

        // The 3D loading stage renders to its own dedicated camera and gets
        // composited *behind* the dim/accent overlays so the procedural
        // background, the rotating blade, and the UI text stack naturally.
        BuildStage();

        GameObject dimObject = new GameObject("DimOverlay");
        dimObject.transform.SetParent(transform, false);
        _dimOverlay = dimObject.AddComponent<Image>();
        _dimOverlay.color = _backgroundImage.sprite != null
            ? new Color(0.02f, 0.03f, 0.05f, 0.34f)
            : new Color(0.02f, 0.03f, 0.05f, 0.55f);
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

        // Hero subtitle — short flavour text so the 3D stage feels purposeful
        // ("PREPARING WEAPON / SYNCING NEURAL LINK / ..."). It also rotates
        // through entries while the scene streams in.
        GameObject heroObject = new GameObject("HeroLabel");
        heroObject.transform.SetParent(transform, false);
        _heroLabel = heroObject.AddComponent<TextMeshProUGUI>();
        _heroLabel.text      = "PRISM-7";
        _heroLabel.fontSize  = 96f;
        _heroLabel.fontStyle = FontStyles.Bold;
        _heroLabel.alignment = TextAlignmentOptions.Center;
        _heroLabel.color     = new Color(0.94f, 0.96f, 1f, 0.95f);
        RectTransform heroRect = _heroLabel.rectTransform;
        heroRect.anchorMin = new Vector2(0.5f, 0.18f);
        heroRect.anchorMax = new Vector2(0.5f, 0.30f);
        heroRect.pivot     = new Vector2(0.5f, 0.5f);
        heroRect.sizeDelta = new Vector2(1200f, 120f);
        heroRect.anchoredPosition = Vector2.zero;
    }

    private static readonly string[] HeroLines =
    {
        "PRISM-7",
        "PREPARING WEAPON",
        "SYNCING NEURAL LINK",
        "CALIBRATING SENSORS",
        "ENGAGING PROTOCOL",
    };

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

        // Cycle through hero lines roughly every 1.5s to give the loading
        // screen a sense of scripted progress.
        if (_heroLabel != null)
        {
            int idx = ((int)(elapsed / 1.5f)) % HeroLines.Length;
            _heroLabel.text = HeroLines[idx];
        }

        // Animate the 3D stage — slow rotation, gentle bob and a fast
        // counter-rotating ring that feels like a power glyph.
        if (_stageBlade != null)
        {
            float bob = Mathf.Sin(elapsed * 1.3f) * 0.18f;
            _stageBlade.localPosition = new Vector3(0f, bob, 0f);
            _stageBlade.localRotation = Quaternion.Euler(0f, elapsed * 38f, 12f * Mathf.Sin(elapsed * 0.85f));
        }

        if (_stageRing != null)
        {
            _stageRing.localRotation = Quaternion.Euler(75f, 0f, -elapsed * 90f);
        }
    }

    /// <summary>
    /// Builds a tiny dedicated 3D scene rendered to its own camera so the
    /// loading screen has visible motion regardless of which scene is being
    /// streamed in next. The stage lives under DontDestroyOnLoad so it
    /// outlives any scene swap.
    /// </summary>
    private void BuildStage()
    {
        _stageRoot = new GameObject("LoadingStage");
        DontDestroyOnLoad(_stageRoot);
        // Park the stage far below the play area so it can never collide
        // with gameplay objects in the next scene.
        _stageRoot.transform.position = new Vector3(0f, -2000f, 0f);

        // Dedicated camera. Depth +50 puts it on top of any gameplay camera
        // that might exist while the scene is loading. clearFlags=SolidColor
        // produces a clean dark backdrop behind the rotating blade.
        GameObject camObj = new GameObject("LoadingStageCamera");
        camObj.transform.SetParent(_stageRoot.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 0f, -3.5f);
        camObj.transform.localRotation = Quaternion.identity;
        _stageCamera = camObj.AddComponent<Camera>();
        _stageCamera.clearFlags  = CameraClearFlags.SolidColor;
        _stageCamera.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
        _stageCamera.fieldOfView = 35f;
        _stageCamera.nearClipPlane = 0.1f;
        _stageCamera.farClipPlane  = 30f;
        _stageCamera.depth         = 50f;
        _stageCamera.cullingMask   = ~0;
        _stageCamera.allowMSAA     = true;

        // Key + rim light for readable silhouettes.
        GameObject keyLight = new GameObject("KeyLight");
        keyLight.transform.SetParent(_stageRoot.transform, false);
        keyLight.transform.localRotation = Quaternion.Euler(35f, -45f, 0f);
        Light kl = keyLight.AddComponent<Light>();
        kl.type      = LightType.Directional;
        kl.color     = new Color(0.85f, 0.92f, 1f, 1f);
        kl.intensity = 1.4f;

        GameObject rimLight = new GameObject("RimLight");
        rimLight.transform.SetParent(_stageRoot.transform, false);
        rimLight.transform.localRotation = Quaternion.Euler(-25f, 145f, 0f);
        Light rl = rimLight.AddComponent<Light>();
        rl.type      = LightType.Directional;
        rl.color     = new Color(0.30f, 0.55f, 1f, 1f);
        rl.intensity = 0.9f;

        // ─── Blade (procedural katana made from primitives) ──────────────
        GameObject bladeRoot = new GameObject("Blade");
        bladeRoot.transform.SetParent(_stageRoot.transform, false);
        bladeRoot.transform.localPosition = Vector3.zero;
        _stageBlade = bladeRoot.transform;

        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "BladeMesh";
        Destroy(blade.GetComponent<Collider>());
        blade.transform.SetParent(bladeRoot.transform, false);
        blade.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        blade.transform.localScale    = new Vector3(0.05f, 1.4f, 0.18f);
        ColorPrim(blade, new Color(0.85f, 0.92f, 1f, 1f), 0.65f, 0.85f);

        GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        guard.name = "Guard";
        Destroy(guard.GetComponent<Collider>());
        guard.transform.SetParent(bladeRoot.transform, false);
        guard.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        guard.transform.localScale    = new Vector3(0.42f, 0.06f, 0.10f);
        ColorPrim(guard, new Color(1f, 0.78f, 0.20f, 1f), 0.45f, 0.65f);

        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        grip.name = "Grip";
        Destroy(grip.GetComponent<Collider>());
        grip.transform.SetParent(bladeRoot.transform, false);
        grip.transform.localPosition = new Vector3(0f, -0.50f, 0f);
        grip.transform.localScale    = new Vector3(0.06f, 0.30f, 0.06f);
        ColorPrim(grip, new Color(0.18f, 0.10f, 0.06f, 1f), 0.10f, 0.20f);

        GameObject pommel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pommel.name = "Pommel";
        Destroy(pommel.GetComponent<Collider>());
        pommel.transform.SetParent(bladeRoot.transform, false);
        pommel.transform.localPosition = new Vector3(0f, -0.85f, 0f);
        pommel.transform.localScale    = Vector3.one * 0.12f;
        ColorPrim(pommel, new Color(1f, 0.78f, 0.20f, 1f), 0.45f, 0.65f);

        // ─── Counter-rotating "energy ring" (a tilted torus made from
        //     stretched cubes around a circle).
        GameObject ringRoot = new GameObject("EnergyRing");
        ringRoot.transform.SetParent(_stageRoot.transform, false);
        ringRoot.transform.localPosition = Vector3.zero;
        _stageRing = ringRoot.transform;

        const int   ringSegments = 24;
        const float ringRadius   = 1.55f;
        for (int i = 0; i < ringSegments; i++)
        {
            float t = i / (float)ringSegments * Mathf.PI * 2f;
            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "RingSeg_" + i;
            Destroy(seg.GetComponent<Collider>());
            seg.transform.SetParent(ringRoot.transform, false);
            seg.transform.localPosition = new Vector3(Mathf.Cos(t) * ringRadius, 0f, Mathf.Sin(t) * ringRadius);
            seg.transform.localRotation = Quaternion.Euler(0f, -t * Mathf.Rad2Deg, 0f);
            seg.transform.localScale    = new Vector3(0.05f, 0.05f, 0.32f);
            // Gradient — alternate cyan/violet so the spin is visible.
            Color c = i % 2 == 0
                ? new Color(0.20f, 0.65f, 1f, 1f)
                : new Color(0.55f, 0.30f, 1f, 1f);
            ColorPrim(seg, c, 0.40f, 0.70f);
        }
    }

    /// <summary>
    /// Tints a primitive's material with the given base colour and PBR
    /// parameters. Uses the legacy "Standard" shader for built-in pipeline.
    /// </summary>
    private static void ColorPrim(GameObject obj, Color color, float metallic, float smoothness)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;
        Material m = new Material(Shader.Find("Standard") ?? r.sharedMaterial.shader);
        m.color = color;
        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic",   metallic);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        // Subtle emission so the blade glows even with no scene lights yet
        // bound (e.g. before the GameScene lighting setup runs).
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.25f);
        }
        r.material = m;
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
