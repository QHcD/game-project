using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public enum EnemyType { Grunt, Soldier, Elite }
    public EnemyType enemyType = EnemyType.Grunt;

    public float maxHealth = 100f;
    private float currentHealth;
    private float damage;
    private float speed;

    private Transform player;
    private bool isDead = false;
    private bool isStunned = false;
    private float attackCooldown = 1.5f;
    private float nextAttack = 0f;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        currentHealth = maxHealth;
        SetTypeStats();
    }

    void SetTypeStats()
    {
        switch (enemyType)
        {
            case EnemyType.Grunt:
                speed = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() * 0.7f : 2f;
                damage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() * 0.6f : 10f;
                maxHealth = 60f;
                break;
            case EnemyType.Soldier:
                speed = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() : 3.5f;
                damage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() : 25f;
                maxHealth = 100f;
                break;
            case EnemyType.Elite:
                speed = GameManager.Instance != null ? GameManager.Instance.GetEnemySpeed() * 1.4f : 5f;
                damage = GameManager.Instance != null ? GameManager.Instance.GetEnemyDamage() * 1.5f : 40f;
                maxHealth = 160f;
                break;
        }
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead || isStunned || player == null) return;

        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0f;
        transform.position += dir * speed * Time.deltaTime;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= 1.8f && Time.time >= nextAttack)
        {
            nextAttack = Time.time + attackCooldown;
            player.GetComponent<PlayerController>()?.TakeDamage(damage);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        currentHealth -= amount;
        if (currentHealth <= 0) Die();
    }

    public void Stun(float duration)
    {
        if (!isDead) StartCoroutine(StunCoroutine(duration));
    }

    System.Collections.IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    void Die()
    {
        isDead = true;
        GameManager.Instance?.EnemyKilled();
        Destroy(gameObject, 1f);
    }
}
