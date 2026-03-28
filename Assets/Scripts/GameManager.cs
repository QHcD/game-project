using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int currentLevel = 1;
    public int score = 0;
    public int enemiesRemaining = 0;
    public string difficulty = "Normal";

    public float levelTime = 0f;

    // Weapon progression for the 20 levels
    public static string[] weaponNames = {
        "Combat Knife", "Katana", "Shovel", "Baseball Bat", "Nunchucks",
        "Brass Knuckles", "Wrench", "Iron Jim", "Hammer", "Axe",
        "Boxing Gloves", "Electric Stick", "Saw", "Sickle", "Golden Spork",
        "Bazooka", "RPG", "Flamethrower", "Minigun", "Sniper Rifle"
    };

    void Awake()
    {
        // Singleton pattern to persist across scenes
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetDifficulty(string diff)
    {
        difficulty = diff;
        PlayerPrefs.SetString("Difficulty", diff);
    }

    // Difficulty scaling based on Proposal specs
    public int GetEnemyCount()
    {
        switch (difficulty)
        {
            case "Easy": return 5;
            case "Hard": return 15;
            default: return 10;
        }
    }

    public float GetEnemySpeed()
    {
        switch (difficulty)
        {
            case "Easy": return 2f;
            case "Hard": return 5f;
            default: return 3.5f;
        }
    }

    public float GetEnemyDamage()
    {
        switch (difficulty)
        {
            case "Easy": return 10f;
            case "Hard": return 40f;
            default: return 25f;
        }
    }

    public void AddScore(int points)
    {
        score += points;
    }

    public void EnemyKilled()
    {
        enemiesRemaining--;
        AddScore(100);

        // Update HUD if it exists
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateEnemyCount(enemiesRemaining);
            HUDManager.Instance.UpdateScore(score);
        }

        if (enemiesRemaining <= 0)
            LevelComplete();
    }

    void LevelComplete()
    {
        int unlocked = PlayerPrefs.GetInt("UnlockedLevels", 1);
        if (currentLevel >= unlocked)
            PlayerPrefs.SetInt("UnlockedLevels", currentLevel + 1);

        SceneManager.LoadScene("LevelComplete"); // Loads the Level Complete UI
    }

    public void LoadNextLevel()
    {
        currentLevel++;
        if (currentLevel > 20)
        {
            SceneManager.LoadScene("Victory");
        }
        else
        {
            // Reset timer and reload the main gameplay loop
            levelTime = 0f;
            SceneManager.LoadScene("GameScene");
        }
    }

    public void GameOver()
    {
        SceneManager.LoadScene("GameOver"); // Assume you have a death screen
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void ResetLevelTimer()
    {
        levelTime = 0f;
    }

    // Star rating system based on Proposal conditions
    public int CalculateStars(float timeLimit)
    {
        // Player gets 3 stars if completed within time limit without damage (damage logic can be added later)
        if (levelTime <= timeLimit) return 3;

        // 2 Stars if completed within time limit but took damage
        // 1 Star if exceeded time limit
        if (levelTime > timeLimit) return 1;

        return 2;
    }
}