/// <summary>
/// Reads Sprint Mode from PlayerPrefs (set in SettingsBuilder).
/// 0 = HOLD (LeftShift held), 1 = TOGGLE (LeftShift tap).
/// </summary>
public static class SprintModeRuntime
{
    public const string PrefKey = "SprintMode";

    public static bool IsToggleMode => UnityEngine.PlayerPrefs.GetInt(PrefKey, 0) == 1;

    public static bool IsHoldMode => !IsToggleMode;
}
