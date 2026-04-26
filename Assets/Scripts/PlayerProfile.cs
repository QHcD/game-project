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
    private const string PrefKey = "PRISM_Username";
    public  const int    MinNameLength = 2;
    public  const int    MaxNameLength = 16;
    public  const string DefaultUsername = "PRISM";

    private static string _cached;

    static PlayerProfile()
    {
        _cached = PlayerPrefs.GetString(PrefKey, string.Empty);
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
        if (string.IsNullOrEmpty(cleaned)) return;

        _cached = cleaned;
        PlayerPrefs.SetString(PrefKey, cleaned);
        PlayerPrefs.Save();
    }

    /// <summary>Removes the saved username — used by the "reset profile" button.</summary>
    public static void Clear()
    {
        _cached = string.Empty;
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    /// <summary>Refresh the in-memory cache from PlayerPrefs (useful after external edits).</summary>
    public static void Reload()
    {
        _cached = PlayerPrefs.GetString(PrefKey, string.Empty);
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
