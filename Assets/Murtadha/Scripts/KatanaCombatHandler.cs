using System.Collections;
using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

/// <summary>
/// Unified katana combat driver for BOTH the local player and every enemy AI.
///
/// ┌─────────────────────────────────────────────────────────────────────────┐
/// │  HOW IT WORKS                                                           │
/// │                                                                         │
/// │  Player (isAI = false):                                                 │
/// │    • Update() detects Left Mouse Button → TriggerAttack()               │
/// │    • Requires that PlayerController suppresses its own attack input      │
/// │      when this component is present (see Integration Notes below).      │
/// │                                                                         │
/// │  Enemy AI (isAI = true):                                                │
/// │    • EnemyController.ExecuteAttack() calls TriggerAttack() instead of   │
/// │      starting its own hitbox coroutine (see Integration Notes below).   │
/// │                                                                         │
/// │  Attack flow (shared):                                                  │
/// │    TriggerAttack()                                                      │
/// │      → Animator trigger fires the swing clip                            │
/// │      → Animation Event "OnKatanaStrike" fires at the swing peak         │
/// │        (forwarded here via MeleeAnimationEventSink)                     │
/// │      → Physics.OverlapBox from WeaponGripOffset.bladeCenter             │
/// │      → Singleplayer : IDamageable.ReceiveDamage() called directly       │
/// │      → Multiplayer  : PhotonView.RPC → RpcApplyDamage on all clients    │
/// │                                                                         │
/// │  INTEGRATION NOTES (no auto-modification of existing files)             │
/// │  ─────────────────────────────────────────────────────────────────────  │
/// │  1. Katana prefab   → add WeaponGripOffset, set offsets, create         │
/// │                        a child Transform named "BladeCenter" at the      │
/// │                        blade mid-point and assign it.                   │
/// │                                                                         │
/// │  2. Player prefab   → add KatanaCombatHandler (isAI = false).           │
/// │     PlayerController: near the top of the attack-input block add:       │
/// │       if (_katanaHandler != null) return; // handler owns lvl-2 input   │
/// │     In Awake/Start: _katanaHandler = GetComponent<KatanaCombatHandler>();│
/// │                                                                         │
/// │  3. Enemy prefab    → add KatanaCombatHandler (isAI = true).            │
/// │     EnemyController.ExecuteAttack(), at the very top add:               │
/// │       KatanaCombatHandler kh = GetComponent<KatanaCombatHandler>();     │
/// │       if (kh != null) { kh.TriggerAttack(); return; }                   │
/// │                                                                         │
/// │  4. After AttachWeaponToHand / player weapon equip, call:               │
/// │       GetComponent<KatanaCombatHandler>()?.BindKatana(                  │
/// │           equippedWeaponObject.GetComponent<WeaponGripOffset>());       │
/// │                                                                         │
/// │  5. Animation clip  → add an Animation Event named "OnKatanaStrike"     │
/// │                        at the frame of peak swing contact.              │
/// │     MeleeAnimationEventSink.cs already has the forwarder — no extra     │
/// │     script needed on the Animator child GameObject.                     │
/// └─────────────────────────────────────────────────────────────────────────┘
/// </summary>
[DisallowMultipleComponent]
public class KatanaCombatHandler : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Ownership")]
    [Tooltip("Set TRUE on enemy prefabs.  FALSE on the local player prefab.\n" +
             "Controls whether Update() reads mouse input or waits for TriggerAttack().")]
    public bool isAI = false;

    [Header("Combat Stats")]
    [Tooltip("Damage applied per successful katana hit.")]
    public int  damage        = 35;

    [Tooltip("Minimum seconds between consecutive attacks.")]
    public float attackCooldown = 0.85f;

    [Header("Animation")]
    [Tooltip("Animator trigger parameter name that starts the katana swing clip.")]
    public string attackTrigger = "Attack";

    [Header("Weapon Reference")]
    [Tooltip("WeaponGripOffset on the instantiated katana model.  " +
             "Assign here OR call BindKatana() at runtime after the weapon is parented.")]
    public WeaponGripOffset katanaGrip;

    [Header("Hit-Detection")]
    [Tooltip("Physics layers that can be hit by the blade.  Include the Player and Enemy layers.")]
    public LayerMask hitLayers = ~0;

    // ── Private state ─────────────────────────────────────────────────────────

    private Animator _anim;
    private bool     _canAttack            = true;
    private bool     _hitRegisteredThisSwing;

