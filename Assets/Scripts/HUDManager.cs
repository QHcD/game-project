using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI enemiesRemainingText;
    public TextMeshProUGUI levelText;
    public Slider healthBar;
    public PlayerController player;

    void Update()
    {
        if (GameManager.Instance == null) return;

        // Score
        if (scoreText != null)
            scoreText.text = "SCORE: " + GameManager.Instance.score;

        // Weapon name
        int level = GameManager.Instance.currentLevel;
        if (weaponNameText != null && level >= 1 && level <= 20)
            weaponNameText.text = GameManager.weaponNames[level - 1].ToUpper();

        // Enemies remaining
        if (enemiesRemainingText != null)
            enemiesRemainingText.text = "ENEMIES: " + GameManager.Instance.enemiesRemaining;

        // Level
        if (levelText != null)
            levelText.text = "LEVEL " + level + " / 20";

        // Health bar
        if (healthBar != null && player != null)
            healthBar.value = player.GetHealthPercent();
    }
}
