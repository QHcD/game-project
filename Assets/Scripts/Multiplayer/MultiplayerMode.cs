using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

public static class MultiplayerMode
{
    public const string SinglePlayerSceneName  = "GameScene";
    public const string MultiplayerSceneName   = "MultiplayerGameScene";
    public const string NetworkPlayerPrefabPath = "FirstPersonMelee/Player";

    // Default level/loadout used when the room does not (yet) specify one.
    // Required because Single-Player saves a "ContinueLevel" PlayerPref that
    // used to leak into multiplayer (Level 8 Hammer instead of L1 Knife).
    public const int DefaultMultiplayerLevel = 1;

    public static bool       IsMultiplayer { get; private set; }
    public static MpGameMode ActiveMode    { get; private set; } = MpGameMode.PurePvP;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsForPlayMode()
    {
        IsMultiplayer = false;
        ActiveMode    = MpGameMode.PurePvP;
        MultiplayerRuntimeConfig.MultiplayerSelectedGameMode = MpGameMode.PurePvP;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitProcess()
    {
        // Photon must keep heartbeating when the app loses focus / a scene
        // changes, otherwise the server drops the connection
        // (DisconnectByServerTimeout / AppOutOfFocus / WinSock).
        Application.runInBackground = true;
#if PUN_2_OR_NEWER
        PhotonNetwork.KeepAliveInBackground = 60f;
#endif
    }

    public static void SetSinglePlayer()
    {
        IsMultiplayer = false;
        ActiveMode    = MpGameMode.PurePvP;
#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom || PhotonNetwork.IsConnected)
        {
            MultiplayerShutdownGuard.BeginLeave();
            if (PhotonNetwork.InRoom && PhotonNetwork.IsConnectedAndReady)
                PhotonNetwork.LeaveRoom();
        }
#endif
    }

    public static void SetMultiplayer()
    {
        IsMultiplayer = true;
        Application.runInBackground = true;
        MultiplayerShutdownGuard.ResetForNewSession();
    }

    /// <summary>
    /// Called by the multiplayer menu when the host picks a mode, and by
    /// MpRoomConfig after reading room properties so all clients share the
    /// same mode without extra RPCs.
    /// </summary>
    public static void SetMode(MpGameMode mode, bool logLocalSelection = true)
    {
        ActiveMode = mode;
        MultiplayerRuntimeConfig.MultiplayerSelectedGameMode = mode;
        if (logLocalSelection)
            Debug.Log($"[MPMode] selected mode = {mode}");
    }
}
