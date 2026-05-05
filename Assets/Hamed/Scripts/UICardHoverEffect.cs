using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// AAA card hover: smooth scale-up + animated glow border on hover,
/// press punch for click feedback. Set State to drive border colour.
/// Uses unscaled delta so it works in paused menus.
/// </summary>
public class UICardHoverEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public enum CardState { Normal, Owned, Equipped, Locked }

    public Outline glowOutline;
    public CardState state = CardState.Normal;
    public float animSpeed = 13f;

    static readonly Color ColNormal   = new Color(0.22f, 0.28f, 0.42f, 0.50f);
    static readonly Color ColHover    = new Color(0.50f, 0.78f, 1.00f, 1.00f);
    static readonly Color ColEquipped = new Color(1.00f, 0.76f, 0.20f, 1.00f);
    static readonly Color ColLocked   = new Color(0.70f, 0.20f, 0.20f, 0.60f);
    static readonly Color ColOwned    = new Color(0.28f, 0.82f, 1.00f, 0.70f);

    static readonly Vector3 NormalScale  = Vector3.one;
    static readonly Vector3 HoverScale   = new Vector3(1.05f, 1.05f, 1f);
    static readonly Vector3 PressedScale = new Vector3(0.97f, 0.97f, 1f);

    bool _hovered, _pressed;

    void Awake()
    {
        if (glowOutline == null)
            glowOutline = GetComponent<Outline>();
        transform.localScale = NormalScale;
    }

    void Update()
    {
        float t = animSpeed * Time.unscaledDeltaTime;
        Vector3 targetScale = _pressed ? PressedScale : _hovered ? HoverScale : NormalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);

        if (glowOutline != null)
        {
            Color target;
            if (state == CardState.Equipped)       target = ColEquipped;
            else if (state == CardState.Locked)    target = ColLocked;
            else if (state == CardState.Owned)     target = _hovered ? ColHover : ColOwned;
            else                                   target = _hovered ? ColHover : ColNormal;
            glowOutline.effectColor = Color.Lerp(glowOutline.effectColor, target, t);
        }
    }

    public void OnPointerEnter(PointerEventData e) { _hovered = true; }
    public void OnPointerExit(PointerEventData e)  { _hovered = false; _pressed = false; }
    public void OnPointerDown(PointerEventData e)  { _pressed = true; }
    public void OnPointerUp(PointerEventData e)    { _pressed = false; }
}
