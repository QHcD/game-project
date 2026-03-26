using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int currentLevel = 1;
    public int score = 0;
    public int enemiesRemaining = 0;
    public string difficulty = "Normal";

    // Weapon names per level
    public static string[] weaponNames = {
        "Combat Knife", "Katana", "Shovel", "Baseball Bat", "Nunchucks",
        "Brass Knuckles", "Wrench", "Iron Jim", "Hammer", "Axe",
        "Boxing Gloves", "Electric Stick", "Saw", "Sickle", "Golden Spork",
        "Bazooka", "RPG", "Flamethrower", "Minigun", "Sniper Rifle"
    };

    void Awake()
    {
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

    public int GetEnemyCount()
    {
        switch (difficulty)
        {
            case "Easy": return 5;
            case "Hard": return 15;
            default: return 10; // Normal
        }
    }

    public float GetEnemySpeed()
    {
        switch (difficulty)
        {
            case "Easy": return 2f;
            case "Hard": return 5f;
            default: return 3.5f; // Normal
        }
    }

    public float GetEnemyDamage()
    {
        switch (difficulty)
        {
            case "Easy": return 10f;
            case "Hard": return 40f;
            default: return 25f; // Normal
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
        if (enemiesRemaining <= 0)
            LevelComplete();
    }

    void LevelComplete()
    {
        int unlocked = PlayerPrefs.GetInt("UnlockedLevels", 1);
        if (currentLevel >= unlocked)
            PlayerPrefs.SetInt("UnlockedLevels", currentLevel + 1);

        SceneManager.LoadScene("LevelComplete");
    }

    public void LoadNextLevel()
    {
        currentLevel++;
        if (currentLevel > 20)
            SceneManager.LoadScene("Victory");
        else
            SceneManager.LoadScene("Level_" + currentLevel.ToString("D2"));
    }

    public void GameOver()
    {
        SceneManager.LoadScene("GameOver");
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
