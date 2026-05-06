using UnityEngine;

public static class AudioSettingsRuntime
{
    public const string MasterKey = "MasterVol";
    public const string MusicKey = "MusicVol";
    public const string SfxKey = "SFXVol";
    public const string UiKey = "UIVol";
    public const string MuteAllKey = "MuteAll";

    /// <summary>Dial for menu BGM vs SFX balance (music slider scales on top).</summary>
    public const float MenuLobbyMusicDesignMix = 0.55f;

    /// <summary>One-time correction if music was accidentally stored at exactly 0.</summary>
    private const string MusicZeroFixFlagKey = "_MusicVolZeroFix_v1";

    public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 0.8f));
    public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.8f));
    public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 0.8f));
    public static float UiVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(UiKey, 0.8f));
    public static bool MuteAll => PlayerPrefs.GetInt(MuteAllKey, 0) == 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void NormalizeAudioPrefs()
    {
        if (!PlayerPrefs.HasKey(MusicKey))
        {
            PlayerPrefs.SetFloat(MusicKey, 0.8f);
            PlayerPrefs.Save();
        }

        if (!PlayerPrefs.HasKey(UiKey))
        {
            PlayerPrefs.SetFloat(UiKey, 0.8f);
            PlayerPrefs.Save();
        }

        if (Mathf.Abs(PlayerPrefs.GetFloat(MusicKey, 0.8f)) <= 1e-5f
            && PlayerPrefs.GetInt(MusicZeroFixFlagKey, 0) == 0)
        {
            PlayerPrefs.SetFloat(MusicKey, 0.8f);
            PlayerPrefs.SetInt(MusicZeroFixFlagKey, 1);
            PlayerPrefs.Save();
            Debug.LogWarning("[AudioSettingsRuntime] MusicVol was saved as zero; restored default 0.8 once. Lower it in Settings if you want muted menus.");
        }

        LogSilentMusicPrefIfNeeded();
    }

    private static void LogSilentMusicPrefIfNeeded()
    {
        if (!PlayerPrefs.HasKey(MusicKey)) return;

        float m = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.8f));
        if (m <= 1e-5f)
            Debug.Log("[AudioSettingsRuntime] MusicVol is 0 — menu music will be silent until raised in Settings.");
    }

    public static void RefreshMenuLobbyMusicIfPresent()
    {
        GameObject go = GameObject.Find("LobbyMusic");
        if (go == null) return;

        AudioSource src = go.GetComponent<AudioSource>();
        if (src == null) return;

        src.mute = false;
        src.volume = ScaledMusic(MenuLobbyMusicDesignMix);
    }

    public static void ApplyListenerVolume()
    {
        AudioListener.volume = MuteAll ? 0f : MasterVolume;
        RefreshMenuLobbyMusicIfPresent();
    }

    public static float ScaledMusic(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * MusicVolume;
    }

    public static float ScaledSfx(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * SfxVolume;
    }

    public static float ScaledUi(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * UiVolume;
    }
}
