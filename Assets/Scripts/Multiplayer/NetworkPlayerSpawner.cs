using System.Collections;
using UnityEngine;
using UnityEngine.AI;

#if PUN_2_OR_NEWER
using Photon.Pun;
using Photon.Realtime;
#endif

#if PUN_2_OR_NEWER
public class NetworkPlayerSpawner : MonoBehaviourPunCallbacks
#else
public class NetworkPlayerSpawner : MonoBehaviour
#endif
{
    public string playerPrefabPath = MultiplayerMode.NetworkPlayerPrefabPath;
    public float spawnSearchRadius = 18f;
    public float navMeshSampleRadius = 4f;
    public float capsuleRadius = 0.45f;
    public float capsuleHeight = 1.8f;
    public float obstacleClearance = 0.35f;
    public LayerMask obstacleMask = ~0;
    private static bool statsResetForCurrentMultiplayerScene;

    // Same fix as PhotonServiceRunner: AfterSceneLoad fires once at startup on the
    // menu (IsMultiplayer false), so the spawner was never created when the
    // multiplayer scene loaded → scene Player at its editor position was used →
    // player appeared outside/under the map. Subscribe via BeforeSceneLoad so
    // OnAnySceneLoaded fires for every subsequent scene transition.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnAnySceneLoaded;
    }

    private static void OnAnySceneLoaded(
        UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!MultiplayerMode.IsMultiplayer)
            return;

        if (scene.name != MultiplayerMode.MultiplayerSceneName)
            return;

        if (FindFirstObjectByType<NetworkPlayerSpawner>() != null)
            return;

        new GameObject("NetworkPlayerSpawner").AddComponent<NetworkPlayerSpawner>();
    }

    private void Start()
    {
        MultiplayerMode.SetMultiplayer();
        if (!statsResetForCurrentMultiplayerScene && MatchStatsManager.Instance != null)
        {
            MatchStatsManager.Instance.ResetMatch();
            statsResetForCurrentMultiplayerScene = true;
        }
        // DisableScenePlayer is deferred to after the local Photon player is
        // confirmed spawned so the scene camera stays alive during the spawn
        // window. If spawn fails the scene player remains as a fallback.
        DisableAiEnemies();

#if PUN_2_OR_NEWER
        if (PhotonNetwork.InRoom)
        {
            EnsureLocalNicknameFallback();
            RegisterPhotonPlayersForLeaderboard();
            SpawnLocalPlayer();
        }
#else
        Debug.LogWarning("[NetworkPlayerSpawner] Photon PUN 2 is not imported. Multiplayer spawning is disabled.");
#endif
    }

