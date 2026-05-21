// test comment
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

    [Tooltip("Strict melee range gate checked at the moment of impact. " +
             "A hit is cancelled if the attacker is farther than this from the target. " +
             "Auto-set from EnemyController.meleeAttackRange for enemy weapons. " +
             "0 = gate disabled (default for player weapons).")]
    public float meleeAttackRange = 0f;

    [Tooltip("Enemy hitbox only. Forward distance (metres) from the enemy root to the overlap sphere centre.\n" +
             "0 = auto-derive from the owner EnemyController.attackRadius so the sphere always reaches\n" +
             "the enemy's full engage distance without needing a manual value per weapon.\n" +
             "Set > 0 to override for a specific weapon/level (e.g. 0.4 to restore legacy behaviour).")]
    public float enemyTipForwardOverride = 0f;

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

        bool isEnemyAttacker = resolvedOwner != null && resolvedOwner.GetComponent<EnemyController>() != null;

        if (target == null)
        {
            if (isEnemyAttacker)
                Debug.Log($"[WeaponHitbox][L9] attacker={resolvedOwner.name} collider={other.name} → no IDamageable found, skipping.");
            return false;
        }

        // Self-hit guard: check the whole hierarchy, not just the root GO.
        // Covers cases where IDamageable (PlayerHealth) lives on a different
        // child object than PlayerController (resolvedOwner), which would make
        // a simple == comparison miss the self-hit and deal self-damage.
        bool isSelf = resolvedOwner != null && (
            target.gameObject == resolvedOwner ||
            target.gameObject.transform.IsChildOf(resolvedOwner.transform) ||
            resolvedOwner.transform.IsChildOf(target.gameObject.transform));

        if (isSelf)
        {
            if (isEnemyAttacker)
                Debug.Log($"[WeaponHitbox][L9] attacker={resolvedOwner.name} → self-hit ignored.");
            return false;
        }

        if (!target.IsAlive)
        {
            if (isEnemyAttacker)
                Debug.Log($"[WeaponHitbox][L9] attacker={resolvedOwner.name} target={target.gameObject.name} → target already dead, skipping.");
            return false;
        }

        // Phantom-range guard: reject hits where the attacker is physically too
        // far away for the weapon to reach. Prevents mis-computed weapon-tip
        // OverlapSpheres from landing damage across the room.
        if (maxAttackRange > 0f && resolvedOwner != null)
        {
            float dist = Vector3.Distance(
                resolvedOwner.transform.position,
                target.gameObject.transform.position);
            if (dist > maxAttackRange)
            {
                if (isEnemyAttacker)
                    Debug.Log($"[WeaponHitbox][L9] attacker={resolvedOwner.name} target={target.gameObject.name} → out of range (dist={dist:F2} max={maxAttackRange:F2}), skipping.");
                return false;
            }
        }

        // Strict melee range gate — checked at impact time using sqrMagnitude for
        // performance. Ensures damage only lands when the attacker is genuinely
        // within melee reach, regardless of how the OverlapSphere was placed.
        if (meleeAttackRange > 0f && resolvedOwner != null)
        {
            float sqrDist  = (target.gameObject.transform.position - resolvedOwner.transform.position).sqrMagnitude;
            float sqrRange = meleeAttackRange * meleeAttackRange;
            if (sqrDist > sqrRange)
            {
                float actualDist = Mathf.Sqrt(sqrDist);
                Debug.Log($"[MeleeGate] HIT_CANCELLED_OUT_OF_RANGE — attacker={resolvedOwner.name} " +
                          $"target={target.gameObject.name} dist={actualDist:F2} meleeAttackRange={meleeAttackRange:F2}");
                return false;
            }
        }

        int id = target.gameObject.GetInstanceID();
        if (hitThisSwing.Contains(id))
        {
            if (isEnemyAttacker)
                Debug.Log($"[WeaponHitbox][L9] attacker={resolvedOwner.name} target={target.gameObject.name} → already hit this swing, skipping.");
            return false;
        }

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

        if (isEnemyAttacker)
            Debug.Log($"[WeaponHitbox][L9] attacker={resolvedOwner.name} target={target.gameObject.name} blockedByWall={blockedWorld} dmg={dmg}");

        if (blockedWorld)
            return false;

        // Log health before damage for diagnostics (covers both PlayerHealth and EnemyController).
        float healthBefore = -1f;
        PlayerHealth ph = target.gameObject.GetComponent<PlayerHealth>();
        EnemyController targetEc = target.gameObject.GetComponent<EnemyController>();
        if (ph != null) healthBefore = ph.currentHealth;
        else if (targetEc != null) healthBefore = targetEc.CurrentHealth;

        target.ReceiveDamage(dmg, resolvedOwner);
        hitThisSwing.Add(id);

        {
            float hitDist = resolvedOwner != null
                ? Vector3.Distance(resolvedOwner.transform.position, target.gameObject.transform.position)
                : -1f;
            float healthAfter = ph != null ? ph.currentHealth
                              : targetEc != null ? (float)targetEc.CurrentHealth
                              : -1f;
            Debug.Log($"[MeleeGate] HIT_LANDED — attacker={resolvedOwner?.name ?? "?"} " +
                      $"target={target.gameObject.name} dist={hitDist:F2} dmg={dmg} " +
                      $"hp {healthBefore:F0}→{healthAfter:F0}");
        }

        // Per-category hit audio + optional hit-spark VFX. No-ops if the
        // attacker has no WeaponCombatAudio component. Weapon level resolved
        // from the attacker's PlayerController or EnemyController.
        int weaponLevel = 0;
        if (resolvedOwner != null)
        {
            PlayerController pc = resolvedOwner.GetComponent<PlayerController>();
            if (pc != null)
            {
                weaponLevel = pc.GetEquippedWeaponLevel();
            }
            else
            {
                EnemyController ec = resolvedOwner.GetComponent<EnemyController>();
                if (ec != null)
                {
                    weaponLevel = ec.GetEquippedWeaponLevel();
                }
            }
        }
        WeaponCombatAudio.PlayHitAt(resolvedOwner, weaponLevel, target.gameObject.transform.position + Vector3.up * 1.0f);

        return true;
    }

    private Vector3 GetWeaponTipWorldPosition()
    {
        if (hitboxCollider == null)
            return transform.position;

        // Enemy weapons: Crosby's bip_hand_R has a different local basis than
        // the player's j_wrist_ri. The axe euler (0,180,90) maps weapon local+Z
        // to handBone -Z on Crosby, so the OverlapSphere ends up BEHIND the
        // enemy. Fix: use the enemy's world-forward as the strike direction so
        // the sphere is always placed in front of whoever is swinging.
        //
        // The forward offset is auto-derived from EnemyController.attackRadius so the
        // sphere always covers the enemy's full engage distance. It can be overridden
        // per-weapon via enemyTipForwardOverride in the Inspector.
        GameObject resolvedOwner = ownerRoot != null ? ownerRoot : ResolveOwnerRoot();
        if (resolvedOwner != null && resolvedOwner.GetComponent<EnemyController>() != null)
        {
            float tipDist;
            if (enemyTipForwardOverride > 0f)
            {
                // Inspector override: use the explicit per-weapon value.
                tipDist = enemyTipForwardOverride;
            }
            else
            {
                // Auto-derive: place the sphere centre at 55 % of the enemy's
                // actual engage distance (attackRadius + attackRangePadding) so
                // the overlapRadius reaches the full edge.  This is read directly
                // from EnemyController so it stays consistent if tuning changes.
                EnemyController ec = resolvedOwner.GetComponent<EnemyController>();
                if (ec != null)
                {
                    float engageDist = ec.attackRadius + ec.attackRangePadding;
                    tipDist = Mathf.Clamp(engageDist * 0.55f, 0.6f, 2.5f);
                }
                else
                {
                    // Fallback if EnemyController reference is lost — use old logic.
                    tipDist = Mathf.Max(0.35f, hitboxCollider.size.z * 0.5f);
                }
            }

            Vector3 tip = resolvedOwner.transform.position
                          + Vector3.up * 1.2f
                          + resolvedOwner.transform.forward * tipDist;
            return tip;
        }

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
