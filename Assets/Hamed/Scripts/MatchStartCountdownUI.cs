using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchStartCountdownUI : MonoBehaviour
{
    private const float StepHoldSeconds = 0.95f;
    private const float GoHoldSeconds   = 0.80f;

    public static IEnumerator Play()
    {
        CleanupStrayOverlays();

        GameObject host = new GameObject("MatchStartCountdown");
        var ui = host.AddComponent<MatchStartCountdownUI>();

        EndMatchCinematic.GameplayLocked = true;

        yield return ui.StartCoroutine(ui.RunSequence());

        EndMatchCinematic.GameplayLocked = false;

        if (ui != null)
            Destroy(ui.gameObject);
    }

    public static void CleanupStrayOverlays()
    {
        GameObject stray = GameObject.Find("MatchStartCountdown");
        if (stray != null)
            Destroy(stray);

        GameObject canvas = GameObject.Find("CountdownCanvas");
        if (canvas != null)
            Destroy(canvas);
    }

    private Canvas _canvas;
    private TextMeshProUGUI _bigLabel;
    private CanvasGroup _canvasGroup;

    private IEnumerator RunSequence()
    {
        float prevScale = Time.timeScale;
        bool pausedForCountdown = !MultiplayerMode.IsMultiplayer;
        if (pausedForCountdown)
            Time.timeScale = 0f;

        BuildOverlay();

        AudioClip countdownClip = ResolveCountdownClip();
        if (countdownClip != null)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlayMatchUiOneShot(countdownClip, 1f);
            else
                PlayFallbackOneShot(countdownClip);
        }

        string[] steps = { "3", "2", "1", "GO!" };
        for (int i = 0; i < steps.Length; i++)
        {
            bool isGo = steps[i] == "GO!";
            SetBigText(steps[i], isGo);
            yield return AnimateStep(isGo);
            yield return new WaitForSecondsRealtime(isGo ? GoHoldSeconds * 0.4f : StepHoldSeconds * 0.5f);
        }

        yield return FadeOut(0.3f);

        if (pausedForCountdown)
            Time.timeScale = Mathf.Approximately(prevScale, 0f) ? 1f : prevScale;
    }

    private IEnumerator AnimateStep(bool isGo)
    {
        if (_bigLabel == null) yield break;

        RectTransform rt = _bigLabel.rectTransform;
        float duration = isGo ? 0.25f : 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(1.6f, 1f, t * t);
            rt.localScale = new Vector3(scale, scale, 1f);

            Color c = _bigLabel.color;
            c.a = Mathf.Lerp(0.3f, 1f, t);
            _bigLabel.color = c;

            yield return null;
        }

        rt.localScale = Vector3.one;
        Color final_ = _bigLabel.color;
        final_.a = 1f;
        _bigLabel.color = final_;
    }

    private IEnumerator FadeOut(float duration)
    {
        if (_canvasGroup == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = 0f;
    }

    private void SetBigText(string t, bool isGo)
    {
        if (_bigLabel == null) return;
        _bigLabel.text = t;
        PrismaticHudTypography.ApplyCountdownStyle(_bigLabel, isGo);

        if (isGo)
            _bigLabel.fontSize = 260f;
        else
            _bigLabel.fontSize = 220f;
    }

    private void BuildOverlay()
    {
        GameObject root = new GameObject("CountdownCanvas");
        root.transform.SetParent(transform, false);

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9998;
        root.AddComponent<GraphicRaycaster>();
        _canvasGroup = root.AddComponent<CanvasGroup>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(root.transform, false);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.35f);
        RectTransform dimRt = dimImg.rectTransform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;

        GameObject labelGo = new GameObject("Count");
        labelGo.transform.SetParent(root.transform, false);
        _bigLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _bigLabel.text = "3";
        _bigLabel.fontSize = 220f;
        _bigLabel.alignment = TextAlignmentOptions.Center;
        PrismaticHudTypography.ApplyCountdownStyle(_bigLabel, false);

        RectTransform lr = _bigLabel.rectTransform;
        lr.anchorMin = new Vector2(0.5f, 0.5f);
        lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(0.5f, 0.5f);
        lr.sizeDelta = new Vector2(800f, 400f);
        lr.anchoredPosition = Vector2.zero;
    }

    private static AudioClip ResolveCountdownClip()
    {
        if (WeaponHitAudioDatabase.Instance != null && WeaponHitAudioDatabase.Instance.countdownClip != null)
            return WeaponHitAudioDatabase.Instance.countdownClip;

#if UNITY_EDITOR
        AudioClip editorClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/MohamedAman/Materials/3 2 1 go sound effect.mp3");
        if (editorClip != null) return editorClip;
#endif

        AudioClip res = Resources.Load<AudioClip>("Audio/3 2 1 go sound effect");
        if (res != null) return res;

        return VoClipAutoIndex.ResolveCountdownStart();
    }

    private static void PlayFallbackOneShot(AudioClip clip)
    {
        var host = new GameObject("CountdownFallbackAudio");
        var src  = host.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (SessionManager.Instance != null)
            SessionManager.Instance.ConfigureMatchAudioSource(src, SessionManager.Instance.MatchUiMixerGroupName);
        src.PlayOneShot(clip, 1f);
        Destroy(host, clip.length + 0.1f);
    }
}
