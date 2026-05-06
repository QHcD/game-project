using UnityEngine;

/// <summary>
/// Static helper for the player's persistent identity. The username is stored
/// via <see cref="PlayerPrefs"/> so it survives between PlayMode sessions
/// without needing JSON I/O. The first time the game launches the username is
/// empty — code paths that show "you" should call <see cref="HasUsername"/>
/// before falling back to a default label, and the main menu should prompt
/// for one with the runtime name-entry overlay.
/// </summary>
public static class PlayerProfile
{
    /// <summary>New unified Settings key (requested).</summary>
    public const string PlayerNameKey = "PlayerName";

    /// <summary>Legacy key kept for backwards compatibility.</summary>
    private const string LegacyPrefKey = "PRISM_Username";

    public  const int    MinNameLength = 2;
    public  const int    MaxNameLength = 16;
    public  const string DefaultUsername = "Player";

    private static string _cached;

    static PlayerProfile()
    {
        Reload();
    }

    // Ensures the cache is refreshed on every Play session even if the Editor
    // has domain reload disabled (Enter Play Mode Options).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ReloadOnBoot()
    {
        Reload();
    }

    /// <summary>True once the player has saved a non-empty username.</summary>
    public static bool HasUsername =>
        !string.IsNullOrWhiteSpace(_cached);

    /// <summary>The saved username, or <see cref="DefaultUsername"/> if none is set yet.</summary>
    public static string Username =>
        HasUsername ? _cached : DefaultUsername;

    /// <summary>Stores a sanitized username and persists it to disk.</summary>
    public static void SetUsername(string raw)
    {
        string cleaned = Sanitize(raw);
        if (string.IsNullOrEmpty(cleaned))
        {
            // Explicitly allow clearing by saving an empty string via settings UI.
            _cached = string.Empty;
            PlayerPrefs.SetString(PlayerNameKey, string.Empty);
            PlayerPrefs.Save();
            return;
        }

        _cached = cleaned;
        PlayerPrefs.SetString(PlayerNameKey, cleaned);
        // Keep legacy key in sync so older UI reads the same name.
        PlayerPrefs.SetString(LegacyPrefKey, cleaned);
        PlayerPrefs.Save();
    }

    /// <summary>Removes the saved username — used by the "reset profile" button.</summary>
    public static void Clear()
    {
        _cached = string.Empty;
        PlayerPrefs.DeleteKey(PlayerNameKey);
        PlayerPrefs.DeleteKey(LegacyPrefKey);
        PlayerPrefs.Save();
    }

    /// <summary>Refresh the in-memory cache from PlayerPrefs (useful after external edits).</summary>
    public static void Reload()
    {
        // Prefer the new Settings key; fall back to legacy data if present.
        _cached = PlayerPrefs.GetString(PlayerNameKey,
            PlayerPrefs.GetString(LegacyPrefKey, string.Empty));
    }

    /// <summary>
    /// Trims, removes line breaks, collapses internal whitespace and clamps
    /// the length to <see cref="MaxNameLength"/>.
    /// </summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        string trimmed = raw.Trim();
        if (trimmed.Length == 0) return string.Empty;

        // Replace internal whitespace runs with a single underscore so the
        // username always reads as a single token in the HUD/leaderboard.
        System.Text.StringBuilder sb = new System.Text.StringBuilder(trimmed.Length);
        bool lastWasSpace = false;
        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (c == '\n' || c == '\r' || c == '\t') continue;
            if (char.IsWhiteSpace(c))
            {
                if (lastWasSpace) continue;
                lastWasSpace = true;
                sb.Append('_');
                continue;
            }
            lastWasSpace = false;
            sb.Append(c);
        }

        string cleaned = sb.ToString();
        if (cleaned.Length < MinNameLength) return string.Empty;
        if (cleaned.Length > MaxNameLength) cleaned = cleaned.Substring(0, MaxNameLength);
        return cleaned;
    }

    /// <summary>True when <paramref name="raw"/> sanitises to a usable name.</summary>
    public static bool IsValid(string raw) =>
        !string.IsNullOrEmpty(Sanitize(raw));
}
