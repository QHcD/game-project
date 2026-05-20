using UnityEngine;

/// <summary>
/// Scalable melee animation category system.
/// Instead of 16 unique per-level animation setups, every weapon maps to one
/// of a small set of categories. The Animator only needs one extra trigger
/// per category (Attack_Light, Attack_Sword, ...). If a category trigger is
/// missing on the controller, the existing generic "Attack" trigger still
/// fires as fallback — see PlayerController.UpdateAnimator.
/// </summary>
public enum WeaponAnimationCategory
{
    Light,    // knife, dagger, small one-handed
    Sword,    // katana, sword, slashing one-handed blades
    Heavy,    // axe, hammer, two-handed crushing
    Blunt,    // baseball bat, pipe, wrench, crowbar
    Polearm,  // spear, long-shaft
    Shield,   // shield bash, defensive
}

public static class WeaponAnimationCategories
{
    public const string GenericAttackTrigger = "Attack";

    /// <summary>
    /// Map level number (1..16) to a melee category. Add new levels here in
    /// one place — the rest of the system (triggers, override clips) flows
    /// automatically from this mapping.
    /// </summary>
    public static WeaponAnimationCategory ForLevel(int level)
    {
        switch (Mathf.Clamp(level, 1, 16))
        {
            case 1:  return WeaponAnimationCategory.Light;    // tactical knife
            case 2:  return WeaponAnimationCategory.Sword;    // katana
            case 3:  return WeaponAnimationCategory.Polearm;  // shovel
            case 4:  return WeaponAnimationCategory.Blunt;    // baseball bat
            case 5:  return WeaponAnimationCategory.Light;    // nunchucks
            case 6:  return WeaponAnimationCategory.Blunt;    // pipe wrench
            case 7:  return WeaponAnimationCategory.Blunt;    // crowbar
            case 8:  return WeaponAnimationCategory.Heavy;    // sledgehammer
            case 9:  return WeaponAnimationCategory.Heavy;    // axe
            case 10: return WeaponAnimationCategory.Polearm;  // spear
            case 11: return WeaponAnimationCategory.Blunt;    // nailed plank
            case 12: return WeaponAnimationCategory.Light;    // hand saw
            case 13: return WeaponAnimationCategory.Light;    // sickle
            case 14: return WeaponAnimationCategory.Heavy;    // morgenstern / mace
            case 15: return WeaponAnimationCategory.Sword;    // L3FT_E blade
            case 16: return WeaponAnimationCategory.Shield;   // riot shield
            default: return WeaponAnimationCategory.Light;
        }
    }

    /// <summary>
    /// Per-category Animator trigger name. Convention: "Attack_&lt;Category&gt;".
    /// These triggers are OPTIONAL — if the controller does not define them
    /// the call no-ops and the generic "Attack" trigger keeps driving combat.
    /// </summary>
    public static string GetAttackTrigger(WeaponAnimationCategory category)
    {
        switch (category)
        {
            case WeaponAnimationCategory.Light:   return "Attack_Light";
            case WeaponAnimationCategory.Sword:   return "Attack_Sword";
            case WeaponAnimationCategory.Heavy:   return "Attack_Heavy";
            case WeaponAnimationCategory.Blunt:   return "Attack_Blunt";
            case WeaponAnimationCategory.Polearm: return "Attack_Polearm";
            case WeaponAnimationCategory.Shield:  return "Attack_Shield";
            default:                              return GenericAttackTrigger;
        }
    }

    /// <summary>
    /// Optional AnimatorOverrideController clip key used by future override-
    /// based wiring. The Animator Controller author can place one state named
    /// "MeleeAttack" with a placeholder clip, then runtime can swap the clip
    /// per category via AnimatorOverrideController["MeleeAttack_&lt;Cat&gt;"].
    /// </summary>
    public static string GetOverrideClipKey(WeaponAnimationCategory category)
    {
        return "MeleeAttack_" + category;
    }
}
