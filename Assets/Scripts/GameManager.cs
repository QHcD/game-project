using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton game-state manager.
///
/// Fix (Issue #5 — Victory Condition):
///   • Added totalEnemiesSpawned + enemiesKilledThisLevel counters.
///   • InitializeEnemyCount() is called by LevelBuilder AFTER all enemies are
///     placed so the baseline is always correct.
///   • EnemyKilled() now only triggers LevelComplete when the kill count
///     MATCHES the number that were actually spawned (not just when
///     enemiesRemaining hits zero, which could fire prematurely if the
///     counter was never initialised).
/// </summary>
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
        Map1,
        Map2
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

    public const int TotalLevels = 16;

    public enum WeaponType
    {
        Melee,
        UltimateMelee
    }

    // Levels 1-16: all melee progression.
    private static readonly string[] LevelWeaponNames = {
        "Tactical Knife", "Katana", "Shovel", "Baseball Bat", "Nunchucks",
        "Wrench", "Crowbar", "Hammer", "Axe", "Spear",
        "Nailed Plank", "Saw", "Sickle", "Morgenstern", "L3FTE",
        "Riot Shield"
    };

    private static readonly float[] LevelWeaponDamage = {
        25f,  30f,  34f,  37f,  40f,
        43f,  46f,  50f,  54f,  56f,
        58f,  62f,  65f,  70f,  75f,
        85f
    };

    private static readonly float[] LevelWeaponRange = {
        2.0f, 2.5f, 2.4f, 2.8f, 2.2f,
        2.3f, 2.5f, 2.6f, 2.5f, 3.2f,
        2.0f, 2.3f, 2.4f, 2.5f, 2.5f,
        2.8f
    };

    private static readonly float[] LevelWeaponExplosionRadius = {
        0f, 0f, 0f, 0f, 0f,
        0f, 0f, 0f, 0f, 0f,
        0f, 0f, 0f, 0f, 0f,
        0f
    };

    private static readonly WeaponType[] LevelWeaponTypes = {
        WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee,
        WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee,
        WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee, WeaponType.Melee,
        WeaponType.Melee
    };

    private static readonly Color[] LevelWeaponColors = {
        new Color(0.80f, 0.85f, 0.90f), new Color(0.85f, 0.30f, 0.30f), new Color(0.85f, 0.70f, 0.45f), new Color(0.85f, 0.60f, 0.35f), new Color(0.70f, 0.90f, 1.00f),
        new Color(0.70f, 0.80f, 0.90f), new Color(0.60f, 0.65f, 0.75f), new Color(0.55f, 0.62f, 0.72f), new Color(0.85f, 0.45f, 0.30f), new Color(0.80f, 0.72f, 0.50f),
        new Color(0.75f, 0.65f, 0.45f), new Color(0.75f, 0.75f, 0.80f), new Color(0.70f, 0.72f, 0.80f), new Color(0.65f, 0.55f, 0.40f), new Color(0.40f, 0.40f, 0.45f),
        new Color(0.30f, 0.45f, 0.60f)
    };

    // ── Runtime state ────────────────────────────────────────────────────────
    public int   currentLevel        = 1;
    public int   score               = 0;
    public int   enemiesRemaining    = 0;
    public string difficulty         = "Normal";
    public bool  playerTookDamage    = false;
    public float levelTime           = 0f;
    public ArenaMap        selectedMap      = ArenaMap.Map1;
    public PerspectiveMode perspectiveMode  = PerspectiveMode.ThirdPerson;
    public MovementScheme  movementScheme   = MovementScheme.Wasd;

    // ── Enemy tracking for correct victory condition (Issue #5) ─────────────
    /// <summary>How many enemies were spawned this level (set by LevelBuilder).</summary>
    public int totalEnemiesSpawned   = 0;
    /// <summary>How many enemies have been killed this level (all sources).</summary>
    public int enemiesKilledThisLevel = 0;

    // ── Victory delay ────────────────────────────────────────────────────────
    /// <summary>
    /// Seconds to wait after the last enemy dies before loading the next scene.
    /// Gives the player time to watch the final enemy fall. Tweak in Inspector.
    /// </summary>
    [SerializeField] private float victoryDelaySeconds = 2.5f;

    /// <summary>Guards against LevelComplete being triggered more than once per level.</summary>
    private bool _levelCompleteTriggered = false;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            difficulty      = PlayerPrefs.GetString("Difficulty", difficulty);
            selectedMap     = (ArenaMap)Mathf.Clamp(PlayerPrefs.GetInt("SelectedMap", (int)selectedMap), 0, 1);
            perspectiveMode = (PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", (int)perspectiveMode), 0, 1);
            movementScheme  = (MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", (int)movementScheme), 0, 1);
            currentLevel    = Mathf.Clamp(PlayerPrefs.GetInt("ContinueLevel", currentLevel), 1, TotalLevels);
        }
        else
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.Equals("GameScene"))
            return;

        StartCoroutine(RefreshEnemyCountNextFrame());
    }

    private IEnumerator RefreshEnemyCountNextFrame()
    {
        // Let LevelBuilder finish spawning for this frame first.
        yield return null;

        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        if (enemies != null && enemies.Length > 0 && totalEnemiesSpawned <= 0)
            InitializeEnemyCount(enemies.Length);
    }

    // ── Settings setters ─────────────────────────────────────────────────────
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

    public PerspectiveMode GetPerspectiveMode() => perspectiveMode;

    public void SetMovementScheme(MovementScheme scheme)
    {
        movementScheme = scheme;
        PlayerPrefs.SetInt("MovementScheme", (int)scheme);
        PlayerPrefs.Save();
    }

    public MovementScheme GetMovementScheme() => movementScheme;
    public ArenaMap GetSelectedMap() => selectedMap;

    public int GetContinueLevel()
        => Mathf.Clamp(PlayerPrefs.GetInt("ContinueLevel", currentLevel), 1, TotalLevels);

    // ── Game flow ─────────────────────────────────────────────────────────────
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

    // ── Enemy helpers ─────────────────────────────────────────────────────────
    public int GetEnemyCount()
    {
        // The arena should always spawn 12 enemies; difficulty is handled
        // through speed and damage, not by reducing the combatant count.
        return 12;
    }

    public float GetEnemySpeed()
    {
        float baseSpeed = 1.85f + ((currentLevel - 1) * 0.045f);
        switch (difficulty)
        {
            case "Easy": return baseSpeed * 0.88f;
            case "Hard": return baseSpeed * 1.05f;
            default:     return baseSpeed;
        }
    }

    public float GetEnemyDamage()
    {
        float baseDamage = 7.5f + ((currentLevel - 1) * 0.45f);
        switch (difficulty)
        {
            case "Easy": return baseDamage * 0.82f;
            case "Hard": return baseDamage * 1.15f;
            default:     return baseDamage;
        }
    }

    // ── Weapon helpers ────────────────────────────────────────────────────────
    public string  GetWeaponNameForLevel(int level)         => LevelWeaponNames[Mathf.Clamp(level - 1, 0, LevelWeaponNames.Length - 1)];
    public float   GetWeaponDamageForLevel(int level)       => LevelWeaponDamage[Mathf.Clamp(level - 1, 0, LevelWeaponDamage.Length - 1)];
    public float   GetWeaponRangeForLevel(int level)        => LevelWeaponRange[Mathf.Clamp(level - 1, 0, LevelWeaponRange.Length - 1)];
    public Color   GetWeaponColorForLevel(int level)        => LevelWeaponColors[Mathf.Clamp(level - 1, 0, LevelWeaponColors.Length - 1)];
    public WeaponType GetWeaponTypeForLevel(int level)      => LevelWeaponTypes[Mathf.Clamp(level - 1, 0, LevelWeaponTypes.Length - 1)];
    public float   GetWeaponExplosionRadiusForLevel(int level) => LevelWeaponExplosionRadius[Mathf.Clamp(level - 1, 0, LevelWeaponExplosionRadius.Length - 1)];

    // ── Score ────────────────────────────────────────────────────────────────
    public void AddScore(int points) { score += points; }

    // ════════════════════════════════════════════════════════════════════════
    //  ENEMY TRACKING — Issue #5
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by LevelBuilder AFTER all enemies are placed.
    /// Establishes the authoritative baseline for this level so that
    /// EnemyKilled() fires LevelComplete only when the LAST of these
    /// exact enemies is eliminated.
    /// </summary>
    public void InitializeEnemyCount(int count)
    {
        totalEnemiesSpawned    = count;
        enemiesRemaining       = count;
        enemiesKilledThisLevel = 0;

        Debug.Log($"[GameManager] InitializeEnemyCount: {count} enemies registered for this level.");
    }

    /// <summary>
    /// Called by EnemyController.Die().
    /// byPlayer = true  → score + kill-feed update.
    /// Victory fires only when every spawned enemy has been eliminated
    /// (kills >= totalEnemiesSpawned), preventing premature level-complete
    /// if counters were in an uninitialized state.
    /// </summary>
    public void EnemyKilled(bool byPlayer = false)
    {
        enemiesKilledThisLevel++;
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);

        if (byPlayer)
            AddScore(100);

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateEnemyCount(enemiesRemaining);

            if (byPlayer)
            {
                HUDManager.Instance.UpdateScore(score);
                HUDManager.Instance.RegisterKill();
            }
        }

        Debug.Log($"[GameManager] EnemyKilled: {enemiesKilledThisLevel}/{totalEnemiesSpawned} killed, {enemiesRemaining} remaining.");

        // ── Victory condition: LAST enemy eliminated (Issue #5) ──
        // Guard: totalEnemiesSpawned > 0  — prevents firing before level loads.
        // Guard: enemiesKilledThisLevel >= totalEnemiesSpawned — ALL must die.
        // Guard: _levelCompleteTriggered  — prevents double-fire if called twice.
        if (totalEnemiesSpawned > 0
            && enemiesKilledThisLevel >= totalEnemiesSpawned
            && !_levelCompleteTriggered)
        {
            _levelCompleteTriggered = true;
            Debug.Log($"[GameManager] All enemies eliminated — waiting {victoryDelaySeconds}s before level complete.");
            StartCoroutine(LevelCompleteDelayed());
        }
    }

    // ── Level transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Waits <see cref="victoryDelaySeconds"/> so the player can watch the last
    /// enemy fall, then saves progress and loads the main-menu scene.
    /// </summary>
    private IEnumerator LevelCompleteDelayed()
    {
        // Let the enemy death animation play out — cursor stays locked while we wait.
        yield return new WaitForSeconds(victoryDelaySeconds);

        LevelComplete();
    }

    private void LevelComplete()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

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
            Cursor.visible   = true;
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
        Cursor.visible   = true;
        PendingMenuScreen = MenuScreen.GameOver;
        SceneManager.LoadScene("MainMenu");
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
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
        => Mathf.Clamp(PlayerPrefs.GetInt("UnlockedLevels", 1), 1, TotalLevels);

    private void ResetLevelState()
    {
        levelTime                = 0f;
        playerTookDamage         = false;
        totalEnemiesSpawned      = 0;
        enemiesKilledThisLevel   = 0;
        enemiesRemaining         = 0;
        _levelCompleteTriggered  = false;   // ← allow the next level to trigger
    }
}
