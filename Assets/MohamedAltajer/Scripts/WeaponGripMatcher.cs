using UnityEngine;

/// <summary>
/// Level 9 weapon grip fixer.
///
/// The black player character has the authoritative, correct weapon grip.
/// This script reads that grip's WeaponGripOffset at runtime and stamps
/// the exact same localPosition / localEulerAngles / uniformScale onto every
/// other character's weapon in the scene — without touching the player.
///
/// HOW TO USE
/// ----------
/// 1. Create an empty GameObject in your Level 9 scene, name it "WeaponGripMatcher".
/// 2. Attach this script to it.
/// 3. Play — it auto-runs on Awake, then again 0.5 s later to catch late-spawned weapons.
/// 4. While playing, right-click the component → "Force Re-Apply All Grips" for a manual refresh.
/// 5. Once the grip looks right, you may optionally bake it: right-click → "Log Source Grip Values"
///    and manually copy those numbers into the WeaponGripOffset fields on your enemy prefabs.
///
/// REQUIREMENTS
/// ------------
/// - The player GameObject must be tagged "Player".
/// - Enemy GameObjects must be tagged "Enemy".
/// - Every weapon (player and enemy) must have a WeaponGripOffset component.
///   WeaponGripSystem.AttachWeapon() adds one automatically via WeaponHitbox; if your
///   weapons were attached manually, add WeaponGripOffset to the weapon prefab root.
/// </summary>
public class WeaponGripMatcher : MonoBehaviour
{
    [Header("Optional — leave null to auto-find via 'Player' tag")]
    [Tooltip("The black player's root GameObject. Auto-resolved from tag 'Player' if empty.")]
    [SerializeField] private GameObject sourcePlayerRoot;

    [Header("Debug")]
    [SerializeField] private bool logDetails = true;

    // ── Internal snapshot ────────────────────────────────────────────────────

    private Vector3 _srcLocalPosition;
    private Vector3 _srcLocalEuler;
    private float   _srcUniformScale;
    private bool    _hasSnapshot;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        ApplyAll();

        // Second pass after 0.5 s catches weapons spawned during LevelBuilder.Start().
        Invoke(nameof(ApplyAll), 0.5f);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    [ContextMenu("Force Re-Apply All Grips")]
    public void ApplyAll()
    {
        if (!TakeSnapshot())
            return;

        int fixedCount = 0;
        WeaponGripOffset[] all = FindObjectsByType<WeaponGripOffset>(FindObjectsSortMode.None);

        foreach (WeaponGripOffset wgo in all)
        {
            if (wgo == null) continue;

            // Skip the source player's own weapon.
            GameObject root = wgo.transform.root.gameObject;
            if (root.CompareTag("Player")) continue;

            // Only touch weapons that belong to an Enemy.
            if (!root.CompareTag("Enemy")) continue;

            wgo.localPosition    = _srcLocalPosition;
            wgo.localEulerAngles = _srcLocalEuler;
            wgo.uniformScale     = _srcUniformScale;
            wgo.Enforce();
            fixedCount++;

            if (logDetails)
                Debug.Log($"[WeaponGripMatcher] Fixed grip on '{root.name}' → weapon '{wgo.name}'.");
        }

        if (logDetails)
            Debug.Log($"[WeaponGripMatcher] Done — {fixedCount} enemy weapon(s) updated.");
    }

    [ContextMenu("Log Source Grip Values")]
    public void LogSourceValues()
    {
        if (!TakeSnapshot()) return;
        Debug.Log($"[WeaponGripMatcher] Source grip snapshot:\n" +
                  $"  localPosition    = {_srcLocalPosition}\n" +
                  $"  localEulerAngles = {_srcLocalEuler}\n" +
                  $"  uniformScale     = {_srcUniformScale}");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads the WeaponGripOffset from the player's weapon and stores the snapshot.
    /// Returns true when a valid snapshot is ready.
    /// </summary>
    private bool TakeSnapshot()
    {
        // Resolve the source root on every call so it works after scene load.
        if (sourcePlayerRoot == null)
            sourcePlayerRoot = GameObject.FindGameObjectWithTag("Player");

        if (sourcePlayerRoot == null)
        {
            Debug.LogWarning("[WeaponGripMatcher] No GameObject tagged 'Player' found in scene.");
            return false;
        }

        // Find the WeaponGripOffset in the player's hierarchy.
        WeaponGripOffset src = sourcePlayerRoot.GetComponentInChildren<WeaponGripOffset>(true);

        if (src == null)
        {
            Debug.LogWarning($"[WeaponGripMatcher] Player '{sourcePlayerRoot.name}' has no " +
                             "WeaponGripOffset in its hierarchy. Attach WeaponGripOffset to the " +
                             "weapon prefab root so the grip can be read.");
            return false;
        }

        _srcLocalPosition  = src.localPosition;
        _srcLocalEuler     = src.localEulerAngles;
        _srcUniformScale   = src.uniformScale;
        _hasSnapshot       = true;

        if (logDetails)
            Debug.Log($"[WeaponGripMatcher] Snapshot taken from '{src.name}' on '{sourcePlayerRoot.name}': " +
                      $"pos={_srcLocalPosition}  euler={_srcLocalEuler}  scale={_srcUniformScale:F3}");
        return true;
    }
}
