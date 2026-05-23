using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cross-scene brightness adjustment implemented as a non-interactive overlay.
/// This avoids pipeline-specific exposure tweaks while remaining safe and reversible.
/// </summary>
public static class BrightnessRuntime
{
    private static Canvas _canvas;
    private static Image _overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnBoot()
    {
        if (!Application.isPlaying)
            return;

        float b = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsManager.BrightnessKey, 1f));
        ApplyNow(b);
    }

    public static void ApplyNow(float brightness01)
    {
        if (!Application.isPlaying)
            return;

        EnsureOverlay();

        // brightness01: 0 -> dimmest, 1 -> brightest
        // We approximate "brightness" by reducing a black overlay alpha.
        float a = Mathf.Lerp(0.35f, 0.00f, Mathf.Clamp01(brightness01));
        if (_overlay != null)
            _overlay.color = new Color(0f, 0f, 0f, a);
    }

    private static void EnsureOverlay()
    {
        if (_overlay != null) return;

        GameObject host = GameObject.Find("__BrightnessOverlayCanvas");
        if (host == null)
        {
            host = new GameObject("__BrightnessOverlayCanvas");
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(host);
        }

        _canvas = host.GetComponent<Canvas>();
        if (_canvas == null) _canvas = host.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000;

        CanvasScaler scaler = host.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (host.GetComponent<GraphicRaycaster>() != null)
            Object.Destroy(host.GetComponent<GraphicRaycaster>());

        GameObject imgObj = host.transform.Find("BrightnessOverlay")?.gameObject;
        if (imgObj == null)
        {
            imgObj = new GameObject("BrightnessOverlay");
            imgObj.transform.SetParent(host.transform, false);
        }

        _overlay = imgObj.GetComponent<Image>();
        if (_overlay == null) _overlay = imgObj.AddComponent<Image>();
        _overlay.raycastTarget = false;

        RectTransform rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

