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

    protected override void ApplySpecialEffect(Actor actor)
    {
        if (actor == null)
            return;

        switch (specialType)
        {
            case SpecialType.Knockback:
                Vector3 dir = (actor.transform.position - transform.position).normalized;
                actor.GetComponent<Rigidbody>()?.AddForce(dir * knockbackForce, ForceMode.Impulse);
                break;
            case SpecialType.Stun:
                break;
            case SpecialType.Bleed:
                StartCoroutine(ApplyDotEffect(actor, bleedDamage, bleedDuration));
                break;
            case SpecialType.FireDoT:
                StartCoroutine(ApplyDotEffect(actor, fireDamage, fireDuration));
                break;
            case SpecialType.AoEExplosion:
                Collider[] cols = Physics.OverlapSphere(transform.position, explosionRadius);
                foreach (var c in cols)
                    c.GetComponent<Actor>()?.TakeDamage(Mathf.RoundToInt(explosionDamage));
                break;
        }
    }

    IEnumerator ApplyDotEffect(Actor actor, float dmg, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && actor != null)
        {
            actor.TakeDamage(Mathf.CeilToInt(dmg * Time.deltaTime));
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
