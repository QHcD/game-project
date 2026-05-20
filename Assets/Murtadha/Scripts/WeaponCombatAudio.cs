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
    public Vector2 pitchJitter = new Vector2(0.95f, 1.05f);

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
        inst.PlayHitInternal(WeaponAnimationCategories.ForLevel(weaponLevel), worldPos);
    }

    private static WeaponCombatAudio FindFor(GameObject owner)
    {
        if (owner == null) return _registry.Count > 0 ? _registry[0] : null;
        // Prefer a component on the owner hierarchy; fall back to any registered.
        WeaponCombatAudio direct = owner.GetComponentInParent<WeaponCombatAudio>();
        if (direct != null) return direct;
        return _registry.Count > 0 ? _registry[0] : null;
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

    private void PlayHitInternal(WeaponAnimationCategory category, Vector3 worldPos)
    {
        bool found = TryGetRow(category, out CategoryAudio row);
        AudioClip clip = found && row.hit != null ? row.hit : genericHit;
        PlayOneShotScaled(clip, hitVolume, worldPos);

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