#if PUN_2_OR_NEWER
    private PhotonView _photonView;
#endif

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>(true);

#if PUN_2_OR_NEWER
        _photonView = GetComponent<PhotonView>();
#endif
    }

    private void Update()
    {
        // Input is only read for the LOCAL, human-controlled player.
        if (!isAI && IsLocallyOwned() && _canAttack && Input.GetMouseButtonDown(0))
            TriggerAttack();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Start a katana attack.
    ///
    /// Called automatically via Update() for the local player (left-click).
    /// Called by EnemyController.ExecuteAttack() for AI combatants.
    ///
    /// Returns false when the cooldown has not elapsed yet, so the caller
    /// can decide whether to try alternative behaviour.
    /// </summary>
    public bool TriggerAttack()
    {
        if (!_canAttack) return false;

        _canAttack               = false;
        _hitRegisteredThisSwing  = false;

        if (_anim != null)
            _anim.SetTrigger(attackTrigger);

        StartCoroutine(CooldownRoutine());

        if (CombatDebug.Enabled)
            CombatDebug.Log($"[KatanaCombatHandler] '{name}' TriggerAttack isAI={isAI}");

        return true;
    }

    /// <summary>
    /// Bind the katana grip at runtime.
    /// Call this immediately after the katana model has been instantiated and
    /// parented to the hand bone — before the next frame renders.
    ///
    /// Calling Enforce() here makes the WeaponGripOffset the FINAL authority
    /// on localPosition / localEulerAngles, overriding any earlier grip code.
    /// </summary>
    public void BindKatana(WeaponGripOffset grip)
    {
        if (grip == null)
        {
            Debug.LogWarning($"[KatanaCombatHandler] '{name}': BindKatana received null grip.");
            return;
        }

        katanaGrip = grip;
        grip.Enforce();

        if (CombatDebug.Enabled)
            CombatDebug.Log($"[KatanaCombatHandler] '{name}' bound katana grip '{grip.name}', " +
                            $"bladeCenter={(grip.bladeCenter != null ? grip.bladeCenter.name : "NONE")}");
    }

    // ── Animation Event entry-point ───────────────────────────────────────────

    /// <summary>
    /// Called by the "OnKatanaStrike" Animation Event placed at the swing peak.
    ///
    /// Forwarded here from MeleeAnimationEventSink.OnKatanaStrike(), which
    /// lives on the same GameObject as the Animator.
    ///
    /// Only the AUTHORITATIVE owner (local player or AI host) executes the
    /// physics cast; damage is then routed to all clients via RPC in multiplayer.
    /// </summary>
    public void OnKatanaStrike()
    {
        if (!IsLocallyOwned())      return; // remote copies: wait for the RPC
        if (_hitRegisteredThisSwing) return; // one damage event per swing

        if (katanaGrip == null || katanaGrip.bladeCenter == null)
        {
            Debug.LogWarning($"[KatanaCombatHandler] '{name}': OnKatanaStrike fired but " +
                             "katanaGrip or bladeCenter is not assigned.  No hit registered.");
            return;
        }

        _hitRegisteredThisSwing = true;

        // ── Physics cast ────────────────────────────────────────────────────
        Vector3    center      = katanaGrip.bladeCenter.position;
        Quaternion orientation = katanaGrip.bladeCenter.rotation;
        Vector3    halfExtents = katanaGrip.bladeBoxHalfExtents;

        Collider[] hits = Physics.OverlapBox(
            center, halfExtents, orientation,
            hitLayers, QueryTriggerInteraction.Ignore);

        if (CombatDebug.Enabled)
            CombatDebug.Log($"[KatanaCombatHandler] '{name}' OnKatanaStrike: {hits.Length} collider(s) in range.");

        foreach (Collider col in hits)
        {
            // Never damage our own body.
            if (col.transform.IsChildOf(transform.root) || col.transform == transform.root)
                continue;

            // Find IDamageable walking up from the hit collider's root.
            IDamageable target = col.GetComponentInParent<IDamageable>()
                              ?? col.GetComponentInChildren<IDamageable>(true);

            if (target == null || !target.IsAlive) continue;

            // Occlusion check: don't deal damage through solid geometry.
            if (DamageOcclusion.IsBlockedFromPoint(gameObject,
                    ((MonoBehaviour)target).gameObject, center))
                continue;

            ApplyDamageToTarget(col.transform.root.gameObject, damage);

            // One target per swing.  Remove 'break' for cleave / AoE.
            break;
        }
    }

    // ── Damage routing ────────────────────────────────────────────────────────

    private void ApplyDamageToTarget(GameObject targetRoot, int dmg)
    {
#if PUN_2_OR_NEWER
        if (MultiplayerMode.IsMultiplayer)
        {
            PhotonView targetView = targetRoot.GetComponent<PhotonView>()
                                 ?? targetRoot.GetComponentInParent<PhotonView>();

            if (targetView != null && _photonView != null)
            {
                // Broadcast to ALL clients so health drops simultaneously.
                _photonView.RPC(nameof(RpcApplyDamage), RpcTarget.All,
                                targetView.ViewID, dmg);
                return;
            }

            Debug.LogWarning($"[KatanaCombatHandler] '{name}': target '{targetRoot.name}' " +
                             "has no PhotonView — falling back to local damage.");
        }
#endif
        // Single-player OR target has no PhotonView: apply locally.
        ApplyDamageLocal(targetRoot, dmg);
    }

#if PUN_2_OR_NEWER
    /// <summary>
    /// Received on every client.  Look up the target by its Photon View ID
    /// and apply damage locally so health and death play out identically
    /// across the network without further round-trips.
    /// </summary>
    [PunRPC]
    private void RpcApplyDamage(int targetViewID, int dmg)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null)
        {
            Debug.LogWarning($"[KatanaCombatHandler] RpcApplyDamage: " +
                             $"could not find PhotonView {targetViewID}.");
            return;
        }

        ApplyDamageLocal(targetView.gameObject, dmg);
    }
