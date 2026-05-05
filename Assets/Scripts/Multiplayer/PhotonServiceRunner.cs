using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

/// <summary>
/// Keeps Photon networking serviced every frame during multiplayer.
/// Without this, clients can time out after scene loads (e.g. DisconnectByServerTimeout).
/// </summary>
public sealed class PhotonServiceRunner : MonoBehaviour
{
    private static PhotonServiceRunner _instance;

    // Subscribe to sceneLoaded at domain startup so the runner is created on
    // every future scene load, including the multiplayer scene that loads after
    // the player joins a room. AfterSceneLoad fires only once (at game start on
    // the menu where IsMultiplayer is still false), which is why the runner was
    // never created and heartbeats stopped → DisconnectByServerTimeout.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnAnySceneLoaded;
    }

    private static void OnAnySceneLoaded(
        UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!MultiplayerMode.IsMultiplayer)
            return;

        if (_instance != null)
            return;

        PhotonServiceRunner existing = FindFirstObjectByType<PhotonServiceRunner>();
        if (existing != null)
        {
            _instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return;
        }

        GameObject go = new GameObject("PhotonServiceRunner");
        _instance = go.AddComponent<PhotonServiceRunner>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
#if PUN_2_OR_NEWER
        // PhotonNetwork.Service() does not exist in PUN 2. If manual servicing is needed,
        // service the underlying LoadBalancingClient (NetworkingClient).
        PhotonNetwork.NetworkingClient?.Service();
#endif
    }
}

