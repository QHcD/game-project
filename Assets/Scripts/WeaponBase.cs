using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponBase : MonoBehaviour
{
    public string weaponName = "Weapon";
    public float damage = 25f;
    public float attackRate = 1f;
    public float attackRange = 2f;
    public bool isRanged = false;
    public ParticleSystem hitEffect;

    protected float nextAttackTime = 0f;
    protected Animator animator;

    void Start()
    {
        animator = GetComponentInParent<Animator>();
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + 1f / attackRate;
            Attack();
        }
    }

    protected virtual void Attack()
    {
        if (animator != null) animator.SetTrigger("Attack");

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var col in hits)
        {
            EnemyController enemy = col.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                ApplySpecialEffect(enemy);
                if (hitEffect != null) hitEffect.Play();
            }
        }
    }

    protected virtual void ApplySpecialEffect(EnemyController enemy) { }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}