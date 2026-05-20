using UnityEngine;

/// <summary>
/// Per-enemy personality. Derived once at Awake from the equipped weapon's
/// <see cref="WeaponAnimationCategory"/> with a small randomized variance so
/// no two enemies behave identically.
///
/// Other AI components (EnemyTacticalBrain, EnemyTacticalActions) read these
/// values to bias decisions. Pure data + a single derivation method — no
/// runtime allocations, no per-frame cost.
/// </summary>
[DisallowMultipleComponent]
public class EnemyPersonality : MonoBehaviour
{
    [Header("Resolved at spawn from weapon category (read-only at runtime)")]
    [Range(0f, 1f)] public float aggression  = 0.55f;   // chance to commit / press
    [Range(0f, 1f)] public float bravery     = 0.55f;   // resists retreat at low HP
    [Range(0f, 1f)] public float patience    = 0.45f;   // wait-and-punish vs rush
    [Range(0f, 1f)] public float mobility    = 0.55f;   // strafe / dash frequency
    [Range(0f, 1f)] public float discipline  = 0.55f;   // spacing accuracy
    [Range(0f, 1f)] public float reactionSpeed = 0.55f; // 0 = sluggish, 1 = twitchy

    [Tooltip("Preferred melee engage distance (m). Set from weapon category.")]
    public float preferredEngageDistance = 1.8f;
    [Tooltip("Preferred separation from same-target allies. Reduces stacking.")]
    public float preferredAllySpacing = 2.2f;

    [Tooltip("Resolved category — convenient cache so brains don't recompute.")]
    public WeaponAnimationCategory category = WeaponAnimationCategory.Sword;

    [Header("Variance")]
    [Tooltip("Each scalar trait is jittered by ± this fraction at Awake.")]
    [Range(0f, 0.4f)] public float traitVariance = 0.15f;

    private bool _initialized;

    private void Awake()
    {
        if (_initialized) return;
        _initialized = true;
        ApplyForLevel(ResolveLevel());
        Jitter();
    }

    private int ResolveLevel()
    {
        if (GameManager.Instance != null) return GameManager.Instance.currentLevel;
        return 1;
    }

    /// <summary>Re-derives traits from a specific weapon level. Call manually
    /// only when the enemy's weapon changes mid-match.</summary>
    public void ApplyForLevel(int level)
    {
        category = WeaponAnimationCategories.ForLevel(level);
        switch (category)
        {
            case WeaponAnimationCategory.Light:
                aggression = 0.75f; bravery = 0.55f; patience = 0.25f;
                mobility = 0.85f; discipline = 0.55f; reactionSpeed = 0.80f;
                preferredEngageDistance = 1.6f;
                break;
            case WeaponAnimationCategory.Sword:
                aggression = 0.65f; bravery = 0.65f; patience = 0.45f;
                mobility = 0.70f; discipline = 0.70f; reactionSpeed = 0.70f;
                preferredEngageDistance = 1.9f;
                break;
            case WeaponAnimationCategory.Heavy:
                aggression = 0.55f; bravery = 0.80f; patience = 0.65f;
                mobility = 0.35f; discipline = 0.65f; reactionSpeed = 0.45f;
                preferredEngageDistance = 2.1f;
                break;
            case WeaponAnimationCategory.Blunt:
                aggression = 0.70f; bravery = 0.70f; patience = 0.40f;
                mobility = 0.50f; discipline = 0.55f; reactionSpeed = 0.55f;
                preferredEngageDistance = 2.0f;
                break;
            case WeaponAnimationCategory.Polearm:
                aggression = 0.45f; bravery = 0.55f; patience = 0.70f;
                mobility = 0.55f; discipline = 0.85f; reactionSpeed = 0.55f;
                preferredEngageDistance = 2.8f;   // thrust reach
                break;
            case WeaponAnimationCategory.Shield:
                aggression = 0.40f; bravery = 0.90f; patience = 0.75f;
                mobility = 0.45f; discipline = 0.80f; reactionSpeed = 0.55f;
                preferredEngageDistance = 1.7f;
                break;
        }
    }

    private void Jitter()
    {
        aggression     = Jit(aggression);
        bravery        = Jit(bravery);
        patience       = Jit(patience);
        mobility       = Jit(mobility);
        discipline     = Jit(discipline);
        reactionSpeed  = Jit(reactionSpeed);
        preferredEngageDistance *= 1f + Random.Range(-traitVariance * 0.5f, traitVariance * 0.5f);
        preferredAllySpacing    *= 1f + Random.Range(-traitVariance * 0.5f, traitVariance * 0.5f);
    }

    private float Jit(float v) => Mathf.Clamp01(v + Random.Range(-traitVariance, traitVariance));
}
