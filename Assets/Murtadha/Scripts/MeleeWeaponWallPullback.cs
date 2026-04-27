using UnityEngine;

/// <summary>
/// Pulls the equipped melee weapon slightly toward the grip when a static obstacle
/// sits between the shoulder and the weapon, reducing wall/container clipping without
/// a second camera. Pair with solid Environment/Default collision on level geometry.
/// </summary>
[DisallowMultipleComponent]
public class MeleeWeaponWallPullback : MonoBehaviour
{
    [Tooltip("World-space height above player root used as ray origin.")]
    public float castHeight = 1.35f;

    [Tooltip("Small radius for SphereCast (more forgiving than a thin Ray).")]
    public float castRadius = 0.07f;

    [Tooltip("Maximum pull toward the player (shoulder cast origin), in meters.")]
    public float maxPullbackWorld = 0.28f;

    [Tooltip("How fast the weapon eases back to rest when clear.")]
    public float recoverSpeed = 14f;

    [Tooltip("Layers that block the weapon (walls, crates). Should not include Player/Hittable.")]
    public LayerMask obstacleMask;

    private Transform _playerRoot;
    private Vector3 _restLocalPosition;
    private float _pull01;

    public void Configure(Transform playerRoot, LayerMask mask)
    {
        _playerRoot = playerRoot;
        obstacleMask = mask;
        CaptureRestPose();
    }

    public void CaptureRestPose()
    {
        _restLocalPosition = transform.localPosition;
        _pull01 = 0f;
    }

    private void OnEnable()
    {
        CaptureRestPose();
    }

    private void LateUpdate()
    {
        if (_playerRoot == null || obstacleMask.value == 0)
            return;

        Vector3 origin = _playerRoot.position + Vector3.up * castHeight;
        Vector3 toWeapon = transform.position - origin;
        float reach = toWeapon.magnitude;
        if (reach < 0.05f)
            return;

        Vector3 dir = toWeapon / reach;
        float target01 = 0f;

        if (Physics.SphereCast(
                origin,
                castRadius,
                dir,
                out RaycastHit hit,
                reach + castRadius + 0.02f,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null
                && !hit.collider.transform.IsChildOf(_playerRoot)
                && !transform.IsChildOf(hit.collider.transform))
            {
                target01 = Mathf.Clamp01(1f - (hit.distance - castRadius) / Mathf.Max(0.01f, reach));
            }
        }

        _pull01 = Mathf.MoveTowards(_pull01, target01, recoverSpeed * Time.deltaTime);
        Vector3 towardPlayer = origin - transform.position;
        towardPlayer.y = 0f;
        if (towardPlayer.sqrMagnitude > 0.0001f && transform.parent != null)
        {
            Vector3 localAxis = transform.parent.InverseTransformDirection(towardPlayer.normalized);
            transform.localPosition = _restLocalPosition + localAxis * (_pull01 * maxPullbackWorld);
        }
    }
}
