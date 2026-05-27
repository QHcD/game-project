using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class WeaponGripMatcher : MonoBehaviour
{
    [SerializeField] private GameObject sourcePlayerRoot;
    [SerializeField] private bool logDetails = false;
    [SerializeField] private bool continuousEnforce = true;
    [SerializeField] private float discoveryInterval = 0.5f;

    private struct GripSnapshot
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3 bladeBoxHalfExtents;
        public bool hasBladeBox;
    }

    private class EnemyWeaponBinding
    {
        public Transform target;
        public WeaponBase weapon;
        public string key;
    }

    private readonly Dictionary<string, GripSnapshot> _snapshots = new Dictionary<string, GripSnapshot>();
    private readonly List<EnemyWeaponBinding> _bindings = new List<EnemyWeaponBinding>();
    private GripSnapshot _fallback;
    private bool _hasFallback;
    private float _nextDiscovery;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Scene s = SceneManager.GetActiveScene();
        if (s.IsValid() && s.isLoaded) OnSceneLoaded(s, LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        string lower = scene.name.ToLowerInvariant();
        if (lower.Contains("menu") || lower.Contains("lobby") || lower.Contains("loading")) return;

        GameObject host = GameObject.Find("WeaponGripMatcher_Auto");
        if (host == null) host = new GameObject("WeaponGripMatcher_Auto");
        WeaponGripMatcher m = host.GetComponent<WeaponGripMatcher>();
        if (m == null) m = host.AddComponent<WeaponGripMatcher>();
        m.ClearAndRescan();
    }

    private void Awake() { ClearAndRescan(); }

    private void OnEnable() { _nextDiscovery = 0f; }

    public void ClearAndRescan()
    {
        _snapshots.Clear();
        _bindings.Clear();
        _hasFallback = false;
        _nextDiscovery = 0f;
    }

    private void LateUpdate()
    {
        // Per-frame enforcement disabled. The previous behaviour copied the
        // player's *local* weapon pose (relative to j_wrist_ri) onto enemy
        // weapons (parented under bip_hand_R) — but because those two bones
        // have different local axes, the resulting world pose was wrong and
        // the enemy katana stuck out at the wrong angle.
        //
        // EnemyController.LateUpdate now performs a rig-independent
        // character-root-space copy from the player every frame, which is the
        // single source of truth. This matcher is left as a no-op so it does
        // not fight that copy.
    }

    private void DiscoverEnemyWeapons()
    {
        _bindings.Clear();
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;
            WeaponBase[] weapons = enemy.GetComponentsInChildren<WeaponBase>(true);
            for (int w = 0; w < weapons.Length; w++)
            {
                WeaponBase wb = weapons[w];
                if (wb == null) continue;
                _bindings.Add(new EnemyWeaponBinding
                {
                    target = wb.transform,
                    weapon = wb,
                    key = ResolveKey(wb)
                });
            }
        }

        if (logDetails && _bindings.Count > 0)
            Debug.Log($"[WeaponGripMatcher] Discovered {_bindings.Count} enemy weapon transform(s).");
    }

    private bool TryResolveSnapshotByKey(string key, out GripSnapshot snap)
    {
        snap = default;
        if (!string.IsNullOrEmpty(key) && _snapshots.TryGetValue(key, out snap))
            return true;
        if (_hasFallback) { snap = _fallback; return true; }
        return false;
    }

    private static string ResolveKey(WeaponBase wb)
    {
        if (wb == null) return null;
        if (!string.IsNullOrEmpty(wb.weaponName) && wb.weaponName != "Weapon")
            return Normalize(wb.weaponName);
        return Normalize(wb.gameObject.name);
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        string s = raw.ToLowerInvariant();
        s = s.Replace("(clone)", "").Replace("weaponmodel", "").Replace(" ", "").Replace("_", "").Replace("-", "");
        return s.Trim();
    }

    private void BuildSnapshots()
    {
        if (sourcePlayerRoot == null)
            sourcePlayerRoot = GameObject.FindGameObjectWithTag("Player");
        if (sourcePlayerRoot == null) return;

        _snapshots.Clear();
        _hasFallback = false;

        WeaponBase[] playerWeapons = sourcePlayerRoot.GetComponentsInChildren<WeaponBase>(true);
        for (int i = 0; i < playerWeapons.Length; i++)
        {
            WeaponBase wb = playerWeapons[i];
            if (wb == null) continue;

            Transform t = wb.transform;
            GripSnapshot snap = new GripSnapshot
            {
                localPosition = t.localPosition,
                localRotation = t.localRotation,
                localScale = t.localScale
            };

            WeaponGripOffset wgo = wb.GetComponent<WeaponGripOffset>();
            if (wgo == null) wgo = wb.GetComponentInChildren<WeaponGripOffset>(true);
            if (wgo != null && wgo.bladeBoxHalfExtents.sqrMagnitude > 0f)
            {
                snap.bladeBoxHalfExtents = wgo.bladeBoxHalfExtents;
                snap.hasBladeBox = true;
            }

            string key = ResolveKey(wb);
            if (!string.IsNullOrEmpty(key))
                _snapshots[key] = snap;

            if (!_hasFallback) { _fallback = snap; _hasFallback = true; }
        }
    }

    [ContextMenu("Log Source Grip Values")]
    public void LogSourceValues()
    {
        BuildSnapshots();
        if (!_hasFallback) { Debug.LogWarning("[WeaponGripMatcher] No source player weapon found."); return; }
        foreach (KeyValuePair<string, GripSnapshot> kv in _snapshots)
        {
            GripSnapshot s = kv.Value;
            Debug.Log($"[WeaponGripMatcher] key='{kv.Key}' pos={s.localPosition} rotEuler={s.localRotation.eulerAngles} scale={s.localScale} blade={s.bladeBoxHalfExtents}");
        }
    }

    [ContextMenu("Force Re-Apply All Grips")]
    public void ApplyAllNow()
    {
        BuildSnapshots();
        DiscoverEnemyWeapons();
        if (!_hasFallback) return;

        for (int i = 0; i < _bindings.Count; i++)
        {
            EnemyWeaponBinding b = _bindings[i];
            if (b == null || b.target == null) continue;
            GripSnapshot snap;
            if (!TryResolveSnapshotByKey(b.key, out snap)) continue;
            b.target.localPosition = snap.localPosition;
            b.target.localRotation = snap.localRotation;
            b.target.localScale = snap.localScale;
        }
    }
}
