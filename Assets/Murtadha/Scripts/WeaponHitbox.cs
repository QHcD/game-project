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

    private const float MeleeForwardAngle = 80f;

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
        {
            hitboxCollider.enabled = false;
            hitboxCollider.enabled = true;
        }
        ScanWeaponColliderContacts();
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

        ScanWeaponColliderContacts();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        TryDamageCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isActive) return;
        TryDamageCollider(other);
    }

    private void ScanWeaponColliderContacts()
    {
        if (hitboxCollider == null)
            return;

        GameObject resolvedOwner = ownerRoot != null ? ownerRoot : ResolveOwnerRoot();
        Transform attackerRoot = resolvedOwner != null ? resolvedOwner.transform : null;

        Transform t = hitboxCollider.transform;
        Vector3 center = t.TransformPoint(hitboxCollider.center);
        Vector3 lossy = t.lossyScale;
        Vector3 halfExtents = new Vector3(
            Mathf.Abs(hitboxCollider.size.x * lossy.x),
            Mathf.Abs(hitboxCollider.size.y * lossy.y),
            Mathf.Abs(hitboxCollider.size.z * lossy.z)) * 0.5f;

        int mask = BuildQueryMask();
        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, OverlapHits, t.rotation, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = OverlapHits[i];
            if (IsAttackerCollider(col, attackerRoot))
                continue;
            TryDamageCollider(col);
        }
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

        bool isCoopSurvival = MultiplayerMode.IsMultiplayer
            && MultiplayerMode.ActiveMode == MpGameMode.CoopSurvival;
        if (!isCoopSurvival)
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

        if (isEnemyAttacker && IsCoopFriendly(target.gameObject))
            return false;

        if (resolvedOwner != null)
        {
            Vector3 toTarget = target.gameObject.transform.position - resolvedOwner.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector3 attackerForward = resolvedOwner.transform.forward;
                attackerForward.y = 0f;
                if (attackerForward.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(attackerForward.normalized, toTarget.normalized) > MeleeForwardAngle)
                    return false;
            }
        }

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
        Vector3 weaponTip = GetWeaponTipWorldPosition();
        if (DamageOcclusion.IsBlockedFromPoint(resolvedOwner, target.gameObject, weaponTip))
            return NotifyEnemyMeleeMiss(resolvedOwner, true);

        if (isEnemyAttacker && !EnemyStrikePathIsClear(resolvedOwner, target.gameObject, weaponTip))
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

    private static bool IsCoopFriendly(GameObject victim)
    {
        if (!MultiplayerMode.IsMultiplayer || MultiplayerMode.ActiveMode != MpGameMode.CoopSurvival)
            return false;

        return victim != null && victim.GetComponentInParent<EnemyController>() != null;
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
        return hitboxCollider.transform.TransformPoint(localTip);
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

    private bool EnemyStrikePathIsClear(GameObject attacker, GameObject target, Vector3 weaponTip)
    {
        if (attacker == null || target == null)
            return true;

        if (attacker.GetComponent<EnemyController>() == null)
            return true;

        Vector3 attackerChest = attacker.transform.position + Vector3.up * 1.25f;
        Vector3 targetTorso = MeleeBodyTargeting.GetTorsoWorldPoint(target.transform);

        if (DamageOcclusion.IsSegmentBlocked(attacker, target, attackerChest, weaponTip))
            return false;

        if (DamageOcclusion.IsSegmentBlocked(attacker, target, attackerChest, targetTorso))
            return false;

        if (DamageOcclusion.IsSegmentBlocked(attacker, target, weaponTip, targetTorso))
            return false;

        return true;
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
