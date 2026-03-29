using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        HUDManager.Instance?.UpdateHealth(currentHealth, maxHealth);
    }

    public void SetMaxHealth(float newMaxHealth, bool resetCurrentHealth)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = resetCurrentHealth ? maxHealth : Mathf.Min(currentHealth, maxHealth);
        HUDManager.Instance?.UpdateHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (GameManager.Instance != null)
            GameManager.Instance.playerTookDamage = true;

        HUDManager.Instance?.UpdateHealth(currentHealth, maxHealth);
        Debug.Log($"Player took {damage} damage. HP = {currentHealth}");

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        Debug.Log("Player Died");
        GameManager.Instance?.GameOver();
    }
}
