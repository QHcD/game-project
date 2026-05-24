using UnityEngine;

/// <summary>
/// Absorbs third-party AnimationEvents that are present on imported clips but
/// not needed by this project. Attach on the same GameObject as the Animator.
/// </summary>
[DisallowMultipleComponent]
public class AnimationEventSink : MonoBehaviour
{
    public void PlayRandomRunStepSFX() { }

    public void PlayFootStepSFX() { }

    public void PlayRandomWalkStepSFX() { }

    public void PlayGetHitSFX() { }

    public void EnableRightUnarmedHitboxes() { }
    public void DisableUnarmedHitboxes() { }
    public void EnableLeftUnarmedHitboxes() { }
}
