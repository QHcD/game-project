using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game-start flow: show an automated intro camera, disable gameplay camera and player control,
/// then hand control back when the cutscene completes (or after a fallback timeout).
/// </summary>
[DisallowMultipleComponent]
public class IntroSequenceFlowManager : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("The cinematic camera rig (must have Camera + IntroCutsceneCamera).")]
    public IntroCutsceneCamera cutsceneRig;

    [Tooltip("Gameplay camera object (e.g. the object with CameraController + Camera). Disabled during intro.")]
    public GameObject gameplayCameraObject;

    [Tooltip("Optional: higher depth on cutscene cam so it draws on top. Set at runtime if non-zero.")]
    public float cutsceneCameraDepth = 10f;

    [Header("Player")]
    [Tooltip("Main player controller — disabled during intro.")]
    public PlayerController playerController;

    [Tooltip("Extra behaviours to disable (combat, weapons, input helpers, etc.).")]
    public List<MonoBehaviour> extraPlayerBehavioursToDisable = new List<MonoBehaviour>();

    [Header("Timing")]
    [Tooltip("Wait this many seconds after cutscene reports done before enabling gameplay (fade / VO hook).")]
    public float postIntroDelay = 0f;

    [Tooltip("Safety: force end intro after this many seconds even if cutscene logic stalls.")]
    public float maxIntroDuration = 120f;

    [Header("Debug")]
    public bool playOnStart = true;
    public bool logSteps = false;

    readonly List<MonoBehaviour> _disabledExtras = new List<MonoBehaviour>();
    bool _introComplete;
    Coroutine _flow;

    void Start()
    {
        if (playOnStart)
            BeginIntroFlow();
    }

    /// <summary>Run intro from code (e.g. after async load).</summary>
    public void BeginIntroFlow()
    {
        if (_flow != null)
            StopCoroutine(_flow);
        _flow = StartCoroutine(IntroFlowRoutine());
    }

    IEnumerator IntroFlowRoutine()
    {
        _introComplete = false;

        if (cutsceneRig == null)
        {
            Debug.LogError("[IntroSequenceFlowManager] Assign cutsceneRig.");
            yield break;
        }

        Camera cutsceneCam = cutsceneRig.GetComponent<Camera>();
        if (cutsceneCam == null)
        {
            Debug.LogError("[IntroSequenceFlowManager] Cutscene rig needs a Camera component.");
            yield break;
        }

        // ── 1) Enable cutscene camera, disable gameplay camera ─────────────────
        if (gameplayCameraObject != null)
            gameplayCameraObject.SetActive(false);

        cutsceneCam.enabled = true;
        if (cutsceneCameraDepth != 0f)
            cutsceneCam.depth = cutsceneCameraDepth;

        cutsceneRig.gameObject.SetActive(true);

        if (logSteps)
            Debug.Log("[IntroSequenceFlowManager] Cutscene camera on, gameplay camera off.");

        // ── 2) Disable player movement / combat ───────────────────────────────
        if (playerController != null)
            playerController.enabled = false;

        _disabledExtras.Clear();
        for (int i = 0; i < extraPlayerBehavioursToDisable.Count; i++)
        {
            MonoBehaviour mb = extraPlayerBehavioursToDisable[i];
            if (mb != null && mb.enabled)
            {
                mb.enabled = false;
                _disabledExtras.Add(mb);
            }
        }

        if (logSteps)
            Debug.Log("[IntroSequenceFlowManager] Player controls disabled.");

        // ── 3) Play cutscene + 4) wait for duration / completion ────────────
        cutsceneRig.SequenceFinished += OnCutsceneFinished;
        cutsceneRig.Play();

        // Wait until the cutscene reports done, or a safety timeout (whichever comes first).
        float deadline = Time.unscaledTime + Mathf.Max(maxIntroDuration, cutsceneRig.TotalDuration + 2f);
        while (!_introComplete && Time.unscaledTime < deadline)
            yield return null;

        cutsceneRig.SequenceFinished -= OnCutsceneFinished;

        if (!_introComplete && logSteps)
            Debug.LogWarning("[IntroSequenceFlowManager] Intro timed out; forcing gameplay start.");

        if (postIntroDelay > 0f)
            yield return new WaitForSecondsRealtime(postIntroDelay);

        // ── 5) Restore gameplay ───────────────────────────────────────────────
        cutsceneRig.Stop();
        cutsceneCam.enabled = false;
        cutsceneRig.gameObject.SetActive(false);

        if (gameplayCameraObject != null)
            gameplayCameraObject.SetActive(true);

        if (playerController != null)
            playerController.enabled = true;

        for (int i = 0; i < _disabledExtras.Count; i++)
        {
            if (_disabledExtras[i] != null)
                _disabledExtras[i].enabled = true;
        }
        _disabledExtras.Clear();

        if (logSteps)
            Debug.Log("[IntroSequenceFlowManager] Gameplay camera and player controls restored.");

        _flow = null;
    }

    void OnCutsceneFinished()
    {
        _introComplete = true;
    }
}
