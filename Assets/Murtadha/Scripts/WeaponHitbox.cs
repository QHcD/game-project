using UnityEngine;

/// <summary>
/// Melee damage during the active swing window uses <see cref="Physics.OverlapSphereNonAlloc"/>
/// at the weapon tip. Triggers are unreliable against <see cref="CharacterController"/>, so the
/// box collider is kept only to define tip offset/size — it stays disabled for physics.
/// </summary>
[DisallowMultipleComponent]
public class WeaponHitbox : MonoBehaviour
{
    [Tooltip("Damage dealt per hit. Auto-set from PlayerController if 0.")]
    public int damage = 0;

    [Tooltip("Radius checked around the weapon tip during each active swing.")]
    public float overlapRadius = 0.85f;

    [Tooltip("Layers that can receive melee damage. Default = Player + Hittable + Character.")]
    public LayerMask meleeVictimMask;

    [Tooltip("Prevents the same enemy from being hit multiple times in one swing.")]
    private System.Collections.Generic.HashSet<int> hitThisSwing = new System.Collections.Generic.HashSet<int>();

    private static readonly Collider[] OverlapHits = new Collider[32];

    private BoxCollider hitboxCollider;
    private bool isActive;
    private GameObject ownerRoot;

    public static LayerMask BuildDefaultMeleeVictimMask()
    {
        int mask = 0;
        TryAdd(ref mask, "Player");
        TryAdd(ref mask, "Hittable");
        TryAdd(ref mask, "Character");
        TryAdd(ref mask, "Enemy");
        TryAdd(ref mask, "Enemies");
        if (mask == 0) return (LayerMask)~0;
        return (LayerMask)mask;
    }

    private static void TryAdd(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) mask |= 1 << layer;
    }

    private void Awake()
    {
        if (meleeVictimMask.value == 0)
            meleeVictimMask = BuildDefaultMeleeVictimMask();

        // Box defines tip position in local space; keep disabled so all hits go through OverlapSphere.
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
            hitboxCollider.enabled = false;
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

        TryDamageCollider(other);
    }

    private void DealOverlapSphereDamage()
    {
        Vector3 tip = GetWeaponTipWorldPosition();
        int mask = meleeVictimMask.value != 0
            ? meleeVictimMask.value
            : BuildDefaultMeleeVictimMask().value;
        int hitCount = Physics.OverlapSphereNonAlloc(
            tip,
            Mathf.Max(0.1f, overlapRadius),
            OverlapHits,
            mask,
            QueryTriggerInteraction.Ignore);

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

        // Melee weapons deal flat body damage only — no headshot multipliers.
        int dmg = damage > 0 ? damage : 25;

        Vector3 origin = GetWeaponTipWorldPosition();
        bool blockedWorld = DamageOcclusion.IsBlockedFromPoint(resolvedOwner, target.gameObject, origin);
        if (CombatDebug.Enabled)
        {
            CombatDebug.Log(
                $"hit target={target.gameObject.name} blockedByWall={blockedWorld}");
        }

        if (blockedWorld)
            return false;

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
