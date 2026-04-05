using UnityEngine;

/// <summary>
/// Absorbs AnimationEvents embedded in third-party melee clips (DragonSouls / Explosive RPG pack).
/// PRISM-7 applies damage via <see cref="PlayerController"/> ray/overlap, not hitbox toggles or these SFX hooks.
/// Attach on the same GameObject as the humanoid <see cref="Animator"/> that plays the clips.
/// </summary>
[DisallowMultipleComponent]
public class MeleeAnimationEventSink : MonoBehaviour
{
    public void DisableUnarmedHitboxes() { }

    public void EnableRightUnarmedHitboxes() { }

    public void EnableLeftUnarmedHitbox() { }

    public void PlayRandomRunStepSFX() { }

    public void PlayFootStepSFX() { }

    public void PlayRandomWalkStepSFX() { }

    public void PlayGetHitSFX() { }
}
