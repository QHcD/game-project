using UnityEngine;
using System.Collections;

public class WeaponSpecialEffects : WeaponBase
{
    public enum SpecialType { None, Knockback, Stun, Bleed, FireDoT, AoEExplosion }
    public SpecialType specialType = SpecialType.None;

    public float knockbackForce = 5f;
    public float stunDuration = 2f;
    public float bleedDamage = 5f;
    public float bleedDuration = 3f;
    public float fireDamage = 8f;
    public float fireDuration = 4f;
    public float explosionRadius = 5f;
    public float explosionDamage = 60f;

    protected override void ApplySpecialEffect(EnemyController enemy)
    {
        switch (specialType)
        {
            case SpecialType.Knockback:
                Vector3 dir = (enemy.transform.position - transform.position).normalized;
                enemy.GetComponent<Rigidbody>()?.AddForce(dir * knockbackForce, ForceMode.Impulse);
                break;
            case SpecialType.Stun:
                enemy.Stun(stunDuration);
                break;
            case SpecialType.Bleed:
                StartCoroutine(ApplyDotEffect(enemy, bleedDamage, bleedDuration));
                break;
            case SpecialType.FireDoT:
                StartCoroutine(ApplyDotEffect(enemy, fireDamage, fireDuration));
                break;
            case SpecialType.AoEExplosion:
                Collider[] cols = Physics.OverlapSphere(transform.position, explosionRadius);
                foreach (var c in cols)
                    c.GetComponent<EnemyController>()?.TakeDamage(explosionDamage);
                break;
        }
    }

    IEnumerator ApplyDotEffect(EnemyController enemy, float dmg, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && enemy != null)
        {
            enemy.TakeDamage(dmg * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}