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

    // Remove Update() - let PlayerController handle input and call Attack() directly

    public virtual void Attack()
    {
        if (Time.time < nextAttackTime) return;
        
        nextAttackTime = Time.time + 1f / attackRate;
        
        if (animator != null) animator.SetTrigger("Attack");

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var col in hits)
        {
            Actor actor = col.GetComponent<Actor>();
            if (actor != null)
            {
                actor.TakeDamage(Mathf.RoundToInt(damage));
                ApplySpecialEffect(actor);
                if (hitEffect != null) hitEffect.Play();
            }
        }
    }

    protected virtual void ApplySpecialEffect(Actor actor) { }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
