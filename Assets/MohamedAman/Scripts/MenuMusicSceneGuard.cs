using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures the DontDestroyOnLoad "LobbyMusic" AudioSource (spawned by
/// RuntimeMenuBuilder.SetupLobbyMusic) is silenced on any non-menu scene
/// and re-enabled when returning to the menu. Also prunes duplicate
/// LobbyMusic GameObjects that can accumulate across scene reloads.
///
/// Pure additive helper — does not modify RuntimeMenuBuilder or
/// AudioSettingsRuntime.
/// </summary>
public static class MenuMusicSceneGuard
{
    private const float FadeDuration = 0.45f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Apply once for the boot scene too.
        ApplyForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // Only react to the new active scene becoming the primary scene.
        if (mode != LoadSceneMode.Single) return;
        ApplyForScene(s);
    }

    private static void ApplyForScene(Scene s)
    {
        PruneDuplicateLobbyMusic();

        bool isMenu = IsMenuScene(s.name);
        GameObject host = GameObject.Find("LobbyMusic");
        if (host == null) return;

        AudioSource src = host.GetComponent<AudioSource>();
        if (src == null) return;

        if (isMenu)
        {
            // RuntimeMenuBuilder restarts the clip on its own when it rebuilds
            // the menu; we just make sure the source is not muted from a
            // previous gameplay scene.
            src.mute = false;
            AudioSettingsRuntime.RefreshMenuLobbyMusicIfPresent();
        }
        else
        {
            // Fade out so transitions don't pop.
            CoroutineHost.Run(FadeAndStop(src, FadeDuration));
        }
    }

    private static bool IsMenuScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        string n = sceneName.ToLowerInvariant();

        // Gameplay scene names (explicitly NOT menu context)
        if (n.Contains("gamescene") || n.Contains("multiplayergamescene") || n.Contains("gameplay"))
            return false;

        // Treat all menu-related screens and pages as menu context
        return n.Contains("mainmenu") 
            || n.Contains("lobby") 
            || n.Contains("settings") 
            || n.Contains("options") 
            || n.Contains("credits")
            || n.Contains("selectlevel")
            || n.Contains("challenges")
            || n.Contains("prismstore");
    }

    private static IEnumerator FadeAndStop(AudioSource src, float duration)
    {
        if (src == null) yield break;
        float startVol = src.volume;
        float t = 0f;
        while (t < duration && src != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            src.volume = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            src.volume = 0f;
        }
    }

    private static void PruneDuplicateLobbyMusic()
    {
#if UNITY_2023_1_OR_NEWER
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
        GameObject[] all = Object.FindObjectsOfType<GameObject>();
#endif
        GameObject first = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].name != "LobbyMusic") continue;
            if (first == null) { first = all[i]; continue; }
            Object.Destroy(all[i]);
        }
    }

    /// <summary>Tiny MonoBehaviour host so static code can run coroutines.</summary>
    private class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost _instance;
        public static void Run(IEnumerator routine)
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("MenuMusicSceneGuardHost");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineHost>();
            }
            _instance.StartCoroutine(routine);
        }
    }
}
