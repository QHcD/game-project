using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 100% code-driven voice/SFX lookup: uses Resources.LoadAll for
/// <c>Audio/VO</c> once and picks clips by scoring file names against event keywords (no Inspector).
/// </summary>
public static class VoClipAutoIndex
{
    private static readonly List<AudioClip> Clips = new List<AudioClip>(32);
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        Clips.Clear();
        Clips.AddRange(Resources.LoadAll<AudioClip>("Audio/VO"));
        _loaded = true;
    }

    public static void Reload()
    {
        _loaded = false;
        EnsureLoaded();
    }

    private static string N(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return raw.Trim().Replace(' ', '_').ToLowerInvariant();
    }

    private static AudioClip BestByScore(Func<string, int> scoreFunc, int minScore)
    {
        EnsureLoaded();
        AudioClip best = null;
        int bestS = minScore - 1;
        for (int i = 0; i < Clips.Count; i++)
        {
            AudioClip c = Clips[i];
            if (c == null) continue;
            int s = scoreFunc(N(c.name));
            if (s > bestS)
            {
                bestS = s;
                best  = c;
            }
        }

        return bestS >= minScore ? best : null;
    }

    /// <summary>~60 seconds remaining / one-minute urgency.</summary>
    public static AudioClip ResolveTimeOneMinuteUrgency()
        => BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("60")) s += 4;
            if (n.Contains("minute") || n.Contains("_min") || n.Contains("min_") || n == "min") s += 4;
            if (n.Contains("one") && n.Contains("min")) s += 5;
            if (n.Contains("urg") || n.Contains("pressure")) s += 2;
            if (n.Contains("time") && n.Contains("60")) s += 3;
            if (n.Contains("sixty")) s += 3;
            return s;
        }, 5);

    public static AudioClip ResolveTimeThirtyUrgency()
        => BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("30")) s += 4;
            if (n.Contains("urg") || n.Contains("pressure")) s += 2;
            if (n.Contains("time") && n.Contains("30")) s += 3;
            if (n.Contains("thirty")) s += 3;
            return s;
        }, 4);

    public static AudioClip ResolveTimeTenUrgency()
        => BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("10")) s += 4;
            if (n.Contains("urg") || n.Contains("pressure")) s += 2;
            if (n.Contains("time") && n.Contains("10")) s += 3;
            if (n.Contains("ten")) s += 2;
            return s;
        }, 4);

    public static AudioClip ResolveEnemiesRemain(int count)
    {
        string digit = count.ToString();
        return BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("remain") && n.Contains(digit)) s += 6;
            if (n.Contains("hostile") && n.Contains(digit)) s += 5;
            if (n.Contains("enemy") && n.Contains(digit)) s += 4;
            if (n.Contains("left") && n.Contains(digit)) s += 4;
            return s;
        }, 4);
    }

    public static AudioClip ResolveLastOneStanding()
        => BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("last")) s += 3;
            if (n.Contains("final") || n.Contains("stand")) s += 3;
            if (n.Contains("one") && (n.Contains("left") || n.Contains("man"))) s += 4;
            if (n.Contains("1") && n.Contains("left")) s += 3;
            if (n.Contains("solo") || n.Contains("end")) s += 2;
            return s;
        }, 5);

    public static AudioClip ResolvePlayerCriticalHealth()
        => BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("critical")) s += 4;
            if (n.Contains("bleed") || n.Contains("bleeding")) s += 4;
            if (n.Contains("warn") || n.Contains("danger")) s += 3;
            if (n.Contains("low") && n.Contains("hp")) s += 3;
            if (n.Contains("health") && (n.Contains("low") || n.Contains("bad"))) s += 3;
            if (n.Contains("move") && n.Contains("!")) s += 1;
            return s;
        }, 4);

    public static AudioClip ResolveCountdownBeep()
        => BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("beep")) s += 5;
            if (n.Contains("tick")) s += 4;
            if (n.Contains("count") && n.Contains("tick")) s += 5;
            if (n.Contains("countdown") && !n.Contains("go")) s += 3;
            return s;
        }, 4);

    public static AudioClip ResolveCountdownStart()
        => BestByScore(n =>
        {
            int s = 0;
            if (n.Contains("countdown_go")) s += 8;
            if (n.Contains("start") && n.Contains("chime")) s += 6;
            if (n == "go" || n.EndsWith("_go") || n.StartsWith("go_")) s += 5;
            if (n.Contains("start") && !n.Contains("tick")) s += 4;
            if (n.Contains("chime") || n.Contains("fanfare")) s += 3;
            return s;
        }, 4);
}
