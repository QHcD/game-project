using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent profile-wide session manager for PRISM-7.
///
/// Tracks PRISM credits ("Soul Shards"), unlocked katana skins, lifetime
/// stats (kills/wins), and challenge progress. Data is saved to JSON in
/// <see cref="Application.persistentDataPath"/> so it survives between
/// PlayMode sessions and full Editor restarts.
///
/// The class boot-straps itself via <see cref="RuntimeInitializeOnLoadMethod"/>
/// before any scene loads so any other script can call <see cref="Instance"/>
/// without worrying about ordering.
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    /// <summary>
    /// Fired whenever credits / unlocks / equipped skin / challenge progress
    /// change. UI panels subscribe to repaint themselves.
    /// </summary>
    public event Action Changed;

    // ─── Reward tunables ────────────────────────────────────────────────────
    public const int CreditsPerKill          = 10;
    public const int CreditsPerMatchWin      = 500;
    public const int CreditsPerMatchComplete = 50;
    public const int CreditsPerChallenge     = 250;
    public const int CreditsPerWeaponMaster  = 500;

    private const string SaveFileName = "prism7_session.json";

    // ─── Skin catalogue ─────────────────────────────────────────────────────
    public static readonly KatanaSkin[] Skins =
    {
        new KatanaSkin("default",  "Standard Steel",  0,    new Color(0.85f, 0.92f, 1.00f, 1f)),
        new KatanaSkin("crimson",  "Crimson Edge",    250,  new Color(0.95f, 0.20f, 0.22f, 1f)),
        new KatanaSkin("emerald",  "Emerald Shard",   400,  new Color(0.20f, 0.85f, 0.45f, 1f)),
        new KatanaSkin("obsidian", "Obsidian Black",  650,  new Color(0.10f, 0.10f, 0.13f, 1f)),
        new KatanaSkin("plasma",   "Plasma Blue",     900,  new Color(0.18f, 0.55f, 1.00f, 1f)),
        new KatanaSkin("solar",    "Solar Flare",    1200,  new Color(1.00f, 0.62f, 0.10f, 1f)),
        new KatanaSkin("phantom",  "Phantom Violet", 1600,  new Color(0.62f, 0.22f, 0.94f, 1f)),
    };

    // ─── Weapon catalogue ───────────────────────────────────────────────────
    // Each entry maps an id to one of the existing GameManager weapon levels.
    // The level index drives the underlying procedural mesh (knife / katana /
    // nunchucks / etc.) and the base damage curve, while attackSpeedMultiplier
    // lets each store weapon feel distinct (Nunchucks chain fast, Hammer is
    // slower but harder hitting). `default` is owned + equipped on first run.
    public static readonly WeaponDefinition[] Weapons =
    {
        new WeaponDefinition("default",   "Tactical Knife",       0, levelIndex:  1, attackSpeedMul: 1.00f, damageMul: 1.00f, glyph: "K", tint: new Color(0.85f, 0.92f, 1f, 1f)),
        new WeaponDefinition("katana",    "Razor Katana",       450, levelIndex:  2, attackSpeedMul: 1.10f, damageMul: 1.00f, glyph: "S", tint: new Color(0.55f, 0.85f, 1f, 1f)),
        new WeaponDefinition("baseball",  "Baseball Bat",       300, levelIndex:  4, attackSpeedMul: 0.85f, damageMul: 1.20f, glyph: "B", tint: new Color(0.78f, 0.55f, 0.22f, 1f)),
        new WeaponDefinition("nunchucks", "Nunchucks",          600, levelIndex:  5, attackSpeedMul: 1.45f, damageMul: 0.85f, glyph: "N", tint: new Color(1f, 0.78f, 0.20f, 1f)),
        new WeaponDefinition("hammer",    "War Hammer",         900, levelIndex:  8, attackSpeedMul: 0.70f, damageMul: 1.60f, glyph: "H", tint: new Color(0.65f, 0.30f, 0.30f, 1f)),
        new WeaponDefinition("axe",       "Battle Axe",        1100, levelIndex:  9, attackSpeedMul: 0.78f, damageMul: 1.50f, glyph: "A", tint: new Color(0.85f, 0.30f, 0.20f, 1f)),
        new WeaponDefinition("spear",     "Reach Spear",       1400, levelIndex: 10, attackSpeedMul: 0.95f, damageMul: 1.30f, glyph: "P", tint: new Color(0.30f, 0.85f, 0.55f, 1f)),
        new WeaponDefinition("morgenstern","Morgenstern",      1800, levelIndex: 14, attackSpeedMul: 0.80f, damageMul: 1.80f, glyph: "M", tint: new Color(0.62f, 0.22f, 0.94f, 1f)),
    };

    // ─── Challenge catalogue ────────────────────────────────────────────────
    public static readonly ChallengeDefinition[] Challenges =
    {
        new ChallengeDefinition("kills_10",      "Kill 10 Enemies",                 10, ChallengeKind.LifetimeKills),
        new ChallengeDefinition("kills_50",      "Kill 50 Enemies",                 50, ChallengeKind.LifetimeKills),
        new ChallengeDefinition("kills_250",     "Kill 250 Enemies",               250, ChallengeKind.LifetimeKills),
        new ChallengeDefinition("wins_3",        "Win 3 Matches",                    3, ChallengeKind.MatchWins),
        new ChallengeDefinition("wins_10",       "Win 10 Matches",                  10, ChallengeKind.MatchWins),
        new ChallengeDefinition("fast_win",      "Win a match in under 3 minutes",   1, ChallengeKind.FastWin),
        new ChallengeDefinition("flawless",      "Win a match without dying",        1, ChallengeKind.FlawlessWin),
        new ChallengeDefinition("master_nunchucks","Weapon Master: 5 kills with Nunchucks", 5, ChallengeKind.WeaponMaster, weaponId: "nunchucks"),
        new ChallengeDefinition("master_katana",   "Weapon Master: 10 kills with the Katana", 10, ChallengeKind.WeaponMaster, weaponId: "katana"),
        new ChallengeDefinition("master_hammer",   "Weapon Master: 5 kills with the War Hammer", 5, ChallengeKind.WeaponMaster, weaponId: "hammer"),
    };

    // ─── Persisted state ────────────────────────────────────────────────────
    private SessionSave _save = new SessionSave();

    public int    Credits               => _save.credits;
    public int    LifetimeKills         => _save.lifetimeKills;
    public int    LifetimeMatchWins     => _save.lifetimeWins;
    public int    LifetimeMatchesPlayed => _save.lifetimeMatches;
    public string EquippedSkinId        => string.IsNullOrEmpty(_save.equippedSkinId)   ? "default" : _save.equippedSkinId;
    public string EquippedWeaponId      => string.IsNullOrEmpty(_save.equippedWeaponId) ? "default" : _save.equippedWeaponId;
    public IReadOnlyList<string> UnlockedSkinIds   => _save.unlockedSkins;
    public IReadOnlyList<string> UnlockedWeaponIds => _save.unlockedWeapons;

    // ─── Per-match transient state ──────────────────────────────────────────
    private int   _currentMatchKills;
    private float _currentMatchStartTime;
    private bool  _currentMatchTookDamage;

    // ────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject host = new GameObject("SessionManager");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<SessionManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Public API — Match flow
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Reset the per-match counters. Call from <c>GameManager.StartRun</c>.</summary>
    public void BeginMatch()
    {
        _currentMatchKills      = 0;
        _currentMatchStartTime  = Time.time;
        _currentMatchTookDamage = false;
    }

    /// <summary>
    /// Award the per-kill bounty + advance kill challenges. The weaponId
    /// argument is used by the Weapon Master challenges (e.g. "5 kills with
    /// the Nunchucks"). Pass <see cref="EquippedWeaponId"/> by default.
    /// </summary>
    public void OnPlayerKilledEnemy(string weaponId = null)
    {
        _save.credits        += CreditsPerKill;
        _save.lifetimeKills  += 1;
        _currentMatchKills   += 1;

        // Track per-weapon kill counts so Weapon Master challenges can
        // resolve. Empty / null weapon ids fall back to the equipped weapon.
        string wid = string.IsNullOrEmpty(weaponId) ? EquippedWeaponId : weaponId;
        IncrementWeaponKill(wid);
        AdvanceWeaponMasterChallenges(wid);

        EvaluateChallenges();
        Persist();
    }

    private void IncrementWeaponKill(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return;
        for (int i = 0; i < _save.weaponKills.Count; i++)
        {
            if (_save.weaponKills[i].id == weaponId)
            {
                ChallengeKvp kv = _save.weaponKills[i];
                kv.value += 1;
                _save.weaponKills[i] = kv;
                return;
            }
        }
        _save.weaponKills.Add(new ChallengeKvp { id = weaponId, value = 1 });
    }

    public int GetWeaponKills(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return 0;
        for (int i = 0; i < _save.weaponKills.Count; i++)
            if (_save.weaponKills[i].id == weaponId) return _save.weaponKills[i].value;
        return 0;
    }

    private void AdvanceWeaponMasterChallenges(string weaponId)
    {
        for (int i = 0; i < Challenges.Length; i++)
        {
            ChallengeDefinition def = Challenges[i];
            if (def.Kind != ChallengeKind.WeaponMaster) continue;
            if (def.WeaponId != weaponId) continue;
            int progress = GetChallengeProgress(def.Id) + 1;
            SetChallengeProgress(def.Id, progress);
        }
    }

    /// <summary>Logged when the player takes damage; gates the "flawless" challenge.</summary>
    public void OnPlayerTookDamage()
    {
        _currentMatchTookDamage = true;
    }

    /// <summary>
    /// Award match-end credits. <paramref name="playerWon"/> grants the win bonus
    /// and advances win-related challenges; otherwise just the consolation bonus.
    /// </summary>
    public void EndMatch(bool playerWon)
    {
        _save.lifetimeMatches += 1;
        if (playerWon)
        {
            _save.credits      += CreditsPerMatchWin;
            _save.lifetimeWins += 1;

            float elapsed = Time.time - _currentMatchStartTime;
            if (elapsed > 0f && elapsed < 180f)
                AdvanceChallenge(ChallengeKind.FastWin, 1);
            if (!_currentMatchTookDamage)
                AdvanceChallenge(ChallengeKind.FlawlessWin, 1);
        }
        else
        {
            _save.credits += CreditsPerMatchComplete;
        }

        EvaluateChallenges();
        Persist();
    }

    public int CurrentMatchKills => _currentMatchKills;

    // ────────────────────────────────────────────────────────────────────────
    //  Public API — Store
    // ────────────────────────────────────────────────────────────────────────

    public bool IsSkinUnlocked(string skinId)
    {
        if (string.IsNullOrEmpty(skinId)) return false;
        if (skinId == "default") return true;
        return _save.unlockedSkins.Contains(skinId);
    }

    /// <summary>Returns true if the skin was successfully purchased + unlocked.</summary>
    public bool TryBuySkin(string skinId)
    {
        KatanaSkin skin = FindSkin(skinId);
        if (skin == null) return false;
        if (IsSkinUnlocked(skinId)) return true;
        if (_save.credits < skin.Price) return false;

        _save.credits -= skin.Price;
        if (!_save.unlockedSkins.Contains(skinId))
            _save.unlockedSkins.Add(skinId);
        Persist();
        return true;
    }

    public bool EquipSkin(string skinId)
    {
        if (!IsSkinUnlocked(skinId)) return false;
        if (_save.equippedSkinId == skinId) return true;
        _save.equippedSkinId = skinId;
        Persist();
        return true;
    }

    public KatanaSkin FindSkin(string skinId)
    {
        if (string.IsNullOrEmpty(skinId)) return null;
        for (int i = 0; i < Skins.Length; i++)
            if (Skins[i].Id == skinId) return Skins[i];
        return null;
    }

    /// <summary>Tint colour of the currently equipped skin (used by PlayerController).</summary>
    public Color EquippedSkinColor =>
        FindSkin(EquippedSkinId)?.Color ?? Skins[0].Color;

    // ────────────────────────────────────────────────────────────────────────
    //  Public API — Weapons
    // ────────────────────────────────────────────────────────────────────────

    public bool IsWeaponUnlocked(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return false;
        if (weaponId == "default") return true;
        return _save.unlockedWeapons.Contains(weaponId);
    }

    /// <summary>Returns true if the weapon was successfully unlocked.</summary>
    public bool TryBuyWeapon(string weaponId)
    {
        WeaponDefinition w = FindWeapon(weaponId);
        if (w == null) return false;
        if (IsWeaponUnlocked(weaponId)) return true;
        if (_save.credits < w.Price) return false;

        _save.credits -= w.Price;
        if (!_save.unlockedWeapons.Contains(weaponId))
            _save.unlockedWeapons.Add(weaponId);
        Persist();
        return true;
    }

    public bool EquipWeapon(string weaponId)
    {
        if (!IsWeaponUnlocked(weaponId)) return false;
        if (_save.equippedWeaponId == weaponId) return true;
        _save.equippedWeaponId = weaponId;
        Persist();
        return true;
    }

    public WeaponDefinition FindWeapon(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return null;
        for (int i = 0; i < Weapons.Length; i++)
            if (Weapons[i].Id == weaponId) return Weapons[i];
        return null;
    }

    /// <summary>
    /// Store catalog entry whose <see cref="WeaponDefinition.LevelIndex"/> matches a campaign level
    /// (used for live-animation presets when the player is not overriding with a mismatched store weapon).
    /// </summary>
    public WeaponDefinition FindWeaponForCampaignLevel(int level)
    {
        for (int i = 0; i < Weapons.Length; i++)
        {
            WeaponDefinition w = Weapons[i];
            if (w.Id != "default" && w.LevelIndex == level) return w;
        }
        return null;
    }

    public WeaponDefinition EquippedWeapon =>
        FindWeapon(EquippedWeaponId) ?? Weapons[0];

    // ────────────────────────────────────────────────────────────────────────
    //  Public API — Challenges
    // ────────────────────────────────────────────────────────────────────────

    public int GetChallengeProgress(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        for (int i = 0; i < _save.challengeProgress.Count; i++)
        {
            ChallengeKvp kv = _save.challengeProgress[i];
            if (kv.id == id) return kv.value;
        }
        return 0;
    }

    public bool IsChallengeCompleted(string id) =>
        !string.IsNullOrEmpty(id) && _save.completedChallenges.Contains(id);

    private void SetChallengeProgress(string id, int value)
    {
        for (int i = 0; i < _save.challengeProgress.Count; i++)
        {
            if (_save.challengeProgress[i].id == id)
            {
                _save.challengeProgress[i] = new ChallengeKvp { id = id, value = value };
                return;
            }
        }
        _save.challengeProgress.Add(new ChallengeKvp { id = id, value = value });
    }

    private void AdvanceChallenge(ChallengeKind kind, int delta)
    {
        if (delta <= 0) return;
        for (int i = 0; i < Challenges.Length; i++)
        {
            if (Challenges[i].Kind != kind) continue;
            string id = Challenges[i].Id;
            int current = GetChallengeProgress(id);
            SetChallengeProgress(id, current + delta);
        }
    }

    private void EvaluateChallenges()
    {
        for (int i = 0; i < Challenges.Length; i++)
        {
            ChallengeDefinition def = Challenges[i];
            if (_save.completedChallenges.Contains(def.Id)) continue;

            int progress = GetChallengeProgress(def.Id);

            // Lifetime stats are recomputed live so they always agree with
            // _save.lifetimeKills / _save.lifetimeWins regardless of when
            // we last updated the per-challenge counters.
            if (def.Kind == ChallengeKind.LifetimeKills) progress = Mathf.Max(progress, _save.lifetimeKills);
            if (def.Kind == ChallengeKind.MatchWins)     progress = Mathf.Max(progress, _save.lifetimeWins);
            if (def.Kind == ChallengeKind.WeaponMaster)  progress = Mathf.Max(progress, GetWeaponKills(def.WeaponId));

            SetChallengeProgress(def.Id, progress);

            if (progress >= def.Target)
            {
                _save.completedChallenges.Add(def.Id);
                // Weapon Master rewards are bigger to celebrate the grind —
                // 500 credits as called out in the design brief.
                int payout = def.Kind == ChallengeKind.WeaponMaster
                    ? CreditsPerWeaponMaster
                    : CreditsPerChallenge;
                _save.credits += payout;
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Persistence
    // ────────────────────────────────────────────────────────────────────────

    public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public void Load()
    {
        try
        {
            string path = SavePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                SessionSave loaded = JsonUtility.FromJson<SessionSave>(json);
                if (loaded != null) _save = loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SessionManager] Failed to load save: {ex.Message}");
            _save = new SessionSave();
        }

        if (_save == null) _save = new SessionSave();
        _save.EnsureValid();
        if (!PlayerProfile.HasUsername && !string.IsNullOrEmpty(_save.profileName))
            PlayerProfile.SetUsername(_save.profileName);
        EvaluateChallenges();
        Changed?.Invoke();
    }

    /// <summary>
    /// Writes session JSON (credits, unlocks, mirrored profile name) and flushes
    /// <see cref="PlayerPrefs"/> (graphics, audio, username). Call on scene
    /// changes and match end so data survives abrupt exits.
    /// </summary>
    public void FlushPersistence()
    {
        SyncProfileIntoSave();
        Persist();
        PlayerPrefs.Save();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FlushPersistence();
    }

    private void SyncProfileIntoSave()
    {
        _save.profileName = PlayerProfile.HasUsername ? PlayerProfile.Username : string.Empty;
    }

    private void Persist()
    {
        SyncProfileIntoSave();
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(_save, prettyPrint: false));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SessionManager] Failed to save: {ex.Message}");
        }
        Changed?.Invoke();
    }

    /// <summary>Wipes the save and reloads defaults — exposed for the "reset" debug button.</summary>
    public void ResetAll()
    {
        _save = new SessionSave();
        _save.EnsureValid();
        Persist();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Match audio — optional mixer from Resources (routes VO / UI SFX)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Resources path to an <see cref="AudioMixer"/> asset (no extension), e.g. <c>Audio/Mixers/PRISM_Master</c>.</summary>
    public string MatchAudioMixerResourcePath = "Audio/Mixers/PRISM_Master";

    /// <summary>Mixer group name substring for commentary / VO (matched via <see cref="AudioMixer.FindMatchingGroups"/>).</summary>
    public string VoiceOverMixerGroupName = "VO";

    /// <summary>Mixer group for match start countdown ticks / GO chime.</summary>
    public string MatchUiMixerGroupName = "UI";

    private AudioMixer _matchMixer;
    private readonly Dictionary<string, AudioMixerGroup> _mixerGroupCache = new Dictionary<string, AudioMixerGroup>();

    /// <summary>Assigns an <see cref="AudioMixerGroup"/> to <paramref name="source"/> when a mixer is available.</summary>
    public void ConfigureMatchAudioSource(AudioSource source, string mixerGroupName)
    {
        if (source == null || string.IsNullOrEmpty(mixerGroupName)) return;

        AudioMixerGroup group = ResolveMixerGroup(mixerGroupName);
        if (group != null)
            source.outputAudioMixerGroup = group;
    }

    private AudioMixerGroup ResolveMixerGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return null;
        if (_mixerGroupCache.TryGetValue(groupName, out AudioMixerGroup cached))
            return cached;

        if (_matchMixer == null && !string.IsNullOrEmpty(MatchAudioMixerResourcePath))
            _matchMixer = Resources.Load<AudioMixer>(MatchAudioMixerResourcePath);

        if (_matchMixer == null)
            return null;

        AudioMixerGroup[] found = _matchMixer.FindMatchingGroups(groupName);
        if (found == null || found.Length == 0)
            return null;

        _mixerGroupCache[groupName] = found[0];
        return found[0];
    }

    /// <summary>Clears cached mixer reference (e.g. after loading a new mixer asset in editor).</summary>
    public void ClearMatchAudioMixerCache()
    {
        _matchMixer = null;
        _mixerGroupCache.Clear();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Internal save layout (JSON-friendly)
    // ────────────────────────────────────────────────────────────────────────

    [Serializable]
    private class SessionSave
    {
        public int credits;
        public int lifetimeKills;
        public int lifetimeWins;
        public int lifetimeMatches;
        /// <summary>Mirror of <see cref="PlayerProfile"/> for JSON backup when prefs are cleared.</summary>
        public string profileName = string.Empty;
        public string equippedSkinId   = "default";
        public string equippedWeaponId = "default";

        public List<string>       unlockedSkins        = new List<string>();
        public List<string>       unlockedWeapons      = new List<string>();
        public List<string>       completedChallenges  = new List<string>();
        public List<ChallengeKvp> challengeProgress    = new List<ChallengeKvp>();
        public List<ChallengeKvp> weaponKills          = new List<ChallengeKvp>();

        public void EnsureValid()
        {
            if (profileName == null) profileName = string.Empty;
            if (unlockedSkins == null)        unlockedSkins = new List<string>();
            if (unlockedWeapons == null)      unlockedWeapons = new List<string>();
            if (completedChallenges == null)  completedChallenges = new List<string>();
            if (challengeProgress == null)    challengeProgress   = new List<ChallengeKvp>();
            if (weaponKills == null)          weaponKills         = new List<ChallengeKvp>();

            if (string.IsNullOrEmpty(equippedSkinId))   equippedSkinId   = "default";
            if (string.IsNullOrEmpty(equippedWeaponId)) equippedWeaponId = "default";

            if (!unlockedSkins.Contains("default"))   unlockedSkins.Add("default");
            if (!unlockedWeapons.Contains("default")) unlockedWeapons.Add("default");
        }
    }

    [Serializable]
    private struct ChallengeKvp
    {
        public string id;
        public int value;
    }
}

