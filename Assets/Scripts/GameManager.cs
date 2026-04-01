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

    public enum ArenaMap
    {
        BlacksiteFacility,
        CyberRuinsNeon,
        ContainerPortYard
    }

    public enum PerspectiveMode
    {
        FirstPerson,
        ThirdPerson
    }

    public enum MovementScheme
    {
        Wasd,
        ArrowKeys
    }

    public static GameManager Instance;
    public static MenuScreen PendingMenuScreen { get; private set; } = MenuScreen.MainMenu;

    public const int TotalLevels = 20;

    private static readonly string[] LevelWeaponNames = {
        "Combat Knife", "Katana", "Hammer", "Baseball Bat", "Axe",
        "Wrench", "Tennis Racket", "Shovel", "Crowbar", "Fire Axe",
        "Machete", "Brass Knuckles", "Nunchucks", "Pipe", "Sickle",
        "Golden Spork", "Electric Baton", "Saw Blade", "Battle Hammer", "Champion Blade"
    };

    private static readonly float[] LevelWeaponDamage = {
        32f, 36f, 40f, 42f, 45f,
        48f, 50f, 52f, 54f, 56f,
        58f, 60f, 63f, 66f, 69f,
        72f, 76f, 80f, 86f, 92f
    };

    private static readonly float[] LevelWeaponRange = {
        2.0f, 2.3f, 2.1f, 2.5f, 2.4f,
        2.2f, 2.8f, 2.6f, 2.3f, 2.5f,
        2.6f, 1.9f, 2.7f, 2.3f, 2.4f,
        2.2f, 2.9f, 2.8f, 3.0f, 3.2f
    };

    private static readonly Color[] LevelWeaponColors = {
        new Color(0.8f, 0.85f, 0.9f), new Color(0.75f, 0.85f, 1f), new Color(0.95f, 0.75f, 0.4f), new Color(0.7f, 0.5f, 0.35f), new Color(0.85f, 0.45f, 0.3f),
        new Color(0.7f, 0.8f, 0.9f), new Color(0.95f, 0.95f, 0.95f), new Color(0.85f, 0.7f, 0.45f), new Color(0.6f, 0.65f, 0.75f), new Color(0.95f, 0.4f, 0.25f),
        new Color(0.8f, 0.9f, 0.7f), new Color(1f, 0.85f, 0.35f), new Color(0.7f, 0.9f, 1f), new Color(0.55f, 0.65f, 0.75f), new Color(0.85f, 0.95f, 0.55f),
        new Color(1f, 0.85f, 0.3f), new Color(0.45f, 0.9f, 1f), new Color(0.95f, 0.55f, 0.4f), new Color(1f, 0.7f, 0.3f), new Color(1f, 0.95f, 0.5f)
    };

    public int currentLevel = 1;
    public int score = 0;
    public int enemiesRemaining = 0;
    public string difficulty = "Normal";
    public bool playerTookDamage = false;
    public float levelTime = 0f;
    public ArenaMap selectedMap = ArenaMap.BlacksiteFacility;
    public PerspectiveMode perspectiveMode = PerspectiveMode.FirstPerson;
    public MovementScheme movementScheme = MovementScheme.Wasd;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            difficulty = PlayerPrefs.GetString("Difficulty", difficulty);
            selectedMap = (ArenaMap)Mathf.Clamp(PlayerPrefs.GetInt("SelectedMap", 0), 0, 2);
            perspectiveMode = (PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", 0), 0, 1);
            movementScheme = (MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
            currentLevel = Mathf.Clamp(PlayerPrefs.GetInt("ContinueLevel", currentLevel), 1, TotalLevels);
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

    public void SetSelectedMap(ArenaMap map)
    {
        selectedMap = map;
        PlayerPrefs.SetInt("SelectedMap", (int)map);
        PlayerPrefs.Save();
    }

    public void SetPerspectiveMode(PerspectiveMode mode)
    {
        perspectiveMode = mode;
        PlayerPrefs.SetInt("PerspectiveMode", (int)mode);
        PlayerPrefs.Save();
    }

    public PerspectiveMode GetPerspectiveMode()
    {
        return perspectiveMode;
    }

    public void SetMovementScheme(MovementScheme scheme)
    {
        movementScheme = scheme;
        PlayerPrefs.SetInt("MovementScheme", (int)scheme);
        PlayerPrefs.Save();
    }

    public MovementScheme GetMovementScheme()
    {
        return movementScheme;
    }

    public ArenaMap GetSelectedMap()
    {
        return selectedMap;
    }

    public int GetContinueLevel()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt("ContinueLevel", currentLevel), 1, TotalLevels);
    }

    public void StartRun(int level = 1)
    {
        Time.timeScale = 1f;
        score = 0;
        SetCurrentLevel(level);
        PendingMenuScreen = MenuScreen.MainMenu;
        SceneManager.LoadScene("GameScene");
    }

    public void ReplayCurrentLevel()
    {
        Time.timeScale = 1f;
        ResetLevelState();
        PendingMenuScreen = MenuScreen.MainMenu;
        SceneManager.LoadScene("GameScene");
    }

    public void SetCurrentLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, TotalLevels);
        enemiesRemaining = 0;
        PlayerPrefs.SetInt("ContinueLevel", currentLevel);
        ResetLevelState();
    }

    public int GetEnemyCount()
    {
        switch (difficulty)
        {
            case "Easy": return 10;
            case "Hard": return 12;
            default: return 12;
        }
    }

    public float GetEnemySpeed()
    {
        float baseSpeed = 1.85f + ((currentLevel - 1) * 0.045f);
        switch (difficulty)
        {
            case "Easy": return baseSpeed * 0.88f;
            case "Hard": return baseSpeed * 1.05f;
            default: return baseSpeed;
        }
    }

    public float GetEnemyDamage()
    {
        float baseDamage = 7.5f + ((currentLevel - 1) * 0.45f);
        switch (difficulty)
        {
            case "Easy": return baseDamage * 0.82f;
            case "Hard": return baseDamage * 1.15f;
            default: return baseDamage;
        }
    }

    public string GetWeaponNameForLevel(int level)
    {
        return LevelWeaponNames[Mathf.Clamp(level - 1, 0, LevelWeaponNames.Length - 1)];
    }

    public float GetWeaponDamageForLevel(int level)
    {
        return LevelWeaponDamage[Mathf.Clamp(level - 1, 0, LevelWeaponDamage.Length - 1)];
    }

    public float GetWeaponRangeForLevel(int level)
    {
        return LevelWeaponRange[Mathf.Clamp(level - 1, 0, LevelWeaponRange.Length - 1)];
    }

    public Color GetWeaponColorForLevel(int level)
    {
        return LevelWeaponColors[Mathf.Clamp(level - 1, 0, LevelWeaponColors.Length - 1)];
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
        Time.timeScale = 1f;
        int unlocked = PlayerPrefs.GetInt("UnlockedLevels", 1);
        if (currentLevel >= unlocked)
            PlayerPrefs.SetInt("UnlockedLevels", Mathf.Min(TotalLevels, currentLevel + 1));

        PlayerPrefs.SetInt("ContinueLevel", Mathf.Min(TotalLevels, currentLevel + 1));

        PlayerPrefs.Save();
        PendingMenuScreen = MenuScreen.LevelComplete;
        SceneManager.LoadScene("MainMenu");
    }

    public void LoadNextLevel()
    {
        Time.timeScale = 1f;
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
        SceneManager.LoadScene("GameScene");
    }

    public void GameOver()
    {
        Time.timeScale = 1f;
        PendingMenuScreen = MenuScreen.GameOver;
        SceneManager.LoadScene("MainMenu");
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        PendingMenuScreen = MenuScreen.MainMenu;
        SceneManager.LoadScene("MainMenu");
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
}
