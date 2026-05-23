using UnityEngine;

[DisallowMultipleComponent]
public class WeaponHitbox : MonoBehaviour
{
    public int damage = 0;
    public float overlapRadius = 0.85f;
    public float maxAttackRange = 0f;
    public float meleeAttackRange = 0f;
    public float enemyTipForwardOverride = 0f;
    public LayerMask meleeVictimMask;

    private readonly System.Collections.Generic.HashSet<int> hitThisSwing = new System.Collections.Generic.HashSet<int>();
    private bool _meleeMissNotifiedThisSwing;

    private static readonly Collider[] OverlapHits = new Collider[32];

    private BoxCollider hitboxCollider;
    private bool isActive;
    private GameObject ownerRoot;
    private Transform attackTarget;

    public static LayerMask BuildDefaultMeleeVictimMask()
    {
        int mask = 0;
        TryAdd(ref mask, "Player");
        TryAdd(ref mask, "Hittable");
        TryAdd(ref mask, "Character");
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

    public void SetAttackTarget(Transform target)
    {
        attackTarget = target;
    }

    public void EnableHitbox()
    {
        ownerRoot = ResolveOwnerRoot();
        hitThisSwing.Clear();
        _meleeMissNotifiedThisSwing = false;
        isActive = true;
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
        DealOverlapSphereDamage();
    }

    public void DisableHitbox()
    {
        isActive = false;
        attackTarget = null;
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

    private bool IsEnemyAttacker()
    {
        GameObject resolvedOwner = ownerRoot != null ? ownerRoot : ResolveOwnerRoot();
        return resolvedOwner != null && resolvedOwner.GetComponent<EnemyController>() != null;
    }

    private int BuildQueryMask()
    {
        int mask = meleeVictimMask.value | BuildDefaultMeleeVictimMask().value;
        if (!IsEnemyAttacker())
            return mask;

        int enemiesLayer = LayerMask.NameToLayer("Enemies");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemiesLayer >= 0) mask &= ~(1 << enemiesLayer);
        if (enemyLayer >= 0) mask &= ~(1 << enemyLayer);
        return mask;
    }

    private bool IsAttackerCollider(Collider other, Transform attackerRoot)
    {
        if (other == null || attackerRoot == null)
            return false;

        return other.transform == attackerRoot || other.transform.IsChildOf(attackerRoot);
    }

    private void DealOverlapSphereDamage()
    {
        GameObject resolvedOwner = ownerRoot != null ? ownerRoot : ResolveOwnerRoot();
        Transform attackerRoot = resolvedOwner != null ? resolvedOwner.transform : null;
        bool isEnemyAttacker = IsEnemyAttacker();
        Vector3 tip = GetWeaponTipWorldPosition();
        float probeRadius = Mathf.Max(0.1f, overlapRadius);
        int mask = BuildQueryMask();

        int hitCount = Physics.OverlapSphereNonAlloc(tip, probeRadius, OverlapHits, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = OverlapHits[i];
            if (IsAttackerCollider(col, attackerRoot))
                continue;
            TryDamageCollider(col);
        }

        if (!isEnemyAttacker || attackTarget == null)
            return;

        Vector3 torso = MeleeBodyTargeting.GetTorsoWorldPoint(attackTarget);
        Vector3 mid = Vector3.Lerp(tip, torso, 0.5f);
        hitCount = Physics.OverlapSphereNonAlloc(mid, probeRadius * 0.95f, OverlapHits, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = OverlapHits[i];
            if (IsAttackerCollider(col, attackerRoot))
                continue;
            TryDamageCollider(col);
        }

        if (MeleeBodyTargeting.TryGetBodyCapsule(attackTarget, out Vector3 capA, out Vector3 capB, out float capRadius))
        {
            hitCount = Physics.OverlapCapsuleNonAlloc(
                capA,
                capB,
                capRadius + probeRadius * 0.35f,
                OverlapHits,
                mask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = OverlapHits[i];
                if (IsAttackerCollider(col, attackerRoot))
                    continue;
                TryDamageCollider(col);
            }
        }

        TryEnemyDirectBodyContact(tip, probeRadius);
    }

    private bool TryEnemyDirectBodyContact(Vector3 weaponTip, float probeRadius)
    {
        if (attackTarget == null)
            return false;

        GameObject resolvedOwner = ownerRoot != null ? ownerRoot : ResolveOwnerRoot();
        if (resolvedOwner == null || resolvedOwner.GetComponent<EnemyController>() == null)
            return false;

        PlayerHealth player = attackTarget.GetComponentInParent<PlayerHealth>();
        if (player == null || !player.IsAlive)
            return false;

        int id = player.gameObject.GetInstanceID();
        if (hitThisSwing.Contains(id))
            return false;

        float bodyReach = meleeAttackRange > 0f ? meleeAttackRange : 2f;
        float tipToBody = MeleeBodyTargeting.GetClosestBodyDistance(weaponTip, player.transform);
        if (tipToBody > bodyReach + probeRadius * 0.35f)
            return false;

        if (DamageOcclusion.IsBlockedFromPoint(resolvedOwner, player.gameObject, weaponTip))
            return false;

        int dmg = damage > 0 ? damage : 25;
        player.ReceiveDamage(dmg, resolvedOwner);
        hitThisSwing.Add(id);

        EnemyController ec = resolvedOwner.GetComponent<EnemyController>();
        if (ec != null)
            ec.RegisterMeleeHitLanded();

        WeaponCombatAudio.PlayHitAt(
            resolvedOwner,
            ec != null ? ec.GetEquippedWeaponLevel() : 0,
            MeleeBodyTargeting.GetTorsoWorldPoint(player.transform));

        return true;
    }

    private bool TryDamageCollider(Collider other)
    {
        if (other == null)
            return false;

        GameObject resolvedOwner = ownerRoot != null ? ownerRoot : ResolveOwnerRoot();
        Transform attackerRoot = resolvedOwner != null ? resolvedOwner.transform : null;
        if (IsAttackerCollider(other, attackerRoot))
            return false;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        bool isEnemyAttacker = resolvedOwner != null && resolvedOwner.GetComponent<EnemyController>() != null;

        if (target == null)
            return false;

        bool isSelf = resolvedOwner != null && (
            target.gameObject == resolvedOwner ||
            target.gameObject.transform.IsChildOf(resolvedOwner.transform) ||
            resolvedOwner.transform.IsChildOf(target.gameObject.transform));

        if (isSelf)
            return false;

        if (!target.IsAlive)
            return false;

        if (maxAttackRange > 0f && resolvedOwner != null)
        {
            float reach = GetStrikeReachDistance(resolvedOwner.transform, target.gameObject.transform);
            if (reach > maxAttackRange)
                return NotifyEnemyMeleeMiss(resolvedOwner, false);
        }

        if (meleeAttackRange > 0f && resolvedOwner != null)
        {
            float reach = GetStrikeReachDistance(resolvedOwner.transform, target.gameObject.transform);
            if (reach > meleeAttackRange)
                return NotifyEnemyMeleeMiss(resolvedOwner, true);
        }

        if (!HasMeleeLineOfSight(resolvedOwner, target.gameObject))
            return NotifyEnemyMeleeMiss(resolvedOwner, true);

        int id = target.gameObject.GetInstanceID();
        if (hitThisSwing.Contains(id))
            return false;

        int dmg = damage > 0 ? damage : 25;
        if (DamageOcclusion.IsBlockedFromPoint(resolvedOwner, target.gameObject, GetWeaponTipWorldPosition()))
            return NotifyEnemyMeleeMiss(resolvedOwner, true);

        target.ReceiveDamage(dmg, resolvedOwner);
        hitThisSwing.Add(id);

        if (isEnemyAttacker)
        {
            EnemyController ec = resolvedOwner.GetComponent<EnemyController>();
            if (ec != null)
                ec.RegisterMeleeHitLanded();
        }

        int weaponLevel = 0;
        if (resolvedOwner != null)
        {
            PlayerController pc = resolvedOwner.GetComponent<PlayerController>();
            if (pc != null)
                weaponLevel = pc.GetEquippedWeaponLevel();
            else
            {
                EnemyController ec = resolvedOwner.GetComponent<EnemyController>();
                if (ec != null)
                    weaponLevel = ec.GetEquippedWeaponLevel();
            }
        }

        WeaponCombatAudio.PlayHitAt(resolvedOwner, weaponLevel, target.gameObject.transform.position + Vector3.up * 1.0f);
        return true;
    }

    private float GetStrikeReachDistance(Transform attacker, Transform victim)
    {
        Vector3 weaponTip = GetWeaponTipWorldPosition();
        PlayerHealth player = victim.GetComponentInParent<PlayerHealth>();
        if (player != null)
            return MeleeBodyTargeting.GetClosestBodyDistance(weaponTip, player.transform);

        Vector3 delta = victim.position - attacker.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private Vector3 GetWeaponTipWorldPosition()
    {
        if (hitboxCollider == null)
            return transform.position;

        Vector3 localTip = hitboxCollider.center + Vector3.forward * (hitboxCollider.size.z * 0.5f);
        Vector3 weaponTip = hitboxCollider.transform.TransformPoint(localTip);

        if (IsEnemyAttacker() && attackTarget != null)
        {
            Vector3 torso = MeleeBodyTargeting.GetTorsoWorldPoint(attackTarget);
            Vector3 toTorso = torso - weaponTip;
            if (toTorso.sqrMagnitude > 0.0001f)
            {
                float extend = Mathf.Clamp(toTorso.magnitude * 0.45f, 0.12f, 0.65f);
                weaponTip += toTorso.normalized * extend;
            }
        }

        return weaponTip;
    }

    private bool HasMeleeLineOfSight(GameObject attacker, GameObject target)
    {
        if (attacker == null || target == null)
            return true;

        Vector3 origin = attacker.GetComponent<EnemyController>() != null
            ? GetWeaponTipWorldPosition()
            : attacker.transform.position + Vector3.up * 1.4f;
        return !DamageOcclusion.IsBlockedFromPoint(attacker, target, origin);
    }

    private bool NotifyEnemyMeleeMiss(GameObject resolvedOwner, bool resumeChase)
    {
        if (resolvedOwner == null || resolvedOwner.GetComponent<EnemyController>() == null)
            return false;

        if (resumeChase && !_meleeMissNotifiedThisSwing)
        {
            _meleeMissNotifiedThisSwing = true;
            EnemyController ec = resolvedOwner.GetComponent<EnemyController>();
            if (ec != null)
                ec.NotifyMeleeAttackMissed();
        }

        return false;
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
