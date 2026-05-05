using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
#endif

#if PHOTON_UNITY_NETWORKING
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
#if PHOTON_UNITY_NETWORKING
        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        SetButtonsInteractable(false);

        if (PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Connected. Joining random room...");
            JoinRandomRoom();
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
#if PHOTON_UNITY_NETWORKING
        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        autoJoinRandomAfterConnect = true;

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            pendingJoinRandom = true;
            pendingCreateRoom = false;
            Connect();
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
#if PHOTON_UNITY_NETWORKING
        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        autoJoinRandomAfterConnect = false;

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            pendingJoinRandom = false;
            pendingCreateRoom = true;
            Connect();
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

#if PHOTON_UNITY_NETWORKING
    public override void OnConnectedToMaster()
    {
        SetButtonsInteractable(true);
        SetStatus("Connected to master.");
        PhotonNetwork.JoinLobby();

        if (pendingCreateRoom)
            CreateRoom();
        else if (pendingJoinRandom || autoJoinRandomAfterConnect)
            JoinRandomRoom();
    }

    public override void OnJoinedLobby()
    {
        SetStatus("In lobby. Join or create a room.");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        SetStatus("No room found. Creating one...");
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
#if PHOTON_UNITY_NETWORKING
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
