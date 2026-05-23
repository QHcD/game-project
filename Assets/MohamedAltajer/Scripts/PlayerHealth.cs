using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

/// <summary>
/// Player health with Call of Duty-style auto-regeneration.
/// After taking damage, if the player avoids further damage for 5 seconds,
/// health smoothly regenerates back to 100.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Corpse Setup")]
    public GameObject deadPlayerCorpsePrefab;

    [Header("Auto-Regeneration (CoD Style)")]
    [Tooltip("Seconds after last damage before regen begins.")]
    public float regenDelay = 5f;

    [Tooltip("Health points restored per second during regen.")]
    public float regenRate = 15f;

    [Tooltip("Enable/disable auto-regeneration.")]
    public bool autoRegenEnabled = true;

    [Header("Combat Voice SFX")]
    public AudioClip[] hurtSounds;
    public AudioClip[] deathSounds;

    // ── Internal state ──
    private float timeSinceLastDamage;
    private bool isRegenerating;
    private string _lastAttackerStatsId;
    private bool _loggedRemoteHudIgnored;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        timeSinceLastDamage = regenDelay + 1f; // Start fully healed, no regen needed

        if (!MultiplayerMode.IsMultiplayer && MatchStatsManager.Instance != null)
        {
            // Use the persistent username from PlayerProfile so the player
            // shows up under their real handle in the leaderboard / kill feed.
            string label = PlayerProfile.HasUsername ? PlayerProfile.Username : "PLAYER";
            MatchStatsManager.Instance.RegisterCombatant(MatchStatsManager.BuildCombatantId(this), label, isPlayer: true, transform: transform);
        }
    }

    private void Update()
    {
        if (!autoRegenEnabled) return;
        if (currentHealth >= maxHealth) return;
        if (currentHealth <= 0f) return; // Dead, no regen

        timeSinceLastDamage += Time.deltaTime;

        // Start regenerating after delay with no damage
        if (timeSinceLastDamage >= regenDelay)
        {
            if (!isRegenerating)
            {
                isRegenerating = true;
                Debug.Log("[PlayerHealth] Auto-regen started");
            }

            float healAmount = regenRate * Time.deltaTime;
            currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);

            // Update HUD during regen
            if (ShouldDriveLocalHud())
                HUDManager.Instance.UpdateHealth(currentHealth, maxHealth, this);

            // Regen complete
            if (currentHealth >= maxHealth)
            {
                currentHealth = maxHealth;
                isRegenerating = false;
            }
        }
    }

    public void TakeDamage(float amount)
    {
        float absorbed = Mathf.Abs(amount);
        currentHealth = Mathf.Max(0f, currentHealth - absorbed);

        // Reset regen timer — must wait another 5 seconds
        timeSinceLastDamage = 0f;
        isRegenerating = false;

        CombatVoiceSfx.GetOrAdd(gameObject).PlayHurt();

        // Notify the persistent profile so the "Win without dying" challenge
        // disqualifies the current match the moment we take any damage.
        if (SessionManager.Instance != null)
            SessionManager.Instance.OnPlayerTookDamage();

        if (ShouldDriveLocalHud())
        {
            HUDManager.Instance.UpdateHealth(currentHealth, maxHealth, this);
            HUDManager.Instance.ShowDamageFlash(absorbed);
        }

        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.playerTookDamage = true;
        }
    }

    /// <summary>
    /// Universal death handler — symmetric with EnemyController.Die(). Disables
    /// movement components, triggers the ragdoll if one is present, reports the
    /// kill, then transitions to the GameOver screen. Guarded so re-entry from
    /// late hits during the death frame is a no-op.
    /// </summary>
    private bool _deathHandled;
    private void HandleDeath()
    {
        if (_deathHandled) return;
        _deathHandled = true;

        SpawnCorpse();

        CombatVoiceSfx.GetOrAdd(gameObject).PlayDeath();

        // Stop all locomotion so the corpse can't slide around or take more hits.
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col != null && !col.isTrigger)
                col.enabled = false;
        }

        // Trigger ragdoll if the player rig has one wired up. Direction is
        // toward the last attacker so the body recoils naturally.
        RagdollController ragdoll = GetComponent<RagdollController>();
        if (ragdoll != null)
        {
            Vector3 hitDir = Vector3.back;
            ragdoll.EnableRagdoll(hitDir);
        }

        if (MatchStatsManager.Instance != null)
        {
            MatchStatsManager.Instance.MarkEliminated(GetStatsId());
            MatchStatsManager.Instance.RecordKill(_lastAttackerStatsId);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerTookDamage = true;
            GameManager.Instance.GameOver();
        }
    }

    private void SpawnCorpse()
    {
        if (deadPlayerCorpsePrefab != null)
        {
            Instantiate(deadPlayerCorpsePrefab, transform.position, transform.rotation);
            Debug.Log("[PlayerHealth] Corpse spawned from deadPlayerCorpsePrefab.");
        }
        else
        {
            // Fallback corpse creation
            Transform visualSource = transform.Find("ThirdPersonBody");
            if (visualSource == null)
            {
                SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null)
                {
                    visualSource = smr.transform;
                    while (visualSource.parent != null && visualSource.parent != transform)
                    {
                        visualSource = visualSource.parent;
                    }
                }
            }

            if (visualSource != null)
            {
                // Duplicate only the visual child/model
                GameObject corpse = Instantiate(visualSource.gameObject, transform.position, transform.rotation);
                corpse.name = "PlayerCorpse_Fallback";
                corpse.transform.parent = null;
                corpse.SetActive(true);

                // Disable animator so it doesn't fight physics or loop animations
                Animator animator = corpse.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.enabled = false;
                }

                // If there are bone rigidbodies, enable them to ragdoll
                Rigidbody[] boneRbs = corpse.GetComponentsInChildren<Rigidbody>(true);
                bool hasBonePhysics = false;
                foreach (Rigidbody boneRb in boneRbs)
                {
                    if (boneRb == null) continue;
                    boneRb.isKinematic = false;
                    boneRb.useGravity = true;
                    boneRb.constraints = RigidbodyConstraints.None;
                    hasBonePhysics = true;
                }

                // If there are bone colliders, make sure they are enabled
                Collider[] boneCols = corpse.GetComponentsInChildren<Collider>(true);
                foreach (Collider boneCol in boneCols)
                {
                    if (boneCol == null) continue;
                    boneCol.enabled = true;
                    boneCol.isTrigger = false;
                }

                // If no bone physics was found, add simple collider/Rigidbody setup to the root of the corpse
                if (!hasBonePhysics)
                {
                    Collider col = corpse.GetComponent<Collider>();
                    if (col == null)
                    {
                        CapsuleCollider cap = corpse.AddComponent<CapsuleCollider>();
                        cap.center = new Vector3(0f, 0.9f, 0f);
                        cap.radius = 0.3f;
                        cap.height = 1.8f;
                    }
                    else
                    {
                        col.enabled = true;
                        col.isTrigger = false;
                    }

                    Rigidbody rb = corpse.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = corpse.AddComponent<Rigidbody>();
                    }
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.mass = 70f;
                    rb.linearDamping = 0.5f;
                    rb.angularDamping = 0.5f;
                    rb.constraints = RigidbodyConstraints.None;
                }

                // Strip gameplay/logic/network components
                MonoBehaviour[] scripts = corpse.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < scripts.Length; i++)
                {
                    if (scripts[i] != null && !(scripts[i] is UnityEngine.EventSystems.UIBehaviour))
                    {
                        Destroy(scripts[i]);
                    }
                }

                Camera[] cameras = corpse.GetComponentsInChildren<Camera>(true);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null) Destroy(cameras[i]);
                }

                UnityEngine.Rendering.Universal.UniversalAdditionalCameraData[] camData = corpse.GetComponentsInChildren<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(true);
                for (int i = 0; i < camData.Length; i++)
                {
                    if (camData[i] != null) Destroy(camData[i]);
                }

                AudioListener[] listeners = corpse.GetComponentsInChildren<AudioListener>(true);
                for (int i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i] != null) Destroy(listeners[i]);
                }

                // Remove NavMeshAgent if present
                var agent = corpse.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
                if (agent != null) Destroy(agent);

                // Remove CharacterController if present
                var cc = corpse.GetComponentInChildren<CharacterController>(true);
                if (cc != null) Destroy(cc);

                #if PUN_2_OR_NEWER
                var pv = corpse.GetComponentInChildren<PhotonView>(true);
                if (pv != null) Destroy(pv);
                #endif

                Debug.Log("[PlayerHealth] Corpse fallback instantiated and configured successfully.");
            }
            else
            {
                Debug.LogWarning("[PlayerHealth] Could not find any visual model child to instantiate as fallback corpse.");
            }
        }
    }

    private void Start()
    {
        MeleeBodyTargeting.EnsureMeleeBodyCollider(transform);

        CombatVoiceSfx voice = CombatVoiceSfx.GetOrAdd(gameObject);
        voice.ApplyInspectorClips(hurtSounds, deathSounds);

        if (MultiplayerMode.IsMultiplayer && MatchStatsManager.Instance != null)
        {
#if PUN_2_OR_NEWER
            PhotonView view = GetComponent<PhotonView>();
            if (view != null && view.Owner != null)
            {
                string label = string.IsNullOrWhiteSpace(view.Owner.NickName) ? $"Player_{view.Owner.ActorNumber}" : view.Owner.NickName;
                MatchStatsManager.Instance.RegisterCombatant(GetStatsId(), label, isPlayer: true, transform: transform);
            }
#endif
        }
    }

    // ── IDamageable ─────────────────────────────────────────────────────────
    public bool IsAlive => currentHealth > 0f;

    public void ReceiveDamage(int amount, GameObject attackerRoot)
    {
        if (MultiplayerMode.IsMultiplayer)
        {
            bool isFriendly = attackerRoot != null && attackerRoot.GetComponentInParent<PlayerHealth>() != null && attackerRoot.GetComponentInParent<PlayerHealth>().gameObject != gameObject;
            if (isFriendly)
            {
                bool ff = true;
#if PUN_2_OR_NEWER
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MpRoomConfig.KeyFriendlyFire, out object ffRaw))
                    ff = (bool)ffRaw;
#endif
                if (MultiplayerMode.ActiveMode == MpGameMode.CoopSurvival)
                    ff = false;

                if (!ff) return; // Block friendly fire
            }
        }

        if (attackerRoot != null)
        {
            EnemyController attackerEnemy = attackerRoot.GetComponentInParent<EnemyController>();
            if (attackerEnemy != null)
                _lastAttackerStatsId = MatchStatsManager.BuildCombatantId(attackerEnemy);
        }

        bool fromEnemy = attackerRoot != null && attackerRoot.GetComponentInParent<EnemyController>() != null;
        if (fromEnemy && GameManager.Instance != null)
        {
            int hitsToKill = Mathf.Max(1, GameManager.Instance.GetPlayerHitsToKill());
            float perHitDamage = maxHealth / hitsToKill;
            float hb = currentHealth;
            if (CombatDebug.Enabled)
                CombatDebug.Log($"applyingDamage amount={perHitDamage:F1} target={gameObject.name}");
            TakeDamage(perHitDamage);
            if (CombatDebug.Enabled)
                CombatDebug.Log($"healthBefore={hb:F1} healthAfter={currentHealth:F1}");
            return;
        }

        // Delegate to the existing float-based TakeDamage for non-enemy sources.
        float hb2 = currentHealth;
        if (CombatDebug.Enabled)
            CombatDebug.Log($"applyingDamage amount={amount} target={gameObject.name}");
        TakeDamage((float)amount);
        if (CombatDebug.Enabled)
            CombatDebug.Log($"healthBefore={hb2:F1} healthAfter={currentHealth:F1}");
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Abs(amount));

        if (ShouldDriveLocalHud())
            HUDManager.Instance.UpdateHealth(currentHealth, maxHealth, this);
    }

    private bool ShouldDriveLocalHud()
    {
        if (HUDManager.Instance == null)
            return false;

        if (!MultiplayerMode.IsMultiplayer)
            return true;

        if (HUDManager.Instance.IsLocalHealthTarget(this))
            return true;

        if (!_loggedRemoteHudIgnored)
        {
            _loggedRemoteHudIgnored = true;
            Debug.Log($"[MPHUD] remote player ignored for local HP actor={GetPhotonActorNumber()}");
        }

        return false;
    }

    private int GetPhotonActorNumber()
    {
#if PUN_2_OR_NEWER
        PhotonView view = GetComponent<PhotonView>();
        if (view != null && view.Owner != null)
            return view.Owner.ActorNumber;
#endif
        return -1;
    }

    private string GetStatsId()
    {
#if PUN_2_OR_NEWER
        PhotonView view = GetComponent<PhotonView>();
        if (MultiplayerMode.IsMultiplayer && view != null && view.Owner != null)
            return $"photon:{view.Owner.ActorNumber}";
#endif
        return MatchStatsManager.BuildCombatantId(this);
    }
}
