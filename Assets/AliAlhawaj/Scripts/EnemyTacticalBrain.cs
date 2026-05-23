using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Coordination / decision-making layer that sits ON TOP of EnemyController
/// without altering its state machine. The brain:
///
///   • Picks a tactical TARGET via scored selection (Phase 1).
///   • Picks a STANCE (pressure / circle / backstep / wait-punish) (Phase 5).
///   • Modulates the agent destination with a tactical offset so enemies
///     strafe, circle, back-step and respect weapon spacing (Phase 2).
///   • Coordinates GROUP claims so 10 enemies don't dogpile one target
///     (Phase 5).
///   • Tracks FRUSTRATION on unreachable targets and forces a switch
///     (Phase 1).
///   • Reacts to incoming damage via EnemyController.OnDamaged for
///     dodges/retreats (Phase 4).
///
/// Performance:
///   • Decision tick every ~0.55 s, jittered per-instance so a group of 25
///     enemies never spikes on the same frame.
///   • Destination modulation every ~0.25 s, only when the stance demands
///     an offset.
///   • Target candidates come from a static registry (this + PlayerController
///     scan), not FindObjectsByType every tick.
///   • Zero per-frame allocations (uses cached scratch buffers).
/// </summary>
[DisallowMultipleComponent]
public class EnemyTacticalBrain : MonoBehaviour
{
    public enum Stance { Pressure, Circle, Backstep, WaitPunish }

    [Header("Cadence (seconds)")]
    [Tooltip("How often the brain re-evaluates target + stance.")]
    public float decisionInterval = 0.55f;
    [Tooltip("How often the destination is nudged with a tactical offset.")]
    public float modulationInterval = 0.25f;
    [Tooltip("Random ± fraction added to each interval per-instance at spawn.")]
    [Range(0f, 0.5f)] public float intervalJitter = 0.25f;

    [Header("Target scoring weights")]
    [Tooltip("Lower = farther targets penalised more.")]
    public float distanceFalloff = 22f;
    public float lowHpBonus      = 0.5f;
    public float isolationBonus  = 0.35f;
    public float recentAttackerBonus = 0.6f;
    public float playerBaseBias  = 0.3f;
    [Tooltip("Penalty per ally already targeting the same victim (group claim).")]
    public float crowdPenalty    = 0.45f;
    [Tooltip("Min score advantage required to switch off the locked-in target.")]
    public float switchHysteresis = 0.25f;

    [Header("Frustration")]
    [Tooltip("If we cannot reach the current target for this long, force a switch.")]
    public float frustrationTimeout = 4.5f;
    public float recentAttackerMemory = 6.0f;

    [Header("Stance / spacing")]
    [Tooltip("Strafe arc speed (degrees per second around the target) in Circle stance.")]
    public float circleAngularSpeed = 65f;
    [Tooltip("Distance kept beyond preferredEngageDistance in WaitPunish.")]
    public float waitPunishStandoff = 1.3f;
    [Tooltip("Distance opened up in Backstep stance.")]
    public float backstepDistance = 4.0f;
    [Tooltip("HP fraction below which Backstep is considered.")]
    [Range(0f, 1f)] public float retreatHpThreshold = 0.30f;

    [Header("Debug")]
    public bool debugLog = false;

    // ── External state cache ─────────────────────────────────────────────────
    private EnemyController _ctrl;
    private NavMeshAgent _agent;
    private EnemyPersonality _personality;

    private float _nextDecisionTime;
    private float _nextModulationTime;
    private float _frustrationSince = -1f;
    private float _circleAngle;          // accumulates in radians for the circle stance
    private int _circleDir = 1;           // +1 cw, -1 ccw — re-randomised on each Circle entry
    private Stance _stance = Stance.Pressure;

    private float _recentDamageTime = -999f;
    private Transform _recentAttackerHint;

    // ── Static registry (Phase 7: no FindObjectsByType per tick) ─────────────
    private static readonly List<EnemyTacticalBrain> _allBrains = new List<EnemyTacticalBrain>(32);
    private static PlayerController _cachedPlayer;
    private static float _playerCacheRefreshTime = -1f;

    // ── Group claims: how many enemies currently target each victim ──────────
    private static readonly Dictionary<Transform, int> _claims = new Dictionary<Transform, int>(32);
    private Transform _claimedTarget;

