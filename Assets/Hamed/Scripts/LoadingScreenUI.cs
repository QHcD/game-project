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
    /// Multiplayer-specific entry point. Shows a SIMPLE timed overlay that
    /// auto-destroys after <paramref name="seconds"/>. While visible, sets
    /// EndMatchCinematic.GameplayLocked = true so attacks / hit audio / enemy
    /// AI cannot fire underneath the overlay. After the timer expires:
    /// unlock gameplay + destroy the overlay. Does NOT wait for Photon
    /// callbacks, HUD init, or any async gameplay state.
    /// </summary>
    public static void ShowTimedForMultiplayer(float seconds = 5f)
    {
        LoadingScreenUI ui = CreateOrGet();
        if (ui == null) return;
        ui.SetLabel("LOADING...");
        ui.BeginMultiplayerLockWindow(seconds);
    }

    private bool _mpLockActive;

    private void BeginMultiplayerLockWindow(float seconds)
    {
        EndMatchCinematic.GameplayLocked = true;
        _mpLockActive = true;
        if (_canvas != null) _canvas.sortingOrder = 9999;
        Debug.Log("[MPLoading] visible before scene load");
        Debug.Log("[MPLoading] overlay shown");
        StartCoroutine(MultiplayerLockCoroutine(seconds));
    }

    private IEnumerator MultiplayerLockCoroutine(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        Debug.Log("[MPLoading] hidden after 5 seconds");
        Debug.Log("[MPLoading] overlay destroyed after " + seconds + " seconds");

        // Hand the gameplay lock off to the 3-2-1-GO overlay (lives on its
        // own GameObject so this MonoBehaviour can be destroyed immediately
        // without killing the countdown coroutine).
        _mpLockActive = false; // suppress OnDestroy unlock — the countdown owns it now
        MpStartCountdown.Spawn();
        DestroySelf();
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
        // Note: the dot-anim loop is intentionally NOT started here. The
        // multiplayer flow uses a static "LOADING..." label and a 5-second
        // timed destroy (see ShowTimedForMultiplayer); the dot animation
        // would overwrite that label every 0.22s.
    }

    private void OnDestroy()
    {
        // Safety net: if the overlay is destroyed externally before the
        // coroutine fires (scene reload, force close), make sure we don't
        // leave gameplay locked.
        if (_mpLockActive)
        {
            EndMatchCinematic.GameplayLocked = false;
            _mpLockActive = false;
            Debug.Log("[MPLoading] gameplay unlocked (overlay destroyed early)");
        }
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
            // Fully opaque — must completely hide the scene behind the overlay.
            if (_dimOverlay != null)
                _dimOverlay.color = new Color(0f, 0f, 0f, 0.92f);
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
        // 9999 beats anything else in the project (HUDCanvas=50, Pause=300,
        // Spectator=999). Verified to render over the freshly loaded scene's
        // own canvases too.
        _canvas.sortingOrder = 9999;
        gameObject.AddComponent<GraphicRaycaster>();
        Debug.Log($"[MPLoading] overlay canvas sortingOrder={_canvas.sortingOrder}");

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
        // Fully opaque dim so the underlying scene is never visible while the
        // overlay is up — previously 0.12 alpha let gameplay bleed through.
        _dimOverlay.color = new Color(0f, 0f, 0f, 0.92f);
        Stretch(_dimOverlay.rectTransform);

        // Bottom-right, matching the normal gameplay loading style.
        GameObject labelObject = new GameObject("LoadingLabel");
        labelObject.transform.SetParent(transform, false);
        _loadingLabel = labelObject.AddComponent<TextMeshProUGUI>();
        _loadingLabel.text = "LOADING...";
        _loadingLabel.fontSize = 36f;
        _loadingLabel.fontStyle = FontStyles.Bold;
        _loadingLabel.alignment = TextAlignmentOptions.BottomRight;
        _loadingLabel.color = new Color(0.90f, 0.95f, 1f, 0.96f);
        TMP_FontAsset azonix = Resources.Load<TMP_FontAsset>("Fonts/Azonix SDF");
        if (azonix != null) _loadingLabel.font = azonix;

        RectTransform labelRect = _loadingLabel.rectTransform;
        labelRect.anchorMin = new Vector2(1f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(1f, 0f);
        labelRect.sizeDelta = new Vector2(560f, 90f);
        labelRect.anchoredPosition = new Vector2(-44f, 28f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

/// <summary>
/// Big centred 3 → 2 → 1 → GO! flash that runs AFTER the LOADING overlay is
/// torn down. Holds <c>EndMatchCinematic.GameplayLocked = true</c> for the
/// duration so the player can't attack until "GO!". Lives on its own
/// DontDestroyOnLoad GameObject; LoadingScreenUI hands the lock to it.
/// </summary>
public class MpStartCountdown : MonoBehaviour
{
    private TextMeshProUGUI _label;

    public static void Spawn()
    {
        if (UnityEngine.Object.FindFirstObjectByType<MpStartCountdown>() != null)
            return;

        GameObject root = new GameObject("RuntimeStartCountdown");
        DontDestroyOnLoad(root);
        root.AddComponent<MpStartCountdown>();
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998;
        gameObject.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(transform, false);
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.35f);
        RectTransform dimRect = dimImg.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        GameObject labelObj = new GameObject("CountdownLabel");
        labelObj.transform.SetParent(transform, false);
        _label = labelObj.AddComponent<TextMeshProUGUI>();
        _label.fontSize = 220f;
        _label.fontStyle = FontStyles.Bold;
        _label.alignment = TextAlignmentOptions.Center;
        _label.color = new Color(0.95f, 0.98f, 1f, 1f);
        TMP_FontAsset azonix = Resources.Load<TMP_FontAsset>("Fonts/Azonix SDF");
        if (azonix != null) _label.font = azonix;
        RectTransform lr = _label.rectTransform;
        lr.anchorMin = new Vector2(0.5f, 0.5f);
        lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(0.5f, 0.5f);
        lr.sizeDelta = new Vector2(800f, 400f);
        lr.anchoredPosition = Vector2.zero;

        // Keep the gameplay lock asserted while we exist.
        EndMatchCinematic.GameplayLocked = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        string[] steps = { "3", "2", "1", "GO!" };
        for (int i = 0; i < steps.Length; i++)
        {
            if (_label != null)
            {
                _label.text = steps[i];
                _label.color = steps[i] == "GO!"
                    ? new Color(0.40f, 1f, 0.55f, 1f)
                    : new Color(0.95f, 0.98f, 1f, 1f);
            }
            Debug.Log("[MPLoading] countdown " + steps[i]);
            yield return new WaitForSecondsRealtime(steps[i] == "GO!" ? 0.6f : 0.8f);
        }

        EndMatchCinematic.GameplayLocked = false;
        Debug.Log("[MPLoading] gameplay unlocked");
        UnityEngine.Object.Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Safety: if torn down externally, never leave gameplay locked.
        if (EndMatchCinematic.GameplayLocked)
        {
            EndMatchCinematic.GameplayLocked = false;
            Debug.Log("[MPLoading] gameplay unlocked (countdown destroyed early)");
        }
    }
}
