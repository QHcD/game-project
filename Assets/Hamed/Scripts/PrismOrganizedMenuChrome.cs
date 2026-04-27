using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Shared “organized menu” look: dark glass panel, neon outline, centered footer chips (RETURN / RESET).
/// Used by Settings, Options, and main-menu overlays (Store, Challenges).
/// </summary>
public static class PrismOrganizedMenuChrome
{
    public static readonly Color PanelFill = new Color(0.08f, 0.10f, 0.18f, 0.84f);
    public static readonly Color PanelOutlinePrimary = new Color(0.55f, 0.38f, 0.98f, 0.78f);
    public static readonly Color ButtonOutlineBlue = new Color(0.28f, 0.58f, 1f, 0.85f);
    public static readonly Color ButtonOutlinePurple = new Color(0.72f, 0.38f, 0.95f, 0.85f);

    public static void ApplyPanelSurface(Image panelImage, Outline panelOutline)
    {
        if (panelImage != null)
            panelImage.color = PanelFill;
        if (panelOutline != null)
        {
            panelOutline.effectColor = PanelOutlinePrimary;
            panelOutline.effectDistance = new Vector2(3f, -3f);
        }
    }

    /// <summary>Full-width strip along the bottom inside a central panel.</summary>
    public static RectTransform CreateFooterRow(Transform panel, float height = 64f, float bottomInset = 16f, float horizontalInset = 32f)
    {
        GameObject row = new GameObject("OrganizedFooter", typeof(RectTransform));
        row.transform.SetParent(panel, false);
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottomInset);
        rt.sizeDelta = new Vector2(-horizontalInset * 2f, height);

        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 28;
        h.padding = new RectOffset(8, 8, 0, 0);
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        return rt;
    }

    public static Button AddFooterChipButton(Transform footerRow, string label, Color baseBg, Color outlineColor,
        UnityAction onClick, TMP_FontAsset font)
    {
        GameObject go = new GameObject(label + "_Btn", typeof(RectTransform));
        go.transform.SetParent(footerRow, false);

        Image img = go.AddComponent<Image>();
        img.color = baseBg;
        Outline o = go.AddComponent<Outline>();
        o.effectColor = outlineColor;
        o.effectDistance = new Vector2(2f, -2f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.96f, 0.97f, 1f, 1f);
        if (font != null) tmp.font = font;
        tmp.raycastTarget = false;
        Stretch(textGo.GetComponent<RectTransform>());

        tmp.ForceMeshUpdate();
        float w = Mathf.Clamp(tmp.GetPreferredValues().x + 56f, 148f, 520f);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = 54f;
        le.minHeight = 50f;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        MenuButtonHoverEffect hover = go.AddComponent<MenuButtonHoverEffect>();
        hover.label = tmp;
        hover.background = img;
        hover.normalTextColor = tmp.color;
        hover.hoverTextColor = Color.white;
        hover.normalBackgroundColor = baseBg;
        hover.hoverBackgroundColor = new Color(
            Mathf.Min(1f, baseBg.r + 0.14f),
            Mathf.Min(1f, baseBg.g + 0.12f),
            Mathf.Min(1f, baseBg.b + 0.22f), 1f);
        return btn;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
