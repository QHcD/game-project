using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
using Photon.Realtime;
#endif

/// <summary>
/// Global guard against late Photon property writes during multiplayer exit.
/// QUIT from the pause menu calls LeaveRoom() → scene unload → MainMenu load;
/// any SetCustomProperties / SetPlayerCustomProperties that fires in that
/// window logs Photon's
///   "Operation SetProperties (252) not called because client is not
///    connected or not ready yet, client state: Leaving"
/// warning. Every property-write call site routes through
/// <see cref="CanWriteProperties"/> first.
/// </summary>
public static class MultiplayerShutdownGuard
{
    /// <summary>
    /// True between the moment QUIT presses LeaveRoom() and the moment the
    /// player re-enters multiplayer (MultiplayerMode.SetMultiplayer).
    /// </summary>
    public static bool IsLeavingMultiplayer { get; private set; }

    /// <summary>Marks the start of a multiplayer leave + main-menu load.</summary>
    public static void BeginLeave()
    {
        if (IsLeavingMultiplayer) return;
        IsLeavingMultiplayer = true;
        Debug.Log("[MPLeave] leaving multiplayer started");
    }

    /// <summary>Reset on (re)entering multiplayer so writes are allowed again.</summary>
    public static void ResetForNewSession()
    {
        if (IsLeavingMultiplayer)
            IsLeavingMultiplayer = false;
    }

    /// <summary>
    /// All call sites that write room/player custom properties must early-out
    /// on `if (!MultiplayerShutdownGuard.CanWriteProperties()) return;`.
    /// </summary>
    public static bool CanWriteProperties()
    {
        if (IsLeavingMultiplayer)
        {
            Debug.Log("[MPPropWrite] BLOCKED (IsLeavingMultiplayer)\n" + System.Environment.StackTrace);
            return false;
        }
#if PUN_2_OR_NEWER
        if (!PhotonNetwork.InRoom) return false;
        ClientState s = PhotonNetwork.NetworkClientState;
        if (s == ClientState.Leaving ||
            s == ClientState.Disconnecting ||
            s == ClientState.DisconnectingFromGameServer ||
            s == ClientState.DisconnectingFromMasterServer ||
            s == ClientState.DisconnectingFromNameServer ||
            s == ClientState.Disconnected)
        {
            Debug.Log("[MPPropWrite] BLOCKED (client state = " + s + ")\n" + System.Environment.StackTrace);
            return false;
        }
#endif
        return true;
    }
}
