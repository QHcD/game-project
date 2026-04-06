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

    public enum WeaponType
    {
        Melee,
        Flamethrower,
        Sniper,
        Explosive,
        UltimateMelee,
        Rifle
    }

    // Levels 1-13: melee progression. Levels 14-20: ranged/special weapons.
    private static readonly string[] LevelWeaponNames = {
        "Tactical Knife", "Katana", "Shovel", "Baseball Bat", "Nunchucks",
        "Wrench", "Crowbar", "Hammer", "Axe", "Spear",
        "Spoon", "Saw", "Sickle", "Minigun", "RPG",
        "Bazooka", "Flamethrower", "M16", "TYR", "Sniper"
    };

    // Damage per hit. Flamethrower/Minigun = per burst ray, Sniper/Explosive = single hit.
    private static readonly float[] LevelWeaponDamage = {
        25f,  30f,  34f,  37f,  40f,
        43f,  46f,  50f,  54f,  56f,
        42f,  58f,  60f,  18f,  100f,
        120f, 22f,  45f,  65f,  200f
    };

    // Attack range. For Explosive = max projectile range.
    private static readonly float[] LevelWeaponRange = {
        2.0f, 2.5f, 2.4f, 2.8f, 2.2f,
        2.3f, 2.5f, 2.6f, 2.5f, 3.2f,
        1.8f, 2.3f, 2.4f, 25f,  60f,
        80f,  8.0f, 50f,  40f,  200f
    };

    // Explosion AoE radius for RPG/Bazooka; 0 = not explosive.
    private static readonly float[] LevelWeaponExplosionRadius = {
        0f, 0f, 0f, 0f, 0f,
        0f, 0f, 0f, 0f, 0f,
        0f, 0f, 0f, 0f, 5.5f,
        7.0f, 0f, 0f, 0f, 0f
    };

    private static readonly WeaponType[] LevelWeaponTypes = {
        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Melee,
        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Melee,
        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Melee,        WeaponType.Flamethrower, WeaponType.Explosive,
        WeaponType.Explosive,    WeaponType.Flamethrower, WeaponType.Rifle,        WeaponType.Rifle,        WeaponType.Sniper
    };

    private static readonly Color[] LevelWeaponColors = {
        new Color(0.80f, 0.85f, 0.90f), new Color(0.85f, 0.30f, 0.30f), new Color(0.85f, 0.70f, 0.45f), new Color(0.85f, 0.60f, 0.35f), new Color(0.70f, 0.90f, 1.00f),
        new Color(0.70f, 0.80f, 0.90f), new Color(0.60f, 0.65f, 0.75f), new Color(0.55f, 0.62f, 0.72f), new Color(0.85f, 0.45f, 0.30f), new Color(0.80f, 0.72f, 0.50f),
        new Color(0.95f, 0.95f, 0.95f), new Color(0.75f, 0.75f, 0.80f), new Color(0.70f, 0.72f, 0.80f), new Color(0.50f, 0.55f, 0.60f), new Color(0.45f, 0.55f, 0.35f),
        new Color(0.45f, 0.55f, 0.35f), new Color(1.00f, 0.50f, 0.15f), new Color(0.30f, 0.30f, 0.30f), new Color(0.60f, 0.60f, 0.65f), new Color(0.30f, 0.90f, 1.00f)
    };

    public int currentLevel = 1;
    public int score = 0;
    public int enemiesRemaining = 0;
    public string difficulty = "Normal";
    public bool playerTookDamage = false;
    public float levelTime = 0f;
    public ArenaMap selectedMap = ArenaMap.BlacksiteFacility;
    public PerspectiveMode perspectiveMode = PerspectiveMode.ThirdPerson;
    public MovementScheme movementScheme = MovementScheme.Wasd;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            difficulty = PlayerPrefs.GetString("Difficulty", difficulty);
            selectedMap = (ArenaMap)Mathf.Clamp(PlayerPrefs.GetInt("SelectedMap", (int)selectedMap), 0, 2);
            perspectiveMode = (PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", (int)perspectiveMode), 0, 1);
            movementScheme = (MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", (int)movementScheme), 0, 1);
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

    public WeaponType GetWeaponTypeForLevel(int level)
    {
        return LevelWeaponTypes[Mathf.Clamp(level - 1, 0, LevelWeaponTypes.Length - 1)];
    }

    public float GetWeaponExplosionRadiusForLevel(int level)
    {
        return LevelWeaponExplosionRadius[Mathf.Clamp(level - 1, 0, LevelWeaponExplosionRadius.Length - 1)];
    }

    public void AddScore(int points)
    {
        score += points;
    }

    public void EnemyKilled(bool byPlayer = false)
    {
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);

        // Only award score and show kill feed when the PLAYER got the kill
        if (byPlayer)
        {
            AddScore(100);
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateEnemyCount(enemiesRemaining);

            if (byPlayer)
            {
                HUDManager.Instance.UpdateScore(score);
                HUDManager.Instance.RegisterKill();
            }
        }

        if (enemiesRemaining <= 0)
            LevelComplete();
    }

    void LevelComplete()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PendingMenuScreen = MenuScreen.GameOver;
        SceneManager.LoadScene("MainMenu");
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
