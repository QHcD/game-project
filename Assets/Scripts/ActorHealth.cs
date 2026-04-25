using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Generic health script for both Player and Enemy.
/// Implements the existing IDamageable interface used across the project.
/// </summary>
public class ActorHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 3f;
    [SerializeField] private Animator animator;
    [SerializeField] private string deathTrigger = "Die";

    public bool IsAlive => currentHealth > 0f;

    private bool _isDead;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void TakeDamage(float damage)
    {
        if (_isDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Abs(damage));

        if (currentHealth <= 0f)
            Die();
    }

    public void ReceiveDamage(int amount, GameObject attackerRoot)
    {
        TakeDamage(amount);
    }

    public void Die()
    {
        if (_isDead)
            return;

        _isDead = true;
        currentHealth = 0f;

        if (animator != null && !string.IsNullOrWhiteSpace(deathTrigger))
            animator.SetTrigger(deathTrigger);

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col != null && !col.isTrigger)
                col.enabled = false;
        }

        if (CompareTag("Player") && GameManager.Instance != null)
            GameManager.Instance.GameOver();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}
