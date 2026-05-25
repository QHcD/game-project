using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SlidingElevatorDoor : MonoBehaviour
{
    [Header("Door Transforms")]
    [Tooltip("The left door panel. Will slide along its parent's local -X axis.")]
    [SerializeField] private Transform leftDoor;

    [Tooltip("The right door panel. Will slide along its parent's local +X axis.")]
    [SerializeField] private Transform rightDoor;

    [Header("Movement Settings")]
    [Tooltip("How far (in local units) each panel slides away from the center.")]
    [SerializeField] private float slideDistance = 1.0f;

    [Tooltip("Units per second each panel moves while opening/closing.")]
    [SerializeField] private float openSpeed = 2.5f;

    [Tooltip("Seconds to wait after the player leaves the trigger before closing.")]
    [SerializeField] private float closeDelay = 1.5f;

    [Header("Trigger Settings")]
    [Tooltip("Tag the trigger collider checks for. Usually 'Player'.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private Coroutine closeRoutine;
    private Coroutine leftMoveRoutine;
    private Coroutine rightMoveRoutine;
    private bool isOpen;

    private void Awake()
    {
        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogError($"[{nameof(SlidingElevatorDoor)}] Left/Right door references are missing on '{name}'.");
            enabled = false;
            return;
        }

        leftClosedPos  = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;

        leftOpenPos  = leftClosedPos  + Vector3.left  * slideDistance;
        rightOpenPos = rightClosedPos + Vector3.right * slideDistance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
            closeRoutine = null;
        }

        OpenDoors();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (closeRoutine != null) StopCoroutine(closeRoutine);
        closeRoutine = StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        CloseDoors();
        closeRoutine = null;
    }

    private void OpenDoors()
    {
        if (isOpen) return;
        isOpen = true;

        PlayClip(openClip);
        StartSlide(leftOpenPos, rightOpenPos);
    }

    private void CloseDoors()
    {
        if (!isOpen) return;
        isOpen = false;

        PlayClip(closeClip);
        StartSlide(leftClosedPos, rightClosedPos);
    }

    private void StartSlide(Vector3 leftTarget, Vector3 rightTarget)
    {
        if (leftMoveRoutine  != null) StopCoroutine(leftMoveRoutine);
        if (rightMoveRoutine != null) StopCoroutine(rightMoveRoutine);

        leftMoveRoutine  = StartCoroutine(SlidePanel(leftDoor,  leftTarget));
        rightMoveRoutine = StartCoroutine(SlidePanel(rightDoor, rightTarget));
    }

    private IEnumerator SlidePanel(Transform panel, Vector3 target)
    {
        while ((panel.localPosition - target).sqrMagnitude > 0.000001f)
        {
            panel.localPosition = Vector3.MoveTowards(
                panel.localPosition,
                target,
                openSpeed * Time.deltaTime);
            yield return null;
        }
        panel.localPosition = target;
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (leftDoor == null || rightDoor == null) return;
        if (leftDoor.parent == null || rightDoor.parent == null) return;

        Gizmos.color = Color.cyan;
        Vector3 lOpen = leftDoor.position  + leftDoor.parent.TransformDirection(Vector3.left)  * slideDistance;
        Vector3 rOpen = rightDoor.position + rightDoor.parent.TransformDirection(Vector3.right) * slideDistance;
        Gizmos.DrawLine(leftDoor.position,  lOpen);
        Gizmos.DrawLine(rightDoor.position, rOpen);
        Gizmos.DrawWireSphere(lOpen, 0.05f);
        Gizmos.DrawWireSphere(rOpen, 0.05f);
    }
#endif
}
