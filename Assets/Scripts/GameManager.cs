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
        UrbanWarzone,
        IndustrialFactory,
        MilitaryBase
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

    public enum WeaponType
    {
        Melee,
        Explosive,
        Flamethrower,
        Automatic,
        Sniper
    }

    private static readonly string[] LevelWeaponNames = {
        "Combat Knife", "Katana", "Shovel", "Baseball Bat", "Nunchucks",
        "Brass Knuckles", "Wrench", "Iron Jim (Crowbar)", "Hammer", "Axe",
        "Boxing Gloves", "Electric Stick", "Saw", "Sickle", "Golden Spork",
        "Bazooka", "RPG", "Flamethrower", "Minigun", "Sniper Rifle"
    };

    private static readonly float[] LevelWeaponDamage = {
        28f, 36f, 42f, 46f, 40f,
        32f, 54f, 58f, 66f, 70f,
        34f, 44f, 52f, 60f, 72f,
        130f, 145f, 24f, 20f, 220f
    };

    private static readonly float[] LevelWeaponRange = {
        2.1f, 2.8f, 2.7f, 2.5f, 2.4f,
        1.9f, 2.6f, 2.7f, 2.6f, 2.8f,
        2.0f, 2.5f, 2.4f, 2.3f, 2.2f,
        72f, 78f, 13f, 110f, 220f
    };

    private static readonly float[] LevelWeaponExplosionRadius = {
        0f, 0f, 0f, 0f, 0f,
        0f, 0f, 0f, 0f, 0f,
        0f, 0f, 0f, 0f, 0f,
        6.5f, 7.5f, 0f, 0f, 0f
    };

    private static readonly WeaponType[] LevelWeaponTypes = {
        WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee,
        WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee,
        WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee,
        WeaponType.Explosive, WeaponType.Explosive, WeaponType.Flamethrower, WeaponType.Automatic, WeaponType.Sniper
    };

    private static readonly Color[] LevelWeaponColors = {
        new Color(0.78f, 0.82f, 0.88f), new Color(0.78f, 0.92f, 1.00f), new Color(0.62f, 0.46f, 0.30f), new Color(0.72f, 0.50f, 0.26f), new Color(0.78f, 0.66f, 0.28f),
        new Color(0.96f, 0.82f, 0.34f), new Color(0.78f, 0.80f, 0.84f), new Color(0.66f, 0.30f, 0.22f), new Color(0.52f, 0.52f, 0.56f), new Color(0.74f, 0.38f, 0.18f),
        new Color(0.92f, 0.30f, 0.26f), new Color(0.30f, 0.86f, 1.00f), new Color(0.90f, 0.72f, 0.26f), new Color(0.74f, 0.88f, 0.34f), new Color(1.00f, 0.86f, 0.28f),
        new Color(1.00f, 0.34f, 0.16f), new Color(1.00f, 0.24f, 0.18f), new Color(1.00f, 0.56f, 0.20f), new Color(0.72f, 0.72f, 0.78f), new Color(0.34f, 0.88f, 1.00f)
    };

    public int currentLevel = 1;
    public int score = 0;
    public int enemiesRemaining = 0;
    public string difficulty = "Normal";
    public bool playerTookDamage = false;
    public float levelTime = 0f;
    public ArenaMap selectedMap = ArenaMap.UrbanWarzone;
    public PerspectiveMode perspectiveMode = PerspectiveMode.ThirdPerson;
    public MovementScheme movementScheme = MovementScheme.Wasd;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            difficulty = PlayerPrefs.GetString("Difficulty", difficulty);
            selectedMap = (ArenaMap)Mathf.Clamp(PlayerPrefs.GetInt("SelectedMap", 0), 0, 2);
            perspectiveMode = (PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", (int)PerspectiveMode.ThirdPerson), 0, 1);
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
        PlayerPrefs.Save(); // FIX: was missing Save()
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
        PlayerPrefs.Save(); // FIX: was missing Save()
        ResetLevelState();
    }

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
        float baseSpeed = currentLevel >= 12 ? 2.7f : currentLevel >= 5 ? 2.25f : 1.9f;
        switch (difficulty)
        {
            case "Easy": return baseSpeed * 0.82f;
            case "Hard": return baseSpeed * 1.18f;
            default: return baseSpeed;
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

    public WeaponType GetWeaponTypeForLevel(int level)
    {
        return LevelWeaponTypes[Mathf.Clamp(level - 1, 0, LevelWeaponTypes.Length - 1)];
    }

    public float GetWeaponExplosionRadiusForLevel(int level)
    {
        return LevelWeaponExplosionRadius[Mathf.Clamp(level - 1, 0, LevelWeaponExplosionRadius.Length - 1)];
    }

    public bool IsHeavyWeaponLevel(int level)
    {
        return Mathf.Clamp(level, 1, TotalLevels) >= 16;
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
            HUDManager.Instance.RegisterKill();
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
        PlayerPrefs.Save(); // FIX: already existed but consolidate here
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
            // FIX: Save score and progress before going to Victory screen
            PlayerPrefs.SetInt("ContinueLevel", TotalLevels);
            PlayerPrefs.Save();
            PendingMenuScreen = MenuScreen.Victory;
            SceneManager.LoadScene("MainMenu");
            return;
        }

        // FIX: Save the new level before loading
        PlayerPrefs.SetInt("ContinueLevel", currentLevel);
        PlayerPrefs.Save();
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
