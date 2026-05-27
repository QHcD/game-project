using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tracks combatant registrations and simple kill leaderboard data for the current match.
/// </summary>
public class MatchStatsManager : MonoBehaviour
{
    public event System.Action StatsChanged;

    public struct CombatantSnapshot
    {
        public string    Id;
        public string    DisplayName;
        public int       Kills;
        public int       Deaths;
        public int       Score;
        public bool      IsPlayer;
        public bool      IsAlive;
        /// <summary>
        /// Live world transform of the combatant (player root or enemy root).
        /// May be null after the entity has been destroyed (e.g. enemy
        /// pooled out of the scene). The end-match cinematic uses this to
        /// orbit the camera around the top three.
        /// </summary>
        public Transform Transform;
    }

    private sealed class CombatantData
    {
        public string    Id;
        public string    DisplayName;
        public int       Kills;
        public int       Deaths;
        public bool      IsPlayer;
        public bool      IsAlive;
        public Transform Transform;

        /// <summary>Score is derived: 100 per kill, -25 per death (floor 0).</summary>
        public int Score => Mathf.Max(0, Kills * 100 - Deaths * 25);
    }

    public static MatchStatsManager Instance { get; private set; }

    private readonly Dictionary<string, CombatantData> _combatants = new Dictionary<string, CombatantData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject managerObject = new GameObject("MatchStatsManager");
        DontDestroyOnLoad(managerObject);
        Instance = managerObject.AddComponent<MatchStatsManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (Instance != this)
            Destroy(gameObject);
    }

    public static string BuildCombatantId(Component component)
    {
        if (component == null)
            return string.Empty;

        return $"{component.gameObject.scene.name}:{component.GetInstanceID()}";
    }

    public void ResetMatch()
    {
        _combatants.Clear();
        NotifyStatsChanged();
    }

    public void RegisterCombatant(string id, string displayName, bool isPlayer, Transform transform = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!_combatants.TryGetValue(id, out CombatantData combatant))
        {
            combatant = new CombatantData
            {
                Id = id
            };
            _combatants[id] = combatant;
        }

        combatant.DisplayName = string.IsNullOrWhiteSpace(displayName) ? (isPlayer ? "PLAYER" : "COMBATANT") : displayName.ToUpperInvariant();
        combatant.IsPlayer  = isPlayer;
        combatant.IsAlive   = true;
        if (transform != null) combatant.Transform = transform;
        NotifyStatsChanged();
    }

    public void MarkEliminated(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (_combatants.TryGetValue(id, out CombatantData combatant))
        {
            // Only count the *first* elimination — re-registering the same
            // id (e.g. respawn) would otherwise double-count the death.
            if (combatant.IsAlive)
            {
                combatant.Deaths += 1;
                combatant.IsAlive = false;
                NotifyStatsChanged();
            }
        }
    }

    public void RecordKill(string killerId)
    {
        if (string.IsNullOrWhiteSpace(killerId))
            return;

        if (_combatants.TryGetValue(killerId, out CombatantData combatant))
        {
            combatant.Kills++;
            NotifyStatsChanged();
        }
    }

    private static IEnumerable<CombatantData> DeduplicatePlayers(IEnumerable<CombatantData> source)
    {
        Dictionary<string, CombatantData> byKey = new Dictionary<string, CombatantData>();
        List<CombatantData> nonPlayer = new List<CombatantData>();
        foreach (CombatantData entry in source)
        {
            if (entry == null) continue;
            if (!entry.IsPlayer)
            {
                nonPlayer.Add(entry);
                continue;
            }

            string key = entry.DisplayName != null ? entry.DisplayName.Trim().ToUpperInvariant() : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                nonPlayer.Add(entry);
                continue;
            }

            if (!byKey.TryGetValue(key, out CombatantData existing))
            {
                byKey[key] = entry;
                continue;
            }

            CombatantData winner = SelectCanonicalDuplicate(existing, entry);
            CombatantData loser = winner == existing ? entry : existing;
            winner.Kills = Mathf.Max(winner.Kills, loser.Kills);
            winner.Deaths = Mathf.Max(winner.Deaths, loser.Deaths);
            winner.IsAlive = winner.IsAlive || loser.IsAlive;
            if (winner.Transform == null && loser.Transform != null)
                winner.Transform = loser.Transform;
            byKey[key] = winner;
        }

        foreach (CombatantData v in byKey.Values) yield return v;
        for (int i = 0; i < nonPlayer.Count; i++) yield return nonPlayer[i];
    }

    private static CombatantData SelectCanonicalDuplicate(CombatantData a, CombatantData b)
    {
        if (a.IsAlive != b.IsAlive) return a.IsAlive ? a : b;
        if (a.Transform != null && b.Transform == null) return a;
        if (b.Transform != null && a.Transform == null) return b;
        if (a.Kills != b.Kills) return a.Kills > b.Kills ? a : b;
        return a;
    }

    public IReadOnlyList<CombatantSnapshot> GetTopCombatants(int count)
    {
        if (count <= 0)
            return System.Array.Empty<CombatantSnapshot>();

        return DeduplicatePlayers(_combatants.Values)
            .OrderByDescending(entry => entry.Kills)
            .ThenBy(entry => entry.Deaths)
            .ThenByDescending(entry => entry.IsPlayer)
            .ThenBy(entry => entry.DisplayName)
            .Take(count)
            .Select(entry => new CombatantSnapshot
            {
                Id          = entry.Id,
                DisplayName = entry.DisplayName,
                Kills       = entry.Kills,
                Deaths      = entry.Deaths,
                Score       = entry.Score,
                IsPlayer    = entry.IsPlayer,
                IsAlive     = entry.IsAlive,
                Transform   = entry.Transform,
            })
            .ToArray();
    }

    public IReadOnlyList<CombatantSnapshot> GetTopPlayers(int count)
    {
        if (count <= 0)
            return System.Array.Empty<CombatantSnapshot>();

        return DeduplicatePlayers(_combatants.Values)
            .Where(entry => entry.IsPlayer)
            .OrderByDescending(entry => entry.Kills)
            .ThenBy(entry => entry.Deaths)
            .ThenBy(entry => entry.DisplayName)
            .Take(count)
            .Select(entry => new CombatantSnapshot
            {
                Id          = entry.Id,
                DisplayName = entry.DisplayName,
                Kills       = entry.Kills,
                Deaths      = entry.Deaths,
                Score       = entry.Score,
                IsPlayer    = entry.IsPlayer,
                IsAlive     = entry.IsAlive,
                Transform   = entry.Transform,
            })
            .ToArray();
    }

    public int GetRegisteredCombatantCount()
    {
        return DeduplicatePlayers(_combatants.Values).Count();
    }

    public int GetRegisteredPlayerCount()
    {
        return DeduplicatePlayers(_combatants.Values).Count(entry => entry.IsPlayer);
    }

    private void NotifyStatsChanged()
    {
        StatsChanged?.Invoke();
    }
}
