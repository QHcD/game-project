using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Auto-attaches a shared hover/selection sound to every Selectable (Button,
/// Toggle, Slider, etc.) in every loaded scene. Mirrors the bootstrap pattern
/// already used by <see cref="UIClickAudio"/>.
///
/// Sources its clip from Resources/Audio/HoverButtonSound (AudioClip). If the
/// asset is missing, the system falls back to a procedurally-synthesised tick
/// so menus are never silent.
///
/// Dedup: <see cref="ISelectHandler.OnSelect"/> fires only when selection
/// actually changes between widgets, and pointer-enter fires only on a fresh
/// hover, so no extra throttling is normally needed. A small per-target
/// cooldown still guards against pointer+select firing twice on the same
/// frame when the mouse lands on the already-selected element.
/// </summary>
[DisallowMultipleComponent]
public class UIHoverSelectAudio : MonoBehaviour
{
    public static UIHoverSelectAudio Instance { get; private set; }

    private const string ResourcePath = "Audio/HoverButtonSound";

    [Header("Mixing")]
    [Tooltip("Master scalar applied to PlayOneShot (multiplied by UI slider).")]
    public float volume = 0.35f;

    [Tooltip("Pitch jitter range — gives consecutive hovers a subtle variety.")]
    public Vector2 pitchJitter = new Vector2(0.97f, 1.03f);

    [Tooltip("Minimum seconds between sounds on the SAME widget. Stops mouse+keyboard double-fire.")]
    public float perTargetCooldown = 0.06f;

    private AudioSource _audioSource;
    private AudioClip _hoverClip;

    private readonly HashSet<int> _attached = new HashSet<int>();
    private int _lastSourceId;
    private float _lastPlayTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject host = new GameObject("UIHoverSelectAudio");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<UIHoverSelectAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;

        ResolveHoverClip();
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToActiveScene();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode) => StartCoroutine(AttachAfterFrame());

    private IEnumerator AttachAfterFrame()
    {
        yield return null;
        AttachToActiveScene();
    }

    public void AttachToActiveScene()
    {
#if UNITY_2023_1_OR_NEWER
        Selectable[] items = Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        Selectable[] items = Object.FindObjectsOfType<Selectable>(true);
#endif
        for (int i = 0; i < items.Length; i++) AttachIfNeeded(items[i]);
    }

    public void AttachIfNeeded(Selectable s)
    {
        if (s == null) return;
        int id = s.GetInstanceID();
        if (_attached.Contains(id)) return;
        _attached.Add(id);

        if (s.GetComponent<UIHoverSelectRelay>() == null)
            s.gameObject.AddComponent<UIHoverSelectRelay>();
    }

    /// <summary>Plays the navigation sound. <paramref name="sourceId"/> is the
    /// instance ID of the originating widget; identical IDs within
    /// <see cref="perTargetCooldown"/> are suppressed.</summary>
    public void PlayNav(int sourceId)
    {
        if (_audioSource == null) return;
        float now = Time.unscaledTime;
        if (sourceId == _lastSourceId && now - _lastPlayTime < perTargetCooldown) return;
        _lastSourceId = sourceId;
        _lastPlayTime = now;

        _audioSource.pitch = Random.Range(pitchJitter.x, pitchJitter.y);

        float vol = Mathf.Clamp01(volume) * AudioSettingsRuntime.ScaledUi(1f);
        if (_hoverClip != null && vol > 0f)
            _audioSource.PlayOneShot(_hoverClip, vol);
    }

    private void ResolveHoverClip()
    {
        AudioClip ac = Resources.Load<AudioClip>(ResourcePath);
        if (ac != null) { _hoverClip = ac; return; }
        _hoverClip = SynthesiseTick();
    }

    private static AudioClip SynthesiseTick()
    {
        const int sr = 44100;
        const float dur = 0.045f;
        int n = Mathf.RoundToInt(sr * dur);
        float[] data = new float[n];
        Random.State prev = Random.state;
        Random.InitState(0x71CC);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 110f);
            float tone = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.6f;
            float noise = Random.Range(-1f, 1f) * 0.25f;
            data[i] = (tone + noise) * env;
        }
        Random.state = prev;
        AudioClip clip = AudioClip.Create("ProceduralUIHover", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>
/// Per-Selectable relay that routes hover (pointer-enter) and selection-change
/// events to <see cref="UIHoverSelectAudio.PlayNav"/>. Added automatically by
/// the bootstrap; no manual setup required.
/// </summary>
[DisallowMultipleComponent]
public class UIHoverSelectRelay : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        Selectable s = GetComponent<Selectable>();
        if (s == null || !s.IsInteractable()) return;
        if (UIHoverSelectAudio.Instance != null)
            UIHoverSelectAudio.Instance.PlayNav(GetInstanceID());
    }

    public void OnSelect(BaseEventData eventData)
    {
        Selectable s = GetComponent<Selectable>();
        if (s == null || !s.IsInteractable()) return;
        if (UIHoverSelectAudio.Instance != null)
            UIHoverSelectAudio.Instance.PlayNav(GetInstanceID());
    }
}