/// <summary>Public skin record used by the store UI.</summary>
public sealed class KatanaSkin
{
    public string Id    { get; }
    public string Name  { get; }
    public int    Price { get; }
    public Color  Color { get; }

    public KatanaSkin(string id, string name, int price, Color color)
    {
        Id    = id;
        Name  = name;
        Price = price;
        Color = color;
    }
}

public enum ChallengeKind
{
    LifetimeKills,
    MatchWins,
    FastWin,
    FlawlessWin,
    WeaponMaster,
}

public sealed class ChallengeDefinition
{
    public string        Id       { get; }
    public string        Title    { get; }
    public int           Target   { get; }
    public ChallengeKind Kind     { get; }
    /// <summary>Required when <see cref="Kind"/> == <c>WeaponMaster</c>.</summary>
    public string        WeaponId { get; }

    public ChallengeDefinition(string id, string title, int target, ChallengeKind kind, string weaponId = null)
    {
        Id       = id;
        Title    = title;
        Target   = target;
        Kind     = kind;
        WeaponId = weaponId;
    }
}

/// <summary>
/// Public weapon record consumed by the PRSIM Store and the player's
/// runtime equipment hooks. <see cref="LevelIndex"/> selects which existing
/// weapon mesh + base damage curve from <c>GameManager</c> to spawn; the
/// per-weapon multipliers tweak the feel (Nunchucks faster but lighter,
/// Hammer slower but harder hitting, etc.).
/// </summary>
public sealed class WeaponDefinition
{
    public string Id                 { get; }
    public string Name               { get; }
    public int    Price              { get; }
    public int    LevelIndex         { get; }
    public float  AttackSpeedMul     { get; }
    public float  DamageMul          { get; }
    public string Glyph              { get; }
    public Color  Tint               { get; }

    public WeaponDefinition(string id, string name, int price, int levelIndex,
        float attackSpeedMul, float damageMul, string glyph, Color tint)
    {
        Id             = id;
        Name           = name;
        Price          = price;
        LevelIndex     = levelIndex;
        AttackSpeedMul = attackSpeedMul;
        DamageMul      = damageMul;
        Glyph          = glyph;
        Tint           = tint;
    }
}
