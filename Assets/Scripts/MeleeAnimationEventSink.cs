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

    public void EnableRightUnarmedHitboxes()
    {
        WeaponHitbox hb = FindWeaponHitbox();
        if (hb != null) hb.EnableHitbox();
    }

    public void DisableUnarmedHitboxes()
    {
        WeaponHitbox hb = FindWeaponHitbox();
        if (hb != null) hb.DisableHitbox();
    }

    public void EnableLeftUnarmedHitbox()
    {
        WeaponHitbox hb = FindWeaponHitbox();
        if (hb != null) hb.EnableHitbox();
    }

    // Generic enable/disable that can be added to any custom animation clip
    public void EnableWeaponHitbox()
    {
        WeaponHitbox hb = FindWeaponHitbox();
        if (hb != null) hb.EnableHitbox();
    }

    public void DisableWeaponHitbox()
    {
        WeaponHitbox hb = FindWeaponHitbox();
        if (hb != null) hb.DisableHitbox();
    }

    // ── SFX hooks (kept as no-ops for compatibility) ──

    public void PlayRandomRunStepSFX() { }

    public void PlayFootStepSFX() { }

    public void PlayRandomWalkStepSFX() { }

    public void PlayGetHitSFX() { }
}
