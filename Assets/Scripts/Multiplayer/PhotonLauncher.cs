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

    [Header("Room Code Flow")]
    public TMP_InputField roomCodeInput;
    public Button joinByCodeButton;
    private bool useLobbyFlow;
    private bool pendingJoinByCode;

    private GameObject lobbyPanelInstance;
    private TextMeshProUGUI lobbyRoomCodeText;
    private TextMeshProUGUI lobbyGameModeText;
    private TextMeshProUGUI lobbyPlayerListText;
    private Button lobbyReadyButton;
    private Button lobbyStartMatchButton;
    private TextMeshProUGUI lobbyReadyButtonText;
    private Image lobbyReadyButtonImage;
    private Outline lobbyReadyButtonOutline;
    private TextMeshProUGUI lobbyStartMatchButtonText;

    [Header("Room")]
    public byte maxPlayers = 8;
    public string multiplayerSceneName = MultiplayerMode.MultiplayerSceneName;
    public bool autoJoinRandomAfterConnect = true;

    private bool pendingJoinRandom;
    private bool pendingCreateRoom;

    private void Awake()
    {
        PlayerProfile.Reload();
        if (playerNameInput != null && string.IsNullOrWhiteSpace(playerNameInput.text))
            playerNameInput.text = PlayerProfile.HasUsername ? PlayerProfile.Username : "Player";

        SetStatus("Enter a name, then play online.");
    }

    public void ConnectAndPlayOnline()
    {
        autoJoinRandomAfterConnect = true;
        pendingJoinRandom = true;
        pendingCreateRoom = false;
        pendingJoinByCode = false;
        useLobbyFlow = false;
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
            if (pendingJoinByCode)
            {
                JoinRoomWithCode();
                return;
            }
            if (pendingCreateRoom)
            {
                if (useLobbyFlow)
                    CreateRoomWithCode();
                else
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
        pendingJoinByCode = false;
        useLobbyFlow = false;

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
        pendingJoinByCode = false;
        useLobbyFlow = false;

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
        
        byte roomMaxPlayers = 8;
        int roomBotCount = 20;
        bool roomBotsEnabled = true;
        bool roomFriendlyFire = true;

        if (MultiplayerMode.ActiveMode == MpGameMode.CoopSurvival)
        {
            roomMaxPlayers = 4;
            roomBotCount = 25;
            roomBotsEnabled = true;
            roomFriendlyFire = false;
        }
        else if (MultiplayerMode.ActiveMode == MpGameMode.PurePvP)
        {
            roomMaxPlayers = 8;
            roomBotCount = 0;
            roomBotsEnabled = false;
            roomFriendlyFire = true;
        }
        else
        {
            roomMaxPlayers = 8;
            roomBotCount = 20;
            roomBotsEnabled = true;
            roomFriendlyFire = true;
        }

        RoomOptions options = new RoomOptions { MaxPlayers = roomMaxPlayers, IsOpen = true, IsVisible = true };
        options.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { MpRoomConfig.KeyMode, (byte)MultiplayerMode.ActiveMode },
            { MpRoomConfig.KeyLevel, GameManager.Instance != null ? GameManager.Instance.currentLevel : 1 },
            { MpRoomConfig.KeyBotCount, roomBotCount },
            { MpRoomConfig.KeyBotsEnabled, roomBotsEnabled },
            { MpRoomConfig.KeyFriendlyFire, roomFriendlyFire },
            { MpRoomConfig.KeyMatchState, (byte)0 }, // WaitingForPlayers
            { MpRoomConfig.KeyTimerDuration, 300f }, // 5 minutes
            { MpRoomConfig.KeyWinnerName, "" },
            { MpRoomConfig.KeyMaxPlayers, (int)roomMaxPlayers }
        };
        options.CustomRoomPropertiesForLobby = new string[] 
        { 
            MpRoomConfig.KeyMode, 
            MpRoomConfig.KeyLevel, 
            MpRoomConfig.KeyBotCount, 
            MpRoomConfig.KeyBotsEnabled,
            MpRoomConfig.KeyFriendlyFire,
            MpRoomConfig.KeyMatchState,
            MpRoomConfig.KeyTimerDuration,
            MpRoomConfig.KeyWinnerName,
            MpRoomConfig.KeyMaxPlayers
        };
        SetStatus($"Creating room {roomName}...");
        pendingCreateRoom = false;
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
#else
        SetStatus("Photon PUN 2 is not imported.");
#endif
    }

    public void CreateRoomWithCode()
    {
#if PUN_2_OR_NEWER
        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        autoJoinRandomAfterConnect = false;
        pendingCreateRoom = true;
        pendingJoinRandom = false;
        pendingJoinByCode = false;
        useLobbyFlow = true;

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
        string roomCode = GenerateRoomCode();
        
        byte roomMaxPlayers = 8;
        int roomBotCount = 20;
        bool roomBotsEnabled = true;
        bool roomFriendlyFire = true;

        if (MultiplayerMode.ActiveMode == MpGameMode.CoopSurvival)
        {
            roomMaxPlayers = 4;
            roomBotCount = 25;
            roomBotsEnabled = true;
            roomFriendlyFire = false;
        }
        else if (MultiplayerMode.ActiveMode == MpGameMode.PurePvP)
        {
            roomMaxPlayers = 8;
            roomBotCount = 0;
            roomBotsEnabled = false;
            roomFriendlyFire = true;
        }
        else
        {
            roomMaxPlayers = 8;
            roomBotCount = 20;
            roomBotsEnabled = true;
            roomFriendlyFire = true;
        }

        RoomOptions options = new RoomOptions { MaxPlayers = roomMaxPlayers, IsOpen = true, IsVisible = true };
        options.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { MpRoomConfig.KeyMode, (byte)MultiplayerMode.ActiveMode },
            { MpRoomConfig.KeyLevel, GameManager.Instance != null ? GameManager.Instance.currentLevel : 1 },
            { MpRoomConfig.KeyBotCount, roomBotCount },
            { MpRoomConfig.KeyBotsEnabled, roomBotsEnabled },
            { MpRoomConfig.KeyFriendlyFire, roomFriendlyFire },
            { MpRoomConfig.KeyMatchState, (byte)0 }, // WaitingForPlayers
            { MpRoomConfig.KeyTimerDuration, 300f }, // 5 minutes
            { MpRoomConfig.KeyWinnerName, "" },
            { MpRoomConfig.KeyMaxPlayers, (int)roomMaxPlayers }
        };
        options.CustomRoomPropertiesForLobby = new string[] 
        { 
            MpRoomConfig.KeyMode, 
            MpRoomConfig.KeyLevel, 
            MpRoomConfig.KeyBotCount, 
            MpRoomConfig.KeyBotsEnabled,
            MpRoomConfig.KeyFriendlyFire,
            MpRoomConfig.KeyMatchState,
            MpRoomConfig.KeyTimerDuration,
            MpRoomConfig.KeyWinnerName,
            MpRoomConfig.KeyMaxPlayers
        };
        SetStatus($"Creating room {roomCode}...");
        pendingCreateRoom = false;
        PhotonNetwork.CreateRoom(roomCode, options, TypedLobby.Default);
