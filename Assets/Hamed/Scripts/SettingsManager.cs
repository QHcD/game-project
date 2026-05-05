using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared settings UI helpers: volume readouts (0–100), menu blues, and fullscreen toggle chrome.
/// Scene logic stays in <see cref="SettingsBuilder"/>; this type keeps values consistent with the video spec.
/// </summary>
public static class SettingsManager
{
    public static readonly Color MenuBlue = new Color(0.32f, 0.56f, 0.96f, 1f);
    public static readonly Color MenuBlueDim = new Color(0.22f, 0.40f, 0.72f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyDisplayPreferencesOnBoot()
    {
        ApplyDisplayPreferences();
    }

    public static void ApplyDisplayPreferences()
    {
        int tier = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, 2);
        int qualityLevel = tier == 0 ? 0 :
            tier == 1 ? Mathf.Max(0, (QualitySettings.names.Length - 1) / 2) :
            Mathf.Max(0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel);

        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        PlayerPrefs.SetInt("GraphicsTier", tier);
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();

        if (Screen.fullScreen != fullscreen)
            Screen.fullScreen = fullscreen;
    }

    public static string FormatVolumePercent(float normalized01)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(normalized01 * 100f), 0, 100);
        return n + "%";
    }

    /// <summary>Standard tick mark; Unity's <see cref="Toggle"/> shows/hides <paramref name="mark"/> when off/on.</summary>
    public static void ApplyFullscreenToggleGraphic(Toggle toggle, TextMeshProUGUI mark, TMP_FontAsset font)
    {
        if (toggle == null || mark == null) return;
        // Avoid missing-glyph warnings with some TMP fonts (✓ U+2713 is not always included).
        mark.text = "ON";
        mark.fontSize = 22f;
        mark.fontStyle = FontStyles.Bold;
        mark.alignment = TextAlignmentOptions.Center;
        mark.color = new Color(0.15f, 0.50f, 0.24f, 1f);
        if (font != null) mark.font = font;
        toggle.graphic = mark;
    }
}
