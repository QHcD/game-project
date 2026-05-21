using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shared combat voice clip pool. Loads MohamedAman Materials clips once so every
/// <see cref="CombatVoiceSfx"/> instance can play audio even when inspector arrays are empty.
/// </summary>
public static class CombatVoiceClipBank
{
    private static readonly string[] EditorSearchFolders =
    {
        "Assets/MohamedAman/Materials",
        "Assets/MohamedAman",
        "Assets/MohamedAltajer",
    };

    private static readonly string[] ResourcesPaths =
    {
        "Audio/CombatVoice",
        "Audio/VO",
    };

    private static List<AudioClip> _hurt;
    private static List<AudioClip> _death;
    private static bool _loaded;

    public static IReadOnlyList<AudioClip> HurtClips
    {
        get { EnsureLoaded(); return _hurt; }
    }

    public static IReadOnlyList<AudioClip> DeathClips
    {
        get { EnsureLoaded(); return _death; }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _hurt = new List<AudioClip>(8);
        _death = new List<AudioClip>(8);

#if UNITY_EDITOR
        for (int f = 0; f < EditorSearchFolders.Length; f++)
        {
            string folder = EditorSearchFolders[f];
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                TryAddClip(clip);
            }
        }
#endif

        for (int p = 0; p < ResourcesPaths.Length; p++)
        {
            AudioClip[] fromResources = Resources.LoadAll<AudioClip>(ResourcesPaths[p]);
            for (int i = 0; i < fromResources.Length; i++)
                TryAddClip(fromResources[i]);
        }

        AudioClip[] loaded = Resources.FindObjectsOfTypeAll<AudioClip>();
        for (int i = 0; i < loaded.Length; i++)
            TryAddClip(loaded[i]);

        _loaded = true;
    }

    public static void Reload()
    {
        _loaded = false;
        _hurt = null;
        _death = null;
        EnsureLoaded();
    }

    private static void TryAddClip(AudioClip clip)
    {
        if (clip == null) return;
        if (string.IsNullOrEmpty(clip.name) || clip.name.StartsWith("Procedural")) return;

        string nameLower = clip.name.ToLowerInvariant();

        if (MatchesHurt(nameLower) && !_hurt.Contains(clip))
            _hurt.Add(clip);

        if (MatchesDeath(nameLower) && !_death.Contains(clip))
            _death.Add(clip);
    }

    private static bool MatchesHurt(string nameLower)
    {
        return nameLower.Contains("hurt")
            || nameLower.Contains("pain")
            || nameLower.Contains("grunt")
            || nameLower.Contains("hit")
            || nameLower.Contains("damage");
    }

    private static bool MatchesDeath(string nameLower)
    {
        return nameLower.Contains("death")
            || nameLower.Contains("die")
            || nameLower.Contains("scream")
            || nameLower.Contains("killed");
    }
}
