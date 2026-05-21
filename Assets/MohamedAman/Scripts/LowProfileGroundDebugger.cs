using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

/// <summary>
/// Local-player-only sink diagnostics for slide/prone. Does not drive gameplay;
/// logs when the capsule bottom drops below the floor during low-profile stances.
/// </summary>
[DisallowMultipleComponent]
public class LowProfileGroundDebugger : MonoBehaviour
{
    [SerializeField] private bool logSinkEvents = true;
    [SerializeField] private float sinkLogThreshold = 0.02f;

    private CharacterController _controller;
    private PlayerController _player;
    private bool _boundLocal;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _player = GetComponent<PlayerController>();
        _boundLocal = IsLocalPlayer();
        enabled = _boundLocal;
    }

    private void LateUpdate()
    {
        if (!_boundLocal || _controller == null)
            return;

        if (!TryGetFloorY(out float floorY))
            return;

        float capsuleBottomY = transform.position.y + _controller.center.y - _controller.height * 0.5f;
        float sink = floorY - capsuleBottomY;
        if (sink <= sinkLogThreshold)
            return;

        if (logSinkEvents)
        {
            Debug.Log(
                $"[LowProfileGroundDebugger] sink={sink:F3}m on {name} " +
                $"h={_controller.height:F2} centerY={_controller.center.y:F2}",
                this);
        }
    }

    private bool TryGetFloorY(out float floorY)
    {
        floorY = 0f;
        Vector3 origin = transform.position + Vector3.up * 0.35f;
        int mask = Physics.DefaultRaycastLayers;
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            return false;

        floorY = hit.point.y;
        return true;
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
