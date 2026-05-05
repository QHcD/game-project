using UnityEngine;
using UnityEngine.AI;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

#if PHOTON_UNITY_NETWORKING
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSpawnerForMultiplayerScene()
    {
        if (!MultiplayerMode.IsMultiplayer)
            return;

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != MultiplayerMode.MultiplayerSceneName)
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
        DisableAiEnemies();

#if PHOTON_UNITY_NETWORKING
        if (PhotonNetwork.InRoom)
            SpawnLocalPlayer();
#else
        Debug.LogWarning("[NetworkPlayerSpawner] Photon PUN 2 is not imported. Multiplayer spawning is disabled.");
#endif
    }

#if PHOTON_UNITY_NETWORKING
    public override void OnJoinedRoom()
    {
        SpawnLocalPlayer();
    }

    private void SpawnLocalPlayer()
    {
        if (FindOwnedPlayer() != null)
            return;

        Vector3 spawn = ResolveSpawnPosition();
        PhotonNetwork.Instantiate(playerPrefabPath, spawn, Quaternion.identity);
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
