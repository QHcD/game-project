using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tracks combatant registrations and simple kill leaderboard data for the current match.
/// </summary>
public class MatchStatsManager : MonoBehaviour
{
    public struct CombatantSnapshot
    {
        public string Id;
        public string DisplayName;
        public int Kills;
        public bool IsPlayer;
        public bool IsAlive;
    }

    private sealed class CombatantData
    {
        public string Id;
        public string DisplayName;
        public int Kills;
        public bool IsPlayer;
        public bool IsAlive;
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
    }

    public void RegisterCombatant(string id, string displayName, bool isPlayer)
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
        combatant.IsPlayer = isPlayer;
        combatant.IsAlive = true;
    }

    public void MarkEliminated(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (_combatants.TryGetValue(id, out CombatantData combatant))
            combatant.IsAlive = false;
    }

    public void RecordKill(string killerId)
    {
        if (string.IsNullOrWhiteSpace(killerId))
            return;

        if (_combatants.TryGetValue(killerId, out CombatantData combatant))
            combatant.Kills++;
    }

    public IReadOnlyList<CombatantSnapshot> GetTopCombatants(int count)
    {
        if (count <= 0)
            return System.Array.Empty<CombatantSnapshot>();

        return _combatants.Values
            .OrderByDescending(entry => entry.Kills)
            .ThenByDescending(entry => entry.IsPlayer)
            .ThenBy(entry => entry.DisplayName)
            .Take(count)
            .Select(entry => new CombatantSnapshot
            {
                Id = entry.Id,
                DisplayName = entry.DisplayName,
                Kills = entry.Kills,
                IsPlayer = entry.IsPlayer,
                IsAlive = entry.IsAlive
            })
            .ToArray();
    }

    public int GetRegisteredCombatantCount()
    {
        return _combatants.Count;
    }
}
