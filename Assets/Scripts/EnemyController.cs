using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    public float health = 50f;
    public float detectionRange = 15f;
    public Animator animator;

    private NavMeshAgent agent;
    private Transform player;
    private float attackCooldown = 1.5f;
    private float lastAttackTime;
    private float damage;
    private float speed;
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Get values from GameManager
        if (GameManager.Instance != null)
        {
            damage = GameManager.Instance.GetEnemyDamage();
            speed = GameManager.Instance.GetEnemySpeed();
            agent.speed = speed;
        }
        else
        {
            damage = 25f;
            agent.speed = 3.5f;
        }
    }

    void Update()
    {
        if (isDead || player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            // Chase player
            agent.SetDestination(player.position);

            if (animator != null)
                animator.SetBool("IsWalking", true);

            // Attack if close enough
            if (distanceToPlayer <= agent.stoppingDistance + 0.5f)
            {
                AttackPlayer();
            }
        }
        else
        {
            agent.ResetPath();
            if (animator != null)
                animator.SetBool("IsWalking", false);
        }
    }

    void AttackPlayer()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;

            if (animator != null)
                animator.SetTrigger("Attack");

            PlayerController playerCtrl = player.GetComponent<PlayerController>();
            if (playerCtrl != null)
                playerCtrl.TakeDamage(damage);
        }
    }

    public void TakeDamage(float dmg)
    {
        if (isDead) return;
        health -= dmg;

        if (animator != null)
            animator.SetTrigger("Hit");

        if (health <= 0)
            Die();
    }

    void Die()
    {
        isDead = true;

        if (animator != null)
            animator.SetTrigger("Die");

        if (GameManager.Instance != null)
            GameManager.Instance.EnemyKilled();

        agent.enabled = false;
        GetComponent<Collider>().enabled = false;

        Destroy(gameObject, 2f);
    }
}
