using UnityEngine;

/// <summary>
/// Trigger-based weapon hitbox that deals damage only during the active swing frames.
/// Attach to the weapon model GameObject. A BoxCollider (trigger) is created automatically.
///
/// Activation flow:
///   1. Animation event or PlayerController calls EnableHitbox()
///   2. OnTriggerEnter deals damage to enemies that touch the collider
///   3. Animation event or PlayerController calls DisableHitbox()
///
/// The hitbox is DISABLED by default — enemies take no damage outside the swing window.
/// </summary>
[DisallowMultipleComponent]
public class WeaponHitbox : MonoBehaviour
{
    [Tooltip("Damage dealt per hit. Auto-set from PlayerController if 0.")]
    public int damage = 0;

    [Tooltip("Prevents the same enemy from being hit multiple times in one swing.")]
    private System.Collections.Generic.HashSet<int> hitThisSwing = new System.Collections.Generic.HashSet<int>();

    private BoxCollider hitboxCollider;
    private bool isActive;

    private void Awake()
    {
        // Create or find the trigger collider
        hitboxCollider = GetComponent<BoxCollider>();
        if (hitboxCollider == null)
        {
            hitboxCollider = gameObject.AddComponent<BoxCollider>();
            hitboxCollider.isTrigger = true;
            hitboxCollider.center = Vector3.zero;
            hitboxCollider.size = new Vector3(0.3f, 0.3f, 0.8f);
        }
        else
        {
            hitboxCollider.isTrigger = true;
        }

        // Start disabled — only active during swing
        hitboxCollider.enabled = false;
        isActive = false;
    }

    /// <summary>
    /// Called by animation event or PlayerController at the start of the damage window.
    /// </summary>
    public void EnableHitbox()
    {
        hitThisSwing.Clear();
        isActive = true;
        if (hitboxCollider != null)
            hitboxCollider.enabled = true;
    }

    /// <summary>
    /// Called by animation event or PlayerController at the end of the damage window.
    /// </summary>
    public void DisableHitbox()
    {
        isActive = false;
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
        hitThisSwing.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        int id = other.gameObject.GetInstanceID();
        if (hitThisSwing.Contains(id)) return;

        // Try EnemyController first
        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            int dmg = damage > 0 ? damage : 25;
            enemy.TakeDamage(dmg, byPlayer: true);
            hitThisSwing.Add(id);
            return;
        }

        // Fallback: Actor base class
        Actor actor = other.GetComponentInParent<Actor>();
        if (actor != null && actor.gameObject != transform.root.gameObject)
        {
            int dmg = damage > 0 ? damage : 25;
            actor.TakeDamage(dmg);
            hitThisSwing.Add(id);
        }
    }
}
