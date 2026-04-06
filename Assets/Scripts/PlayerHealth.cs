using UnityEngine;

/// <summary>
/// Player health with Call of Duty-style auto-regeneration.
/// After taking damage, if the player avoids further damage for 5 seconds,
/// health smoothly regenerates back to 100.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Auto-Regeneration (CoD Style)")]
    [Tooltip("Seconds after last damage before regen begins.")]
    public float regenDelay = 5f;

    [Tooltip("Health points restored per second during regen.")]
    public float regenRate = 15f;

    [Tooltip("Enable/disable auto-regeneration.")]
    public bool autoRegenEnabled = true;

    // ── Internal state ──
    private float timeSinceLastDamage;
    private bool isRegenerating;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        timeSinceLastDamage = regenDelay + 1f; // Start fully healed, no regen needed
    }

    private void Update()
    {
        if (!autoRegenEnabled) return;
        if (currentHealth >= maxHealth) return;
        if (currentHealth <= 0f) return; // Dead, no regen

        timeSinceLastDamage += Time.deltaTime;

        // Start regenerating after delay with no damage
        if (timeSinceLastDamage >= regenDelay)
        {
            if (!isRegenerating)
            {
                isRegenerating = true;
                Debug.Log("[PlayerHealth] Auto-regen started");
            }

            float healAmount = regenRate * Time.deltaTime;
            currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);

            // Update HUD during regen
            if (HUDManager.Instance != null)
                HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);

            // Regen complete
            if (currentHealth >= maxHealth)
            {
                currentHealth = maxHealth;
                isRegenerating = false;
            }
        }
    }

    public void TakeDamage(float amount)
    {
        float absorbed = Mathf.Abs(amount);
        currentHealth = Mathf.Max(0f, currentHealth - absorbed);

        // Reset regen timer — must wait another 5 seconds
        timeSinceLastDamage = 0f;
        isRegenerating = false;

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
            HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
    }
}
