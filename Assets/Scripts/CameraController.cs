using UnityEngine;

public class CameraController : MonoBehaviour
{
    // Target to follow (usually the player)
    public Transform target;

    // Offset distance between the camera and target
    public Vector3 offset = new Vector3(0f, 2.2f, -3.2f);

    // Smoothness of camera movement
    public float smoothSpeed = 10f;

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate desired position
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);

        // Smoothly move the camera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Make camera look at the target
        transform.LookAt(target.position + Vector3.up * 1.35f);
    }
}
