using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

/// <summary>
/// SINGLE SOURCE OF TRUTH for multiplayer level / weapon selection.
///
/// Every multiplayer system (PhotonLauncher room creation, PlayerController
/// weapon equip, HUDManager level + weapon text) must read from here, NEVER
/// from GameManager.currentLevel or PlayerPrefs "ContinueLevel" — those are
/// single-player Continue progress and used to leak into multiplayer loadouts
/// (HUD said LEVEL 8 / HAMMER while the player held the L1 Tactical Knife).
/// </summary>
public static class MultiplayerRuntimeConfig
{
    /// <summary>
    /// Host menu selection written to Photon room custom properties on CreateRoom.
    /// Joining clients must read the room property via <see cref="GetSelectedGameMode"/>.
    /// </summary>
    public static MpGameMode MultiplayerSelectedGameMode = MpGameMode.PurePvP;

    /// <summary>
    /// Default level used when the host creates a new multiplayer room.
    /// Future: the multiplayer menu will let the host pick this; for now it
    /// is hard-clamped to 1 so every match starts on Tactical Knife.
    /// </summary>
    public static int MultiplayerSelectedLevel = 1;

    /// <summary>
    /// Returns the level the local client should display + equip. Order:
    ///   1. Photon room custom property (set by the host on CreateRoom)
    ///   2. <see cref="MultiplayerSelectedLevel"/> (default 1)
    /// Emits [MPConfig] with the chosen source.
    /// </summary>
    private static int _lastLoggedLevel = -1;
    private static string _lastLoggedSource = string.Empty;

    public static int GetSelectedLevel()
    {
        int level;
        string source;
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MpRoomConfig.KeyLevel, out object raw))
        {
            level = Mathf.Clamp((int)raw, 1, GameManager.TotalLevels);
            source = "RoomProperty";
        }
        else
#endif
        {
            level = Mathf.Clamp(MultiplayerSelectedLevel, 1, GameManager.TotalLevels);
            source = "Default";
        }

        if (level != _lastLoggedLevel || source != _lastLoggedSource)
        {
            _lastLoggedLevel = level;
            _lastLoggedSource = source;
            Debug.Log($"[MPConfig] selectedLevel={level} weapon={ResolveWeaponName(level)} source={source}");
        }
        return level;
    }

    public static string GetSelectedWeaponName()
    {
        return ResolveWeaponName(GetSelectedLevel());
    }

    private static string ResolveWeaponName(int level)
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.GetWeaponNameForLevel(level);
        // Mirror PlayerController's hard-coded L1/L2 fallback when GameManager
        // hasn't booted yet (e.g. fresh Editor enter-play).
        return level == 2 ? "Razor Katana" : "Tactical Knife";
    }

    /// <summary>
    /// Single source of truth for multiplayer game mode. Order:
    ///   1. Photon room custom property "gm" (set by the host on CreateRoom)
    ///   2. <see cref="MultiplayerSelectedGameMode"/> (default PurePvP)
    /// Emits [MPMode] with the chosen source.
    /// </summary>
    private static MpGameMode _lastLoggedMode = (MpGameMode)255;
    private static string _lastLoggedModeSource = string.Empty;

    public static MpGameMode GetSelectedGameMode()
    {
        MpGameMode mode;
        string source;
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MpRoomConfig.KeyMode, out object raw))
        {
            mode = (MpGameMode)(byte)raw;
            source = "RoomProperty";
        }
        else
#endif
        {
            mode = MultiplayerSelectedGameMode;
            source = "Default";
        }

        if (mode != _lastLoggedMode || source != _lastLoggedModeSource)
        {
            _lastLoggedMode = mode;
            _lastLoggedModeSource = source;
            Debug.Log($"[MPMode] room mode = {mode} source={source}");
        }

        return mode;
    }

    public static bool IsPurePvP()
    {
        return GetSelectedGameMode() == MpGameMode.PurePvP;
    }

    public static bool IsAiEnabled()
    {
        MpGameMode mode = GetSelectedGameMode();
        return mode == MpGameMode.HybridChaos || mode == MpGameMode.CoopSurvival;
    }
}
