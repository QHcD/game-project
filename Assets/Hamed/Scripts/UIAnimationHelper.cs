using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton MonoBehaviour that owns coroutines for common UI animations:
/// panel fade-in, punch scale (click feedback), and lateral shake (locked items).
/// Static helpers auto-create and keep alive across scene loads.
/// </summary>
public sealed class UIAnimationHelper : MonoBehaviour
{
    static UIAnimationHelper _instance;

    public static UIAnimationHelper Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("UIAnimationHelper");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UIAnimationHelper>();
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Fades a CanvasGroup from 0 to 1 over <paramref name="duration"/> seconds.
    public static void FadeIn(CanvasGroup cg, float duration = 0.22f)
        => Instance.StartCoroutine(Instance.CoFadeIn(cg, duration));

    /// Quick scale burst for click feedback (default: 12 % overshoot, 0.14 s).
    public static void PunchScale(Transform t, float duration = 0.14f, float peak = 1.12f)
        => Instance.StartCoroutine(Instance.CoPunchScale(t, duration, peak));

    /// Lateral shake — use for locked / disabled items when clicked.
    public static void Shake(Transform t, float duration = 0.32f, float strength = 7f)
        => Instance.StartCoroutine(Instance.CoShake(t, duration, strength));

    // ── Coroutines ────────────────────────────────────────────────────────────

    IEnumerator CoFadeIn(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;
        cg.alpha = 0f;
        for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
        {
            if (cg == null) yield break;
            cg.alpha = Mathf.Clamp01(e / duration);
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
    }

    IEnumerator CoPunchScale(Transform t, float duration, float peak)
    {
        if (t == null) yield break;
        Vector3 orig = t.localScale;
        float half = duration * 0.5f;
        for (float e = 0f; e < half; e += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            t.localScale = Vector3.Lerp(orig, orig * peak, e / half);
            yield return null;
        }
        for (float e = 0f; e < half; e += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            t.localScale = Vector3.Lerp(orig * peak, orig, e / half);
            yield return null;
        }
        if (t != null) t.localScale = orig;
    }

    IEnumerator CoShake(Transform t, float duration, float strength)
    {
        if (t == null) yield break;
        Vector3 origin = t.localPosition;
        for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            float fade = 1f - e / duration;
            t.localPosition = origin + new Vector3(Mathf.Sin(e * 55f) * strength * fade, 0f, 0f);
            yield return null;
        }
        if (t != null) t.localPosition = origin;
    }
}
