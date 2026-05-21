using UnityEngine;

/// <summary>
/// Scalable per-category melee audio + VFX. Attach to the Player root (and
/// optionally to enemies) and assign clips in the Inspector. Reuses the
/// <see cref="WeaponAnimationCategory"/> taxonomy so adding a new weapon
/// level requires zero changes here — just map the level in
/// <see cref="WeaponAnimationCategories.ForLevel"/>.
///
/// Hook points are static so callers (PlayerController.FireAttack,
/// WeaponHitbox.TryDamageCollider) can fire them in one line without
/// holding a direct reference.
///
/// EVERYTHING is null-safe. Missing clips or prefabs simply no-op.
/// </summary>
[DisallowMultipleComponent]
public class WeaponCombatAudio : MonoBehaviour
{
    [System.Serializable]
    public struct CategoryAudio
    {
        public WeaponAnimationCategory category;
        public AudioClip swing;
        public AudioClip hit;
        [Tooltip("Optional VFX spawned on a successful hit at the impact point.")]
        public GameObject hitSparkPrefab;
        [Tooltip("Optional VFX spawned at the weapon hand on each swing (one-shot).")]
        public GameObject swingTrailPrefab;
    }

    [Header("Per-category overrides (assign clips in Inspector)")]
    [Tooltip("Lookup table: one row per category you want to customise. " +
             "Missing rows / clips fall back to the generic clips below.")]
    public CategoryAudio[] categories = new CategoryAudio[0];

    [Header("Generic fallbacks")]
    [Tooltip("Used for any category whose 'swing' slot is empty.")]
    public AudioClip genericSwing;
    [Tooltip("Used for any category whose 'hit' slot is empty.")]
    public AudioClip genericHit;
    [Tooltip("Spawned on hit if no category-specific spark prefab is set.")]
    public GameObject genericHitSparkPrefab;
    [Tooltip("Spawned at hand on swing if no category-specific trail prefab is set.")]
    public GameObject genericSwingTrailPrefab;

    [Header("Mixing")]
    [Range(0f, 2f)] public float swingVolume = 0.9f;
    [Range(0f, 2f)] public float hitVolume = 1.0f;
    public Vector2 pitchJitter = new Vector2(0.94f, 1.06f);
    [Tooltip("Multiplicative volume jitter applied on top of hitVolume for organic variation.")]
    public Vector2 hitVolumeJitter = new Vector2(0.90f, 1.00f);

    [Header("Hit Spam Guard")]
    [Tooltip("Minimum seconds between two hit-sound plays for the same (attacker, target, level) triple. " +
             "Prevents OverlapSphere ticking from machine-gunning the clip on a slow swing.")]
    public float perTargetHitCooldown = 0.08f;

    [Header("Debug")]
    [Tooltip("If enabled, logs which clip was resolved per hit and which fallback (if any) was used.")]
    public bool debugLog = false;

    // Per-(attacker,target,level) cooldown so repeated OverlapSphere ticks
    // within the same swing window don't retrigger the same clip every frame.
    private static readonly System.Collections.Generic.Dictionary<long, float> _hitCooldownByKey
        = new System.Collections.Generic.Dictionary<long, float>();

    [Tooltip("Optional override for the hand transform where the swing trail VFX spawns. " +
             "If null, falls back to this GameObject's transform.")]
    public Transform handTransform;

    [Tooltip("Lifetime (seconds) for spawned hit-spark / swing-trail instances. " +
             "Prefabs with their own auto-destroy can leave this at 0 to skip.")]
    public float spawnedEffectLifetime = 2.5f;

