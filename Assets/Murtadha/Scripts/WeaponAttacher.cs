using UnityEngine;

/// <summary>
/// Drop this on any weapon prefab. On Start() it finds the correct parent
/// (FPS camera in first-person mode, RightHand bone in third-person mode)
/// and parents the weapon with the mandatory grip offset.
/// </summary>
public class WeaponAttacher : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Force a specific mode. Auto detects when set to Auto.")]
    public AttachMode mode = AttachMode.Auto;

    [Header("Offset (applied in all modes)")]
    public Vector3 localPosition = new Vector3(0.3f, -0.4f, 0.6f);
    public Vector3 localEuler    = new Vector3(0f, 90f, 0f);
    public float   localScale    = 1f;

    [Header("TP Bone Name")]
    [Tooltip("Name of the hand bone to search for in third-person rigs.")]
    public string rightHandBoneName = "RightHand";

    public enum AttachMode { Auto, FirstPerson, ThirdPerson }

    private void Start()
    {
        switch (mode)
        {
            case AttachMode.FirstPerson:  AttachFP(); break;
            case AttachMode.ThirdPerson:  AttachTP(); break;
            default:                      AutoAttach(); break;
        }
    }

    // ── Auto detection ────────────────────────────────────────────────────────

    private void AutoAttach()
    {
        // Prefer FPS camera if one is tagged MainCamera and has no SkinnedMeshRenderer
        // in its hierarchy (i.e. it is not inside a third-person body rig).
        Camera fpsCam = Camera.main;
        if (fpsCam != null)
        {
            AttachFP(fpsCam.transform);
            return;
        }

        // Fall back to TP hand bone search.
        AttachTP();
    }

    // ── First-person attach ───────────────────────────────────────────────────

    private void AttachFP(Transform camTransform = null)
    {
        if (camTransform == null)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                // Search all cameras for one tagged MainCamera or named with "FP"/"First"
                foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    if (c.CompareTag("MainCamera") ||
                        c.name.Contains("FP") ||
                        c.name.Contains("First"))
                    {
                        cam = c;
                        break;
                    }
                }
            }

            if (cam == null)
            {
                Debug.LogWarning("[WeaponAttacher] No FPS camera found; falling back to TP attach.", this);
                AttachTP();
                return;
            }

            camTransform = cam.transform;
        }

        // Ensure Camera.main near clip is tight so the weapon is never clipped.
        if (Camera.main != null && Camera.main.nearClipPlane > 0.01f)
            Camera.main.nearClipPlane = 0.01f;

        // Find or create a "Weapon" slot directly under the camera.
        Transform slot = camTransform.Find("Weapon");
        if (slot == null)
        {
            GameObject slotGo = new GameObject("Weapon");
            slotGo.transform.SetParent(camTransform, false);
            slot = slotGo.transform;
        }

        ApplyOffset(slot);
        Debug.Log($"[WeaponAttacher] '{name}' attached to FPS camera slot.", this);
    }

    // ── Third-person attach ───────────────────────────────────────────────────

    private void AttachTP()
    {
        // Try to find the PlayerController first — it owns the body hierarchy.
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        Transform searchRoot = pc != null ? pc.transform : null;

        // If no PlayerController, search every SkinnedMeshRenderer root.
        if (searchRoot == null)
        {
            SkinnedMeshRenderer[] smrs =
                FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                Transform found = FindBone(smr.transform.root, rightHandBoneName);
                if (found != null)
                {
                    ApplyOffset(found);
                    Debug.Log($"[WeaponAttacher] '{name}' attached to TP hand bone via SMR search.", this);
                    return;
                }
            }

            Debug.LogWarning("[WeaponAttacher] No hand bone found; weapon left unparented.", this);
            return;
        }

        Transform handBone = FindBone(searchRoot, rightHandBoneName);
        if (handBone == null)
        {
            // Try common alternate names.
            string[] alternates = { "Bip01 R Hand", "mixamorig:RightHand", "RightHandIndex1", "Hand_R" };
            foreach (string alt in alternates)
            {
                handBone = FindBone(searchRoot, alt);
                if (handBone != null) break;
            }
        }

        if (handBone == null)
        {
            Debug.LogWarning("[WeaponAttacher] RightHand bone not found in player hierarchy.", this);
            return;
        }

        ApplyOffset(handBone);
        Debug.Log($"[WeaponAttacher] '{name}' attached to TP hand bone '{handBone.name}'.", this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyOffset(Transform parent)
    {
        transform.SetParent(parent, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEuler);
        transform.localScale    = Vector3.one * localScale;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        if (root == null) return null;
        if (string.Equals(root.name, boneName, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindBone(root.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Re-attach with updated mode at runtime (e.g. after view-mode switch).</summary>
    public void Reattach(AttachMode newMode)
    {
        mode = newMode;
        transform.SetParent(null, true);
        Start();
    }
}
