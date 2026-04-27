using UnityEngine;

/// <summary>
/// OOD contract for anything that can receive damage (player, enemy, destructible, etc.).
/// Implemented by PlayerHealth and EnemyController so the AI and WeaponHitbox can
/// deal damage without knowing the concrete type.
/// </summary>
public interface IDamageable
{
    /// <summary>The Transform of the damageable entity (used for distance/position checks).</summary>
    Transform transform { get; }

    /// <summary>The root GameObject (used for GetInstanceID deduplication).</summary>
    GameObject gameObject { get; }

    /// <summary>Is this target still alive and valid?</summary>
    bool IsAlive { get; }

    /// <summary>
    /// Deal <paramref name="amount"/> points of damage.
    /// <paramref name="attackerRoot"/> is the root GameObject of whoever dealt the blow
    /// (used to prevent self-damage and to track kill credit).
    /// </summary>
    void ReceiveDamage(int amount, GameObject attackerRoot);
}
