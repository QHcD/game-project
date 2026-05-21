using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

/// <summary>
/// Ensures low-profile ground debugging is bound to the active local player only.
/// Remote proxies disable this component and any attached debugger.
/// </summary>
[DisallowMultipleComponent]
public class StanceTestController : MonoBehaviour
{
    private PlayerController _player;
    private LowProfileGroundDebugger _debugger;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (!IsLocalPlayer())
        {
            DisableOnRemote();
            return;
        }

        _debugger = GetComponent<LowProfileGroundDebugger>();
        if (_debugger == null)
            _debugger = gameObject.AddComponent<LowProfileGroundDebugger>();

        _debugger.enabled = true;

        PlayerTacticalActions tactical = GetComponent<PlayerTacticalActions>();
        if (tactical == null)
            tactical = gameObject.AddComponent<PlayerTacticalActions>();
        tactical.CacheStandingCollider();

        enabled = true;
    }

    private void OnEnable()
    {
        if (!IsLocalPlayer())
        {
            DisableOnRemote();
            return;
        }

        if (_debugger == null)
            _debugger = GetComponent<LowProfileGroundDebugger>();

        if (_debugger != null)
            _debugger.enabled = true;
    }

    private void DisableOnRemote()
    {
        enabled = false;
        if (_debugger != null)
            _debugger.enabled = false;
    }

    private bool IsLocalPlayer()
    {
#if PUN_2_OR_NEWER
        if (MultiplayerMode.IsMultiplayer)
        {
            PhotonView view = GetComponent<PhotonView>();
            return view == null || view.IsMine;
        }
#endif
        return true;
    }
}
