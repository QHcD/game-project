using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

/// <summary>
/// Master-client-only bot director for multiplayer modes.
///
/// Phase 1 / Phase 2 responsibilities:
///   - Spawns a configurable number of AI enemies on the master client.
///   - Builds a shared "human targets" list from all networked PlayerControllers.
///   - Refreshes the list when players join or leave.
///   - In CoopSurvival: bots only target humans (friendly fire between bots off).
///   - In HybridChaos: bots target everyone (default EnemyController behaviour).
///
/// IMPORTANT: Only runs on the master client. Non-master clients receive bot
/// positions via normal NavMeshAgent transforms — no extra sync needed for
/// Phase 1 (bots are master-local, not network-instantiated yet).
/// </summary>
public class MpBotDirector : MonoBehaviour
{
    public static MpBotDirector Instance { get; private set; }

    [Header("Bot Prefab")]
    [Tooltip("Enemy prefab path under Resources/ — same one used in singleplayer.")]
    public string botPrefabPath = "Enemies/Enemy";

    private MpGameMode _mode;
    private int        _targetBotCount;
    private readonly List<EnemyController> _activeBots   = new List<EnemyController>();
    private readonly List<Transform>       _humanTargets = new List<Transform>();

    // ── Bootstrap ────────────────────────────────────────────────────────────

    public static void EnsureExists(MpGameMode mode, int botCount)
    {
        if (mode == MpGameMode.PurePvP)
        {
            Debug.Log("[MPMode] PurePvP active - skipping bot director");
            return;
        }

        if (Instance != null)
        {
            Instance.Reconfigure(mode, botCount);
            return;
        }

        var go = new GameObject("MpBotDirector");
        var dir = go.AddComponent<MpBotDirector>();
        dir._mode            = mode;
        dir._targetBotCount  = botCount;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if PUN_2_OR_NEWER
        if (!PhotonNetwork.IsMasterClient)
        {
            Destroy(gameObject);
            return;
        }
#endif
        if (_mode == MpGameMode.PurePvP)
        {
            Debug.Log("[MPMode] PurePvP active - skipping bot director");
            Destroy(gameObject);
            return;
        }
        RefreshHumanTargets();
        SpawnBots();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void Reconfigure(MpGameMode mode, int botCount)
    {
        _mode           = mode;
        _targetBotCount = botCount;
        if (_mode == MpGameMode.PurePvP)
        {
            foreach (var bot in _activeBots)
            {
                if (bot != null) bot.gameObject.SetActive(false);
            }
            _activeBots.Clear();
            return;
        }
        RefreshHumanTargets();
        ApplyTargetsToAllBots();
    }

    /// <summary>
    /// Called by NetworkPlayerSpawner when a new human player spawns.
    /// </summary>
    public void OnHumanPlayerSpawned(Transform playerTransform)
    {
        if (playerTransform == null) return;
        if (!_humanTargets.Contains(playerTransform))
            _humanTargets.Add(playerTransform);

        ApplyTargetsToAllBots();
        Debug.Log($"[MpBotDirector] human registered, total={_humanTargets.Count}");
    }

    /// <summary>
    /// Called when a human player disconnects or dies.
    /// </summary>
    public void OnHumanPlayerRemoved(Transform playerTransform)
    {
        _humanTargets.Remove(playerTransform);
        ApplyTargetsToAllBots();
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void RefreshHumanTargets()
    {
        _humanTargets.Clear();

        PlayerController[] players = FindObjectsByType<PlayerController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (PlayerController pc in players)
            _humanTargets.Add(pc.transform);

        Debug.Log($"[MpBotDirector] found {_humanTargets.Count} human player(s)");
    }

    private void SpawnBots()
    {
        StartCoroutine(SpawnBotsDelayed());
    }

    private IEnumerator SpawnBotsDelayed()
    {
        if (_mode == MpGameMode.PurePvP)
        {
            Debug.Log("[MPMode] PurePvP active - skipping enemy spawn");
            yield break;
        }

        // Wait one frame for the scene and NavMesh to be fully ready
        // before activating enemies — prevents NavMesh.CalculatePath null spam.
        yield return null;

        LevelBuilder.MapReadinessInfo mapInfo = new LevelBuilder.MapReadinessInfo();
        while (!LevelBuilder.IsMultiplayerBuildComplete ||
               !LevelBuilder.TryGetMultiplayerMapReadiness(out mapInfo))
        {
            Debug.Log("[AISpawn] waiting for map");
            yield return null;
        }

        Debug.Log("[MPBuild] map root found: " + mapInfo.Root.name);

        bool navReady;
        while (!LevelBuilder.TryValidateMultiplayerNavMesh(out navReady) || !navReady)
        {
            Debug.Log("[AISpawn] waiting for NavMesh");
            if (LevelBuilder.Instance != null)
                LevelBuilder.Instance.TriggerBuild();
            yield return null;
        }

        RefreshHumanTargets();
        while (_humanTargets.Count <= 0)
        {
            Debug.Log("[AISpawn] waiting for player target");
            yield return null;
            RefreshHumanTargets();
        }

        EnemyController[] sceneEnemies = FindObjectsByType<EnemyController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        _activeBots.Clear();
        int spawned = 0;
        foreach (EnemyController enemy in sceneEnemies)
        {
            if (spawned >= _targetBotCount) break;

            if (enemy == null) continue;

            if (!enemy.gameObject.activeSelf)
            {
                enemy.gameObject.SetActive(true);

                // Warp NavMeshAgent to a valid NavMesh position after re-enable.
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    if (agent.enabled)
                        agent.enabled = false;

                    if (NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                    {
                        enemy.transform.position = hit.position;
                        Debug.Log("[AINav] warped enemy " + enemy.name + " to NavMesh position");
                    }
                    else
                    {
                        Debug.LogError("[AINav] ERROR no NavMesh found near enemy " + enemy.name);
                    }

                    agent.enabled = true;
                }
            }

            enemy.PrepareForMultiplayerAi();
            _activeBots.Add(enemy);
            spawned++;
        }

        Debug.Log("[AISpawn] enemies activated count = " + spawned);
        Debug.Log($"[MpBotDirector] activated {spawned} bots, mode={_mode}");
        ApplyTargetsToAllBots();
    }

    private void ApplyTargetsToAllBots()
    {
        foreach (EnemyController bot in _activeBots)
        {
            if (bot == null) continue;
            if (_mode == MpGameMode.CoopSurvival)
                bot.SetMultiplayerTargets(_humanTargets);
            else
                bot.SetMultiplayerTargets(null);
            bot.PrepareForMultiplayerAi();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
