using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Black Ops 3-style "Winners Circle" end-match sequence:
///   1. Locks gameplay (player input + enemy AI freeze).
///   2. Smoothly drops <see cref="Time.timeScale"/> to 0.2× for the slow-mo.
///   3. Orbits a dedicated cinematic camera around the centroid of the top
///      three combatants (by kill count).
///   4. Triggers a programmatic victory pose on the #1 combatant.
///   5. Fades a full-screen results table in (Username, Kills, Deaths, Score)
///      with the winner's row highlighted in gold.
///   6. Hands off to <see cref="GameManager.LevelComplete"/> /
///      <see cref="GameManager.GameOver"/> when the player picks Play Again
///      or Main Menu.
///
/// All visuals are built procedurally — no prefabs required.
/// </summary>
public class EndMatchCinematic : MonoBehaviour
{
    public static EndMatchCinematic Instance { get; private set; }

    /// <summary>
    /// While the cinematic is running, gameplay scripts (player + enemies)
    /// short-circuit their per-frame updates so input/AI is fully frozen.
    /// </summary>
    public static bool GameplayLocked { get; private set; }

    private const float SlowMotionScale     = 0.2f;
    private const float OrbitRadius         = 6.5f;
    private const float OrbitHeight         = 2.4f;
    private const float OrbitSpeedDeg       = 22f;   // unscaled deg/sec
    private const float VictoryPoseDuration = 5.5f;  // seconds (real time)
    private const float ScoreboardFadeIn    = 0.55f; // seconds (real time)

    private Camera     _cinematicCam;
    private Camera     _previousMain;
    private float      _previousFixedDelta;
    private float      _previousTimeScale;
    private GameObject _scoreboardRoot;
    private bool       _hasFinished;
    private bool       _replayChosen;
    private bool       _userChoseTransition;

