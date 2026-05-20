using UnityEngine;

/// <summary>
/// Trigger-based melee damage for Level 9.
///
/// Works ALONGSIDE the existing WeaponHitbox (OverlapSphere) system.
/// Both can live on the same weapon — WeaponDamage acts as a safety net
/// for the cases where the OverlapSphere misses (too fast a swing, bad frame timing).
///
/// HOW TO USE
/// ----------
/// 1. Attach to the weapon prefab root (same GameObject as WeaponHitbox).
/// 2. The weapon needs a TRIGGER Collider (BoxCollider with Is Trigger = true).
///    If one is missing, this script creates a default box trigger automatically.
/// 3. The weapon needs a Rigidbody (kinematic = true) so Unity fires OnTriggerEnter.
///    Without a Rigidbody on EITHER the weapon or the target, triggers don't fire.
/// 4. Call EnableDamage() from an Animation Event at the START of the attack swing.
/// 5. Call DisableDamage() from an Animation Event at the END of the swing.
///    If you have no animation events wired yet, use the auto-reset timer below.
///
/// TAG RULE (strict):
///   Player weapon  (owner tag == "Player") → only damages "Enemy"
///   Enemy  weapon  (owner tag == "Enemy")  → only damages "Player"
///
/// SAFETY:
///   hasDealtDamage = true after the FIRST valid hit per swing window.
///   This prevents one swing animation from dealing damage on every overlap frame.
///   Reset via EnableDamage() at swing start.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WeaponDamage : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("HP removed per hit.")]
    [SerializeField] private float damage = 25f;

    [Header("Auto-Reset (fallback if no animation events)")]
    [Tooltip("If > 0, the damage window is automatically closed after this many seconds. " +
             "Set to 0 when you have animation events wired instead.")]
    [SerializeField] private float autoDisableAfterSeconds = 0.4f;

    // ── Runtime state ────────────────────────────────────────────────────────

    /// <summary>
    /// True after the first valid hit this swing.
    /// Prevents multi-frame damage stacking from a single animation.
    /// Reset automatically when EnableDamage() is called.
    /// </summary>
    [HideInInspector] public bool hasDealtDamage;

    private bool _damageWindowOpen;
    private float _autoDisableTimer;
    private string _ownerTag;          // "Player" or "Enemy"
    private string _targetTag;         // opposite of ownerTag
    private Collider _triggerCollider;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;

        // Rigidbody is required for OnTriggerEnter to fire on a moving weapon.
        // Use kinematic so physics doesn't take over the hand-attached object.
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        ResolveOwnerTag();
        _damageWindowOpen = false;
    }

    private void Update()
    {
        if (!_damageWindowOpen || autoDisableAfterSeconds <= 0f) return;

        _autoDisableTimer -= Time.deltaTime;
        if (_autoDisableTimer <= 0f)
            DisableDamage();
    }

    // ── Animation Event hooks ────────────────────────────────────────────────

    /// <summary>
    /// Call this from an Animation Event at the START of a melee swing.
    /// Resets hasDealtDamage so a fresh hit can land.
    /// </summary>
    public void EnableDamage()
    {
        ResolveOwnerTag();
        hasDealtDamage    = false;
        _damageWindowOpen = true;
        _autoDisableTimer = autoDisableAfterSeconds;
    }

    /// <summary>
    /// Call this from an Animation Event at the END of a melee swing.
    /// </summary>
    public void DisableDamage()
    {
        _damageWindowOpen = false;
        hasDealtDamage    = false;
    }

    // ── Trigger detection ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!_damageWindowOpen) return;
        if (hasDealtDamage)     return;   // one damage event per swing

        // Tag filter: player weapon must only hit enemies and vice versa.
        if (string.IsNullOrEmpty(_targetTag))
            ResolveOwnerTag();

        if (!string.IsNullOrEmpty(_targetTag) && !other.CompareTag(_targetTag))
        {
            // Check root as well — colliders on child bones inherit no tag.
            if (!other.transform.root.CompareTag(_targetTag))
                return;
        }

        // Prefer IDamageable (works with Health, ActorHealth, PlayerHealth, EnemyController).
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            if (!target.IsAlive) return;
            // Prevent self-damage: walk up to the owner root and compare.
            if (target.gameObject == transform.root.gameObject) return;

            target.ReceiveDamage((int)damage, transform.root.gameObject);
            hasDealtDamage = true;
            Debug.Log($"[WeaponDamage] {transform.root.name} hit {target.gameObject.name} for {damage} dmg.");
            return;
        }

        // Fallback: try the simpler Health.TakeDamage directly (no interface needed).
        Health health = other.GetComponentInParent<Health>();
        if (health != null && health.IsAlive)
        {
            health.TakeDamage(damage);
            hasDealtDamage = true;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ResolveOwnerTag()
    {
        // Walk up to the root (hand bone → character skeleton → character root).
        string rootTag = transform.root.tag;

        if (rootTag == "Player")
        {
            _ownerTag  = "Player";
            _targetTag = "Enemy";
        }
        else if (rootTag == "Enemy")
        {
            _ownerTag  = "Enemy";
            _targetTag = "Player";
        }
        else
        {
            // Weapon is not yet parented to a tagged root — try again next call.
            _ownerTag  = string.Empty;
            _targetTag = string.Empty;
        }
    }
}
