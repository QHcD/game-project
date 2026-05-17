public static class MultiplayerMode
{
    public const string SinglePlayerSceneName = "GameScene";
    public const string MultiplayerSceneName = "MultiplayerGameScene";
    public const string NetworkPlayerPrefabPath = "FirstPersonMelee/Player";

    public static bool IsMultiplayer { get; private set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsForPlayMode()
    {
        IsMultiplayer = false;
    }

    public static void SetSinglePlayer()
    {
        IsMultiplayer = false;
    }

    public static void SetMultiplayer()
    {
        IsMultiplayer = true;
    }
}
