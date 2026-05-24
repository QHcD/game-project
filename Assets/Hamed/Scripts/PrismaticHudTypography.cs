using TMPro;
using UnityEngine;

public static class PrismaticHudTypography
{
    public static TMP_FontAsset ResolveAzonix()
    {
        TMP_FontAsset azonix = Resources.Load<TMP_FontAsset>("Fonts/Azonix SDF");
        if (azonix != null)
            return azonix;

        TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            string lower = all[i].name.ToLowerInvariant();
            if (lower.Contains("azonix"))
                return all[i];
        }

        return TMP_Settings.defaultFontAsset;
    }

    public static void ApplyLoadingStyle(TextMeshProUGUI label)
    {
        if (label == null) return;
        TMP_FontAsset font = ResolveAzonix();
        if (font != null) label.font = font;
        label.color = new Color(0.98f, 0.99f, 1f, 1f);
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color(0.18f, 0.42f, 0.95f, 1f);
    }

    public static void ApplyCountdownStyle(TextMeshProUGUI label, bool isGo)
    {
        if (label == null) return;
        TMP_FontAsset font = ResolveAzonix();
        if (font != null) label.font = font;
        label.fontStyle = FontStyles.Bold;
        label.color = isGo
            ? new Color(0.40f, 1f, 0.55f, 1f)
            : new Color(0.98f, 0.99f, 1f, 1f);
        label.outlineWidth = 0.24f;
        label.outlineColor = isGo
            ? new Color(0.12f, 0.55f, 0.28f, 1f)
            : new Color(0.18f, 0.42f, 0.95f, 1f);
    }
}
