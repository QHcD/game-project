using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public TextMeshProUGUI label;
    public Image background;
    public Color normalTextColor = new Color(0.94f, 0.94f, 1f, 1f);
    public Color hoverTextColor = Color.white;
    public Color normalBackgroundColor = new Color(1f, 1f, 1f, 0f);
    public Color hoverBackgroundColor = new Color(0.58f, 0.24f, 0.88f, 0.18f);
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1f);
    public Vector3 pressedScale = new Vector3(0.98f, 0.98f, 1f);
    public float animationSpeed = 10f;

    /// <summary>Optional menu rim glow; when set, outline color animates with hover.</summary>
    public Outline neonOutline;
    public Color normalOutlineColor = new Color(0.45f, 0.78f, 1f, 0.65f);
    public Color hoverOutlineColor = new Color(0.78f, 0.96f, 1f, 1f);

    bool isHovered;
    bool isPressed;
    /// <summary>When true, menu keyboard navigation keeps this item visually highlighted.</summary>
    public bool KeyboardFocus { get; set; }

    void Awake()
    {
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>();
        if (background == null)
            background = GetComponent<Image>();

        transform.localScale = normalScale;
        ApplyState(true);
    }

    void Update()
    {
        ApplyState(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    void ApplyState(bool instant)
    {
        bool highlight = isHovered || KeyboardFocus;
        Vector3 targetScale = isPressed ? pressedScale : highlight ? hoverScale : normalScale;
        Color targetText = highlight ? hoverTextColor : normalTextColor;
        Color targetBackground = highlight ? hoverBackgroundColor : normalBackgroundColor;

        float step = instant ? 1f : animationSpeed * Time.unscaledDeltaTime;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, step);

        if (label != null)
            label.color = Color.Lerp(label.color, targetText, step);

        if (background != null)
            background.color = Color.Lerp(background.color, targetBackground, step);

        if (neonOutline != null)
        {
            Color targetOutline = highlight ? hoverOutlineColor : normalOutlineColor;
            neonOutline.effectColor = Color.Lerp(neonOutline.effectColor, targetOutline, step);
        }
    }
}