#if PUN_2_OR_NEWER
    public override void OnJoinedRoom()
    {
        EnsureLocalNicknameFallback();
        RegisterPhotonPlayersForLeaderboard();
        SpawnLocalPlayer();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RegisterPhotonPlayerForLeaderboard(newPlayer);
    }

    private void SpawnLocalPlayer()
    {
        if (FindOwnedPlayer() != null)
            return;

        Debug.Log($"[PhotonSpawn] prefab path={playerPrefabPath}");

        // Validate prefab is loadable from Resources before instantiating.
        GameObject prefabCheck = Resources.Load<GameObject>(playerPrefabPath);
        bool pvReady = prefabCheck != null && prefabCheck.GetComponent<PhotonView>() != null;
        Debug.Log($"[PhotonSpawn] PhotonView ready={pvReady}");

        if (!pvReady)
        {
            Debug.LogError($"[PhotonSpawn] Player prefab at Resources/{playerPrefabPath} is missing a PhotonView. " +
                           "Run PRISM/Multiplayer/Fix Photon Player Prefab from the Unity menu to add it.");
            return;
        }

        Debug.Log("[PhotonSpawn] spawning local player");
        Vector3 spawn = ResolveSpawnPosition();
        GameObject spawned = PhotonNetwork.Instantiate(playerPrefabPath, spawn, Quaternion.identity);

        if (spawned != null)
        {
            Debug.Log($"[PhotonSpawn] spawned local player {spawned.name} at {spawn}");
            // Only now disable the scene-placed player — the Photon player's
            // camera is live so there is no black-screen window.
            DisableScenePlayer();
            LogCameraState();
            StartCoroutine(FinalizeLocalPlayerSpawn(spawned));
        }
        else
        {
            // Spawn failed — leave scene player active so the screen is not black.
            Debug.LogError("[PhotonSpawn] PhotonNetwork.Instantiate returned null. Keeping scene player/camera active.");
        }
    }

    private IEnumerator FinalizeLocalPlayerSpawn(GameObject spawned)
    {
        // Let PlayerController.Start finish third-person body + camera setup first.
        yield return null;

        if (spawned == null)
            yield break;

        PlayerController pc = spawned.GetComponent<PlayerController>();
        PlayerHealth ph = spawned.GetComponent<PlayerHealth>();

        if (pc != null)
            pc.ForceEquipLevelWeaponForMultiplayer();

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.InitForMultiplayerLocalPlayer(pc, ph);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[MPFlow] local player ready");
            Debug.Log("[MPFlow] gameplay state active");
            Debug.Log("[MPFlow] hiding match stats");
            Debug.Log("[MPFlow] input enabled");
        }
    }

    private static void LogCameraState()
    {
        Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int enabledCount = 0;
        foreach (Camera c in allCams)
            if (c.enabled) enabledCount++;
        Debug.Log($"[MPDebug] active cameras count={enabledCount}");
    }

    private GameObject FindOwnedPlayer()
    {
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view != null && view.IsMine && view.GetComponent<PlayerController>() != null)
                return view.gameObject;
        }
        return null;
    }

    private void EnsureLocalNicknameFallback()
    {
        if (PhotonNetwork.LocalPlayer != null && string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            PhotonNetwork.NickName = $"Player_{PhotonNetwork.LocalPlayer.ActorNumber}";
    }

    private void RegisterPhotonPlayersForLeaderboard()
    {
        if (PhotonNetwork.PlayerList == null)
            return;

        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
            RegisterPhotonPlayerForLeaderboard(PhotonNetwork.PlayerList[i]);
    }

    private void RegisterPhotonPlayerForLeaderboard(Player player)
    {
        if (player == null || MatchStatsManager.Instance == null)
            return;

        string displayName = string.IsNullOrWhiteSpace(player.NickName)
            ? $"Player_{player.ActorNumber}"
            : player.NickName;
        MatchStatsManager.Instance.RegisterCombatant($"photon:{player.ActorNumber}", displayName, isPlayer: true);
        Debug.Log($"[MPHUD] registered player actor={player.ActorNumber} name={displayName}");
    }
#endif

    private Vector3 ResolveSpawnPosition()
    {
        Transform[] spawnPoints = FindSpawnPoints();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int index = (i + Random.Range(0, Mathf.Max(1, spawnPoints.Length))) % spawnPoints.Length;
            if (TryGetSafePoint(spawnPoints[index].position, out Vector3 safe))
                return safe;
        }

        Vector3 origin = transform.position;
        float golden = 137.50776f * Mathf.Deg2Rad;
        for (int i = 0; i < 64; i++)
        {
            float radius = Mathf.Lerp(0.5f, spawnSearchRadius, Mathf.Sqrt(i / 63f));
            float angle = i * golden;
            Vector3 candidate = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (TryGetSafePoint(candidate, out Vector3 safe))
                return safe;
        }

        // Last resort: raycast straight down from high above the spawner origin
        // to land on actual geometry, preventing spawn underground/out of bounds.
        Vector3 above = new Vector3(origin.x, origin.y + 300f, origin.z);
        if (Physics.Raycast(above, Vector3.down, out RaycastHit groundHit, 600f, ~0, QueryTriggerInteraction.Ignore))
            return groundHit.point + Vector3.up * 0.1f;

        return origin + Vector3.up;
    }

    private Transform[] FindSpawnPoints()
    {
        GameObject[] tagged = GameObject.FindGameObjectsWithTag("PlayerSpawn");
        if (tagged.Length > 0)
        {
            Transform[] result = new Transform[tagged.Length];
            for (int i = 0; i < tagged.Length; i++)
                result[i] = tagged[i].transform;
            return result;
        }

        GameObject root = GameObject.Find("SpawnPoints") ?? GameObject.Find("PlayerSpawns");
        if (root != null)
            return root.GetComponentsInChildren<Transform>(false);

        return new[] { transform };
    }

    private bool TryGetSafePoint(Vector3 candidate, out Vector3 safe)
    {
        safe = candidate;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            return false;

        safe = hit.position + Vector3.up * 0.05f;
        Vector3 center = safe + Vector3.up * (capsuleHeight * 0.5f);
        Vector3 bottom = center - Vector3.up * (capsuleHeight * 0.5f - capsuleRadius);
        Vector3 top = center + Vector3.up * (capsuleHeight * 0.5f - capsuleRadius);

        if (Physics.CheckCapsule(bottom, top, capsuleRadius, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return !Physics.CheckSphere(center, capsuleRadius + obstacleClearance, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private void DisableScenePlayer()
    {
        // Disable any PlayerController that has no PhotonView — these are
        // scene-placed (non-network) players baked into the editor scene.
        // Leaving them active caused the local player to be the scene object
        // at its editor position (often 0,0,0 / underground) instead of the
        // PhotonNetwork.Instantiate-spawned one.
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerController pc in players)
        {
#if PUN_2_OR_NEWER
            if (pc.GetComponent<PhotonView>() == null)
            {
                pc.gameObject.SetActive(false);
                Debug.Log("[NetworkPlayerSpawner] Disabled scene Player: " + pc.gameObject.name);
            }
#else
            pc.gameObject.SetActive(false);
#endif
        }
    }

    private void DisableAiEnemies()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
                enemies[i].gameObject.SetActive(false);
        }
    }
}