    // Scratch buffer for scoring candidates — reused, no GC.
    private readonly List<Transform> _scoreScratch = new List<Transform>(32);

    private void Awake()
    {
        _ctrl = GetComponent<EnemyController>();
        _agent = GetComponent<NavMeshAgent>();
        _personality = GetComponent<EnemyPersonality>();

        decisionInterval = Mathf.Max(0.1f, decisionInterval);
        modulationInterval = Mathf.Max(0.1f, modulationInterval);

        // Stagger ticks per-enemy so 25 brains never recompute in lockstep.
        float jit = 1f + Random.Range(-intervalJitter, intervalJitter);
        _nextDecisionTime = Time.time + Random.Range(0f, decisionInterval) * jit;
        _nextModulationTime = Time.time + Random.Range(0f, modulationInterval) * jit;
        _circleDir = Random.value < 0.5f ? -1 : 1;
    }

    private void OnEnable()
    {
        if (!_allBrains.Contains(this)) _allBrains.Add(this);
        if (_ctrl != null) _ctrl.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        _allBrains.Remove(this);
        if (_ctrl != null) _ctrl.OnDamaged -= HandleDamaged;
        ReleaseClaim();
    }

    private void Update()
    {
        if (_ctrl == null || !_ctrl.IsAlive) { ReleaseClaim(); return; }

        if (Time.time >= _nextDecisionTime)
        {
            _nextDecisionTime = Time.time + decisionInterval;
            UpdateFrustration();
            ReconsiderTarget();
            ChooseStance();
        }

        if (Time.time >= _nextModulationTime)
        {
            _nextModulationTime = Time.time + modulationInterval;
            ModulateDestination();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Phase 1 — TARGET SELECTION
    // ═════════════════════════════════════════════════════════════════════════

    private void ReconsiderTarget()
    {
        Transform current = _ctrl.CurrentTarget;
        Transform best = current;
        float bestScore = current != null ? ScoreCandidate(current, isCurrent: true) : float.NegativeInfinity;

        // Player is always a candidate (free for all).
        Transform playerT = ResolvePlayer();
        if (playerT != null && playerT != transform)
            ConsiderCandidate(playerT, ref best, ref bestScore, current);

        // Other living enemies (FFA targeting in the arena).
        for (int i = 0; i < _allBrains.Count; i++)
        {
            EnemyTacticalBrain other = _allBrains[i];
            if (other == null || other == this) continue;
            if (other._ctrl == null || !other._ctrl.IsAlive) continue;
            ConsiderCandidate(other.transform, ref best, ref bestScore, current);
        }

        if (best != null && best != current)
        {
            ReleaseClaim();
            _ctrl.SuggestTarget(best);
            ClaimTarget(best);
            _frustrationSince = -1f;
            if (debugLog) Debug.Log($"[Brain {name}] switched target → {best.name} (score {bestScore:F2})");
        }
        else if (best != null)
        {
            // Keep our claim fresh on the (unchanged) current target.
            ClaimTarget(best);
        }
    }

    private void ConsiderCandidate(Transform cand, ref Transform best, ref float bestScore, Transform current)
    {
        float s = ScoreCandidate(cand, isCurrent: cand == current);
        if (s > bestScore + (cand == current ? 0f : switchHysteresis))
        {
            best = cand;
            bestScore = s;
        }
    }

    private float ScoreCandidate(Transform t, bool isCurrent)
    {
        if (t == null) return float.NegativeInfinity;

        // Distance term — exponential decay so distant rivals are not picked.
        float dist = Vector3.Distance(transform.position, t.position);
        float distanceTerm = Mathf.Exp(-dist / Mathf.Max(1f, distanceFalloff));

        // Low-HP bonus (finish off the weakest first).
        float hpFrac = 1f;
        EnemyController otherEc = t.GetComponentInParent<EnemyController>();
        if (otherEc != null && otherEc.maxHealth > 0)
            hpFrac = Mathf.Clamp01((float)otherEc.CurrentHealth / otherEc.maxHealth);
        float lowHpTerm = (1f - hpFrac) * lowHpBonus;

        // Isolation bonus — fewer claims on this target = juicier.
        int claims = 0;
        _claims.TryGetValue(t, out claims);
        if (isCurrent) claims = Mathf.Max(0, claims - 1);
        float crowdTerm = -claims * crowdPenalty;
        float isolationTerm = (claims == 0 ? isolationBonus : 0f);

        // Recent-attacker memory.
        float attackerTerm = 0f;
        if (_recentAttackerHint != null && t == _recentAttackerHint
            && Time.time - _recentDamageTime < recentAttackerMemory)
        {
            attackerTerm = recentAttackerBonus;
        }

        // Player bias (the player tends to be the most threatening combatant).
        float playerTerm = (t.GetComponentInParent<PlayerController>() != null) ? playerBaseBias : 0f;

        float aggressionScale = _personality != null ? Mathf.Lerp(0.8f, 1.25f, _personality.aggression) : 1f;
        return (distanceTerm + lowHpTerm + isolationTerm + crowdTerm + attackerTerm + playerTerm) * aggressionScale;
    }

    private void UpdateFrustration()
    {
        Transform current = _ctrl.CurrentTarget;
        if (current == null || _agent == null) { _frustrationSince = -1f; return; }

        bool reachable = _agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathComplete;
        if (reachable)
        {
            _frustrationSince = -1f;
            return;
        }

        if (_frustrationSince < 0f) _frustrationSince = Time.time;
        if (Time.time - _frustrationSince > frustrationTimeout)
        {
            if (debugLog) Debug.Log($"[Brain {name}] frustrated, dropping target {current.name}");
            ReleaseClaim();
            // Don't null directly — SuggestTarget validates; instead pick the
            // next-best NOW so we never sit idle.
            Transform forced = PickAnyAliveAlternative(current);
            if (forced != null)
            {
                _ctrl.SuggestTarget(forced);
                ClaimTarget(forced);
            }
            _frustrationSince = -1f;
        }
    }

    private Transform PickAnyAliveAlternative(Transform avoid)
    {
        Transform p = ResolvePlayer();
        if (p != null && p != avoid && p != transform) return p;
        for (int i = 0; i < _allBrains.Count; i++)
        {
            var b = _allBrains[i];
            if (b == null || b == this) continue;
            if (b._ctrl == null || !b._ctrl.IsAlive) continue;
            if (b.transform == avoid) continue;
            return b.transform;
        }
        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Phase 5 — STANCE SELECTION
    // ═════════════════════════════════════════════════════════════════════════

    private void ChooseStance()
    {
        Transform target = _ctrl.CurrentTarget;
        if (target == null) { _stance = Stance.Pressure; return; }

        float dist = Vector3.Distance(transform.position, target.position);
        float strikeRange = _ctrl.meleeAttackRange;

        if (dist > strikeRange)
        {
            _stance = Stance.Pressure;
            return;
        }

        float hpFrac = _ctrl.maxHealth > 0
            ? (float)_ctrl.CurrentHealth / _ctrl.maxHealth
            : 1f;

        float aggressionFactor = _personality != null ? _personality.aggression : 0.5f;
        float braveryFactor    = _personality != null ? _personality.bravery    : 0.5f;
        float patienceFactor   = _personality != null ? _personality.patience   : 0.5f;
        float mobilityFactor   = _personality != null ? _personality.mobility   : 0.5f;

        // Low HP + low bravery → retreat. Otherwise:
        //   close + aggressive  → Pressure or Circle (mobility decides)
        //   close + patient     → WaitPunish
        //   far                 → Pressure (close the gap)
        if (hpFrac < retreatHpThreshold && Random.value > braveryFactor)
        {
            _stance = Stance.Backstep;
            return;
        }

        float roll = Random.value;
        if (roll < mobilityFactor * 0.7f)
        {
            if (_stance != Stance.Circle) _circleDir = Random.value < 0.5f ? -1 : 1;
            _stance = Stance.Circle;
        }
        else if (roll < mobilityFactor * 0.7f + patienceFactor * 0.6f)
        {
            _stance = Stance.WaitPunish;
        }
        else
        {
            _stance = Stance.Pressure;
        }

        // Unused now but keeps the param meaningful for future swing-throw logic.
        _ = aggressionFactor;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Phase 2 — DESTINATION MODULATION
    // ═════════════════════════════════════════════════════════════════════════

    private void ModulateDestination()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        Transform target = _ctrl.CurrentTarget;
        if (target == null) return;

        Vector3 targetPos = target.position;
        Vector3 self = transform.position;
        Vector3 toTarget = targetPos - self;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        if (dist < 0.01f) return;
        Vector3 dir = toTarget / dist;

        // Ally-stacking avoidance: shove sideways if any ally targeting the
        // same victim is closer than preferredAllySpacing.
        float spacing = _personality != null ? _personality.preferredAllySpacing : 2.2f;
        Vector3 antiStack = Vector3.zero;
        int allyContacts = 0;
        for (int i = 0; i < _allBrains.Count; i++)
        {
            var other = _allBrains[i];
            if (other == null || other == this) continue;
            if (other._ctrl == null || !other._ctrl.IsAlive) continue;
            Vector3 d = self - other.transform.position;
            d.y = 0f;
            float dd = d.magnitude;
            if (dd > 0.01f && dd < spacing)
            {
                antiStack += d.normalized * (spacing - dd);
                allyContacts++;
            }
        }
        if (allyContacts > 0) antiStack /= allyContacts;

        if (dist > _ctrl.meleeAttackRange)
        {
            if (antiStack.sqrMagnitude < 0.0001f)
                return;

            Vector3 pressureDest = _agent.destination + antiStack;
            if (NavMesh.SamplePosition(pressureDest, out NavMeshHit pressureHit, 2.5f, NavMesh.AllAreas))
                _agent.SetDestination(pressureHit.position);
            return;
        }

        Vector3 desired;
        switch (_stance)
        {
            case Stance.Circle:
            {
                // Tangential motion around the target at the preferred radius.
                _circleAngle += _circleDir * (circleAngularSpeed * Mathf.Deg2Rad * modulationInterval);
                float radius = _personality != null ? _personality.preferredEngageDistance : 1.9f;
                // Build a yaw-rotated point on a circle around the target.
                Vector3 fromTarget = -dir; // points from target to self
                Vector3 rotated = Quaternion.Euler(0f, _circleDir * 35f, 0f) * fromTarget;
                desired = targetPos + rotated.normalized * radius;
                break;
            }
            case Stance.Backstep:
            {
                desired = targetPos - dir * backstepDistance;
                break;
            }
            case Stance.WaitPunish:
            {
                float radius = (_personality != null ? _personality.preferredEngageDistance : 1.9f) + waitPunishStandoff;
                desired = targetPos - dir * radius;
                break;
            }
            case Stance.Pressure:
            default:
                // In Pressure stance, leave the existing EnemyController destination
                // alone — closing the gap is its job. Anti-stack still applies.
                if (antiStack.sqrMagnitude < 0.0001f) return;
                desired = _agent.destination + antiStack;
                break;
        }

        desired += antiStack;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Phase 4 — DAMAGE REACTION
    // ═════════════════════════════════════════════════════════════════════════

    private void HandleDamaged(GameObject attacker)
    {
        _recentDamageTime = Time.time;
        if (attacker != null) _recentAttackerHint = attacker.transform;
        // EnemyTacticalActions also subscribes for slide-dodge — this brain
        // only stores the memory used by target scoring.
    }

    public bool WasRecentlyDamaged(float withinSeconds)
    {
        return Time.time - _recentDamageTime <= withinSeconds;
    }

    public Stance CurrentStance => _stance;

    // ═════════════════════════════════════════════════════════════════════════
    // Group claims helpers
    // ═════════════════════════════════════════════════════════════════════════

    private void ClaimTarget(Transform t)
    {
        if (t == _claimedTarget) return;
        ReleaseClaim();
        _claimedTarget = t;
        if (t == null) return;
        _claims.TryGetValue(t, out int c);
        _claims[t] = c + 1;
    }

    private void ReleaseClaim()
    {
        if (_claimedTarget == null) return;
        if (_claims.TryGetValue(_claimedTarget, out int c))
        {
            if (c <= 1) _claims.Remove(_claimedTarget);
            else _claims[_claimedTarget] = c - 1;
        }
        _claimedTarget = null;
    }

    private static Transform ResolvePlayer()
    {
        // Refresh cache every 2 seconds (player rarely changes; cheap).
        if (_cachedPlayer == null || Time.time - _playerCacheRefreshTime > 2f)
        {
            _cachedPlayer = Object.FindFirstObjectByType<PlayerController>();
            _playerCacheRefreshTime = Time.time;
        }
        return _cachedPlayer != null ? _cachedPlayer.transform : null;
    }
}
