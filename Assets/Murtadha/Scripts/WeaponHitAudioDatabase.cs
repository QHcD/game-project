using UnityEngine;

/// <summary>
/// Per-level weapon hit AudioClip lookup populated by
/// <c>WeaponHitAudioDatabaseBuilder</c> (Editor) at import time. Lives in
/// Resources so the runtime can <see cref="Resources.Load{T}(string)"/> it
/// once and cache the result — no Inspector wiring required for any of the
/// 16 level clips.
///
/// The asset itself is generated at:
///   Assets/MohamedAman/Resources/WeaponHitAudioDatabase.asset
/// from the .mp3 files dropped under Assets/MohamedAman/Materials/.
///
/// If the asset is absent (clean clone, first import), <see cref="GetClip"/>
/// returns <c>null</c> and the runtime audio path falls back to the existing
/// per-category clips on <c>WeaponCombatAudio</c>.
/// </summary>
[CreateAssetMenu(fileName = "WeaponHitAudioDatabase", menuName = "PRISM-7/Audio/Weapon Hit Audio Database")]
public class WeaponHitAudioDatabase : ScriptableObject
{
    public const int MaxLevel = 16;
    public const string ResourcesPath = "WeaponHitAudioDatabase";

    [Tooltip("Index 0 = level 1, Index 15 = level 16. Populated by the editor builder.")]
    public AudioClip[] levelClips = new AudioClip[MaxLevel];

    [Tooltip("Auto-discovered victory clip (filename contains 'victory' / 'victroy'). " +
             "PlayerSfx.PlayVictory falls back to this when its own clip slot is empty.")]
    public AudioClip victoryClip;

    [Tooltip("Auto-discovered UI click sound clip (filename contains 'tapclick').")]
    public AudioClip uiClickClip;

    [Tooltip("Auto-discovered UI hover sound clip (filename contains 'ui_clicksound').")]
    public AudioClip uiHoverClip;

    [Tooltip("Auto-discovered footstep clip (filename contains 'footstep').")]
    public AudioClip footstepClip;

    [Tooltip("Auto-discovered countdown clip (filename contains '3 2 1 go' or 'countdown').")]
    public AudioClip countdownClip;

    // ── Runtime accessor ────────────────────────────────────────────────────
    private static WeaponHitAudioDatabase _cached;
    private static bool _loadAttempted;

    public static WeaponHitAudioDatabase Instance
    {
        get
        {
            if (_cached != null) return _cached;
            if (_loadAttempted) return null;
            _loadAttempted = true;
            _cached = Resources.Load<WeaponHitAudioDatabase>(ResourcesPath);
            return _cached;
        }
    }

    /// <summary>
    /// Returns the AudioClip mapped to <paramref name="level"/> (1..16), or
    /// <c>null</c> if the database is missing or the slot is empty.
    /// </summary>
    public AudioClip GetClip(int level)
    {
        if (levelClips == null) return null;
        int idx = level - 1;
        if (idx < 0 || idx >= levelClips.Length) return null;
        return levelClips[idx];
    }

    // Editor-only escape hatch so the builder can invalidate the runtime cache
    // after rewriting the asset.
    public static void InvalidateCache()
    {
        _cached = null;
        _loadAttempted = false;
    }
}
