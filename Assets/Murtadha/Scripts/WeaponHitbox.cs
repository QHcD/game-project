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

    [Tooltip("Max distance from the weapon owner to a valid target. " +
             "Set from EnemyController.attackRadius + buffer. 0 = no limit (player weapons).")]
    public float maxAttackRange = 0f;

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
        // Also include layer 0 (Default) so player prefabs that haven't been
        // explicitly moved to a named layer are still detectable. IDamageable
        // guard in TryDamageCollider prevents false positives on environment.
        mask |= 1 << 0;
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
        // Always merge with the default mask so serialized Inspector values
        // cannot silently exclude layer 0 (Default), where most characters live.
        int mask = meleeVictimMask.value | BuildDefaultMeleeVictimMask().value;
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

        // Phantom-range guard: reject hits where the attacker is physically too
        // far away for the weapon to reach. Prevents mis-computed weapon-tip
        // OverlapSpheres from landing damage across the room.
        if (maxAttackRange > 0f && resolvedOwner != null)
        {
            float dist = Vector3.Distance(
                resolvedOwner.transform.position,
                target.gameObject.transform.position);
            if (dist > maxAttackRange)
                return false;
        }

        int id = target.gameObject.GetInstanceID();
        if (hitThisSwing.Contains(id)) return false;

        // Melee weapons deal flat body damage only — no headshot multipliers.
        int dmg = damage > 0 ? damage : 25;

        // Use the attacker's chest as the occlusion origin — the weapon-tip
        // calculation depends on the weapon's rotation and can land inside the
        // floor or a wall for curved/rotated weapons (sickle, shovel, etc.),
        // which causes every hit to appear "blocked by geometry."
        Vector3 origin = resolvedOwner != null
            ? resolvedOwner.transform.position + Vector3.up * 1.4f
            : GetWeaponTipWorldPosition();
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

        // Per-category hit audio + optional hit-spark VFX. No-ops if the
        // attacker has no WeaponCombatAudio component. Weapon level resolved
        // from the attacker's PlayerController (enemies pass level 0 → falls
        // back to the generic clip/prefab on the player's combat audio).
        int weaponLevel = 0;
        PlayerController pc = resolvedOwner != null ? resolvedOwner.GetComponent<PlayerController>() : null;
        if (pc != null) weaponLevel = pc.GetEquippedWeaponLevel();
        WeaponCombatAudio.PlayHitAt(resolvedOwner, weaponLevel, target.gameObject.transform.position + Vector3.up * 1.0f);

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
