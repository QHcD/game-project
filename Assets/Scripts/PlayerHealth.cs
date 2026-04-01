using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        if (currentHealth <= 0f)
        {
            currentHealth = maxHealth;
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Abs(amount));

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
        }

        if (currentHealth <= 0f)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.playerTookDamage = true;
                GameManager.Instance.GameOver();
            }
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.playerTookDamage = true;
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Abs(amount));

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
        }
    }
}
