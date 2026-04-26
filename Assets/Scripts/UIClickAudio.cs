using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Global UI click-sound bootstrap. Auto-attaches a click sound effect to
/// every <see cref="Button"/> in every scene loaded for the rest of the
/// session.
///
/// Loading order, picked at boot:
///   1. <c>Resources/Audio/ClickButtonSound</c> as <see cref="AudioClip"/>.
///   2. <c>Resources/Audio/ClickButtonSound</c> as <see cref="VideoClip"/>
///      (auto-extracted into the AudioSource via <see cref="VideoPlayer"/>).
///   3. A snappy procedural click synthesised at runtime — guaranteed
///      fallback so the system always produces audio even if the asset
///      can't be decoded (e.g. <c>.mov</c> codec not supported by Unity's
///      audio backend).
///
/// Add or change the source clip by dropping a <c>.wav</c>, <c>.ogg</c>
/// or <c>.mp3</c> named <c>ClickButtonSound</c> into
/// <c>Assets/Resources/Audio/</c> — the loader prefers <see cref="AudioClip"/>
/// over <see cref="VideoClip"/> so any audio asset overrides the fallback.
/// </summary>
public class UIClickAudio : MonoBehaviour
{
    public  static UIClickAudio Instance { get; private set; }
    private const string ResourcePath = "Audio/ClickButtonSound";

    [Header("Mixing")]
    [Tooltip("Master volume scalar applied to PlayOneShot.")]
    public float volume = 0.55f;

    [Tooltip("Pitch jitter range — gives consecutive clicks a subtle variety.")]
    public Vector2 pitchJitter = new Vector2(0.96f, 1.04f);

    private AudioSource _audioSource;
    private AudioClip   _clickClip;
    private VideoPlayer _videoFallback;

    // Track the buttons we've already wired so we don't stack listeners on
    // the same Button across multiple scene loads.
    private readonly HashSet<int> _attachedInstanceIds = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject host = new GameObject("UIClickAudio");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<UIClickAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop        = false;
        _audioSource.spatialBlend = 0f;

        ResolveClickClip();

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Attach to anything that's already loaded (e.g. the boot menu).
        AttachToActiveScene();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // Defer one frame so dynamically-built canvases (RuntimeMenuBuilder
        // creates buttons in Awake/Start) finish spawning their children.
        StartCoroutine(AttachAfterFrame());
    }

    private System.Collections.IEnumerator AttachAfterFrame()
    {
        yield return null;
        AttachToActiveScene();
    }

    /// <summary>
    /// Plays the click sound. Safe to call from any UnityEvent listener.
    /// </summary>
    public void PlayClick()
    {
        if (_audioSource == null) return;

        // Subtle pitch jitter so spamming buttons doesn't sound robotic.
        _audioSource.pitch = Random.Range(pitchJitter.x, pitchJitter.y);

        if (_clickClip != null)
        {
            _audioSource.PlayOneShot(_clickClip, Mathf.Clamp01(volume));
            return;
        }

        if (_videoFallback != null)
        {
            // VideoPlayer audio extraction — restart from t=0 so rapid clicks
            // don't run into the previous play tail.
            _videoFallback.Stop();
            _videoFallback.time = 0;
            _videoFallback.Play();
        }
    }

    /// <summary>
    /// Walks every loaded scene + every active <see cref="Button"/> and
    /// adds <see cref="PlayClick"/> as an additional onClick listener.
    /// Idempotent — re-runs on scene load are cheap because every wired
    /// button is registered in <see cref="_attachedInstanceIds"/>.
    /// </summary>
    public void AttachToActiveScene()
    {
        // FindObjectsByType is the modern API; FindObjectOfType is deprecated
        // on Unity 6 and emits CS0618 warnings.
#if UNITY_2023_1_OR_NEWER
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
#endif
        for (int i = 0; i < buttons.Length; i++) AttachIfNeeded(buttons[i]);
    }

    /// <summary>
    /// Public hook so any script that programmatically spawns a button
    /// can opt-in immediately without waiting for the next scene-load tick.
    /// </summary>
    public void AttachIfNeeded(Button button)
    {
        if (button == null) return;
        int id = button.GetInstanceID();
        if (_attachedInstanceIds.Contains(id)) return;
        _attachedInstanceIds.Add(id);
        button.onClick.AddListener(PlayClick);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Click-clip resolution
    // ────────────────────────────────────────────────────────────────────────

    private void ResolveClickClip()
    {
        // Priority 1 — AudioClip. Works for .wav/.ogg/.mp3 dropped at
        //   Resources/Audio/ClickButtonSound.
        AudioClip ac = Resources.Load<AudioClip>(ResourcePath);
        if (ac != null)
        {
            _clickClip = ac;
            return;
        }

        // Priority 2 — VideoClip. .mov/.mp4 import as VideoClip; route their
        //   audio track through a hidden VideoPlayer into our AudioSource.
        VideoClip vc = Resources.Load<VideoClip>(ResourcePath);
        if (vc != null)
        {
            _videoFallback = gameObject.AddComponent<VideoPlayer>();
            _videoFallback.playOnAwake       = false;
            _videoFallback.isLooping         = false;
            _videoFallback.source            = VideoSource.VideoClip;
            _videoFallback.clip              = vc;
            _videoFallback.audioOutputMode   = VideoAudioOutputMode.AudioSource;
            _videoFallback.renderMode        = VideoRenderMode.APIOnly; // headless
            _videoFallback.skipOnDrop        = true;
            _videoFallback.EnableAudioTrack(0, true);
            _videoFallback.SetTargetAudioSource(0, _audioSource);
            return;
        }

        // Priority 3 — synthesise a snappy procedural click. Guaranteed to
        //   exist on every platform.
        _clickClip = SynthesiseClick();
    }

    /// <summary>
    /// Produces a short, percussive click — a quick noise burst layered with
    /// a high-frequency sine that decays exponentially. ~70 ms long, mono.
    /// </summary>
    private static AudioClip SynthesiseClick()
    {
        const int   sampleRate = 44100;
        const float duration   = 0.072f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];

        // Use Random.Range with a fixed-ish seed so the click sounds the same
        // every boot — easier on the player's ear when spamming buttons.
        Random.State previous = Random.state;
        Random.InitState(0xC11C);

        for (int i = 0; i < samples; i++)
        {
            float t        = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 65f);                  // fast decay
            float noise    = Random.Range(-1f, 1f) * 0.55f;
            float tone     = Mathf.Sin(2f * Mathf.PI * 1500f * t) * 0.75f;
            data[i]        = (noise + tone) * envelope;
        }

        Random.state = previous;

        AudioClip clip = AudioClip.Create("ProceduralUIClick", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
