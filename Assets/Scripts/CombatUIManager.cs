using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatUIManager : MonoBehaviour
{
    private sealed class CombatNotification
    {
        public RectTransform Root;
        public CanvasGroup CanvasGroup;
        public TextMeshProUGUI XpText;
        public TextMeshProUGUI ActionText;
        public Coroutine LifetimeRoutine;
        public float CurrentY;
        public float TargetY;
    }

    private static CombatUIManager _instance;
    public static CombatUIManager Instance => _instance;

    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private float centerYOffset = 124f;
    [SerializeField] private float stackSpacing = 84f;
    [SerializeField] private int maxVisibleNotifications = 4;
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float holdDuration = 1.1f;
    [SerializeField] private float fadeOutDuration = 0.22f;
    [SerializeField] private float stackLerpSpeed = 14f;
    [SerializeField] private float introSlideDistance = 28f;

    private readonly List<CombatNotification> _activeNotifications = new List<CombatNotification>();

    private RectTransform _stackRoot;
    private TMP_FontAsset _font;

    public static CombatUIManager CreateOrGet(Transform hudCanvasTransform, TMP_FontAsset font = null)
    {
        if (hudCanvasTransform == null)
            return null;

        if (_instance != null)
        {
            _instance.Configure(hudCanvasTransform, font);
            return _instance;
        }

        Transform existing = hudCanvasTransform.Find("CombatUIManager");
        GameObject root = existing != null ? existing.gameObject : new GameObject("CombatUIManager");
        root.transform.SetParent(hudCanvasTransform, false);

        _instance = root.GetComponent<CombatUIManager>();
        if (_instance == null)
            _instance = root.AddComponent<CombatUIManager>();

        _instance.Configure(hudCanvasTransform, font);
        return _instance;
    }

    private static readonly string[] BO3MedalPool =
    {
        "HEADSHOT!", "DOMINATING!", "RAMPAGE!", "UNSTOPPABLE!", "SAVAGE!",
        "DOUBLE KILL!", "TRIPLE KILL!", "FIRST BLOOD!", "REVENGE!", "PAYBACK!",
        "NEUTRALIZED!", "BLINDSIDE!", "BACKSTAB!", "MERCILESS!", "BRUTAL!",
        "CLEAN HIT!", "EXECUTIONER!"
    };

    private RectTransform _newsFeedRoot;
    private readonly List<RectTransform> _newsFeedItems = new List<RectTransform>();
    private int _killStreak = 0;
    private float _killStreakWindow = 4f;
    private float _killStreakResetAt = 0f;

    public void ShowKill()
    {
        if (Time.unscaledTime > _killStreakResetAt) _killStreak = 0;
        _killStreak++;
        _killStreakResetAt = Time.unscaledTime + _killStreakWindow;

        ShowXpPopup(100, "KILL");
        PushNewsFeed(PickKillMedal(_killStreak));
    }

    public void ShowAssist()
    {
        ShowXpPopup(50, "ASSIST");
    }

    private static string PickKillMedal(int streak)
    {
        switch (streak)
        {
            case 2: return "DOUBLE KILL!";
            case 3: return "TRIPLE KILL!";
            case 4: return "DOMINATING!";
            case 5: return "RAMPAGE!";
            case 6: return "UNSTOPPABLE!";
            default:
                if (streak >= 7) return "SAVAGE!";
                return BO3MedalPool[Random.Range(0, BO3MedalPool.Length)];
        }
    }

    private void PushNewsFeed(string medal)
    {
        if (_stackRoot == null) BuildUi(transform);
        if (_newsFeedRoot == null) BuildNewsFeed(transform.parent != null ? transform.parent : transform);

        RectTransform item = GetOrCreateRect(_newsFeedRoot, "News_" + Time.frameCount + "_" + Random.Range(0, 10000));
        item.anchorMin = new Vector2(0f, 0f);
        item.anchorMax = new Vector2(1f, 0f);
        item.pivot = new Vector2(0.5f, 0f);
        item.sizeDelta = new Vector2(0f, 40f);

        CanvasGroup cg = item.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        TextMeshProUGUI label = CreateText(item, "Label", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
        label.color = Color.white;
        label.text = medal;
        AddTextOutline(label, new Color(0f, 0f, 0f, 0.7f), new Vector2(1.2f, -1.2f));
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16f, 0f);
        label.rectTransform.offsetMax = new Vector2(-16f, 0f);

        _newsFeedItems.Add(item);
        StartCoroutine(NewsFeedLifetime(item, cg));
    }

    private void BuildNewsFeed(Transform parent)
    {
        _newsFeedRoot = GetOrCreateRect(parent, "NewsFeedSlider");
        _newsFeedRoot.anchorMin = new Vector2(0f, 0f);
        _newsFeedRoot.anchorMax = new Vector2(1f, 0f);
        _newsFeedRoot.pivot = new Vector2(0.5f, 0f);
        _newsFeedRoot.anchoredPosition = new Vector2(0f, 32f);
        _newsFeedRoot.sizeDelta = new Vector2(0f, 220f);

        VerticalLayoutGroup vlg = _newsFeedRoot.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = _newsFeedRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerLeft;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(24, 24, 6, 6);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.reverseArrangement = false;
    }

    private IEnumerator NewsFeedLifetime(RectTransform item, CanvasGroup cg)
    {
        // Slide-in fade
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.18f);
            yield return null;
        }
        cg.alpha = 1f;

        yield return new WaitForSecondsRealtime(3f);

        t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / 0.4f);
            yield return null;
        }

        _newsFeedItems.Remove(item);
        if (item != null) Destroy(item.gameObject);
    }

    public void ShowXpPopup(int xpAmount, string actionWord)
    {
        if (_stackRoot == null)
            BuildUi(transform);

        CombatNotification notification = CreateNotification();
        notification.XpText.text = $"+{Mathf.Max(0, xpAmount)} XP";
        notification.ActionText.text = string.IsNullOrWhiteSpace(actionWord)
            ? "ELIMINATED"
            : actionWord.Trim().ToUpperInvariant();

        _activeNotifications.Insert(0, notification);
        if (_activeNotifications.Count > maxVisibleNotifications)
        {
            CombatNotification oldest = _activeNotifications[_activeNotifications.Count - 1];
            _activeNotifications.RemoveAt(_activeNotifications.Count - 1);
            DestroyNotification(oldest);
        }

        RefreshStackTargets(true);
        notification.LifetimeRoutine = StartCoroutine(AnimateNotification(notification));
    }

    private void Awake()
    {
        _instance = this;
    }

    private void Update()
    {
        for (int i = 0; i < _activeNotifications.Count; i++)
        {
            CombatNotification notification = _activeNotifications[i];
            if (notification?.Root == null)
                continue;

            notification.CurrentY = Mathf.Lerp(
                notification.CurrentY,
                notification.TargetY,
                1f - Mathf.Exp(-stackLerpSpeed * Time.unscaledDeltaTime));

            Vector2 anchored = notification.Root.anchoredPosition;
            anchored.y = notification.CurrentY;
            notification.Root.anchoredPosition = anchored;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Configure(Transform hudCanvasTransform, TMP_FontAsset font)
    {
        transform.SetParent(hudCanvasTransform, false);
        _font = font != null ? font : ResolveFallbackFont();

        CanvasScaler scaler = hudCanvasTransform.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (_stackRoot == null)
            BuildUi(hudCanvasTransform);
    }

    private void BuildUi(Transform parent)
    {
        _stackRoot = GetOrCreateRect(parent, "CombatStackRoot");
        _stackRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _stackRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _stackRoot.pivot = new Vector2(0.5f, 0.5f);
        _stackRoot.anchoredPosition = new Vector2(0f, centerYOffset);
        _stackRoot.sizeDelta = new Vector2(720f, 420f);
    }

    private CombatNotification CreateNotification()
    {
        RectTransform root = GetOrCreateRect(_stackRoot, "Notification_" + Time.frameCount + "_" + Random.Range(0, 10000));
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(520f, 74f);
        root.anchoredPosition = new Vector2(0f, -introSlideDistance);

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        TextMeshProUGUI xpText = CreateText(root, "XpText", 42f, FontStyles.Bold, TextAlignmentOptions.Center);
        xpText.color = Color.white;
        AddTextOutline(xpText, new Color(0f, 0f, 0f, 0.52f), new Vector2(1.4f, -1.4f));
        xpText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        xpText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        xpText.rectTransform.pivot = new Vector2(0.5f, 1f);
        xpText.rectTransform.anchoredPosition = Vector2.zero;
        xpText.rectTransform.sizeDelta = new Vector2(520f, 42f);

        TextMeshProUGUI actionText = CreateText(root, "ActionText", 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        actionText.color = new Color(1f, 1f, 1f, 0.86f);
        actionText.characterSpacing = 8f;
        AddTextOutline(actionText, new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, -1f));
        actionText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        actionText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        actionText.rectTransform.pivot = new Vector2(0.5f, 0f);
        actionText.rectTransform.anchoredPosition = Vector2.zero;
        actionText.rectTransform.sizeDelta = new Vector2(520f, 28f);

        return new CombatNotification
        {
            Root = root,
            CanvasGroup = canvasGroup,
            XpText = xpText,
            ActionText = actionText,
            CurrentY = -introSlideDistance,
            TargetY = 0f
        };
    }

    private IEnumerator AnimateNotification(CombatNotification notification)
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeInDuration));
            notification.CanvasGroup.alpha = t;
            yield return null;
        }

        notification.CanvasGroup.alpha = 1f;

        float hold = 0f;
        while (hold < holdDuration)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeOutDuration));
            notification.CanvasGroup.alpha = 1f - t;
            yield return null;
        }

        RemoveNotification(notification);
    }

    private void RefreshStackTargets(bool snapNewest)
    {
        for (int i = 0; i < _activeNotifications.Count; i++)
        {
            CombatNotification notification = _activeNotifications[i];
            if (notification == null)
                continue;

            notification.TargetY = i * stackSpacing;
            if (snapNewest && i == 0)
                notification.CurrentY = notification.TargetY - introSlideDistance;

            if (notification.Root != null)
                notification.Root.SetSiblingIndex(_activeNotifications.Count - 1 - i);
        }
    }

    private void RemoveNotification(CombatNotification notification)
    {
        if (notification == null)
            return;

        _activeNotifications.Remove(notification);
        DestroyNotification(notification);
        RefreshStackTargets(false);
    }

    private static void DestroyNotification(CombatNotification notification)
    {
        if (notification == null)
            return;

        if (notification.LifetimeRoutine != null && _instance != null)
            _instance.StopCoroutine(notification.LifetimeRoutine);

        if (notification.Root != null)
            Destroy(notification.Root.gameObject);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name);
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = obj.AddComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.font = _font != null ? _font : ResolveFallbackFont();
        return text;
    }

    private static void AddTextOutline(Graphic graphic, Color color, Vector2 distance)
    {
        if (graphic == null)
            return;

        Outline outline = graphic.GetComponent<Outline>();
        if (outline == null)
            outline = graphic.gameObject.AddComponent<Outline>();

        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static RectTransform GetOrCreateRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect == null)
            rect = obj.AddComponent<RectTransform>();
        return rect;
    }

    private static TMP_FontAsset ResolveFallbackFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }
}
