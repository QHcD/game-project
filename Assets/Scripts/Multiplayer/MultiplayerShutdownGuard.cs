using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
using Photon.Realtime;
#endif

public static class MultiplayerShutdownGuard
{
    public static bool IsLeavingMultiplayer { get; private set; }

    public static void BeginLeave()
    {
        if (IsLeavingMultiplayer) return;
        IsLeavingMultiplayer = true;
#if PUN_2_OR_NEWER
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.AutomaticallySyncScene = false;
#endif
        Debug.Log("[MPLeave] leaving multiplayer started");
    }

    public static void ResetForNewSession()
    {
        if (IsLeavingMultiplayer)
            IsLeavingMultiplayer = false;
    }

    public static bool CanWriteProperties(string context = "PhotonPropertyWrite", bool requireRoom = true)
    {
        if (IsLeavingMultiplayer)
        {
            Debug.Log("[MPLeave] skipped property write because leaving");
            Debug.Log("[MPPropWrite] blocked from " + context + " (IsLeavingMultiplayer)\n" + System.Environment.StackTrace);
            return false;
        }
#if PUN_2_OR_NEWER
        ClientState s = PhotonNetwork.NetworkClientState;
        if (s == ClientState.Leaving ||
            s == ClientState.Disconnecting ||
            s == ClientState.DisconnectingFromGameServer ||
            s == ClientState.DisconnectingFromMasterServer ||
            s == ClientState.DisconnectingFromNameServer ||
            s == ClientState.Disconnected)
        {
            Debug.Log("[MPLeave] skipped property write because leaving");
            Debug.Log("[MPPropWrite] blocked from " + context + " (client state = " + s + ")\n" + System.Environment.StackTrace);
            return false;
        }

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.Log("[MPPropWrite] blocked from " + context + " (not connected and ready)\n" + System.Environment.StackTrace);
            return false;
        }

        if (requireRoom && (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null))
        {
            Debug.Log("[MPPropWrite] blocked from " + context + " (not in room)\n" + System.Environment.StackTrace);
            return false;
        }
#endif
        return true;
    }

    public static void LogPropertyWrite(string context)
    {
        Debug.Log("[MPPropWrite] writing from " + context + "\n" + System.Environment.StackTrace);
    }
}