    /// <summary>
    /// Public entry point. Call from <see cref="GameManager"/> the moment
    /// the match timer expires. Spawns the cinematic if one isn't already
    /// running.
    /// </summary>
    public static void Begin(bool playerWon, Action onFinished)
    {
        if (Instance != null) return;
        GameObject host = new GameObject("EndMatchCinematic");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<EndMatchCinematic>();
        Instance.StartCoroutine(Instance.RunSequence(playerWon, onFinished));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameplayLocked = false;
        // Defensive — restore time scale if the host is killed mid-cinematic
        // (e.g. scene unloaded by another path).
        if (Mathf.Approximately(Time.timeScale, SlowMotionScale))
        {
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            if (_previousFixedDelta > 0f) Time.fixedDeltaTime = _previousFixedDelta;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Coroutine sequence
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunSequence(bool playerWon, Action onFinished)
    {
        GameplayLocked       = true;
        _previousTimeScale   = Time.timeScale;
        _previousFixedDelta  = Time.fixedDeltaTime;

        // Slow motion (over real time so the transition itself feels nice).
        yield return EaseTimeScaleTo(SlowMotionScale, 0.45f);

        // Resolve the top-3 combatants and their world transforms.
        List<MatchStatsManager.CombatantSnapshot> top3 = ResolveTopThree();
        Vector3 centroid = ComputeCentroid(top3);

        SetupCinematicCamera(centroid);
        TriggerVictoryPoseOnTopOne(top3);

        // Reveal cursor so the eventual scoreboard buttons are clickable.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Orbit phase — runs in real time so slow-mo doesn't slow the camera.
        float elapsed = 0f;
        float orbitDuration = VictoryPoseDuration;
        while (elapsed < orbitDuration)
        {
            UpdateOrbit(centroid, elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Build + fade in the scoreboard while the camera keeps a slow drift.
        BuildScoreboard(playerWon);
        yield return FadeInScoreboard(ScoreboardFadeIn);

        // Wait for player choice. RunSequence stays alive until either Play
        // Again or Main Menu is pressed via the buttons we built.
        while (!_hasFinished)
        {
            UpdateOrbit(centroid, Time.unscaledTime);
            yield return null;
        }

        // Restore time scale before transitioning out so the next scene
        // doesn't load in slow motion.
        Time.timeScale     = _previousTimeScale > 0f ? _previousTimeScale : 1f;
        Time.fixedDeltaTime = _previousFixedDelta > 0f ? _previousFixedDelta : 0.02f;
        GameplayLocked     = false;

        // Tear down our camera + UI before the host scene unloads.
        if (_cinematicCam != null) Destroy(_cinematicCam.gameObject);
        if (_scoreboardRoot != null) Destroy(_scoreboardRoot);
        Destroy(gameObject);

        // Drive the actual scene transition. If the user clicked one of our
        // scoreboard buttons we route through that path. Otherwise we fall
        // back to the caller's onFinished (LevelComplete / GameOver in
        // GameManager) — used when the cinematic is forcefully aborted.
        if (_userChoseTransition)
        {
            if (GameManager.Instance == null)
            {
                SceneManager.LoadScene("MainMenu");
                yield break;
            }
            if (_replayChosen) GameManager.Instance.ReplayCurrentLevel();
            else               GameManager.Instance.GoToMainMenu();
        }
        else
        {
            onFinished?.Invoke();
        }
    }

    private IEnumerator EaseTimeScaleTo(float target, float duration)
    {
        float t = 0f;
        float start = Time.timeScale;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            Time.timeScale = Mathf.Lerp(start, target, k);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            yield return null;
        }
        Time.timeScale = target;
        Time.fixedDeltaTime = 0.02f * target;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Camera + orbit
    // ════════════════════════════════════════════════════════════════════════

    private void SetupCinematicCamera(Vector3 centroid)
    {
        _previousMain = Camera.main;
        if (_previousMain != null) _previousMain.enabled = false;

        // Disable the existing third-person CameraController so it doesn't
        // fight us for control of the rendered camera.
        if (CameraController.Instance != null)
            CameraController.Instance.enabled = false;

        GameObject camObj = new GameObject("CinematicCamera");
        camObj.transform.SetParent(transform, false);
        _cinematicCam = camObj.AddComponent<Camera>();
        _cinematicCam.clearFlags    = CameraClearFlags.Skybox;
        _cinematicCam.fieldOfView   = 42f;
        _cinematicCam.nearClipPlane = 0.05f;
        _cinematicCam.farClipPlane  = 600f;
        _cinematicCam.depth         = 30f;

        // Tag this camera as the main camera while the cinematic runs so any
        // Camera.main lookups in HUD code resolve to it.
        camObj.tag = "MainCamera";

        camObj.AddComponent<AudioListener>();
        if (_previousMain != null)
        {
            AudioListener oldListener = _previousMain.GetComponent<AudioListener>();
            if (oldListener != null) oldListener.enabled = false;
        }

        // Initial framing — face the centroid from the front.
        camObj.transform.position = centroid + new Vector3(0f, OrbitHeight, OrbitRadius);
        camObj.transform.LookAt(centroid + Vector3.up * 1.0f);
    }

    private void UpdateOrbit(Vector3 centroid, float orbitTime)
    {
        if (_cinematicCam == null) return;

        // Slow ease-in: speed ramps from 0 → OrbitSpeedDeg over the first
        // 0.7s so the orbit doesn't snap-spin at frame 0.
        float speedRamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(orbitTime / 0.7f));
        float angle     = orbitTime * OrbitSpeedDeg * speedRamp * Mathf.Deg2Rad;
        // Subtle vertical bob so the orbit doesn't feel like a turntable.
        float bob       = Mathf.Sin(orbitTime * 0.9f) * 0.35f;

        Vector3 desired = centroid
                        + new Vector3(Mathf.Sin(angle) * OrbitRadius,
                                      OrbitHeight + bob,
                                      Mathf.Cos(angle) * OrbitRadius);
        _cinematicCam.transform.position = Vector3.Lerp(
            _cinematicCam.transform.position, desired, 6f * Time.unscaledDeltaTime);
        _cinematicCam.transform.LookAt(centroid + Vector3.up * 1.05f);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Victory pose
    // ════════════════════════════════════════════════════════════════════════

    private void TriggerVictoryPoseOnTopOne(List<MatchStatsManager.CombatantSnapshot> top)
    {
        if (top == null || top.Count == 0) return;
        Transform t = top[0].Transform;
        if (t == null) return;
        VictoryPoseDriver driver = t.gameObject.GetComponent<VictoryPoseDriver>();
        if (driver == null) driver = t.gameObject.AddComponent<VictoryPoseDriver>();
        driver.Begin();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Scoreboard UI
    // ════════════════════════════════════════════════════════════════════════

    private void BuildScoreboard(bool playerWon)
    {
        _scoreboardRoot = new GameObject("EndMatchScoreboard");
        DontDestroyOnLoad(_scoreboardRoot);

        Canvas canvas = _scoreboardRoot.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        _scoreboardRoot.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = _scoreboardRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // Dim the cinematic behind the panel.
        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(_scoreboardRoot.transform, false);
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(dimImg.rectTransform);

        // Panel.
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(_scoreboardRoot.transform, false);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.10f, 0.16f, 0.93f);
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor    = new Color(0.30f, 0.55f, 1f, 0.55f);
        panelOutline.effectDistance = new Vector2(2.5f, -2.5f);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot     = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(1320f, 880f);
        panelRT.anchoredPosition = Vector2.zero;

        // Title — "MATCH OVER" + winner name, sized large for impact.
        IReadOnlyList<MatchStatsManager.CombatantSnapshot> all = GetAllCombatantsSorted();
        string winnerName = (all.Count > 0) ? all[0].DisplayName : "—";
        bool   winnerIsPlayer = (all.Count > 0) && all[0].IsPlayer;
        string banner = playerWon || winnerIsPlayer ? "VICTORY" : "MATCH OVER";

        MakeText(panel.transform, banner, 96, FontStyles.Bold,
            (playerWon || winnerIsPlayer) ? new Color(0.30f, 0.95f, 0.55f, 1f) : new Color(0.95f, 0.55f, 0.30f, 1f),
            new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.97f));
        MakeText(panel.transform, "WINNER  " + winnerName, 42, FontStyles.Bold,
            new Color(1.00f, 0.84f, 0.30f, 1f),
            new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.86f));

        // Header row.
        BuildHeaderRow(panel.transform, 0.70f);

        // Result rows.
        const float topY    = 0.69f;
        const float rowH    = 0.075f;
        const float rowGap  = 0.005f;
        int rows = Mathf.Min(all.Count, 8);
        for (int i = 0; i < rows; i++)
            BuildResultRow(panel.transform, all[i], i == 0,
                topY - i * (rowH + rowGap), topY - i * (rowH + rowGap) - rowH);

        // Earned-credits flash.
        int earned = (SessionManager.Instance != null && all.Count > 0 && all[0].IsPlayer)
            ? (all[0].Kills * SessionManager.CreditsPerKill + SessionManager.CreditsPerMatchWin)
            : (SessionManager.Instance != null ? all.Count > 0 ? all[0].Kills * SessionManager.CreditsPerKill : 0 : 0);

        int liveCredits = SessionManager.Instance != null ? SessionManager.Instance.Credits : 0;
        MakeText(panel.transform,
            "+" + earned.ToString("N0") + " CR EARNED   ·   BALANCE  " + liveCredits.ToString("N0"),
            28, FontStyles.Bold,
            new Color(0.30f, 0.85f, 1f, 1f),
            new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.20f));

        // Buttons.
        Button playAgainBtn = MakeButton(panel.transform, "PLAY AGAIN",
            new Vector2(0.18f, 0.04f), new Vector2(0.48f, 0.11f),
            new Color(0.30f, 0.95f, 0.55f, 1f), new Color(0.04f, 0.06f, 0.10f, 1f),
            () => Finish(replay: true));
        Button mainMenuBtn = MakeButton(panel.transform, "MAIN MENU",
            new Vector2(0.52f, 0.04f), new Vector2(0.82f, 0.11f),
            new Color(0.32f, 0.56f, 0.96f, 1f), Color.white,
            () => Finish(replay: false));

        // Arrow + Enter navigation.
        MenuKeyboardNavigator.AttachHorizontal(_scoreboardRoot, new List<Selectable> { playAgainBtn, mainMenuBtn });

        // Default selection so Enter works without a click.
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playAgainBtn.gameObject);
    }

