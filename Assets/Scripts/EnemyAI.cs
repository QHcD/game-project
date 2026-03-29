using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;
    public Animator animator;

    public float detectionRange = 12f;
    public float attackRange = 2f;
    public int damage = 10;
    public float attackCooldown = 1.5f;
    public bool lookAtPlayerWhenAttacking = true;

    private float lastAttackTime;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (player == null)
        {
            GameObject playerObj = GameObject.Find("Player");
            if (playerObj != null)
                player = playerObj.transform;

            if (player == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged.transform;
            }
        }
    }

    private void Update()
    {
        if (player == null || agent == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRange && distance > attackRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else if (distance <= attackRange)
        {
            agent.isStopped = true;

            if (lookAtPlayerWhenAttacking)
            {
                Vector3 lookPos = player.position - transform.position;
                lookPos.y = 0f;

                if (lookPos != Vector3.zero)
                {
                    Quaternion rot = Quaternion.LookRotation(lookPos);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 8f);
                }
            }

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;

                if (animator != null)
                    animator.SetTrigger("Attack");

                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(damage);
            }
        }
        else
        {
            agent.isStopped = true;
        }

        if (animator != null)
            animator.SetFloat("Speed", agent.velocity.magnitude);
    }
}