#else
        SetStatus("Photon PUN 2 is not imported.");
#endif
    }

    public void JoinRoomWithCode()
    {
#if PUN_2_OR_NEWER
        if (roomCodeInput == null || string.IsNullOrWhiteSpace(roomCodeInput.text))
        {
            SetStatus("Enter a room code first!");
            return;
        }
        string code = roomCodeInput.text.Trim().ToUpper();
        if (code.Length != 6)
        {
            SetStatus("Room code must be exactly 6 characters.");
            return;
        }

        MultiplayerMode.SetMultiplayer();
        ApplyPlayerName();
        autoJoinRandomAfterConnect = false;
        pendingJoinRandom = false;
        pendingCreateRoom = false;
        pendingJoinByCode = true;
        useLobbyFlow = true;

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
        SetStatus($"Joining room {code}...");
        pendingJoinByCode = false;
        PhotonNetwork.JoinRoom(code);
#else
        SetStatus("Photon PUN 2 is not imported.");
#endif
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] stringChars = new char[6];
        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[Random.Range(0, chars.Length)];
        }
        return new string(stringChars);
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

        if (pendingJoinByCode)
        {
            JoinRoomWithCode();
            return;
        }

        // Execute any pending action once we're actually in the lobby.
        if (pendingCreateRoom)
        {
            if (useLobbyFlow)
                CreateRoomWithCode();
            else
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
        if (useLobbyFlow && returnCode == 32766) // ErrorCode.RoomAlreadyExists is 32766
        {
            SetStatus("Room code collision, retrying with new code...");
            CreateRoomWithCode();
        }
        else
        {
            SetButtonsInteractable(true);
            SetStatus($"Create room failed: {message}");
        }
    }

    public override void OnJoinedRoom()
    {
        MultiplayerMode.SetMultiplayer();
        SetStatus($"Joined room: {PhotonNetwork.CurrentRoom.Name}");

        MpRoomConfig.ApplyToLocalState();

        SetReadyState(false);

        if (useLobbyFlow)
        {
            ShowLobbyUI();
        }
        else
        {
            // Hide the launcher canvas for every client immediately — non-master
            // clients do not call LoadLevel so the menu would stay visible and
            // block input until the scene finishes loading.
            HideLauncherCanvas();

            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.LoadLevel(multiplayerSceneName);
        }
    }

    private void HideLauncherCanvas()
    {
        // Walk up to the root canvas that contains this launcher.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
            Debug.Log("[MPUI] multiplayer menu hidden");
            return;
        }
        // Fallback: disable any Canvas children of this GameObject.
        Canvas[] children = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas c in children)
            c.gameObject.SetActive(false);
        if (children.Length > 0)
            Debug.Log("[MPUI] multiplayer menu hidden");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetButtonsInteractable(true);
        SetStatus($"Disconnected: {cause}");
        if (useLobbyFlow)
        {
            HideLobbyUI();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (useLobbyFlow && changedProps.ContainsKey("rd"))
        {
            RefreshLobbyUI();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (useLobbyFlow)
        {
            RefreshLobbyUI();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (useLobbyFlow)
        {
            RefreshLobbyUI();
        }
    }

    public override void OnLeftRoom()
    {
        if (useLobbyFlow)
        {
            HideLobbyUI();
        }
    }

    private void ShowLobbyUI()
    {
        // 1. Hide the MultiplayerPanel
        GameObject mpPanel = GetMultiplayerPanel();
        if (mpPanel != null)
            mpPanel.SetActive(false);

        // 2. Create the LobbyPanel
        if (lobbyPanelInstance != null)
            Destroy(lobbyPanelInstance);

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Lobby] Root canvas not found!");
            return;
        }

        lobbyPanelInstance = new GameObject("LobbyPanel");
        lobbyPanelInstance.transform.SetParent(canvas.transform, false);
        
        Image panelImg = lobbyPanelInstance.AddComponent<Image>();
        panelImg.color = new Color(0.06f, 0.10f, 0.22f, 0.96f);
        
        Outline panelOutline = lobbyPanelInstance.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.30f, 0.55f, 1f, 0.90f);
        panelOutline.effectDistance = new Vector2(3f, -3f);
        
        RectTransform panelRect = lobbyPanelInstance.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1040f, 820f);
        panelRect.anchoredPosition = new Vector2(0f, -8f);

        // Resolve Font
        TMP_FontAsset customFont = null;
        if (playerNameInput != null && playerNameInput.textComponent != null)
            customFont = playerNameInput.textComponent.font;

        // Title
        GameObject titleObj = new GameObject("LobbyTitle");
        titleObj.transform.SetParent(lobbyPanelInstance.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "MULTIPLAYER LOBBY";
        titleText.fontSize = 56;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        if (customFont != null) titleText.font = customFont;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.85f);
        titleRect.anchorMax = new Vector2(0.95f, 0.95f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

        // Room Code Container
        GameObject codeObj = new GameObject("RoomCodeText");
        codeObj.transform.SetParent(lobbyPanelInstance.transform, false);
        lobbyRoomCodeText = codeObj.AddComponent<TextMeshProUGUI>();
        lobbyRoomCodeText.text = "ROOM CODE: LOADING...";
        lobbyRoomCodeText.fontSize = 36;
        lobbyRoomCodeText.color = Color.white;
        lobbyRoomCodeText.fontStyle = FontStyles.Bold;
        lobbyRoomCodeText.alignment = TextAlignmentOptions.Center;
        if (customFont != null) lobbyRoomCodeText.font = customFont;
        RectTransform codeRect = codeObj.GetComponent<RectTransform>();
        codeRect.anchorMin = new Vector2(0.05f, 0.72f);
        codeRect.anchorMax = new Vector2(0.95f, 0.82f);
        codeRect.offsetMin = codeRect.offsetMax = Vector2.zero;

        // Game Mode Container
        GameObject modeObj = new GameObject("LobbyGameModeText");
        modeObj.transform.SetParent(lobbyPanelInstance.transform, false);
        lobbyGameModeText = modeObj.AddComponent<TextMeshProUGUI>();
        lobbyGameModeText.text = "GAME MODE: CO-OP SURVIVAL";
        lobbyGameModeText.fontSize = 24;
        lobbyGameModeText.color = new Color(0.3f, 0.8f, 1f, 1f);
        lobbyGameModeText.fontStyle = FontStyles.Bold;
        lobbyGameModeText.alignment = TextAlignmentOptions.Center;
        if (customFont != null) lobbyGameModeText.font = customFont;
        RectTransform modeRect = modeObj.GetComponent<RectTransform>();
        modeRect.anchorMin = new Vector2(0.05f, 0.63f);
        modeRect.anchorMax = new Vector2(0.95f, 0.70f);
        modeRect.offsetMin = modeRect.offsetMax = Vector2.zero;

        // Player List Container
        GameObject listObj = new GameObject("PlayerListText");
        listObj.transform.SetParent(lobbyPanelInstance.transform, false);
        lobbyPlayerListText = listObj.AddComponent<TextMeshProUGUI>();
        lobbyPlayerListText.text = "";
        lobbyPlayerListText.fontSize = 24;
        lobbyPlayerListText.color = new Color(0.85f, 0.92f, 1f, 0.95f);
        lobbyPlayerListText.alignment = TextAlignmentOptions.TopLeft;
        if (customFont != null) lobbyPlayerListText.font = customFont;
        RectTransform listRect = listObj.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.15f, 0.25f);
        listRect.anchorMax = new Vector2(0.85f, 0.61f);
        listRect.offsetMin = listRect.offsetMax = Vector2.zero;

        // ── Ready Button ──
        GameObject readyBtnObj = new GameObject("ReadyButton");
        readyBtnObj.transform.SetParent(lobbyPanelInstance.transform, false);
        lobbyReadyButtonImage = readyBtnObj.AddComponent<Image>();
        lobbyReadyButtonImage.color = new Color(0.4f, 0.05f, 0.15f, 0.9f);
        lobbyReadyButtonOutline = readyBtnObj.AddComponent<Outline>();
        lobbyReadyButtonOutline.effectColor = new Color(1.0f, 0.2f, 0.4f, 0.9f);
        lobbyReadyButtonOutline.effectDistance = new Vector2(1.5f, -1.5f);
        
        lobbyReadyButton = readyBtnObj.AddComponent<Button>();
        lobbyReadyButton.targetGraphic = lobbyReadyButtonImage;
        lobbyReadyButton.onClick.AddListener(() => {
            SetReadyState(!IsPlayerReady(PhotonNetwork.LocalPlayer));
        });
        
        RectTransform readyRect = readyBtnObj.GetComponent<RectTransform>();
        readyRect.anchorMin = new Vector2(0.15f, 0.12f);
        readyRect.anchorMax = new Vector2(0.48f, 0.20f);
        readyRect.offsetMin = readyRect.offsetMax = Vector2.zero;

        GameObject readyBtnTextObj = new GameObject("ReadyButtonText");
        readyBtnTextObj.transform.SetParent(readyBtnObj.transform, false);
        lobbyReadyButtonText = readyBtnTextObj.AddComponent<TextMeshProUGUI>();
        lobbyReadyButtonText.text = "PRESS READY";
        lobbyReadyButtonText.fontSize = 28;
        lobbyReadyButtonText.color = Color.white;
        lobbyReadyButtonText.fontStyle = FontStyles.Bold;
        lobbyReadyButtonText.alignment = TextAlignmentOptions.Center;
        if (customFont != null) lobbyReadyButtonText.font = customFont;
        RectTransform readyTextRect = readyBtnTextObj.GetComponent<RectTransform>();
        readyTextRect.anchorMin = Vector2.zero;
        readyTextRect.anchorMax = Vector2.one;
        readyTextRect.offsetMin = readyTextRect.offsetMax = Vector2.zero;

        // ── Start Match Button ──
        GameObject startBtnObj = new GameObject("StartMatchButton");
        startBtnObj.transform.SetParent(lobbyPanelInstance.transform, false);
        Image startBtnImg = startBtnObj.AddComponent<Image>();
        startBtnImg.color = new Color(0.3f, 0.1f, 0.5f, 0.9f);
        Outline startBtnOutline = startBtnObj.AddComponent<Outline>();
        startBtnOutline.effectColor = new Color(0.7f, 0.3f, 1.0f, 0.9f);
        startBtnOutline.effectDistance = new Vector2(1.5f, -1.5f);
        
        lobbyStartMatchButton = startBtnObj.AddComponent<Button>();
        lobbyStartMatchButton.targetGraphic = startBtnImg;
        lobbyStartMatchButton.onClick.AddListener(StartMatch);
        
        RectTransform startRect = startBtnObj.GetComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.52f, 0.12f);
        startRect.anchorMax = new Vector2(0.85f, 0.20f);
        startRect.offsetMin = startRect.offsetMax = Vector2.zero;

        GameObject startBtnTextObj = new GameObject("StartMatchButtonText");
        startBtnTextObj.transform.SetParent(startBtnObj.transform, false);
        lobbyStartMatchButtonText = startBtnTextObj.AddComponent<TextMeshProUGUI>();
        lobbyStartMatchButtonText.text = "START MATCH";
        lobbyStartMatchButtonText.fontSize = 22;
        lobbyStartMatchButtonText.color = Color.white;
        lobbyStartMatchButtonText.fontStyle = FontStyles.Bold;
        lobbyStartMatchButtonText.alignment = TextAlignmentOptions.Center;
        if (customFont != null) lobbyStartMatchButtonText.font = customFont;
        RectTransform startTextRect = startBtnTextObj.GetComponent<RectTransform>();
        startTextRect.anchorMin = Vector2.zero;
        startTextRect.anchorMax = Vector2.one;
        startTextRect.offsetMin = startTextRect.offsetMax = Vector2.zero;

        // ── Leave Lobby Button ──
        GameObject leaveBtnObj = new GameObject("LeaveLobbyButton");
        leaveBtnObj.transform.SetParent(lobbyPanelInstance.transform, false);
        Image leaveBtnImg = leaveBtnObj.AddComponent<Image>();
        leaveBtnImg.color = new Color(0.15f, 0.18f, 0.25f, 0.9f);
        Outline leaveBtnOutline = leaveBtnObj.AddComponent<Outline>();
        leaveBtnOutline.effectColor = new Color(0.4f, 0.45f, 0.55f, 0.9f);
        leaveBtnOutline.effectDistance = new Vector2(1.5f, -1.5f);
        
        Button leaveBtn = leaveBtnObj.AddComponent<Button>();
        leaveBtn.targetGraphic = leaveBtnImg;
        leaveBtn.onClick.AddListener(LeaveLobby);
        
        RectTransform leaveRect = leaveBtnObj.GetComponent<RectTransform>();
        leaveRect.anchorMin = new Vector2(0.38f, 0.02f);
        leaveRect.anchorMax = new Vector2(0.62f, 0.08f);
        leaveRect.offsetMin = leaveRect.offsetMax = Vector2.zero;

        GameObject leaveBtnTextObj = new GameObject("LeaveLobbyButtonText");
        leaveBtnTextObj.transform.SetParent(leaveBtnObj.transform, false);
        TextMeshProUGUI leaveBtnText = leaveBtnTextObj.AddComponent<TextMeshProUGUI>();
        leaveBtnText.text = "LEAVE LOBBY";
        leaveBtnText.fontSize = 20;
        leaveBtnText.color = Color.white;
        leaveBtnText.fontStyle = FontStyles.Bold;
        leaveBtnText.alignment = TextAlignmentOptions.Center;
        if (customFont != null) leaveBtnText.font = customFont;
        RectTransform leaveTextRect = leaveBtnTextObj.GetComponent<RectTransform>();
        leaveTextRect.anchorMin = Vector2.zero;
        leaveTextRect.anchorMax = Vector2.one;
        leaveTextRect.offsetMin = leaveTextRect.offsetMax = Vector2.zero;

        // Refresh initially
        RefreshLobbyUI();
    }

    private void HideLobbyUI()
    {
        if (lobbyPanelInstance != null)
        {
            Destroy(lobbyPanelInstance);
            lobbyPanelInstance = null;
        }

        GameObject mpPanel = GetMultiplayerPanel();
        if (mpPanel != null)
            mpPanel.SetActive(true);
            
        SetButtonsInteractable(true);
    }

    private void RefreshLobbyUI()
    {
        if (lobbyPanelInstance == null) return;

        // 1. Update room code display
        if (PhotonNetwork.InRoom)
        {
            lobbyRoomCodeText.text = $"ROOM CODE: <color=#38F>{PhotonNetwork.CurrentRoom.Name}</color>";
        }

        // 1.5 Update game mode display
        if (lobbyGameModeText != null)
        {
            MpGameMode activeMode = MpRoomConfig.ReadMode();
            string modeName = "HYBRID CHAOS";
            if (activeMode == MpGameMode.CoopSurvival) modeName = "CO-OP SURVIVAL";
            else if (activeMode == MpGameMode.PurePvP) modeName = "PURE PVP";

            lobbyGameModeText.text = $"GAME MODE: <color=#38F>{modeName}</color>";
        }

        // 2. Refresh Player List
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=28><color=#7AC>OPERATORS IN LOBBY:</color></size>\n");

        bool allReady = true;
        Player[] players = PhotonNetwork.PlayerList;
        foreach (Player p in players)
        {
            bool isHost = p.IsMasterClient;
            bool isReady = IsPlayerReady(p);
            
            if (!isReady)
                allReady = false;

            string hostTag = isHost ? " <color=#93F>[HOST]</color>" : "";
            string readyTag = isReady 
                ? " <color=#0F9>[READY]</color>" 
                : " <color=#F36>[NOT READY]</color>";

            sb.AppendLine($"<size=26>• {p.NickName}{hostTag} — {readyTag}</size>");
        }
        lobbyPlayerListText.text = sb.ToString();

        // 3. Update local player's ready button visual state
        bool localReady = IsPlayerReady(PhotonNetwork.LocalPlayer);
        if (lobbyReadyButtonText != null)
        {
            lobbyReadyButtonText.text = localReady ? "READY" : "PRESS READY";
        }
        if (lobbyReadyButtonImage != null)
        {
            lobbyReadyButtonImage.color = localReady 
                ? new Color(0.0f, 0.4f, 0.2f, 0.9f) 
                : new Color(0.4f, 0.05f, 0.15f, 0.9f);
        }
        if (lobbyReadyButtonOutline != null)
        {
            lobbyReadyButtonOutline.effectColor = localReady 
                ? new Color(0.0f, 1.0f, 0.6f, 0.9f) 
                : new Color(1.0f, 0.2f, 0.4f, 0.9f);
        }

        // 4. Update Start Match button (Master Client only)
        if (PhotonNetwork.IsMasterClient)
        {
            lobbyStartMatchButton.gameObject.SetActive(true);

            MpGameMode activeMode = MpRoomConfig.ReadMode();
            int minPlayersRequired = 2;
            if (activeMode == MpGameMode.HybridChaos)
            {
                minPlayersRequired = 1;
            }

            int playerCount = players.Length;
            bool minPlayersMet = playerCount >= minPlayersRequired;

            if (!minPlayersMet)
            {
                lobbyStartMatchButton.interactable = false;
                if (lobbyStartMatchButtonText != null)
                {
                    lobbyStartMatchButtonText.text = $"WAITING FOR PLAYERS ({playerCount}/{minPlayersRequired})";
                }
                Image startImg = lobbyStartMatchButton.GetComponent<Image>();
                if (startImg != null) startImg.color = new Color(0.2f, 0.1f, 0.25f, 0.8f);
            }
            else if (!allReady)
            {
                lobbyStartMatchButton.interactable = false;
                if (lobbyStartMatchButtonText != null)
                {
                    lobbyStartMatchButtonText.text = "WAITING FOR READY";
                }
                Image startImg = lobbyStartMatchButton.GetComponent<Image>();
                if (startImg != null) startImg.color = new Color(0.25f, 0.15f, 0.35f, 0.85f);
            }
            else
            {
                lobbyStartMatchButton.interactable = true;
                if (lobbyStartMatchButtonText != null)
                {
                    lobbyStartMatchButtonText.text = "START MATCH";
                }
                Image startImg = lobbyStartMatchButton.GetComponent<Image>();
                if (startImg != null) startImg.color = new Color(0.4f, 0.1f, 0.7f, 1.0f);
            }
        }
        else
        {
            lobbyStartMatchButton.gameObject.SetActive(false);
        }
    }

    private bool IsPlayerReady(Player player)
    {
        if (player.CustomProperties.TryGetValue("rd", out object raw))
            return (bool)raw;
        return false;
    }

    private void SetReadyState(bool ready)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props["rd"] = ready;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public void StartMatch()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            SetStatus("Starting match...");
            
            // Hide Lobby UI right before scene sync so there is no visual clutter
            HideLobbyUI();
            HideLauncherCanvas();

            PhotonNetwork.LoadLevel(multiplayerSceneName);
        }
    }

    public void LeaveLobby()
    {
        if (PhotonNetwork.InRoom)
        {
            SetReadyState(false);
            PhotonNetwork.LeaveRoom();
        }
        HideLobbyUI();
    }

    private GameObject GetMultiplayerPanel()
    {
        if (transform.parent != null)
        {
            Transform t = transform.parent.Find("MultiplayerPanel");
            if (t != null) return t.gameObject;
        }
        return null;
    }
#endif

    private void ApplyPlayerName()
    {
        PlayerProfile.Reload();
        string rawName = playerNameInput != null ? playerNameInput.text : string.Empty;
        string playerName = PlayerProfile.Sanitize(rawName);
        if (string.IsNullOrWhiteSpace(playerName) && PlayerProfile.HasUsername)
            playerName = PlayerProfile.Username;
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = PlayerProfile.DefaultUsername;

        PlayerProfile.SetUsername(playerName);
        PlayerProfile.Reload();
#if PUN_2_OR_NEWER
        PhotonNetwork.NickName = PlayerProfile.Username;
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