    private AudioSource _source;
    private static readonly System.Collections.Generic.List<WeaponCombatAudio> _registry
        = new System.Collections.Generic.List<WeaponCombatAudio>();

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        if (_source == null)
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0.6f;
        }
    }

    private void OnEnable()
    {
        if (!_registry.Contains(this)) _registry.Add(this);
    }

    private void OnDisable()
    {
        _registry.Remove(this);
    }

    // ── Static fire-and-forget API ───────────────────────────────────────────

    /// <summary>Plays the swing sound + spawns swing trail VFX for the
    /// category mapped from <paramref name="weaponLevel"/>.</summary>
    public static void PlaySwingFor(GameObject owner, int weaponLevel)
    {
        WeaponCombatAudio inst = FindFor(owner);
        if (inst == null) return;
        inst.PlaySwingInternal(WeaponAnimationCategories.ForLevel(weaponLevel));
    }

    /// <summary>Plays the hit sound + spawns hit spark VFX at <paramref name="worldPos"/>.</summary>
    public static void PlayHitAt(GameObject owner, int weaponLevel, Vector3 worldPos)
    {
        WeaponCombatAudio inst = FindFor(owner);
        if (inst == null) return;

        // Anti-spam: throttle the same (attacker, level) combo. Target identity is
        // folded into the position hash so distinct targets still play in parallel.
        int attackerId = owner != null ? owner.GetInstanceID() : 0;
        long key = ((long)attackerId << 24) ^ ((long)weaponLevel << 16) ^ Mathf.RoundToInt(worldPos.x * 17f + worldPos.z * 29f);
        float now = Time.unscaledTime;
        if (_hitCooldownByKey.TryGetValue(key, out float nextOk) && now < nextOk)
            return;
        _hitCooldownByKey[key] = now + Mathf.Max(0f, inst.perTargetHitCooldown);

        inst.PlayHitInternal(weaponLevel, WeaponAnimationCategories.ForLevel(weaponLevel), worldPos);
    }

    private static WeaponCombatAudio FindFor(GameObject owner)
    {
        if (owner == null) return _registry.Count > 0 ? _registry[0] : null;
        // Prefer a component on the owner hierarchy; fall back to any registered.
        WeaponCombatAudio direct = owner.GetComponentInParent<WeaponCombatAudio>();
        if (direct != null) return direct;
        if (_registry.Count > 0) return _registry[0];

        // Auto-attach safety net: the player prefab in this project does not
        // always ship with WeaponCombatAudio wired up, which silently dropped
        // every hit sound. Adding the component on demand to the owner root
        // means the very first confirmed hit will both play correctly AND
        // leave the component in place for every subsequent hit.
        Transform root = owner.transform.root;
        if (root == null) return null;
        WeaponCombatAudio created = root.gameObject.AddComponent<WeaponCombatAudio>();
        Debug.Log($"[WeaponCombatAudio] Auto-attached to {root.name} (no instance found in scene).");
        return created;
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private void PlaySwingInternal(WeaponAnimationCategory category)
    {
        bool found = TryGetRow(category, out CategoryAudio row);
        AudioClip clip = found && row.swing != null ? row.swing : genericSwing;
        PlayOneShotScaled(clip, swingVolume);

        GameObject trail = found && row.swingTrailPrefab != null ? row.swingTrailPrefab : genericSwingTrailPrefab;
        if (trail != null)
        {
            Transform spawnAt = handTransform != null ? handTransform : transform;
            SpawnTimed(trail, spawnAt.position, spawnAt.rotation, spawnAt);
        }
    }

    private void PlayHitInternal(int weaponLevel, WeaponAnimationCategory category, Vector3 worldPos)
    {
        // Resolution order:
        //   1. Per-level clip from the auto-generated WeaponHitAudioDatabase.
        //   2. Per-category clip serialised on this component.
        //   3. Generic hit clip.
        string source = "generic";
        AudioClip clip = null;
        var db = WeaponHitAudioDatabase.Instance;
        if (db != null)
        {
            clip = db.GetClip(weaponLevel);
            if (clip != null) source = "level";
        }
        bool found = TryGetRow(category, out CategoryAudio row);
        if (clip == null && found && row.hit != null)
        {
            clip = row.hit;
            source = "category";
        }
        if (clip == null)
        {
            clip = genericHit;
            source = "generic";
        }

        // Volume jitter on top of base for organic variation.
        float volMul = Random.Range(
            Mathf.Min(hitVolumeJitter.x, hitVolumeJitter.y),
            Mathf.Max(hitVolumeJitter.x, hitVolumeJitter.y));
        PlayOneShotScaled(clip, hitVolume * volMul, worldPos);

        if (debugLog)
            Debug.Log($"[WeaponCombatAudio] hit level={weaponLevel} cat={category} clip={(clip != null ? clip.name : "<null>")} source={source}");

        GameObject spark = found && row.hitSparkPrefab != null ? row.hitSparkPrefab : genericHitSparkPrefab;
        if (spark != null)
            SpawnTimed(spark, worldPos, Quaternion.identity, null);
    }

    private bool TryGetRow(WeaponAnimationCategory category, out CategoryAudio row)
    {
        if (categories != null)
        {
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i].category == category)
                {
                    row = categories[i];
                    return true;
                }
            }
        }
        row = default;
        return false;
    }

    private void PlayOneShotScaled(AudioClip clip, float baseVol)
    {
        if (clip == null || _source == null) return;
        _source.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        _source.PlayOneShot(clip, AudioSettingsRuntime.ScaledSfx(Mathf.Max(0f, baseVol)));
    }

    private void PlayOneShotScaled(AudioClip clip, float baseVol, Vector3 worldPos)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, worldPos, AudioSettingsRuntime.ScaledSfx(Mathf.Max(0f, baseVol)));
    }

    private void SpawnTimed(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (prefab == null) return;
        GameObject go = Instantiate(prefab, pos, rot, parent);
        if (spawnedEffectLifetime > 0f)
            Destroy(go, spawnedEffectLifetime);
    }
}
