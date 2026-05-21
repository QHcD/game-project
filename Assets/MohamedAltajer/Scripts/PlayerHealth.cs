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

    private AudioSource _audioSource;
    private float _lastVoiceTime = -100f;

    // ── Internal state ──
    private float timeSinceLastDamage;
    private bool isRegenerating;
    private string _lastAttackerStatsId;
    private bool _loggedRemoteHudIgnored;

    private void SetupVoiceAudioSource()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1f; // 3D sound
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.minDistance = 2f;
        _audioSource.maxDistance = 40f;
        _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    private void PlayHurtVoice()
    {
        if (_audioSource == null) SetupVoiceAudioSource();
        if (_audioSource == null) return;
        if (hurtSounds == null || hurtSounds.Length == 0) return;
        if (Time.unscaledTime - _lastVoiceTime < 0.25f) return; // avoid spam
        _lastVoiceTime = Time.unscaledTime;

        AudioClip clip = hurtSounds[Random.Range(0, hurtSounds.Length)];
        if (clip != null)
        {
            _audioSource.pitch = Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(clip, AudioSettingsRuntime.ScaledSfx(0.7f));
        }
    }

    private void PlayDeathVoice()
    {
        if (_audioSource == null) SetupVoiceAudioSource();
        if (_audioSource == null) return;
        if (deathSounds == null || deathSounds.Length == 0) return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        if (clip != null)
        {
            _audioSource.pitch = Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(clip, AudioSettingsRuntime.ScaledSfx(1.0f));
        }
    }

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

        // Play hurt voice sound
        PlayHurtVoice();

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

        // Play death voice sound
        PlayDeathVoice();

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

    private void Start()
    {
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
