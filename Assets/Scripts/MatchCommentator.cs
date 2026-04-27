using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Audio-only match commentary. 100% programmatic: uses <see cref="GameManager.PlayMatchVoiceOneShot"/>,
/// <see cref="VoClipAutoIndex"/> for <c>Resources/Audio/VO</c>, no Inspector wiring, no UI text.
/// Lives on the same GameObject as <see cref="GameManager"/> (added from <c>GameManager.Awake</c>).
/// </summary>
[DisallowMultipleComponent]
public class MatchCommentator : MonoBehaviour
{
    private const int PriorityTime10  = 100;
    private const int PriorityTime30  = 85;
    private const int PriorityTime60  = 70;
    private const int PriorityLowHp   = 55;
    private const int PriorityAlive1  = 50;
    private const int PriorityAlive3  = 38;
    private const int PriorityAlive5  = 34;
    private const int PriorityAlive10 = 30;

    private const float LowHpEnterRatio = 0.20f;
    private const float LowHpClearRatio = 0.35f;
    private const float CooldownHigh    = 0.28f;
    private const float CooldownLow     = 0.85f;

    private float _nextVoiceAllowedUnscaled;
    private int _prevAliveCount = -1;
    private float _prevTimeRemaining = -1f;
    private bool _lowHealthLatched;

    private void OnEnable()
    {
        VoClipAutoIndex.EnsureLoaded();
        SceneDispatch.SceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneDispatch.SceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
            ResetMatchLatches();
    }

    private void ResetMatchLatches()
    {
        _prevAliveCount    = -1;
        _prevTimeRemaining = -1f;
        _lowHealthLatched  = false;
        _nextVoiceAllowedUnscaled = 0f;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene") return;
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        if (EndMatchCinematic.GameplayLocked) return;
        if (HUDManager.Instance != null && HUDManager.Instance.IsMatchFinished) return;

        VoClipAutoIndex.EnsureLoaded();

        int alive = CountLivingEnemies();
        float limit = gm.LevelTimeLimitSeconds;
        float elapsed = HUDManager.Instance != null
            ? HUDManager.Instance.MatchElapsedSeconds
            : gm.levelTime;
        float remaining = Mathf.Max(0f, limit - elapsed);

        if (_prevTimeRemaining < 0f)
        {
            _prevTimeRemaining = remaining;
            _prevAliveCount    = alive;
            return;
        }

        PlayerHealth ph = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        float hpRatio = (ph != null && ph.maxHealth > 0.1f)
            ? ph.currentHealth / ph.maxHealth
            : 1f;

        if (hpRatio > LowHpClearRatio)
            _lowHealthLatched = false;

        var candidates = new List<VoCandidate>(8);

        if (remaining > 0f)
        {
            if (_prevTimeRemaining > 10f && remaining <= 10f)
                candidates.Add(new VoCandidate(PriorityTime10, VoClipAutoIndex.ResolveTimeTenUrgency));
            if (_prevTimeRemaining > 30f && remaining <= 30f)
                candidates.Add(new VoCandidate(PriorityTime30, VoClipAutoIndex.ResolveTimeThirtyUrgency));
            if (_prevTimeRemaining > 60f && remaining <= 60f)
                candidates.Add(new VoCandidate(PriorityTime60, VoClipAutoIndex.ResolveTimeOneMinuteUrgency));
        }

        if (!_lowHealthLatched && hpRatio <= LowHpEnterRatio && hpRatio > 0f)
        {
            _lowHealthLatched = true;
            candidates.Add(new VoCandidate(PriorityLowHp, VoClipAutoIndex.ResolvePlayerCriticalHealth));
        }

        // Audio-only: milestones when living enemy count hits 10 / 5 / 3 / 1
        // (same cadence as “few left” elimination VO in fixed-size matches).
        if (alive < _prevAliveCount)
        {
            if (_prevAliveCount > 10 && alive == 10)
                candidates.Add(new VoCandidate(PriorityAlive10, () => VoClipAutoIndex.ResolveEnemiesRemain(10)));
            else if (_prevAliveCount > 5 && alive == 5)
                candidates.Add(new VoCandidate(PriorityAlive5, () => VoClipAutoIndex.ResolveEnemiesRemain(5)));
            else if (_prevAliveCount > 3 && alive == 3)
                candidates.Add(new VoCandidate(PriorityAlive3, () => VoClipAutoIndex.ResolveEnemiesRemain(3)));
            else if (_prevAliveCount > 1 && alive == 1)
                candidates.Add(new VoCandidate(PriorityAlive1, VoClipAutoIndex.ResolveLastOneStanding));
        }

        _prevTimeRemaining = remaining;
        _prevAliveCount    = alive;

        if (candidates.Count == 0) return;

        candidates.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        VoCandidate best = candidates[0];

        float cd   = best.Priority >= PriorityTime60 ? CooldownHigh : CooldownLow;
        bool urgent = best.Priority >= PriorityTime60;
        if (!urgent && Time.unscaledTime < _nextVoiceAllowedUnscaled)
            return;

        AudioClip clip = best.ResolveClip();
        if (clip == null) return;

        gm.PlayMatchVoiceOneShot(clip, 1f);
        _nextVoiceAllowedUnscaled = Time.unscaledTime + cd;
    }

    private static int CountLivingEnemies()
    {
        EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int n = 0;
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive)
                    n++;
            }
        }

        if (GameManager.Instance != null)
        {
            int gmRem = GameManager.Instance.enemiesRemaining;
            if (Mathf.Abs(n - gmRem) > 2)
                n = Mathf.Min(n, gmRem);
        }

        return n;
    }

    private readonly struct VoCandidate
    {
        public readonly int Priority;
        public readonly Func<AudioClip> ResolveClip;

        public VoCandidate(int priority, Func<AudioClip> resolveClip)
        {
            Priority    = priority;
            ResolveClip = resolveClip;
        }
    }

    private static class SceneDispatch
    {
        public static event Action<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode> SceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Forward;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += Forward;
        }

        private static void Forward(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => SceneLoaded?.Invoke(s, m);
    }
}
