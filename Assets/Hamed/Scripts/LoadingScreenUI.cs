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
            existing.StopLegacyAnimatorsOnly();
            existing.StartLoadingDotsAnimation();
            return existing;
        }

        GameObject root = new GameObject("RuntimeLoadingScreen");
        DontDestroyOnLoad(root);
        LoadingScreenUI ui = root.AddComponent<LoadingScreenUI>();
        ui.BuildUi();
        ui.ApplyMinimalLook();
        ui.StopLegacyAnimatorsOnly();
        ui.StartLoadingDotsAnimation();
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
        ui.StartLoadingDotsAnimation();
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
        _mpLockActive = false;
        MatchInitializer.HandleMultiplayerLoadingFinished();
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

    private void StartLoadingDotsAnimation()
    {
        if (_dotsRoutine != null)
            StopCoroutine(_dotsRoutine);
        _dotsRoutine = StartCoroutine(LoadingDotsLoop());
    }

    private void StopLegacyAnimatorsOnly()
    {
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
        if (_dotsRoutine != null)
        {
            StopCoroutine(_dotsRoutine);
            _dotsRoutine = null;
        }
        DestroyLoadingStageIfAny();
        if (gameObject != null)
            UnityEngine.Object.Destroy(gameObject);
    }

    private void OnEnable()
    {
        DestroyLoadingStageIfAny();
        StartLoadingDotsAnimation();
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
        _loadingLabel.text = "LOADING";
        _loadingLabel.fontSize = 36f;
        _loadingLabel.fontStyle = FontStyles.Bold;
        _loadingLabel.alignment = TextAlignmentOptions.BottomRight;
        PrismaticHudTypography.ApplyLoadingStyle(_loadingLabel);

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
    public static void Spawn()
    {
        MatchInitializer.HandleMultiplayerLoadingFinished();
    }
}
