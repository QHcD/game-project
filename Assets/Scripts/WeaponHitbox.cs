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

    [Tooltip("Radius checked around the weapon tip during each active swing.")]
    public float overlapRadius = 0.85f;

    [Tooltip("Prevents the same enemy from being hit multiple times in one swing.")]
    private System.Collections.Generic.HashSet<int> hitThisSwing = new System.Collections.Generic.HashSet<int>();

    private static readonly Collider[] OverlapHits = new Collider[32];

    private BoxCollider hitboxCollider;
    private bool isActive;
    private GameObject ownerRoot;

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
        ownerRoot = ResolveOwnerRoot();
    }

    /// <summary>
    /// Called by animation event or PlayerController at the start of the damage window.
    /// </summary>
    public void EnableHitbox()
    {
        ownerRoot = ResolveOwnerRoot();
        hitThisSwing.Clear();
        isActive = true;
        if (hitboxCollider != null)
            hitboxCollider.enabled = true;
        DealOverlapSphereDamage();
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

    private void Update()
    {
        if (!isActive)
            return;

        DealOverlapSphereDamage();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (hitboxCollider == null || !hitboxCollider.enabled) return;

        TryDamageCollider(other);
    }

    private void DealOverlapSphereDamage()
    {
        Vector3 tip = GetWeaponTipWorldPosition();
        int hitCount = Physics.OverlapSphereNonAlloc(
            tip,
            Mathf.Max(0.1f, overlapRadius),
            OverlapHits,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
            TryDamageCollider(OverlapHits[i]);
    }

    private bool TryDamageCollider(Collider other)
    {
        if (other == null)
            return false;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        GameObject resolvedOwner = ownerRoot != null ? ownerRoot : ResolveOwnerRoot();
        if (target == null || target.gameObject == resolvedOwner) return false;
        if (!target.IsAlive) return false;

        int id = target.gameObject.GetInstanceID();
        if (hitThisSwing.Contains(id)) return false;

        int dmg = damage > 0 ? damage : 25;
        target.ReceiveDamage(dmg, resolvedOwner);
        hitThisSwing.Add(id);
        return true;
    }

    private Vector3 GetWeaponTipWorldPosition()
    {
        if (hitboxCollider == null)
            return transform.position;

        Vector3 localTip = hitboxCollider.center + Vector3.forward * (hitboxCollider.size.z * 0.5f);
        return hitboxCollider.transform.TransformPoint(localTip);
    }

    private GameObject ResolveOwnerRoot()
    {
        PlayerHealth player = GetComponentInParent<PlayerHealth>();
        if (player != null) return player.gameObject;

        PlayerController playerController = GetComponentInParent<PlayerController>();
        if (playerController != null) return playerController.gameObject;

        EnemyController enemy = GetComponentInParent<EnemyController>();
        if (enemy != null) return enemy.gameObject;

        IDamageable damageable = GetComponentInParent<IDamageable>();
        if (damageable != null) return damageable.gameObject;

        return transform.root != null ? transform.root.gameObject : gameObject;
    }

}
