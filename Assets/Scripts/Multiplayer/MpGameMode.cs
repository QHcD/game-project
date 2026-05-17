/// <summary>
/// Multiplayer game mode selector for PRISM-7.
/// Stored in Photon Room Custom Properties under key "gm".
/// </summary>
public enum MpGameMode : byte
{
    /// <summary>
    /// Default free-for-all — human players + bots, everyone fights everyone.
    /// Matches current working prototype behaviour exactly.
    /// </summary>
    HybridChaos  = 0,

    /// <summary>
    /// 2–4 human players cooperate against up to 25 AI enemies.
    /// Bots target all human players. Match ends when all humans die.
    /// </summary>
    CoopSurvival = 1,

    /// <summary>
    /// Human players only — no bots.
    /// Kill leaderboard + match timer. Last alive / highest kills wins.
    /// </summary>
    PurePvP      = 2,
}
