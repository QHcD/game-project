using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Post-load 3 → 2 → 1 → GO: large centered numerals (no VO captions) + beeps via
/// <see cref="VoClipAutoIndex"/> / <see cref="GameManager.PlayMatchUiOneShot"/>.
/// </summary>
public class MatchStartCountdownUI : MonoBehaviour
{
    private const float StepHoldSeconds = 0.82f;
    private const float GoHoldSeconds   = 0.72f;

    public static IEnumerator Play()
    {
        GameObject host = new GameObject("MatchStartCountdown");
        DontDestroyOnLoad(host);
        var runner = host.AddComponent<MatchStartCountdownUI>();
        yield return runner.RunSequence();
        Destroy(host);
    }

    private Canvas _canvas;
    private TextMeshProUGUI _bigLabel;

    private IEnumerator RunSequence()
    {
        float prevScale = Time.timeScale;
        bool pausedForCountdown = !MultiplayerMode.IsMultiplayer;
        if (pausedForCountdown)
            Time.timeScale = 0f;

        BuildOverlay();
        VoClipAutoIndex.EnsureLoaded();

        for (int i = 3; i >= 1; i--)
        {
            SetBigText(i.ToString());
            AudioClip beep = VoClipAutoIndex.ResolveCountdownBeep();
            if (beep != null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.PlayMatchUiOneShot(beep, 1f);
                else
                    PlayFallbackOneShot(beep);
            }

            yield return new WaitForSecondsRealtime(StepHoldSeconds);
        }

        SetBigText("GO!");
        AudioClip go = VoClipAutoIndex.ResolveCountdownStart();
        if (go != null)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlayMatchUiOneShot(go, 1f);
            else
                PlayFallbackOneShot(go);
        }

        yield return new WaitForSecondsRealtime(GoHoldSeconds);

        if (pausedForCountdown)
            Time.timeScale = Mathf.Approximately(prevScale, 0f) ? 1f : prevScale;
    }

    private void SetBigText(string t)
    {
        if (_bigLabel != null)
            _bigLabel.text = t;
    }

    private void BuildOverlay()
    {
        GameObject root = new GameObject("CountdownCanvas");
        root.transform.SetParent(transform, false);

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 4000;
        root.AddComponent<GraphicRaycaster>();

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
        _bigLabel.fontStyle = FontStyles.Bold;
        _bigLabel.alignment = TextAlignmentOptions.Center;
        _bigLabel.color = new Color(1f, 1f, 1f, 0.96f);
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) _bigLabel.font = font;

        RectTransform lr = _bigLabel.rectTransform;
        lr.anchorMin = new Vector2(0.5f, 0.5f);
        lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(0.5f, 0.5f);
        lr.sizeDelta = new Vector2(900f, 360f);
        lr.anchoredPosition = Vector2.zero;
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
