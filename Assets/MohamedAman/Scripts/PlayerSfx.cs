using UnityEngine;

/// <summary>
/// Central sound hub for MohamedAman audio assets: jump, slide, footsteps,
/// victory, plus an optional UI click clip override that is forwarded into
/// the existing <see cref="UIClickAudio"/> singleton on Awake.
///
/// Place ONE instance of this component on the Player root prefab and
/// assign the clips in the Inspector. Every hook below null-checks, so
/// leaving slots empty is safe.
///
/// Wiring summary:
///   - PlayerSfx.NotifyJump()        ← RobustThirdPersonMovement.HandleJumpAndGravity
///   - PlayerSfx.NotifySlideStart()  ← RobustThirdPersonMovement.HandleTacticalInput / HandleMovement
///   - Footsteps                     ← driven internally from Update() (no external call).
///   - PlayerSfx.PlayVictory()       ← LevelCompleteManager.Start
/// </summary>
[DisallowMultipleComponent]
public class PlayerSfx : MonoBehaviour
{
    public static PlayerSfx Instance { get; private set; }

    [Header("Clips (assign in Inspector)")]
    [Tooltip("Plays once on jump key press.")]
    public AudioClip jumpClip;
    [Tooltip("Plays once on the start of a slide (X tactical slide or Ctrl+sprint slide).")]
    public AudioClip slideClip;
    [Tooltip("One step is played every footstepInterval seconds while grounded + moving.")]
    public AudioClip footstepClip;
    [Tooltip("Plays once on level-complete / victory.")]
    public AudioClip victoryClip;
    [Tooltip("Optional override pushed into UIClickAudio on Awake. Leave empty to keep the existing click sound.")]
    public AudioClip uiClickOverride;
    [Tooltip("Optional hover override pushed into UIClickAudio on Awake. Leave empty to keep the existing hover sound.")]
    public AudioClip uiHoverOverride;

    [Header("Mixing")]
    [Range(0f, 2f)] public float jumpVolume      = 0.9f;
    [Range(0f, 2f)] public float slideVolume     = 0.9f;
    [Range(0f, 2f)] public float footstepVolume  = 0.55f;
    [Range(0f, 2f)] public float victoryVolume   = 1.0f;
    public Vector2 pitchJitter = new Vector2(0.95f, 1.05f);

    [Header("Footstep cadence (seconds between steps)")]
    [Tooltip("Step interval while walking.")]
    public float walkStepInterval = 0.48f;
    [Tooltip("Step interval while sprinting.")]
    public float sprintStepInterval = 0.32f;
    [Tooltip("Movement-vector magnitude threshold below which no footstep fires.")]
    public float minMoveSpeed = 0.15f;

    private AudioSource _source;
    private float _stepTimer;
    private bool _victoryPlayed;
    private float _lastJumpTime = -1f;
    private float _lastSlideTime = -1f;

    private void Awake()
    {
        Instance = this; // last one wins; PlayerSfx is per-player and gameplay scenes have one local player

        _source = GetComponent<AudioSource>();
        if (_source == null)
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0.4f;
        }

        if (uiClickOverride != null && UIClickAudio.Instance != null)
            UIClickAudio.Instance.SetClickClip(uiClickOverride);
        if (uiHoverOverride != null && UIClickAudio.Instance != null)
            UIClickAudio.Instance.SetHoverClip(uiHoverOverride);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public hooks (called from movement script) ───────────────────────────

    /// <summary>Fire once when the player initiates a jump.</summary>
    public void NotifyJump()
    {
        if (Time.time - _lastJumpTime < 0.05f) return; // de-dupe within a single frame burst
        _lastJumpTime = Time.time;
        PlayOneShot(jumpClip, jumpVolume);
    }

    /// <summary>Fire once when a slide begins (either Ctrl slide or X tactical slide).</summary>
    public void NotifySlideStart()
    {
        if (Time.time - _lastSlideTime < 0.15f) return;
        _lastSlideTime = Time.time;
        PlayOneShot(slideClip, slideVolume);
    }

    /// <summary>Drives the footstep cadence. Call every frame from the movement script
    /// with current grounded/sprint/speed state.</summary>
    public void TickFootsteps(bool grounded, bool sprinting, float horizontalSpeed)
    {
        if (!grounded || horizontalSpeed < minMoveSpeed)
        {
            _stepTimer = 0f;
            return;
        }
        float interval = sprinting ? sprintStepInterval : walkStepInterval;
        if (interval <= 0f) return;
        _stepTimer += Time.deltaTime;
        if (_stepTimer >= interval)
        {
            _stepTimer = 0f;
            PlayOneShot(footstepClip, footstepVolume);
        }
    }

    /// <summary>Plays the victory clip once per PlayerSfx lifetime.</summary>
    public static void PlayVictory()
    {
        if (Instance == null)
        {
            // Fallback: build a one-shot AudioSource on the camera so the
            // victory sound still plays even when the player PlayerSfx
            // instance has been destroyed by the end-match cinematic.
            AudioClip fallbackClip = WeaponHitAudioDatabase.Instance != null
                ? WeaponHitAudioDatabase.Instance.victoryClip
                : null;
            if (fallbackClip == null) return;
            Camera cam = Camera.main;
            Vector3 pos = cam != null ? cam.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(fallbackClip,
                pos,
                AudioSettingsRuntime.ScaledSfx(1f));
            return;
        }
        if (Instance._victoryPlayed) return;
        Instance._victoryPlayed = true;

        // Auto-fallback: if the designer never wired up victoryClip in the
        // Inspector, pull it from the auto-generated WeaponHitAudioDatabase,
        // which the editor builder populates from Assets/MohamedAman/Materials.
        AudioClip clip = Instance.victoryClip;
        if (clip == null && WeaponHitAudioDatabase.Instance != null)
            clip = WeaponHitAudioDatabase.Instance.victoryClip;

        Instance.PlayOneShot(clip, Instance.victoryVolume);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private void PlayOneShot(AudioClip clip, float baseVol)
    {
        if (clip == null || _source == null) return;
        _source.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        _source.PlayOneShot(clip, AudioSettingsRuntime.ScaledSfx(Mathf.Max(0f, baseVol)));
    }
}
