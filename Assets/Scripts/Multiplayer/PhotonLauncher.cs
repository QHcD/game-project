using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if PUN_2_OR_NEWER
using Photon.Pun;
using Photon.Realtime;
#endif

#if PUN_2_OR_NEWER
public class PhotonLauncher : MonoBehaviourPunCallbacks
#else
public class PhotonLauncher : MonoBehaviour
#endif
{
    private const string PhotonAppId = "1de73976-fe2a-4f0d-9947-b27313fd431a";

    [Header("UI")]
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI statusText;
    public Button connectButton;
    public Button joinRandomButton;
    public Button createRoomButton;

    [Header("Room")]
    public byte maxPlayers = 8;
    public string multiplayerSceneName = MultiplayerMode.MultiplayerSceneName;
    public bool autoJoinRandomAfterConnect = true;

    private bool pendingJoinRandom;
    private bool pendingCreateRoom;

    private void Awake()
    {
        MultiplayerMode.SetMultiplayer();
        if (playerNameInput != null && string.IsNullOrWhiteSpace(playerNameInput.text))
            playerNameInput.text = PlayerProfile.HasUsername ? PlayerProfile.Username : $"Player{Random.Range(1000, 9999)}";

        SetStatus("Enter a name, then play online.");
    }

    public void ConnectAndPlayOnline()
    {
        autoJoinRandomAfterConnect = true;
        pendingJoinRandom = true;
        pendingCreateRoom = false;
        Connect();
    }

    public void Connect()
    {
#if PUN_2_OR_NEWER
        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        SetButtonsInteractable(false);

        if (PhotonNetwork.IsConnectedAndReady)
        {
            // If we are already connected, ensure we're in the lobby before any room ops.
            if (!PhotonNetwork.InLobby)
            {
                SetStatus("Connecting... (joining lobby)");
                PhotonNetwork.JoinLobby();
                return;
            }

            // Already in lobby — execute any pending action.
            if (pendingCreateRoom)
            {
                CreateRoom();
                return;
            }
            if (pendingJoinRandom || autoJoinRandomAfterConnect)
            {
                JoinRandomRoom();
                return;
            }

            SetButtonsInteractable(true);
            SetStatus("Joined lobby");
            return;
        }

        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.PhotonServerSettings != null)
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = PhotonAppId;

        SetStatus("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
#else
        SetStatus("Photon PUN 2 is not imported. Import PUN 2, then press Play Online again.");
#endif
    }

    public void JoinRandomRoom()
    {
#if PUN_2_OR_NEWER
        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        autoJoinRandomAfterConnect = true;
        pendingJoinRandom = true;
        pendingCreateRoom = false;

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Connecting...");
            Connect();
            return;
        }

        if (!PhotonNetwork.InLobby)
        {
            SetButtonsInteractable(false);
            SetStatus("Connecting... (joining lobby)");
            PhotonNetwork.JoinLobby();
            return;
        }

        SetButtonsInteractable(false);
        SetStatus("Joining random room...");
        pendingJoinRandom = false;
        PhotonNetwork.JoinRandomRoom();
#else
        SetStatus("Photon PUN 2 is not imported.");
#endif
    }

    public void CreateRoom()
    {
#if PUN_2_OR_NEWER
        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        autoJoinRandomAfterConnect = false;
        pendingCreateRoom = true;
        pendingJoinRandom = false;

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Connecting...");
            Connect();
            return;
        }

        if (!PhotonNetwork.InLobby)
        {
            SetButtonsInteractable(false);
            SetStatus("Connecting... (joining lobby)");
            PhotonNetwork.JoinLobby();
            return;
        }

        SetButtonsInteractable(false);
        string roomName = $"PRISM7-{Random.Range(1000, 9999)}";
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers, IsOpen = true, IsVisible = true };
        SetStatus($"Creating room {roomName}...");
        pendingCreateRoom = false;
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
#else
        SetStatus("Photon PUN 2 is not imported.");
#endif
    }

#if PUN_2_OR_NEWER
    public override void OnConnectedToMaster()
    {
        SetButtonsInteractable(false);
        SetStatus("Connecting... (joining lobby)");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        SetStatus("Joined lobby");
        SetButtonsInteractable(true);

        // Execute any pending action once we're actually in the lobby.
        if (pendingCreateRoom)
        {
            CreateRoom();
            return;
        }
        if (pendingJoinRandom || autoJoinRandomAfterConnect)
        {
            JoinRandomRoom();
            return;
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // Common flow: no room exists yet. Create one with the requested max players.
        maxPlayers = 8;
        SetStatus("No room found. Creating room...");
        CreateRoom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetButtonsInteractable(true);
        SetStatus($"Create room failed: {message}");
    }

    public override void OnJoinedRoom()
    {
        MultiplayerMode.SetMultiplayer();
        SetStatus($"Joined room: {PhotonNetwork.CurrentRoom.Name}");

        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel(multiplayerSceneName);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetButtonsInteractable(true);
        SetStatus($"Disconnected: {cause}");
    }
#endif

    private void ApplyPlayerName()
    {
        string playerName = playerNameInput != null ? playerNameInput.text : string.Empty;
        playerName = string.IsNullOrWhiteSpace(playerName) ? $"Player{Random.Range(1000, 9999)}" : playerName.Trim();
        PlayerProfile.SetUsername(playerName);
#if PUN_2_OR_NEWER
        PhotonNetwork.NickName = playerName;
#endif
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (connectButton != null) connectButton.interactable = interactable;
        if (joinRandomButton != null) joinRandomButton.interactable = interactable;
        if (createRoomButton != null) createRoomButton.interactable = interactable;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log("[PhotonLauncher] " + message);
    }
}
