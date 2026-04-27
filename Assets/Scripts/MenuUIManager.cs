using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Programmatic main-menu layout and profile chrome. Used by <see cref="RuntimeMenuBuilder"/> so
/// all sizing stays in one place (sketch-aligned column + paired rows + profile panel).
/// </summary>
public static class MenuUIManager
{
    public const float MainMenuAnchorXMin = 0.18f;
    public const float MainMenuAnchorXMax = 0.82f;
    public const int MainMenuRowCount = 8;
    /// <summary>Tight gutter between paired buttons (Custom Match | Select Level).</summary>
    public const float PairColumnGutter = 0.006f;

    /// <summary>Top of the first menu row (normalized Y anchor max).</summary>
    public const float MenuStackTop = 0.65f;

    /// <summary>Bottom of the Quit row — extra padding so Quit stays above device safe areas.</summary>
    public const float MenuStackBottom = 0.15f;

    public const float RowGap = 0.011f;

    /// <summary>
    /// Sleek top-left profile: semi-transparent panel, subtle border, name with credits clearly below.
    /// </summary>
    public static void BuildProfileHeader(Transform root, TMP_FontAsset font)
    {
        GameObject header = new GameObject("ProfileHeader");
        header.transform.SetParent(root, false);
        RectTransform rt = header.AddComponent<RectTransform>();
        // Static profile strip — tall enough for name + single-line credits (no clip).
        rt.anchorMin = new Vector2(0.02f, 0.885f);
        rt.anchorMax = new Vector2(0.20f, 0.985f);
        rt.offsetMin = new Vector2(8f, 6f);
        rt.offsetMax = new Vector2(-6f, -6f);


        string nameText = PlayerProfile.HasUsername ? PlayerProfile.Username : "Set name";
        int credits = SessionManager.Instance != null ? SessionManager.Instance.Credits : 0;

        GameObject nameGo = new GameObject("ProfileName");
        nameGo.transform.SetParent(header.transform, false);
        TextMeshProUGUI nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text = nameText;
        nameTmp.fontSize = 28f;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = new Color(0.96f, 0.97f, 1f, 1f);
        nameTmp.alignment = TextAlignmentOptions.TopLeft;
        if (font != null) nameTmp.font = font;
        RectTransform nameRt = nameTmp.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 0.55f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(12f, 2f);
        nameRt.offsetMax = new Vector2(-12f, -4f);

        GameObject creditsGo = new GameObject("ProfileCredits");
        creditsGo.transform.SetParent(header.transform, false);
        TextMeshProUGUI creditsTmp = creditsGo.AddComponent<TextMeshProUGUI>();
        creditsTmp.text = "PRSIM CREDITS = " + credits.ToString("N0");
        creditsTmp.fontSize = 20f;
        creditsTmp.fontStyle = FontStyles.Bold;
        creditsTmp.color = new Color(0.75f, 0.82f, 0.95f, 0.95f);
        creditsTmp.alignment = TextAlignmentOptions.TopLeft;
        if (font != null) creditsTmp.font = font;
        RectTransform creditsRt = creditsTmp.rectTransform;
        creditsRt.anchorMin = new Vector2(0f, 0f);
        creditsRt.anchorMax = new Vector2(1f, 0.52f);
        creditsRt.offsetMin = new Vector2(12f, 4f);
        creditsRt.offsetMax = new Vector2(-12f, 2f);
    }

    /// <summary>Normalized anchors for a full-width row in the menu stack.</summary>
    public static void GetFullWidthRow(int rowIndex, out Vector2 anchorMin, out Vector2 anchorMax)
    {
        ComputeRowVerticalExtents(rowIndex, out float yMin, out float yMax);
        anchorMin = new Vector2(MainMenuAnchorXMin, yMin);
        anchorMax = new Vector2(MainMenuAnchorXMax, yMax);
    }

    /// <summary>Left / right column anchors for a paired row.</summary>
    public static void GetPairRow(int rowIndex, out Vector2 leftMin, out Vector2 leftMax, out Vector2 rightMin, out Vector2 rightMax)
    {
        ComputeRowVerticalExtents(rowIndex, out float yMin, out float yMax);
        float mid = (MainMenuAnchorXMin + MainMenuAnchorXMax) * 0.5f;
        float gutterHalf = PairColumnGutter * 0.5f;

        leftMin  = new Vector2(MainMenuAnchorXMin, yMin);
        leftMax  = new Vector2(mid - gutterHalf, yMax);
        rightMin = new Vector2(mid + gutterHalf, yMin);
        rightMax = new Vector2(MainMenuAnchorXMax, yMax);
    }

    private static void ComputeRowVerticalExtents(int rowIndex, out float yMin, out float yMax)
    {
        float span = MenuStackTop - MenuStackBottom;
        float rowHeight = (span - (MainMenuRowCount - 1) * RowGap) / MainMenuRowCount;

        float top = MenuStackTop - rowIndex * (rowHeight + RowGap);
        yMax = top;
        yMin = top - rowHeight;
    }
}
