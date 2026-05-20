using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// STRICT SINGLETON — "Destroyer" Pattern.
/// In Awake(), if an instance already exists, this immediately calls
/// DestroyImmediate(gameObject) and returns, guaranteeing only ONE
/// GameManager exists globally across all 16 levels.
///
/// This eliminates the hierarchy flood of duplicate GameManagers that
/// was causing extreme lag, frozen AI, and broken physics.
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

    public static GameManager Instance { get; private set; }
    public static MenuScreen PendingMenuScreen { get; private set; } = MenuScreen.MainMenu;

    public const int TotalLevels = 16;

    public enum WeaponType
    {
        Melee,
        TwoHandedMelee,   // Two-handed swords, spears, axes — enables left-hand IK grip
        UltimateMelee
    }

    private static readonly string[] LevelWeaponNames = {
        "Tactical Knife", "Razor Katana", "Shovel", "Baseball Bat", "Nunchucks",
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
        //          Knife              Katana                  Shovel                  Baseball Bat            Nunchucks
        WeaponType.Melee,  WeaponType.TwoHandedMelee, WeaponType.TwoHandedMelee, WeaponType.TwoHandedMelee, WeaponType.Melee,
        //          Wrench             Crowbar                 Hammer                  Axe                     Spear
        WeaponType.Melee,  WeaponType.Melee, WeaponType.TwoHandedMelee, WeaponType.TwoHandedMelee, WeaponType.TwoHandedMelee,
        //          Nailed Plank           Saw                Sickle             Morgenstern              L3FTE
        WeaponType.TwoHandedMelee, WeaponType.Melee, WeaponType.Melee, WeaponType.TwoHandedMelee, WeaponType.Melee,
        //          Riot Shield
        WeaponType.TwoHandedMelee
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
    public int   killCount           = 0;
    public int   enemiesRemaining    = 0;
    public string difficulty         = "Normal";
    public bool  playerTookDamage    = false;
    public float levelTime           = 0f;
    public ArenaMap        selectedMap      = ArenaMap.Map1;
    public PerspectiveMode perspectiveMode  = PerspectiveMode.ThirdPerson;
    public MovementScheme  movementScheme   = MovementScheme.Wasd;

    public int totalEnemiesSpawned   = 0;
    public int enemiesKilledThisLevel = 0;

    /// <summary>Fired whenever <see cref="enemiesRemaining"/> is updated (spawn init or kill).</summary>
    public event Action<int> OnEnemiesRemainingChanged;

    // ── Custom Match (set by the Custom Match menu) ──────────────────────────
    // When IsCustomMatch is true, GetEnemyCount/LevelTimeLimitSeconds and the
    // active difficulty switch over to these per-session overrides instead of
    // the persisted PlayerPrefs values.
    public bool   isCustomMatch          = false;
    public int    customEnemyCount       = 12;
    public int    customMatchTimeSeconds = 300;
    public string customDifficulty       = "Normal";

    public const int DefaultEnemyCount      = 25;
    public const int CustomEnemyMin         = 1;
    public const int CustomEnemyMax         = 25;
    public const int CustomMatchTimeMin     = 60;   // 1 minute
    public const int CustomMatchTimeMax     = 1800; // 30 minutes (defensive cap)

    [SerializeField] private float victoryDelaySeconds = 2.5f;
    [SerializeField] private float loadingScreenMinSeconds = 5.5f;
    [Tooltip("If multiple cameras keep the MainCamera tag, extras are disabled so only one renders (prefers CameraController).")]
    [SerializeField] private bool enforceSingleMainCameraTag = true;

    private bool _levelCompleteTriggered = false;
    private Coroutine _sceneLoadRoutine;

    private AudioSource _matchVoiceAudio;
    private AudioSource _matchUiAudio;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsForPlayMode()
    {
        Instance = null;
        PendingMenuScreen = MenuScreen.MainMenu;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  STRICT SINGLETON — "Destroyer" Pattern
    //  CRITICAL: Any duplicate GameManager (prefab leftover, scene copy, or
    //  AddComponent call) is IMMEDIATELY destroyed. This is the one fix that
    //  eliminates the hierarchy flood, the lag, and the frozen AI.
    // ════════════════════════════════════════════════════════════════════════
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Scenes may still contain an extra GameManager. We destroy duplicates to keep gameplay stable.
            // Avoid spamming warnings; the proper fix is removing the extra scene instance.
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;

        if (Application.isPlaying)
        {
            GameManager[] all = UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != this)
                {
                    DestroyImmediate(all[i].gameObject);
                }
            }

            DontDestroyOnLoad(gameObject);

            SetupProgrammaticMatchAudio();
            if (GetComponent<MatchCommentator>() == null)
                gameObject.AddComponent<MatchCommentator>();
        }

        LoadPersistedSettings();
    }

    private void Start()
    {
        if (!Application.isPlaying || !enforceSingleMainCameraTag)
            return;
        EnforceSingleMainCameraTag();
    }

    /// <summary>
    /// Keeps a single enabled <c>MainCamera</c> when duplicates exist (common after merges / prefab copies).
    /// Does not remove UI or minimap cameras that use other tags.
    /// </summary>
    private void EnforceSingleMainCameraTag()
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera preferred = null;
        if (CameraController.Instance != null)
            preferred = CameraController.Instance.GetComponent<Camera>();

        var mains = new List<Camera>(4);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera c = cameras[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            if (!c.CompareTag("MainCamera")) continue;
            mains.Add(c);
        }

        if (mains.Count <= 1)
            return;

        Camera keep = preferred != null && mains.Contains(preferred) ? preferred : mains[0];
        for (int i = 0; i < mains.Count; i++)
        {
            if (mains[i] != null && mains[i] != keep)
                mains[i].enabled = false;
        }
    }

    /// <summary>Child <c>__MatchVoice</c> — commentary / VO <see cref="PlayMatchVoiceOneShot"/>.</summary>
    public AudioSource MatchVoiceAudio => _matchVoiceAudio;

    /// <summary>Child <c>__MatchUiSfx</c> — countdown beeps, UI stingers.</summary>
    public AudioSource MatchUiAudio => _matchUiAudio;

    private void SetupProgrammaticMatchAudio()
    {
        _matchVoiceAudio = EnsureChildAudioSource("__MatchVoice", SessionManager.Instance != null
            ? SessionManager.Instance.VoiceOverMixerGroupName
            : "VO");
        _matchUiAudio = EnsureChildAudioSource("__MatchUiSfx", SessionManager.Instance != null
            ? SessionManager.Instance.MatchUiMixerGroupName
            : "UI");
    }

    private AudioSource EnsureChildAudioSource(string childName, string mixerGroupName)
    {
        Transform existing = transform.Find(childName);
        if (existing == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            existing = go.transform;
        }

        AudioSource src = existing.GetComponent<AudioSource>();
        if (src == null)
            src = existing.gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.loop = false;

        if (SessionManager.Instance != null && !string.IsNullOrEmpty(mixerGroupName))
            SessionManager.Instance.ConfigureMatchAudioSource(src, mixerGroupName);

        return src;
    }

    /// <summary>VO / commentator lines (overlapping friendly).</summary>
    public void PlayMatchVoiceOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (!Application.isPlaying || clip == null) return;
        if (_matchVoiceAudio == null)
            SetupProgrammaticMatchAudio();
        _matchVoiceAudio?.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    /// <summary>Countdown beeps / GO chime and other non-positional UI audio.</summary>
    public void PlayMatchUiOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (!Application.isPlaying || clip == null) return;
        if (_matchUiAudio == null)
            SetupProgrammaticMatchAudio();
        _matchUiAudio?.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LoadPersistedSettings()
    {
        difficulty      = PlayerPrefs.GetString("Difficulty", difficulty);
        selectedMap     = (ArenaMap)Mathf.Clamp(PlayerPrefs.GetInt("SelectedMap", (int)selectedMap), 0, 1);
        // Third-person only: if older saves stored FirstPerson, ignore and overwrite.
        perspectiveMode = PerspectiveMode.ThirdPerson;
        int persistedPerspective = Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", (int)PerspectiveMode.ThirdPerson), 0, 1);
        if (persistedPerspective != (int)PerspectiveMode.ThirdPerson)
        {
            PlayerPrefs.SetInt("PerspectiveMode", (int)PerspectiveMode.ThirdPerson);
            PlayerPrefs.Save();
        }
        movementScheme  = (MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", (int)movementScheme), 0, 1);
        currentLevel    = Mathf.Clamp(PlayerPrefs.GetInt("ContinueLevel", currentLevel), 1, TotalLevels);
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

        if (currentLevel == 6)
            StartCoroutine(RefreshLevel6Agents());
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        Scene active = SceneManager.GetActiveScene();
        if (!active.name.Equals("GameScene")) return;
        if (_levelCompleteTriggered) return;

        float timeLimit = LevelTimeLimitSeconds;
        levelTime += Time.deltaTime;
        if (levelTime >= timeLimit)
        {
            levelTime = timeLimit;
            _levelCompleteTriggered = true;
            ResolveTimedMatchWinner();
        }
    }

    private IEnumerator RefreshEnemyCountNextFrame()
    {
        yield return null;

        EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        if (enemies != null && enemies.Length > 0 && totalEnemiesSpawned <= 0)
            InitializeEnemyCount(enemies.Length);
    }

    private IEnumerator RefreshLevel6Agents()
    {
        yield return null;
        yield return null;

        EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        if (enemies == null) yield break;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            UnityEngine.AI.NavMeshAgent agent = enemies[i].GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null) continue;

            agent.enabled = false;
            agent.enabled = true;

            // Do not warp enemies during normal play. EnemyController only
            // warps after a real out-of-map fall below Y = -5.
        }

        Debug.Log($"[GameManager] Level 6 agent refresh: rebound {enemies.Length} NavMeshAgent(s).");
    }

    // ── Settings setters ─────────────────────────────────────────────────────
    public void SetDifficulty(string diff)
    {
        string normalized = NormalizeDifficulty(diff);
        difficulty = normalized;
        PlayerPrefs.SetString("Difficulty", normalized);
        PlayerPrefs.Save();
    }

    /// <summary>Canonicalises a difficulty string. Accepts Easy/Normal/Hard/Veteran.</summary>
    private static string NormalizeDifficulty(string diff)
    {
        if (string.IsNullOrWhiteSpace(diff)) return "Normal";
        string trimmed = diff.Trim();
        if (trimmed.Equals("easy",    System.StringComparison.OrdinalIgnoreCase)) return "Easy";
        if (trimmed.Equals("hard",    System.StringComparison.OrdinalIgnoreCase)) return "Hard";
        if (trimmed.Equals("veteran", System.StringComparison.OrdinalIgnoreCase)) return "Veteran";
        return "Normal";
    }

    public void SetSelectedMap(ArenaMap map)
    {
        selectedMap = map;
        PlayerPrefs.SetInt("SelectedMap", (int)map);
        PlayerPrefs.Save();
    }

    public void SetPerspectiveMode(PerspectiveMode mode)
    {
        // Third-person only.
        perspectiveMode = PerspectiveMode.ThirdPerson;
        PlayerPrefs.SetInt("PerspectiveMode", (int)PerspectiveMode.ThirdPerson);
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

    public bool IsNewPlayer()
        => !PlayerPrefs.HasKey("ContinueLevel") && !PlayerPrefs.HasKey("UnlockedLevels");

    // ── Game flow ─────────────────────────────────────────────���───────────────
    public void StartRun(int level = 1)
    {
        MultiplayerMode.SetSinglePlayer();
        Time.timeScale = 1f;
        score = 0;
        // A "regular" run always clears any pending custom-match overrides
        // — only StartCustomRun re-enables them.
        isCustomMatch = false;
        if (MatchStatsManager.Instance != null)
            MatchStatsManager.Instance.ResetMatch();
        if (SessionManager.Instance != null)
            SessionManager.Instance.BeginMatch();
        SetCurrentLevel(level);
        PendingMenuScreen = MenuScreen.MainMenu;
        BeginSceneLoad("GameScene");
    }

    /// <summary>
    /// Begin a Custom Match using the values picked in the Custom Match menu.
    /// </summary>
    /// <param name="enemyCount">Number of enemies (1..25).</param>
    /// <param name="matchTimeSeconds">Total match length in seconds (e.g. 120, 300, 600).</param>
    /// <param name="difficultyLabel">"Easy", "Normal", or "Veteran".</param>
    /// <param name="level">Which weapon level to play with (defaults to 1).</param>
    public void StartCustomRun(int enemyCount, int matchTimeSeconds, string difficultyLabel, int level = 1)
    {
        MultiplayerMode.SetSinglePlayer();
        Time.timeScale = 1f;
        score = 0;

        isCustomMatch          = true;
        customEnemyCount       = Mathf.Clamp(enemyCount, CustomEnemyMin, CustomEnemyMax);
        customMatchTimeSeconds = Mathf.Clamp(matchTimeSeconds, CustomMatchTimeMin, CustomMatchTimeMax);
        customDifficulty       = NormalizeDifficulty(difficultyLabel);

        if (MatchStatsManager.Instance != null)
            MatchStatsManager.Instance.ResetMatch();
        if (SessionManager.Instance != null)
            SessionManager.Instance.BeginMatch();
        SetCurrentLevel(level);
        PendingMenuScreen = MenuScreen.MainMenu;
        BeginSceneLoad("GameScene");
    }

    public void ReplayCurrentLevel()
    {
        MultiplayerMode.SetSinglePlayer();
        Time.timeScale = 1f;
        if (MatchStatsManager.Instance != null)
            MatchStatsManager.Instance.ResetMatch();
        ResetLevelState();
        PendingMenuScreen = MenuScreen.MainMenu;
        BeginSceneLoad("GameScene");
    }

    public void SetCurrentLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, TotalLevels);
        enemiesRemaining = 0;
        PlayerPrefs.SetInt("ContinueLevel", currentLevel);
        if (MatchStatsManager.Instance != null)
            MatchStatsManager.Instance.ResetMatch();
        ResetLevelState();
    }

    /// <summary>
    /// Match length in seconds. Default match length is 5 minutes (300s).
    /// In a Custom Match, the player picks 2/5/10 minutes via the menu and
    /// we honour that instead.
    /// </summary>
    public float LevelTimeLimitSeconds
        => isCustomMatch ? Mathf.Clamp(customMatchTimeSeconds, CustomMatchTimeMin, CustomMatchTimeMax) : 300f;

    public int GetEnemyCount()
        => isCustomMatch ? Mathf.Clamp(customEnemyCount, CustomEnemyMin, CustomEnemyMax) : DefaultEnemyCount;

    /// <summary>Active difficulty string — Custom Match overrides the persisted Options value.</summary>
    public string ActiveDifficulty => isCustomMatch ? NormalizeDifficulty(customDifficulty) : difficulty;

    public float GetEnemySpeed()
    {
        float baseSpeed = 1.85f + ((currentLevel - 1) * 0.045f);
        switch (ActiveDifficulty)
        {
            case "Easy":    return baseSpeed * 0.88f;
            case "Hard":    return baseSpeed * 1.05f;
            case "Veteran": return baseSpeed * 1.18f;
            default:        return baseSpeed;
        }
    }

    public float GetEnemyDamage()
    {
        float baseDamage = 7.5f + ((currentLevel - 1) * 0.45f);
        switch (ActiveDifficulty)
        {
            case "Easy":    return baseDamage * 0.82f;
            case "Hard":    return baseDamage * 1.15f;
            case "Veteran": return baseDamage * 1.40f;
            default:        return baseDamage;
        }
    }

    /// <summary>Multiplier applied to enemy max-HP. Veteran enemies tank more hits.</summary>
    public float GetEnemyHealthMultiplier()
    {
        switch (ActiveDifficulty)
        {
            case "Easy":    return 0.85f;
            case "Hard":    return 1.15f;
            case "Veteran": return 1.45f;
            default:        return 1.00f;
        }
    }

    public string  GetWeaponNameForLevel(int level)         => LevelWeaponNames[Mathf.Clamp(level - 1, 0, LevelWeaponNames.Length - 1)];
    public float   GetWeaponDamageForLevel(int level)       => LevelWeaponDamage[Mathf.Clamp(level - 1, 0, LevelWeaponDamage.Length - 1)];
    public float   GetWeaponRangeForLevel(int level)        => LevelWeaponRange[Mathf.Clamp(level - 1, 0, LevelWeaponRange.Length - 1)];
    public Color   GetWeaponColorForLevel(int level)        => LevelWeaponColors[Mathf.Clamp(level - 1, 0, LevelWeaponColors.Length - 1)];
    public WeaponType GetWeaponTypeForLevel(int level)      => LevelWeaponTypes[Mathf.Clamp(level - 1, 0, LevelWeaponTypes.Length - 1)];
    public float   GetWeaponExplosionRadiusForLevel(int level) => LevelWeaponExplosionRadius[Mathf.Clamp(level - 1, 0, LevelWeaponExplosionRadius.Length - 1)];

    public void AddScore(int points) { score += points; }

    public void InitializeEnemyCount(int count)
    {
        totalEnemiesSpawned    = count;
        enemiesRemaining       = count;
        enemiesKilledThisLevel = 0;

        OnEnemiesRemainingChanged?.Invoke(enemiesRemaining);

        Debug.Log($"[GameManager] InitializeEnemyCount: {count} enemies registered for this level.");
    }

    /// <summary>Enemy hits to kill the player from full health (with 100 HP, Normal = 10 damage per hit).</summary>
    public int GetPlayerHitsToKill()
    {
        switch (ActiveDifficulty)
        {
            case "Easy":    return 15;
            case "Hard":    return 7;
            case "Veteran": return 6;
            default:        return 10;
        }
    }

    public int GetEnemyHitsToKillByPlayer()
    {
        switch (ActiveDifficulty)
        {
            case "Easy":    return 3;
            case "Hard":    return 7;
            case "Veteran": return 8;
            default:        return 5;
        }
    }

    public void EnemyKilled(bool byPlayer = false, bool assistedByPlayer = false)
    {
        enemiesKilledThisLevel++;
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);
        OnEnemiesRemainingChanged?.Invoke(enemiesRemaining);

        if (byPlayer)
            AddScore(100);
        else if (assistedByPlayer)
            AddScore(50);

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateEnemyCount(enemiesRemaining);

            if (byPlayer)
            {
                killCount++;
                HUDManager.Instance.UpdateScore(score);
                HUDManager.Instance.RegisterKill();
                if (CombatUIManager.Instance != null) CombatUIManager.Instance.ShowKill();
                else HUDManager.Instance.ShowXpPopup(100, "KILL");
            }
            else if (assistedByPlayer)
            {
                HUDManager.Instance.UpdateScore(score);
                if (CombatUIManager.Instance != null) CombatUIManager.Instance.ShowAssist();
                else HUDManager.Instance.ShowXpPopup(50, "ASSIST");
            }
        }

        Debug.Log($"[GameManager] EnemyKilled: {enemiesKilledThisLevel}/{totalEnemiesSpawned} killed, {enemiesRemaining} remaining.");

        if (totalEnemiesSpawned > 0
            && enemiesKilledThisLevel >= totalEnemiesSpawned
            && !_levelCompleteTriggered)
        {
            _levelCompleteTriggered = true;
            Debug.Log($"[GameManager] All enemies eliminated — waiting {victoryDelaySeconds}s before level complete.");
            StartCoroutine(LevelCompleteDelayed());
        }
    }

    private IEnumerator LevelCompleteDelayed()
    {
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

        // PRISM session — pay the +500 win bounty + advance Fast Win / Flawless
        // / Match Wins challenges before transitioning to the menu.
        if (SessionManager.Instance != null)
            SessionManager.Instance.EndMatch(playerWon: true);

        PendingMenuScreen = MenuScreen.LevelComplete;
        SceneManager.LoadScene("MainMenu");
    }

    private void ResolveTimedMatchWinner()
    {
        // Winner resolution when the match timer expires:
        //   1. If only one fighter is still alive, that fighter wins.
        //   2. Otherwise the highest kill count wins.
        //   3. Tie-break is delegated to MatchStatsManager's existing stable
        //      ordering: Kills DESC → Deaths ASC → IsPlayer DESC → Name ASC.
        //      "IsPlayer DESC" intentionally favours the human on a perfect
        //      tie since the registration order is otherwise opaque to the
        //      player; lower Deaths approximates "most health remaining" for
        //      the alive set.
        MatchStatsManager stats = MatchStatsManager.Instance;
        bool playerWon;

        if (stats == null)
        {
            playerWon = killCount > 0;
        }
        else
        {
            var leaders = stats.GetTopCombatants(64);

            // Prefer the highest-kill ALIVE combatant. If everyone shown as
            // dead, fall back to the highest-kill row regardless of alive
            // state (stale IsAlive flags should not deny anyone a victory).
            MatchStatsManager.CombatantSnapshot? winner = null;
            for (int i = 0; i < leaders.Count; i++)
            {
                if (!leaders[i].IsAlive) continue;
                winner = leaders[i];
                break;
            }
            if (winner == null && leaders.Count > 0)
                winner = leaders[0];

            if (winner.HasValue)
            {
                playerWon = winner.Value.IsPlayer
                            // If the player's local killCount has somehow
                            // outpaced the stats snapshot (e.g. last frame
                            // before timeout), still award the win.
                            || killCount > winner.Value.Kills;
            }
            else
            {
                playerWon = killCount > 0;
            }
        }

        // Skip EndMatchCinematic (Winners Circle + VICTORY scoreboard popup).
        // Transition straight to the main-menu results flow instead.
        if (playerWon) LevelComplete();
        else           GameOver();
    }

    public void LoadNextLevel()
    {
        MultiplayerMode.SetSinglePlayer();
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
        BeginSceneLoad("GameScene");
    }

    public void GameOver()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Player lost — still award a tiny consolation credit drop so the
        // economy never zero-sums after a hard match. EndMatch with
        // playerWon=false also bumps the lifetimeMatchesPlayed counter.
        if (SessionManager.Instance != null)
            SessionManager.Instance.EndMatch(playerWon: false);

        PendingMenuScreen = MenuScreen.GameOver;
        SceneManager.LoadScene("MainMenu");
    }

    public void GoToMainMenu()
    {
        MultiplayerMode.SetSinglePlayer();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        // Custom matches are session-scoped; returning to the menu always
        // ends them. If the player wants another, they re-pick it.
        isCustomMatch     = false;
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
        => TotalLevels; // Level gating removed — all levels available from the start.

    private void ResetLevelState()
    {
        levelTime                = 0f;
        killCount                = 0;
        playerTookDamage         = false;
        totalEnemiesSpawned      = 0;
        enemiesKilledThisLevel   = 0;
        enemiesRemaining         = 0;
        _levelCompleteTriggered  = false;
    }

    private void BeginSceneLoad(string sceneName)
    {
        if (!isActiveAndEnabled)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        if (_sceneLoadRoutine != null)
            StopCoroutine(_sceneLoadRoutine);

        _sceneLoadRoutine = StartCoroutine(LoadSceneWithLoadingScreen(sceneName));
    }

    private IEnumerator LoadSceneWithLoadingScreen(string sceneName)
    {
        LoadingScreenUI loadingUi = LoadingScreenUI.CreateOrGet();
        loadingUi.SetLabel("LOADING");

        float minDuration = Mathf.Max(0.1f, loadingScreenMinSeconds);
        float start = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            loadingUi.DestroySelf();
            _sceneLoadRoutine = null;
            yield break;
        }

        op.allowSceneActivation = false;

        while (true)
        {
            bool minTimeDone = (Time.unscaledTime - start) >= minDuration;
            bool sceneReady = op.progress >= 0.9f;
            if (minTimeDone && sceneReady)
                break;
            yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;

        const float maxReadyWait = 4f;
        float readyStart = Time.unscaledTime;
        while (Time.unscaledTime - readyStart < maxReadyWait)
        {
            if (IsGameplayReady())
                break;
            yield return null;
        }

        yield return null;
        yield return null;

        if (loadingUi != null)
            loadingUi.DestroySelf();

        if (sceneName == "GameScene")
        {
            yield return MatchStartCountdownUI.Play();
            if (!MultiplayerMode.IsMultiplayer)
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    pc.enabled = true;
                    CharacterController cc = pc.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = true;
                }
                if (HUDManager.Instance != null)
                    HUDManager.Instance.ApplySinglePlayerGameplayState();
            }
        }

        _sceneLoadRoutine = null;
    }

    private bool IsGameplayReady()
    {
        if (totalEnemiesSpawned <= 0) return false;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return false;

        if (Physics.Raycast(player.transform.position + Vector3.up * 0.5f,
                            Vector3.down, out RaycastHit groundHit, 3f,
                            ~0, QueryTriggerInteraction.Ignore))
        {
            float airborne = (player.transform.position.y - groundHit.point.y);
            if (airborne > 1.2f) return false;
        }
        else
        {
            return false;
        }

        Camera cam = Camera.main;
        if (cam == null) return false;

        return true;
    }
}