#endif

    private void ApplyDamageLocal(GameObject targetRoot, int dmg)
    {
        IDamageable damageable = targetRoot.GetComponentInChildren<IDamageable>(true)
                              ?? targetRoot.GetComponentInParent<IDamageable>();

        if (damageable == null || !damageable.IsAlive) return;

        if (CombatDebug.Enabled)
            CombatDebug.Log($"[KatanaCombatHandler] '{name}' applying {dmg} dmg → '{targetRoot.name}'");

        damageable.ReceiveDamage(dmg, gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when this instance is the authoritative owner of the
    /// character — always true in single-player, true only for IsMine in MP.
    /// </summary>
    private bool IsLocallyOwned()
    {
#if PUN_2_OR_NEWER
        if (MultiplayerMode.IsMultiplayer)
            return _photonView != null && _photonView.IsMine;
#endif
        return true;
    }

    private IEnumerator CooldownRoutine()
    {
        yield return new WaitForSeconds(attackCooldown);
        _canAttack = true;
    }

    // ── Editor gizmos ─────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (katanaGrip == null || katanaGrip.bladeCenter == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.30f);
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            katanaGrip.bladeCenter.position,
            katanaGrip.bladeCenter.rotation,
            Vector3.one);
        Gizmos.DrawCube(Vector3.zero, katanaGrip.bladeBoxHalfExtents * 2f);

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.85f);
        Gizmos.DrawWireCube(Vector3.zero, katanaGrip.bladeBoxHalfExtents * 2f);
        Gizmos.matrix = prev;
    }
}
