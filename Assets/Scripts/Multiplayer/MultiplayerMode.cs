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
    public static MpGameMode ActiveMode    { get; private set; } = MpGameMode.HybridChaos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsForPlayMode()
    {
        IsMultiplayer = false;
        ActiveMode    = MpGameMode.HybridChaos;
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
        ActiveMode    = MpGameMode.HybridChaos;
#if PUN_2_OR_NEWER
        // Hard-leave any room/lobby so single-player runs are never observed
        // by Photon (RPCs, ownership, and serialization stop immediately).
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
#endif
    }

    public static void SetMultiplayer()
    {
        IsMultiplayer = true;
        Application.runInBackground = true;
        MultiplayerShutdownGuard.ResetForNewSession();
    }

    /// <summary>
    /// Called by MpRoomConfig after reading room properties so all clients
    /// share the same mode without extra RPCs.
    /// </summary>
    public static void SetMode(MpGameMode mode)
    {
        ActiveMode = mode;
    }
}
