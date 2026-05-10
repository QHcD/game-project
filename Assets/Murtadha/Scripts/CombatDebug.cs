using UnityEngine;

/// <summary>
/// Temporary combat tracing — set <see cref="debugCombatDamage"/> false to silence logs.
/// </summary>
public static class CombatDebug
{
    public static bool debugCombatDamage = true;

    public static bool Enabled => debugCombatDamage;

    public static void Log(string message)
    {
        if (!debugCombatDamage) return;
        Debug.Log("[CombatDebug] " + message);
    }
}
