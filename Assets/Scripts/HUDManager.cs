using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    public TextMeshProUGUI healthText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI weaponText;
    public TextMeshProUGUI enemyCountText;
    public TextMeshProUGUI timerText;
    public Slider healthBar;

    private float elapsed = 0f;
    private float timeLimit = 120f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (GameManager.Instance == null) return;

        int level = GameManager.Instance.currentLevel;
        if (levelText != null)
            levelText.text = "Level " + level;

        string[] names = GameManager.weaponNames;
        if (weaponText != null && level <= names.Length)
            weaponText.text = names[level - 1];

        UpdateScore(GameManager.Instance.score);
        UpdateEnemyCount(GameManager.Instance.enemiesRemaining);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float remaining = Mathf.Max(0, timeLimit - elapsed);
        if (timerText != null)
            timerText.text = "Time: " + Mathf.CeilToInt(remaining);

        if (GameManager.Instance != null)
            GameManager.Instance.levelTime = elapsed;
    }

    public void UpdateHealth(float current, float max)
    {
        if (healthText != null) healthText.text = "HP: " + Mathf.CeilToInt(current);
        if (healthBar != null) healthBar.value = current / max;
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
    }

    public void UpdateEnemyCount(int count)
    {
        if (enemyCountText != null) enemyCountText.text = "Enemies: " + count;
    }
}