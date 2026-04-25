using UnityEngine;

public static class AudioSettingsRuntime
{
    public const string MasterKey = "MasterVol";
    public const string MusicKey = "MusicVol";
    public const string SfxKey = "SFXVol";

    public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 0.8f));
    public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.8f));
    public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 0.8f));

    public static void ApplyListenerVolume()
    {
        AudioListener.volume = MasterVolume;
    }

    public static float ScaledMusic(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * MusicVolume;
    }

    public static float ScaledSfx(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * SfxVolume;
    }
}
