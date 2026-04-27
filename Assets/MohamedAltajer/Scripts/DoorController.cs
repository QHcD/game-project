using System.Collections;
using UnityEngine;

/// <summary>
/// Smoothly swings a door 90° on the Y axis to a permanently-open state.
///
/// Two activation modes (both can be enabled simultaneously):
///   • openOnStart  — door swings open as soon as the scene loads.
///   • Trigger zone — attach a child Collider with isTrigger = true and tag
///                    the player "Player"; entering the trigger opens the door.
///
/// Once open the door stays open. There is no close logic by design — this is
/// a one-shot environmental script for arena entrances and level gates.
/// </summary>
[DisallowMultipleComponent]
public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Swing")]
    [Tooltip("Degrees to rotate around the Y axis. Positive = clockwise from above.")]
    public float openAngle = 90f;

    [Tooltip("Seconds to complete the open swing.")]
    public float openDuration = 0.9f;

    [Tooltip("Eases the swing — start slow, accelerate, decelerate to a stop.")]
    public AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Activation")]
    [Tooltip("If true, the door opens immediately on Start().")]
    public bool openOnStart = true;

    [Tooltip("If true, entering an attached trigger Collider opens the door.")]
    public bool openOnPlayerTrigger = true;

    [Tooltip("Tag the entering object must have. Leave empty to accept any rigidbody.")]
    public string playerTag = "Player";

    [Header("Interaction (Raycast / [E])")]
    [Tooltip("If true, the player can press [E] while looking at this door to toggle it open/closed.")]
    public bool interactiveToggle = true;

    [Tooltip("Prompt label shown by the player's interaction reticle.")]
    public string interactionPrompt = "OPEN DOOR";

    private Quaternion _closedRot;
    private Quaternion _openRot;
    private bool       _hasOpened;
    private bool       _isOpen;
    private Coroutine  _swingRoutine;

    private void Awake()
    {
        _closedRot = transform.localRotation;
        _openRot   = _closedRot * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Start()
    {
        if (openOnStart)
            Open();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!openOnPlayerTrigger || _isOpen) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
        Open();
    }

    /// <summary>
    /// Public entry point — safe to call from other scripts (cutscenes, etc.).
    /// Idempotent: subsequent calls are no-ops once the door has opened.
    /// </summary>
    public void Open()
    {
        if (_isOpen) return;
        _hasOpened = true;
        _isOpen    = true;

        if (_swingRoutine != null) StopCoroutine(_swingRoutine);
        _swingRoutine = StartCoroutine(SwingDoor(_openRot));
    }

    /// <summary>
    /// Swings the door back to its closed rotation. Used by the player
    /// interaction system so [E] toggles the door instead of being a one-way
    /// open. Idempotent: no-ops if the door is already closed.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        if (_swingRoutine != null) StopCoroutine(_swingRoutine);
        _swingRoutine = StartCoroutine(SwingDoor(_closedRot));
    }

    /// <summary>
    /// Toggles the door open/closed. The default action when the player
    /// presses [E] while looking at the door.
    /// </summary>
    public void Toggle()
    {
        if (_isOpen) Close();
        else         Open();
    }

    // ── IInteractable ───────────────────────────────────────────────────────
    string IInteractable.GetPrompt() => interactiveToggle
        ? (_isOpen ? "CLOSE DOOR" : interactionPrompt)
        : string.Empty;
    void   IInteractable.Interact(GameObject by) { if (interactiveToggle) Toggle(); }
    bool   IInteractable.CanInteract           => interactiveToggle;

    private IEnumerator SwingDoor(Quaternion targetRot)
    {
        Quaternion startRot = transform.localRotation;
        float t = 0f;
        float duration = Mathf.Max(0.05f, openDuration);

        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = easing != null ? easing.Evaluate(normalized) : normalized;
            transform.localRotation = Quaternion.Slerp(startRot, targetRot, eased);
            yield return null;
        }

        transform.localRotation = targetRot;
        _swingRoutine = null;
    }
}
