/// <summary>
/// Single global kill-switch for the multiplayer match lifecycle.
///
/// While <see cref="Enabled"/> is false (current default), multiplayer is in
/// "stable test mode": players join, the scene loads, both players spawn and
/// can move, but NO match rules run — no ReadyCheck, no countdown, no
/// timer-driven end, no auto winner, no Victory panel, no bot director.
///
/// Flip back to true once basic movement / spawn / camera / weapon parity
/// is verified end-to-end.
/// </summary>
public static class MpMatchRules
{
    public static bool Enabled = false;
}
