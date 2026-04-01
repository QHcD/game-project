using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;

    // FIX: Raised camera higher and pulled back more so it doesn't point at the ground
    public Vector3 offset = new Vector3(0f, 3.2f, -5.5f);
    public float smoothSpeed = 8f;
    public float rotationSmoothSpeed = 12f;
    public float collisionRadius = 0.28f;
    public float minimumDistance = 1.15f;

    // FIX: Raised focus point so camera looks at chest/head level, not feet
    public Vector3 focusOffset = new Vector3(0f, 1.85f, 0f);

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 focusPoint = target.position + focusOffset;
        Vector3 desiredPosition = focusPoint + target.TransformDirection(offset);
        Vector3 resolvedPosition = ResolveCameraCollision(focusPoint, desiredPosition);

        transform.position = Vector3.Lerp(transform.position, resolvedPosition, smoothSpeed * Time.deltaTime);

        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }

    private Vector3 ResolveCameraCollision(Vector3 focusPoint, Vector3 desiredPosition)
    {
        Vector3 travel = desiredPosition - focusPoint;
        float desiredDistance = travel.magnitude;
        if (desiredDistance <= 0.001f)
        {
            return desiredPosition;
        }

        Vector3 direction = travel / desiredDistance;
        RaycastHit[] hits = Physics.SphereCastAll(
            focusPoint,
            collisionRadius,
            direction,
            desiredDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        float resolvedDistance = desiredDistance;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || hitTransform == target || hitTransform.IsChildOf(target))
            {
                continue;
            }

            resolvedDistance = Mathf.Min(resolvedDistance, Mathf.Max(minimumDistance, hits[i].distance - collisionRadius));
        }

        return focusPoint + direction * resolvedDistance;
    }
}