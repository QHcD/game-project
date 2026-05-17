using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
#endif

/// <summary>
/// Master-client-authoritative match lifecycle manager.
///
/// Responsibilities:
///   - Reads room config and applies mode to all clients via RPC.
///   - Kicks off MpBotDirector on master client for Co-op / Hybrid modes.
///   - Decides when the match ends and notifies all clients.
///   - Handles master-client migration (bot authority transfer).
///
/// Phase 1: wires up config reading + bot director bootstrap.
///          HybridChaos behaves identically to the current prototype.
/// </summary>
#if PUN_2_OR_NEWER
public class MpMatchController : MonoBehaviourPunCallbacks
#else
public class MpMatchController : MonoBehaviour
#endif
{
    public static MpMatchController Instance { get; private set; }

    // RPC key for room-property sync to late-joining clients
    private const string RpcSyncMode = nameof(RpcReceiveMode);

    private bool _matchStarted;

    // ── Bootstrap ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by NetworkPlayerSpawner on master client after scene load.
    /// Creates the controller if it doesn't exist yet.
    /// </summary>
    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("MpMatchController");
        go.AddComponent<MpMatchController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if PUN_2_OR_NEWER
        // PhotonView required for RPC sending.
        if (GetComponent<PhotonView>() == null)
        {
            PhotonView pv = gameObject.AddComponent<PhotonView>();
            pv.ViewID = 999; // reserved ID — safe for a manager-only object
        }
#endif
    }

    private void Start()
    {
#if PUN_2_OR_NEWER
        if (!PhotonNetwork.InRoom) return;
#endif
        // Apply room config to local state (mode/level/bots).
        MpRoomConfig.ApplyToLocalState();
        StartMatch();
    }

    private void StartMatch()
    {
        if (_matchStarted) return;
        _matchStarted = true;

        MpGameMode mode = MultiplayerMode.ActiveMode;
        Debug.Log($"[MpMatch] StartMatch mode={mode}");

#if PUN_2_OR_NEWER
        // Broadcast mode to all clients so late-joiners are also in sync.
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(RpcSyncMode, RpcTarget.Others, (byte)mode);
            BootBotDirector(mode);
        }
#endif
    }

    // ── Bot director ─────────────────────────────────────────────────────────

    private void BootBotDirector(MpGameMode mode)
    {
        if (mode == MpGameMode.PurePvP) return;   // No bots in PvP

        int botCount = MpRoomConfig.ReadBotCount();
        MpBotDirector.EnsureExists(mode, botCount);
    }

    // ── Photon callbacks ─────────────────────────────────────────────────────

#if PUN_2_OR_NEWER
    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        // Re-apply config whenever master updates room properties.
        if (changedProps.ContainsKey(MpRoomConfig.KeyMode))
            MpRoomConfig.ApplyToLocalState();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // If this client just became master, take over bot authority.
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[MpMatch] became master — taking bot authority");
            BootBotDirector(MultiplayerMode.ActiveMode);
        }
    }

    [PunRPC]
    private void RpcReceiveMode(byte rawMode)
    {
        MpGameMode mode = (MpGameMode)rawMode;
        MultiplayerMode.SetMode(mode);
        Debug.Log($"[MpMatch] RPC received mode={mode}");
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
