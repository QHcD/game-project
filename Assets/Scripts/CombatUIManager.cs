using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all in-game combat pop-up notifications:
///
///   Zone A — CENTER POPUP ZONE (anchor 0.5, 0.65 of screen)
///     • "+100 XP / KILL"  floats upward then fades — never touches the HUD corners.
///     • Up to 4 stack vertically so rapid kills don't overlap.
///
///   Zone B — BOTTOM-LEFT NEWS FEED (anchor 0, 0 — above health bar)
///     • Medal strings ("PAYBACK!", "HEADSHOT!", …) slide in, hold 3 s, fade out.
///     • Positioned at y ≥ 160 px so it sits above the health panel.
///
/// Colour rules (applied per message type):
///   Medal   "PAYBACK!"  → red     (#FF3B3B)
///   Medal   "REVENGE!"  → orange  (#FF8C00)
///   XP line "+N XP"     → yellow  (#FFE135)
///   Kill    "KILL"      → white
///   Assist  "ASSIST"    → cyan    (#5AC8FA)
///   Default             → white
///
/// Canvas layout contract (from HUDManager):
///   TopBar        — anchors (0,1)→(1,1), height 64 px
///   BottomPanel   — anchors (0,0)→(0.5,0), height 120 px
///   KillCountText — bottom-left corner
///   MinimapRoot   — bottom-right corner
///   CombatUIManager sits on the SAME HUDCanvas but uses centre/bottom-left
///   anchors that avoid all four corners.
/// </summary>
public class CombatUIManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    private static CombatUIManager _instance;
    public  static CombatUIManager Instance => _instance;

    // ── Tuning ────────────────────────────────────────────────────────────────
    [Header("Center Popup Zone")]
    [Tooltip("Normalised vertical anchor for the center popup stack (0=bottom, 1=top). " +
             "0.62 places pops roughly 2/3 up the screen, well above the health bar.")]
    [SerializeField] private float popupAnchorY      = 0.62f;
    [SerializeField] private int   maxPopups         = 4;
    [SerializeField] private float fadeInDuration    = 0.10f;
    [SerializeField] private float holdDuration      = 1.20f;
    [SerializeField] private float fadeOutDuration   = 0.25f;
    [SerializeField] private float floatSpeed        = 38f;   // px/s upward drift
    [SerializeField] private float stackLerpSpeed    = 14f;
    [SerializeField] private float introSlideDown    = 22f;   // starts this many px below target

    [Header("News Feed Zone")]
    [Tooltip("Pixels from the bottom of the screen. Must be above the health bar (~120 px).")]
    [SerializeField] private float newsFeedBottomPad = 168f;
    [SerializeField] private float newsFeedLeftPad   = 24f;

    // ── Colour palette ────────────────────────────────────────────────────────
    private static readonly Color ColXP      = new Color(1.00f, 0.88f, 0.20f, 1f); // yellow
    private static readonly Color ColKill    = new Color(1.00f, 1.00f, 1.00f, 1f); // white
    private static readonly Color ColAssist  = new Color(0.35f, 0.78f, 1.00f, 1f); // cyan
    private static readonly Color ColPayback = new Color(1.00f, 0.23f, 0.23f, 1f); // red
    private static readonly Color ColRevenge = new Color(1.00f, 0.55f, 0.00f, 1f); // orange
    private static readonly Color ColMedal   = new Color(1.00f, 1.00f, 1.00f, 1f); // white

    // ── Medal pool ────────────────────────────────────────────────────────────
    private static readonly string[] BO3MedalPool =
    {
        "HEADSHOT!", "NEUTRALIZED!", "BLINDSIDE!", "BACKSTAB!",
        "MERCILESS!", "BRUTAL!", "CLEAN HIT!", "EXECUTIONER!",
        "FIRST BLOOD!", "COMEBACK!",
    };

    // ── Internal state ────────────────────────────────────────────────────────
    private sealed class Popup
    {
        public RectTransform  Root;
        public CanvasGroup    CG;
        public TextMeshProUGUI XpLabel;
        public TextMeshProUGUI ActionLabel;
        public Coroutine      Lifetime;
        public float          CurrentY;
        public float          TargetY;
    }

    private readonly List<Popup>          _popups    = new List<Popup>();
    private readonly List<RectTransform>  _feedItems = new List<RectTransform>();

    private RectTransform _popupRoot;   // Zone A parent
    private RectTransform _feedRoot;    // Zone B parent
    private TMP_FontAsset _font;

    private int   _killStreak;
    private float _streakResetAt;
    private const float StreakWindow = 4f;

    // ═════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates or returns the singleton. Call from HUDManager.EnsureCombatUi().
    /// </summary>
    public static CombatUIManager CreateOrGet(Transform hudCanvas, TMP_FontAsset font = null)
    {
        if (hudCanvas == null) return null;

        if (_instance != null)
        {
            _instance.Init(hudCanvas, font);
            return _instance;
        }

        Transform existing = hudCanvas.Find("CombatUIManager");
        RectTransform selfRect = EnsureRectTransform(hudCanvas, existing, "CombatUIManager");
        GameObject go = selfRect.gameObject;
        // Stretch to fill the entire canvas so it doesn't clip child zones.
        selfRect.anchorMin        = Vector2.zero;
        selfRect.anchorMax        = Vector2.one;
        selfRect.offsetMin        = Vector2.zero;
        selfRect.offsetMax        = Vector2.zero;
        selfRect.anchoredPosition = Vector2.zero;

        _instance = go.GetComponent<CombatUIManager>() ?? go.AddComponent<CombatUIManager>();
        _instance.Init(hudCanvas, font);
        return _instance;
    }

    /// <summary>Kill event: "+100 XP / KILL" popup + streak medal in feed.</summary>
    public void ShowKill()
    {
        if (Time.unscaledTime > _streakResetAt) _killStreak = 0;
        _killStreak++;
        _streakResetAt = Time.unscaledTime + StreakWindow;

        SpawnPopup(100, "KILL");
        PushFeed(PickMedal(_killStreak));
    }

    /// <summary>Assist event: "+50 XP / ASSIST" popup.</summary>
    public void ShowAssist()
    {
        SpawnPopup(50, "ASSIST");
    }

    /// <summary>
    /// Generic XP popup. actionWord sets the sub-label and tints the message.
    /// Colour is resolved automatically from <see cref="ResolveActionColour"/>.
    /// </summary>
    public void ShowXpPopup(int xpAmount, string actionWord)
    {
        SpawnPopup(xpAmount, actionWord);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═════════════════════════════════════════════════════════════════════════

    private void Awake()  => _instance = this;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    private void Update()
    {
        // Smooth-float every active popup upward toward its target Y.
        float dt = Time.unscaledDeltaTime;
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            Popup p = _popups[i];
            if (p?.Root == null) continue;

            // Drift upward over time (float effect).
            p.TargetY += floatSpeed * dt;
            p.CurrentY = Mathf.Lerp(p.CurrentY, p.TargetY,
                1f - Mathf.Exp(-stackLerpSpeed * dt));

            Vector2 ap = p.Root.anchoredPosition;
            ap.y = p.CurrentY;
            p.Root.anchoredPosition = ap;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  INITIALISATION
    // ═════════════════════════════════════════════════════════════════════════

    private void Init(Transform hudCanvas, TMP_FontAsset font)
    {
        transform.SetParent(hudCanvas, false);
        _font = font != null ? font : FallbackFont();

        // Ensure CanvasScaler on the parent canvas scales correctly.
        CanvasScaler scaler = hudCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920f, 1080f);
            scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight   = 0.5f;
        }

        BuildZones(hudCanvas);
    }

    private void BuildZones(Transform hudCanvas)
    {
        // Use THIS transform (which now has a RectTransform stretched to the
        // full canvas) as the parent for both zones. This keeps the hierarchy
        // clean and ensures Unity never traverses a node without a RectTransform.
        Transform zoneParent = transform;

        // ── Zone A: centre popup stack ────────────────────────────────────────
        if (_popupRoot == null)
        {
            _popupRoot = MakeRect(zoneParent, "CombatPopupZone");
            _popupRoot.anchorMin        = new Vector2(0.5f, popupAnchorY);
            _popupRoot.anchorMax        = new Vector2(0.5f, popupAnchorY);
            _popupRoot.pivot            = new Vector2(0.5f, 0f);
            _popupRoot.anchoredPosition = Vector2.zero;
            _popupRoot.sizeDelta        = new Vector2(600f, 400f);
        }

        // ── Zone B: bottom-left news feed ─────────────────────────────────────
        if (_feedRoot == null)
        {
            _feedRoot = MakeRect(zoneParent, "CombatNewsFeed");
            _feedRoot.anchorMin        = new Vector2(0f, 0f);
            _feedRoot.anchorMax        = new Vector2(0f, 0f);
            _feedRoot.pivot            = new Vector2(0f, 0f);
            _feedRoot.anchoredPosition = new Vector2(newsFeedLeftPad, newsFeedBottomPad);
            _feedRoot.sizeDelta        = new Vector2(480f, 260f);

            // Stack children bottom-up.
            VerticalLayoutGroup vlg = _feedRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment      = TextAnchor.LowerLeft;
            vlg.spacing             = 4f;
            vlg.padding             = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth   = true;
            vlg.childControlHeight  = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.reverseArrangement  = true; // newest entry at bottom of stack
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CENTER POPUP (Zone A)
    // ═════════════════════════════════════════════════════════════════════════

    private void SpawnPopup(int xp, string actionWord)
    {
        if (_popupRoot == null) BuildZones(transform.parent != null ? transform.parent : transform);
        if (_popupRoot == null) return; // canvas not ready yet

        // Evict oldest if we're at the cap.
        if (_popups.Count >= maxPopups)
        {
            KillPopup(_popups[_popups.Count - 1]);
        }

        string action  = string.IsNullOrWhiteSpace(actionWord) ? "ELIMINATED" : actionWord.Trim().ToUpperInvariant();
        Color  xpColor = ResolveXpColour(xp);
        Color  actColor = ResolveActionColour(action);

        // Build popup root.
        RectTransform root = MakeRect(_popupRoot, "Popup_" + Time.frameCount);
        root.anchorMin        = new Vector2(0.5f, 0f);
        root.anchorMax        = new Vector2(0.5f, 0f);
        root.pivot            = new Vector2(0.5f, 0f);
        root.sizeDelta        = new Vector2(500f, 80f);
        root.anchoredPosition = Vector2.zero;

        CanvasGroup cg = root.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // XP label (large, bold, coloured).
        TextMeshProUGUI xpLabel = MakeText(root, "XpLabel", 46f, FontStyles.Bold, TextAlignmentOptions.Center);
        xpLabel.text  = xp > 0 ? $"+{xp} XP" : string.Empty;
        xpLabel.color = xpColor;
        Outline(xpLabel, new Color(0f, 0f, 0f, 0.6f), new Vector2(1.5f, -1.5f));
        xpLabel.rectTransform.anchorMin        = new Vector2(0f, 0.5f);
        xpLabel.rectTransform.anchorMax        = new Vector2(1f, 1f);
        xpLabel.rectTransform.offsetMin        = Vector2.zero;
        xpLabel.rectTransform.offsetMax        = Vector2.zero;

        // Action label (smaller, character-spaced).
        TextMeshProUGUI actLabel = MakeText(root, "ActionLabel", 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        actLabel.text             = action;
        actLabel.color            = actColor;
        actLabel.characterSpacing = 10f;
        Outline(actLabel, new Color(0f, 0f, 0f, 0.55f), new Vector2(1f, -1f));
        actLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        actLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        actLabel.rectTransform.offsetMin = Vector2.zero;
        actLabel.rectTransform.offsetMax = Vector2.zero;

        Popup p = new Popup
        {
            Root        = root,
            CG          = cg,
            XpLabel     = xpLabel,
            ActionLabel = actLabel,
            CurrentY    = -introSlideDown,
            TargetY     = 0f,
        };
        _popups.Insert(0, p);
        p.Lifetime = StartCoroutine(PopupLifetime(p));
    }

    private IEnumerator PopupLifetime(Popup p)
    {
        // Fade in.
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            if (p.CG != null) p.CG.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        if (p.CG != null) p.CG.alpha = 1f;

        // Hold.
        yield return new WaitForSecondsRealtime(holdDuration);

        // Fade out.
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            if (p.CG != null) p.CG.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        KillPopup(p);
    }

    private void KillPopup(Popup p)
    {
        if (p == null) return;
        _popups.Remove(p);
        if (p.Lifetime != null) StopCoroutine(p.Lifetime);
        if (p.Root != null) Destroy(p.Root.gameObject);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  NEWS FEED (Zone B)
    // ═════════════════════════════════════════════════════════════════════════

    private void PushFeed(string medal)
    {
        if (_feedRoot == null) BuildZones(transform.parent != null ? transform.parent : transform);
        if (_feedRoot == null) return;

        RectTransform item = MakeRect(_feedRoot, "Feed_" + Time.frameCount);
        item.sizeDelta = new Vector2(0f, 38f);

        CanvasGroup cg = item.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        TextMeshProUGUI label = MakeText(item, "Label", 27f, FontStyles.Bold, TextAlignmentOptions.Left);
        label.text  = medal;
        label.color = ResolveMedalColour(medal);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        Outline(label, new Color(0f, 0f, 0f, 0.72f), new Vector2(1.1f, -1.1f));

        // Fill parent width.
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(8f, 0f);
        lr.offsetMax = new Vector2(-8f, 0f);

        _feedItems.Add(item);
        StartCoroutine(FeedLifetime(item, cg));
    }

    private IEnumerator FeedLifetime(RectTransform item, CanvasGroup cg)
    {
        // Slide-in.
        float t = 0f;
        while (t < 0.15f) { t += Time.unscaledDeltaTime; cg.alpha = t / 0.15f; yield return null; }
        cg.alpha = 1f;

        yield return new WaitForSecondsRealtime(3f);

        // Fade-out.
        t = 0f;
        while (t < 0.35f) { t += Time.unscaledDeltaTime; cg.alpha = 1f - t / 0.35f; yield return null; }

        _feedItems.Remove(item);
        if (item != null) Destroy(item.gameObject);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  COLOUR RESOLUTION
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Colour for the "+N XP" top line based on amount.</summary>
    private static Color ResolveXpColour(int xp)
    {
        if (xp >= 100) return ColXP;          // yellow — standard kill
        if (xp >= 50)  return ColAssist;       // cyan   — assist
        return ColKill;                         // white  — tiny bonus
    }

    /// <summary>Colour for the action sub-label ("KILL", "ASSIST", "PAYBACK!", …).</summary>
    private static Color ResolveActionColour(string action)
    {
        if (string.IsNullOrEmpty(action)) return ColKill;

        // Exact matches first.
        switch (action)
        {
            case "KILL":     return ColKill;
            case "ASSIST":   return ColAssist;
            case "PAYBACK!": return ColPayback;
            case "REVENGE!": return ColRevenge;
        }

        // Keyword tinting for other messages.
        if (action.Contains("KILL"))   return ColKill;
        if (action.Contains("ASSIST")) return ColAssist;

        return ColKill; // default white
    }

    /// <summary>Colour for a news-feed medal string.</summary>
    private static Color ResolveMedalColour(string medal)
    {
        if (string.IsNullOrEmpty(medal)) return ColMedal;

        switch (medal)
        {
            case "PAYBACK!":     return ColPayback;
            case "REVENGE!":     return ColRevenge;
            case "DOMINATING!":  return new Color(1.00f, 0.40f, 0.00f, 1f); // deep orange
            case "RAMPAGE!":     return new Color(1.00f, 0.25f, 0.10f, 1f); // red-orange
            case "UNSTOPPABLE!": return new Color(0.90f, 0.20f, 0.20f, 1f); // red
            case "SAVAGE!":      return new Color(0.80f, 0.10f, 0.10f, 1f); // dark red
            case "FIRST BLOOD!": return new Color(0.95f, 0.20f, 0.20f, 1f); // red
            case "HEADSHOT!":    return new Color(1.00f, 0.92f, 0.20f, 1f); // bright yellow
            default:             return ColMedal;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MEDAL SELECTION
    // ═════════════════════════════════════════════════════════════════════════

    private static string PickMedal(int streak)
    {
        switch (streak)
        {
            case 2: return "DOUBLE KILL!";
            case 3: return "TRIPLE KILL!";
            case 4: return "DOMINATING!";
            case 5: return "RAMPAGE!";
            case 6: return "UNSTOPPABLE!";
            default: return streak >= 7 ? "SAVAGE!" : BO3MedalPool[Random.Range(0, BO3MedalPool.Length)];
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    private static RectTransform MakeRect(Transform parent, string name)
    {
        Transform ex = parent.Find(name);
        return EnsureRectTransform(parent, ex, name);
    }

    private static RectTransform EnsureRectTransform(Transform parent, Transform existing, string name)
    {
        if (existing != null)
        {
            RectTransform existingRect = existing as RectTransform;
            if (existingRect != null)
            {
                existingRect.SetParent(parent, false);
                return existingRect;
            }

            // Recreate invalid legacy node that was built as a plain Transform.
            Object.Destroy(existing.gameObject);
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name,
        float size, FontStyles style, TextAlignmentOptions align)
    {
        Transform ex = parent.Find(name);
        GameObject go = ex != null ? ex.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        t.fontSize         = size;
        t.fontStyle        = style;
        t.alignment        = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode     = TextOverflowModes.Overflow;
        t.color            = Color.white;
        t.font             = _font != null ? _font : FallbackFont();
        return t;
    }

    private static void Outline(Graphic g, Color colour, Vector2 dist)
    {
        if (g == null) return;
        UnityEngine.UI.Outline o = g.GetComponent<UnityEngine.UI.Outline>()
                                 ?? g.gameObject.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor    = colour;
        o.effectDistance = dist;
    }

    private static TMP_FontAsset FallbackFont()
    {
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }
}
