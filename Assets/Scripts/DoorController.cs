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
public class DoorController : MonoBehaviour
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

    private Quaternion _closedRot;
    private Quaternion _openRot;
    private bool       _hasOpened;
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
        if (!openOnPlayerTrigger || _hasOpened) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
        Open();
    }

    /// <summary>
    /// Public entry point — safe to call from other scripts (cutscenes, etc.).
    /// Idempotent: subsequent calls are no-ops once the door has opened.
    /// </summary>
    public void Open()
    {
        if (_hasOpened) return;
        _hasOpened = true;

        if (_swingRoutine != null) StopCoroutine(_swingRoutine);
        _swingRoutine = StartCoroutine(SwingDoor(_openRot));
    }

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
