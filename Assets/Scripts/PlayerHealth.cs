using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    private float invulnerableTimer;
    public bool IsInvulnerable => invulnerableTimer > 0f;

    // HP regen after not taking damage
    private const float RegenDelay = 3.5f;
    private const float RegenRate = 6f;
    private float timeSinceLastHit;

    public void SetInvulnerable(float duration)
    {
        invulnerableTimer = Mathf.Max(invulnerableTimer, duration);
    }

    private void Update()
    {
        if (invulnerableTimer > 0f) invulnerableTimer -= Time.deltaTime;

        // Passive HP regen
        timeSinceLastHit += Time.deltaTime;
        if (timeSinceLastHit >= RegenDelay && currentHealth > 0f && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + RegenRate * Time.deltaTime);
            if (HUDManager.Instance != null)
                HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
        }
    }

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
        if (IsInvulnerable) return;

        float absorbed = Mathf.Abs(amount);
        currentHealth = Mathf.Max(0f, currentHealth - absorbed);
        timeSinceLastHit = 0f;

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
            HUDManager.Instance.ShowDamageFlash(absorbed);
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
