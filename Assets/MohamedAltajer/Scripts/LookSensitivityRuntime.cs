using UnityEngine;

/// <summary>
/// Persistent, scene-spanning store for the player's mouse-look sensitivity
/// preference (Options menu).
///
/// The Options menu writes a 0–7 slider value via <see cref="SetSliderValue"/>;
/// <see cref="PlayerController"/> reads <see cref="LookMultiplier"/> every
/// frame in <c>ApplyLook</c> so the slider takes effect instantly without a
/// scene reload.
///
/// Slider mapping (0–7):
///   0  → 0.20× (very slow, accessibility-friendly)
///   3.5→ 1.00× (default — matches the historical sensitivity)
///   7  → 2.20× (very fast)
/// </summary>
public static class LookSensitivityRuntime
{
    public const string PrefKey      = "MouseSensitivity_07";
    public const float  MinSlider    = 0f;
    public const float  MaxSlider    = 7f;
    public const float  DefaultSlider = 3.5f;

    private const float MinMultiplier = 0.15f;
    private const float MaxMultiplier = 10.00f;

    /// <summary>Cached multiplier applied to <c>sensitivity</c> in PlayerController.</summary>
    public static float LookMultiplier { get; private set; } = 1f;

    /// <summary>Cached raw 0–7 slider value (for re-displaying in menus).</summary>
    public static float SliderValue { get; private set; } = DefaultSlider;

    static LookSensitivityRuntime()
    {
        LoadFromPrefs();
    }

    /// <summary>
    /// Reload the slider value from PlayerPrefs. Safe to call from any scene's
    /// Awake/Start as a defensive refresh.
    /// </summary>
    public static void LoadFromPrefs()
    {
        float stored = PlayerPrefs.GetFloat(PrefKey, DefaultSlider);
        SetSliderValue(stored, persist: false);
    }

    /// <summary>Updates the multiplier from a 0–7 slider value and (optionally) persists it.</summary>
    public static void SetSliderValue(float sliderValue, bool persist = true)
    {
        SliderValue    = Mathf.Clamp(sliderValue, MinSlider, MaxSlider);
        float t        = (SliderValue - MinSlider) / (MaxSlider - MinSlider);
        LookMultiplier = Mathf.Lerp(MinMultiplier, MaxMultiplier, t);

        if (!persist) return;
        PlayerPrefs.SetFloat(PrefKey, SliderValue);
        PlayerPrefs.Save();
    }
}