    private IReadOnlyList<MatchStatsManager.CombatantSnapshot> GetAllCombatantsSorted()
    {
        if (MatchStatsManager.Instance == null)
            return Array.Empty<MatchStatsManager.CombatantSnapshot>();
        return MatchStatsManager.Instance.GetTopCombatants(64);
    }

    private void BuildHeaderRow(Transform parent, float y)
    {
        GameObject row = new GameObject("HeaderRow");
        row.transform.SetParent(parent, false);
        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.22f, 0.32f, 1f);
        RectTransform rRT = row.GetComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0.04f, y);
        rRT.anchorMax = new Vector2(0.96f, y + 0.06f);
        rRT.offsetMin = rRT.offsetMax = Vector2.zero;

        MakeRowText(row.transform, "#",       new Vector2(0.00f, 0f), new Vector2(0.06f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        MakeRowText(row.transform, "USERNAME",new Vector2(0.07f, 0f), new Vector2(0.55f, 1f), TextAlignmentOptions.Left,   FontStyles.Bold);
        MakeRowText(row.transform, "KILLS",   new Vector2(0.55f, 0f), new Vector2(0.68f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        MakeRowText(row.transform, "DEATHS",  new Vector2(0.68f, 0f), new Vector2(0.81f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        MakeRowText(row.transform, "SCORE",   new Vector2(0.81f, 0f), new Vector2(1.00f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private void BuildResultRow(Transform parent, MatchStatsManager.CombatantSnapshot s, bool isWinner, float yMax, float yMin)
    {
        GameObject row = new GameObject("Row_" + s.DisplayName);
        row.transform.SetParent(parent, false);
        Image bg = row.AddComponent<Image>();

        // Winner row → gold/blue banner. Player row → cyan if not the winner.
        // Everyone else → flat dark slate.
        if (isWinner)
            bg.color = new Color(1.00f, 0.84f, 0.30f, 0.95f);
        else if (s.IsPlayer)
            bg.color = new Color(0.20f, 0.50f, 0.95f, 0.85f);
        else
            bg.color = new Color(0.12f, 0.16f, 0.24f, 0.85f);

        RectTransform rRT = row.GetComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0.04f, yMin);
        rRT.anchorMax = new Vector2(0.96f, yMax);
        rRT.offsetMin = rRT.offsetMax = Vector2.zero;

        Color txt = isWinner ? new Color(0.05f, 0.05f, 0.10f, 1f) : new Color(0.94f, 0.96f, 1f, 1f);

        // Pull the actual queue position so we can show #1, #2, ...
        // Caller passed a pre-sorted list so the array index ≈ rank.
        IReadOnlyList<MatchStatsManager.CombatantSnapshot> all = GetAllCombatantsSorted();
        int rank = 1;
        for (int i = 0; i < all.Count; i++) if (all[i].Id == s.Id) { rank = i + 1; break; }

        MakeRowText(row.transform, "#" + rank,            new Vector2(0.00f, 0f), new Vector2(0.06f, 1f), TextAlignmentOptions.Center, FontStyles.Bold, txt, 28);
        MakeRowText(row.transform, s.DisplayName,         new Vector2(0.07f, 0f), new Vector2(0.55f, 1f), TextAlignmentOptions.Left,   FontStyles.Bold, txt, 28);
        MakeRowText(row.transform, s.Kills.ToString(),    new Vector2(0.55f, 0f), new Vector2(0.68f, 1f), TextAlignmentOptions.Center, FontStyles.Bold, txt, 28);
        MakeRowText(row.transform, s.Deaths.ToString(),   new Vector2(0.68f, 0f), new Vector2(0.81f, 1f), TextAlignmentOptions.Center, FontStyles.Bold, txt, 28);
        MakeRowText(row.transform, s.Score.ToString("N0"),new Vector2(0.81f, 0f), new Vector2(1.00f, 1f), TextAlignmentOptions.Center, FontStyles.Bold, txt, 28);
    }

    private IEnumerator FadeInScoreboard(float duration)
    {
        if (_scoreboardRoot == null) yield break;
        CanvasGroup cg = _scoreboardRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = _scoreboardRoot.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private void Finish(bool replay)
    {
        if (_hasFinished) return;
        _hasFinished        = true;
        _replayChosen       = replay;
        _userChoseTransition = true;
        // Actual scene transition happens in RunSequence after time scale
        // has been restored — keeps the slow-mo from leaking into the next
        // scene.
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════════════

    private List<MatchStatsManager.CombatantSnapshot> ResolveTopThree()
    {
        List<MatchStatsManager.CombatantSnapshot> list = new List<MatchStatsManager.CombatantSnapshot>();
        if (MatchStatsManager.Instance == null) return list;
        var top = MatchStatsManager.Instance.GetTopCombatants(3);
        for (int i = 0; i < top.Count; i++) list.Add(top[i]);
        return list;
    }

    private Vector3 ComputeCentroid(List<MatchStatsManager.CombatantSnapshot> list)
    {
        if (list == null || list.Count == 0)
        {
            // Fallback to the player's position.
            GameObject p = GameObject.FindWithTag("Player");
            return p != null ? p.transform.position : Vector3.zero;
        }

        Vector3 acc = Vector3.zero;
        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            Transform t = list[i].Transform;
            if (t == null) continue;
            acc += t.position;
            count++;
        }
        if (count == 0)
        {
            GameObject p = GameObject.FindWithTag("Player");
            return p != null ? p.transform.position : Vector3.zero;
        }
        return acc / count;
    }

    // ─── UI helpers ────────────────────────────────────────────────────────

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
        FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = new GameObject("T_" + (text.Length > 4 ? text.Substring(0, 4) : text));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return tmp;
    }

    private static TextMeshProUGUI MakeRowText(Transform parent, string text,
        Vector2 anchorMin, Vector2 anchorMax,
        TextAlignmentOptions align, FontStyles style, Color? color = null, float size = 22f)
    {
        Color c = color ?? new Color(0.94f, 0.96f, 1f, 1f);
        GameObject obj = new GameObject("RT_" + (text.Length > 4 ? text.Substring(0, 4) : text));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = c;
        tmp.alignment = align;
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = new Vector2(12f, 0f);
        r.offsetMax = new Vector2(-12f, 0f);
        return tmp;
    }

    private static Button MakeButton(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject(label + "_Btn");
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.color = bg;
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        ColorBlock cb = btn.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
        cb.pressedColor     = Color.Lerp(bg, Color.black, 0.18f);
        cb.selectedColor    = Color.Lerp(bg, Color.white, 0.18f);
        btn.colors = cb;

        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor    = new Color(0.04f, 0.06f, 0.10f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject tx = new GameObject("Label");
        tx.transform.SetParent(obj.transform, false);
        TextMeshProUGUI tmp = tx.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 32f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = fg;
        RectTransform tr = tx.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;

        // UIClickAudio's auto-attach runs after every scene load — but our
        // overlay is built mid-frame, so make sure the click sound binds now.
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.AttachIfNeeded(btn);
        return btn;
    }
}

/// <summary>
/// Programmatic victory pose driver — scales the body up slightly, rotates
/// it slowly, and bobs it vertically so the #1 combatant feels celebratory
/// without requiring an Animator override clip.
/// </summary>
public class VictoryPoseDriver : MonoBehaviour
{
    private Vector3 _baseScale;
    private Vector3 _basePos;
    private Quaternion _baseRot;
    private bool _running;
    private float _t;

    public void Begin()
    {
        _baseScale = transform.localScale;
        _basePos   = transform.position;
        _baseRot   = transform.rotation;
        _running   = true;
        _t         = 0f;
    }

    private void LateUpdate()
    {
        if (!_running) return;
        _t += Time.unscaledDeltaTime;

        float scaleK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / 0.6f));
        Vector3 victoryScale = _baseScale * Mathf.Lerp(1f, 1.15f, scaleK);
        transform.localScale = victoryScale;

        // Vertical bob + slow yaw so the winner looks alive on the podium.
        float bob = Mathf.Sin(_t * 2.6f) * 0.15f;
        transform.position = new Vector3(_basePos.x, _basePos.y + bob, _basePos.z);
        transform.rotation = _baseRot * Quaternion.Euler(0f, _t * 32f, 0f);
    }
}
