using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Preferred melee method:
/// Animation Event -> Physics.OverlapSphere exactly on the hit frame.
/// </summary>
public class MeleeOverlapAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackOrigin;

    [Header("Attack")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.1f, 1.1f);
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private string attackTrigger = "Attack";

    private readonly HashSet<int> _hitThisSwing = new HashSet<int>();
    private float _cooldownTimer;
    private bool _attackWindowOpen;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryAttack();
    }

    public void TryAttack()
    {
        if (_cooldownTimer > 0f)
            return;

        _cooldownTimer = attackCooldown;
        _hitThisSwing.Clear();

        if (animator != null && !string.IsNullOrWhiteSpace(attackTrigger))
            animator.SetTrigger(attackTrigger);
    }

    // Call this with an Animation Event at the first active damage frame.
    public void AnimationEvent_BeginAttackWindow()
    {
        _attackWindowOpen = true;
        _hitThisSwing.Clear();
    }

    // Call this with an Animation Event on the exact impact frame.
    public void AnimationEvent_ApplyAttackHit()
    {
        if (!_attackWindowOpen)
            return;

        Vector3 center = attackOrigin != null
            ? attackOrigin.position
            : transform.TransformPoint(localOffset);

        Collider[] hits = Physics.OverlapSphere(center, radius, hitLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive || damageable.gameObject == gameObject)
                continue;

            int uniqueId = damageable.gameObject.GetInstanceID();
            if (_hitThisSwing.Contains(uniqueId))
                continue;

            _hitThisSwing.Add(uniqueId);
            damageable.ReceiveDamage(Mathf.RoundToInt(damage), gameObject);
        }
    }

    // Call this with an Animation Event at the last active damage frame.
    public void AnimationEvent_EndAttackWindow()
    {
        _attackWindowOpen = false;
        _hitThisSwing.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = attackOrigin != null
            ? attackOrigin.position
            : transform.TransformPoint(localOffset);
        Gizmos.DrawWireSphere(center, radius);
    }
}
