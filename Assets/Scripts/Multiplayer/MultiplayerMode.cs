public static class MultiplayerMode
{
    public const string SinglePlayerSceneName  = "GameScene";
    public const string MultiplayerSceneName   = "MultiplayerGameScene";
    public const string NetworkPlayerPrefabPath = "FirstPersonMelee/Player";

    public static bool       IsMultiplayer { get; private set; }
    public static MpGameMode ActiveMode    { get; private set; } = MpGameMode.HybridChaos;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsForPlayMode()
    {
        IsMultiplayer = false;
        ActiveMode    = MpGameMode.HybridChaos;
    }

    public static void SetSinglePlayer()
    {
        IsMultiplayer = false;
    }

    public static void SetMultiplayer()
    {
        IsMultiplayer = true;
    }

    /// <summary>
    /// Called by MpRoomConfig after reading room properties so all clients
    /// share the same mode without extra RPCs.
    /// </summary>
    public static void SetMode(MpGameMode mode)
    {
        ActiveMode = mode;
    }
}
