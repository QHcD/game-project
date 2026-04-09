using UnityEngine;

/// <summary>
/// Absorbs AnimationEvents embedded in third-party melee clips (DragonSouls / Explosive RPG pack)
/// and forwards hitbox-enable/disable calls to the WeaponHitbox on the equipped weapon.
///
/// Attach on the same GameObject as the humanoid Animator that plays the clips.
/// The WeaponHitbox is found automatically via the PlayerController's equipped weapon.
/// </summary>
[DisallowMultipleComponent]
public class MeleeAnimationEventSink : MonoBehaviour
{
    private WeaponHitbox cachedHitbox;

    private WeaponHitbox FindWeaponHitbox()
    {
        if (cachedHitbox != null) return cachedHitbox;

        // Search up to the PlayerController and find the equipped weapon's hitbox
        PlayerController pc = GetComponentInParent<PlayerController>();
        if (pc != null && pc.equippedWeaponObject != null)
            cachedHitbox = pc.equippedWeaponObject.GetComponentInChildren<WeaponHitbox>(true);

        return cachedHitbox;
    }

    // Called when the weapon cache should be refreshed (e.g. weapon re-equip)
    public void ClearCache() => cachedHitbox = null;

    // ── Animation Event Receivers ──
    //
    // IMPORTANT: All Enable* entry points are intentional NO-OPS.
    //
    // The player's melee damage is routed EXCLUSIVELY through
    // PlayerController.AttackMelee(), which performs a strict, single-target
    // overlap/sweep query against real colliders and damages at most ONE
    // enemy per swing. Enabling the physical trigger hitbox (WeaponHitbox) caused
    // massive AoE "Bluetooth" damage on large weapons (Baseball Bat, Axe)
    // on levels 4/7/9 — its collider swept through multiple enemies at once
    // and one-shot them via overlapping logic.
    //
    // These methods are kept as empty stubs so existing DragonSouls /
    // ExplosiveLLC animation clips that embed these AnimationEvent names
    // (EnableRightUnarmedHitboxes, EnableLeftUnarmedHitbox, etc.) continue
    // to resolve at runtime without throwing "No receiver" warnings —
    // they simply do nothing.

    public void EnableRightUnarmedHitboxes() { /* disabled — see header */ }

    public void DisableUnarmedHitboxes()     { /* disabled — see header */ }

    public void EnableLeftUnarmedHitbox()    { /* disabled — see header */ }

    // Generic enable/disable that can be added to any custom animation clip
    public void EnableWeaponHitbox()         { /* disabled — see header */ }

    public void DisableWeaponHitbox()        { /* disabled — see header */ }

    // ── SFX hooks (kept as no-ops for compatibility) ──

    public void PlayRandomRunStepSFX() { }

    public void PlayFootStepSFX() { }

    public void PlayRandomWalkStepSFX() { }

    public void PlayGetHitSFX() { }
}
