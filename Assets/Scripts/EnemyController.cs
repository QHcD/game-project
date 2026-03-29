using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public enum EnemyType { Grunt, Soldier, Elite }
    public EnemyType enemyType = EnemyType.Grunt;

    public float maxHealth = 100f;
    public float attackRange = 1.55f;
    public float retargetInterval = 0.75f;

    private float currentHealth;
    private float damage;
    private float speed;
    private float nextAttack = 0f;
    private float nextRetarget = 0f;

    private Transform currentTarget;
    private bool isDead = false;
    private bool isStunned = false;
    private readonly float attackCooldown = 1.35f;

    void Start()
    {
        SetTypeStats();
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead || isStunned) return;

        if (Time.time >= nextRetarget || currentTarget == null)
        {
            currentTarget = FindClosestCombatTarget();
            nextRetarget = Time.time + retargetInterval;
        }

        if (currentTarget == null) return;

        Vector3 flatTarget = currentTarget.position;
        flatTarget.y = transform.position.y;

        Vector3 dir = (flatTarget - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, flatTarget);

        if (dist > attackRange)
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);

        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        SnapToGround();

        if (dist <= attackRange && Time.time >= nextAttack)
        {
            nextAttack = Time.time + attackCooldown;
            AttackCurrentTarget();
        }
    }

    void SetTypeStats()
    {
        switch (enemyType)
        {
            case EnemyType.Grunt:
                speed = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() * 0.95f : 2.4f;
                damage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() * 0.85f : 10f;
                maxHealth = 70f;
                break;
            case EnemyType.Soldier:
                speed = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() : 2.8f;
                damage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() : 14f;
                maxHealth = 90f;
                break;
            case EnemyType.Elite:
                speed = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() * 1.08f : 3.1f;
                damage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() * 1.15f : 18f;
                maxHealth = 120f;
                break;
        }
    }

    Transform FindClosestCombatTarget()
    {
        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            ConsiderTarget(player.transform, ref bestTarget, ref bestDistance);

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == this || enemy.isDead) continue;
            ConsiderTarget(enemy.transform, ref bestTarget, ref bestDistance);
        }

        return bestTarget;
    }

    void ConsiderTarget(Transform candidate, ref Transform bestTarget, ref float bestDistance)
    {
        float distance = Vector3.Distance(transform.position, candidate.position);
        if (distance < bestDistance)
        {
            bestDistance = distance;
            bestTarget = candidate;
        }
    }

    void AttackCurrentTarget()
    {
        if (currentTarget == null) return;

        EnemyController enemyTarget = currentTarget.GetComponent<EnemyController>();
        if (enemyTarget != null)
        {
            enemyTarget.TakeDamage(damage);
            return;
        }

        PlayerController player = currentTarget.GetComponent<PlayerController>();
        if (player != null)
            player.TakeDamage(damage);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
            Die();
    }

    public void Stun(float duration)
    {
        if (!isDead) StartCoroutine(StunCoroutine(duration));
    }

    IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    void Die()
    {
        isDead = true;
        GameManager.Instance?.EnemyKilled();
        Destroy(gameObject);
    }

    void SnapToGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.05f, transform.position.z);
    }
}
