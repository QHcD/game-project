using UnityEngine;

/// <summary>
/// Attach this component directly to the Katana prefab root.
///
/// It stores the exact local Position / Rotation / Scale that the blade must
/// have when it is parented to a humanoid RightHand bone, and exposes a single
/// Enforce() call so the attachment code (PlayerController / EnemyController)
/// can apply them in one line after parenting.
///
/// A child Transform called "BladeCenter" marks the mid-point of the active
/// blade zone used by KatanaCombatHandler for hit-detection box-casts.
/// </summary>
[DisallowMultipleComponent]
public class WeaponGripOffset : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Grip — relative to the parent Hand bone")]
    [Tooltip("Local position offset applied after the weapon is parented to the hand bone.")]
    public Vector3 localPosition    = Vector3.zero;

    [Tooltip("Local euler angles. (0, 0, 160) matches the black samurai player's katana grip.")]
    public Vector3 localEulerAngles = new Vector3(0f, 0f, 160f);

    [Tooltip("Uniform local scale.  1 = keep whatever the attachment code sized it to.")]
    [Min(0.01f)]
    public float uniformScale = 1f;

    [Header("Blade Hit-Detection")]
    [Tooltip("Child Transform placed at the physical mid-point of the blade (halfway up the edge). " +
             "KatanaCombatHandler performs an OverlapBox centred here during a strike.")]
    public Transform bladeCenter;

    [Tooltip("Half-extents of the hit-detection box in BladeCenter's local space.\n" +
             "Z = half-length along the blade.\n" +
             "X / Y = hit width and height.")]
    public Vector3 bladeBoxHalfExtents = new Vector3(0.07f, 0.07f, 0.40f);

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Apply the stored offsets to this transform.
    /// MUST be called AFTER the weapon has been parented to the hand bone,
    /// otherwise localPosition / localEulerAngles are relative to the wrong parent.
    /// </summary>
    public void Enforce()
    {
        transform.localPosition    = localPosition;
        transform.localEulerAngles = localEulerAngles;
        transform.localScale       = Vector3.one * Mathf.Max(0.01f, uniformScale);
    }

    // ── Editor visualisation ─────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (bladeCenter == null) return;

        // Red transparent box = hit-detection volume
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.35f);
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(bladeCenter.position, bladeCenter.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, bladeBoxHalfExtents * 2f);

        // Wireframe outline
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, bladeBoxHalfExtents * 2f);
        Gizmos.matrix = prev;

        // Blue sphere = BladeCenter pivot
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(bladeCenter.position, 0.025f);
    }
}
