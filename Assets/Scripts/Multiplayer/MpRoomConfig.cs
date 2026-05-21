using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
using ExitGames.Client.Photon;
#endif

/// <summary>
/// Thin wrapper around Photon Room Custom Properties.
/// Master client writes config when creating the room.
/// All clients read it on join to sync their local MultiplayerMode.ActiveMode.
///
/// Keys are kept to 2 chars to minimise bandwidth.
///   "gm" = MpGameMode  (byte)
///   "lv" = level index (int)
///   "bc" = bot count   (int)
/// </summary>
public static class MpRoomConfig
{
    public const string KeyMode     = "gm";
    public const string KeyLevel    = "lv";
    public const string KeyBotCount = "bc";
    public const string KeyBotsEnabled   = "be";
    public const string KeyFriendlyFire  = "ff";
    public const string KeyMatchState    = "ms";
    public const string KeyTimerDuration = "td";
    public const string KeyWinnerName    = "wn";
    public const string KeyMaxPlayers    = "mh";

    public const int DefaultBotCount = 20;

    // ── Write (Master Client only) ───────────────────────────────────────────

    /// <summary>
    /// Writes game mode, level, and bot count into the current room's
    /// custom properties. Safe to call only from Master Client.
    /// </summary>
    public static void WriteRoomConfig(MpGameMode mode, int level, int botCount)
    {
#if PUN_2_OR_NEWER
        if (!PhotonNetwork.InRoom) return;

        var props = new Hashtable
        {
            { KeyMode,     (byte)mode  },
            { KeyLevel,    level       },
            { KeyBotCount, botCount    },
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"[MpRoomConfig] wrote mode={mode} level={level} bots={botCount}");
#endif
    }

    // ── Read (all clients) ───────────────────────────────────────────────────

    public static MpGameMode ReadMode()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyMode, out object raw))
            return (MpGameMode)(byte)raw;
#endif
        return MpGameMode.HybridChaos;
    }

    public static int ReadLevel()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyLevel, out object raw))
            return (int)raw;
#endif
        return GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
    }

    public static int ReadBotCount()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyBotCount, out object raw))
            return (int)raw;
#endif
        return DefaultBotCount;
    }

    public static bool ReadBotsEnabled()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyBotsEnabled, out object raw))
            return (bool)raw;
#endif
        return true;
    }

    public static bool ReadFriendlyFire()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyFriendlyFire, out object raw))
            return (bool)raw;
#endif
        return true;
    }

    public static byte ReadMatchState()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyMatchState, out object raw))
            return (byte)raw;
#endif
        return 0; // WaitingForPlayers
    }

    public static float ReadTimerDuration()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyTimerDuration, out object raw))
            return (float)raw;
#endif
        return 300f;
    }

    public static string ReadWinnerName()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyWinnerName, out object raw))
            return (string)raw;
#endif
        return string.Empty;
    }

    public static int ReadMaxPlayers()
    {
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KeyMaxPlayers, out object raw))
            return (int)raw;
#endif
        return 8;
    }

    /// <summary>
    /// Reads all properties from the room and applies them to local state.
    /// Call this on every client after joining a room.
    /// </summary>
    public static void ApplyToLocalState()
    {
        MpGameMode mode = ReadMode();
        MultiplayerMode.SetMode(mode);
        Debug.Log($"[MpRoomConfig] applied mode={mode} level={ReadLevel()} bots={ReadBotCount()} botsEnabled={ReadBotsEnabled()} friendlyFire={ReadFriendlyFire()} state={ReadMatchState()}");
    }
}
