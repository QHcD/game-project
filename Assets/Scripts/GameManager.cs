using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum MenuScreen
    {
        MainMenu,
        LevelComplete,
        GameOver,
        Victory
    }

    public static GameManager Instance;
    public static MenuScreen PendingMenuScreen { get; private set; } = MenuScreen.MainMenu;

    public const int TotalLevels = 20;

    public int currentLevel = 1;
    public int score = 0;
    public int enemiesRemaining = 0;
    public string difficulty = "Normal";
    public bool playerTookDamage = false;
    public float levelTime = 0f;

    public static readonly string[] weaponNames = {
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
            difficulty = PlayerPrefs.GetString("Difficulty", difficulty);
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

    public void StartRun(int level = 1)
    {
        score = 0;
        SetCurrentLevel(level);
        PendingMenuScreen = MenuScreen.MainMenu;
        LoadGameplayScene();
    }

    public void ReplayCurrentLevel()
    {
        ResetLevelState();
        PendingMenuScreen = MenuScreen.MainMenu;
        LoadGameplayScene();
    }

    public void SetCurrentLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, TotalLevels);
        enemiesRemaining = 0;
        ResetLevelState();
    }

    public int GetEnemyCount()
    {
        int baseCount = Mathf.Clamp(4 + currentLevel, 5, 18);
        switch (difficulty)
        {
            case "Easy": return Mathf.Max(4, baseCount - 2);
            case "Hard": return Mathf.Min(22, baseCount + 3);
            default: return baseCount;
        }
    }

    public float GetEnemySpeed()
    {
        float baseSpeed = 2.8f + ((currentLevel - 1) * 0.08f);
        switch (difficulty)
        {
            case "Easy": return baseSpeed * 0.85f;
            case "Hard": return baseSpeed * 1.2f;
            default: return baseSpeed;
        }
    }

    public float GetEnemyDamage()
    {
        float baseDamage = 12f + ((currentLevel - 1) * 1.25f);
        switch (difficulty)
        {
            case "Easy": return baseDamage * 0.75f;
            case "Hard": return baseDamage * 1.25f;
            default: return baseDamage;
        }
    }

    public void AddScore(int points)
    {
        score += points;
    }

    public void EnemyKilled()
    {
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);
        AddScore(100);

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
            PlayerPrefs.SetInt("UnlockedLevels", Mathf.Min(TotalLevels, currentLevel + 1));

        PlayerPrefs.Save();
        PendingMenuScreen = MenuScreen.LevelComplete;
        SceneManager.LoadScene("MainMenu");
    }

    public void LoadNextLevel()
    {
        currentLevel++;
        if (currentLevel > TotalLevels)
        {
            currentLevel = TotalLevels;
            PendingMenuScreen = MenuScreen.Victory;
            SceneManager.LoadScene("MainMenu");
            return;
        }

        ResetLevelState();
        PendingMenuScreen = MenuScreen.MainMenu;
        LoadGameplayScene();
    }

    public void GameOver()
    {
        PendingMenuScreen = MenuScreen.GameOver;
        SceneManager.LoadScene("MainMenu");
    }

    public void GoToMainMenu()
    {
        PendingMenuScreen = MenuScreen.MainMenu;
        SceneManager.LoadScene("MainMenu");
    }

    public void ResetLevelTimer()
    {
        levelTime = 0f;
        playerTookDamage = false;
    }

    public int CalculateStars(float timeLimit)
    {
        if (levelTime <= timeLimit && !playerTookDamage) return 3;
        if (levelTime <= timeLimit) return 2;
        return 1;
    }

    public int GetUnlockedLevelCount()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt("UnlockedLevels", 1), 1, TotalLevels);
    }

    private void ResetLevelState()
    {
        levelTime = 0f;
        playerTookDamage = false;
    }

    private void LoadGameplayScene()
    {
        SceneManager.LoadScene("GameScene");
    }
}
