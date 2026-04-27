using UnityEngine;

/// <summary>
/// Anchors the main camera to a head bone or dedicated camera root and keeps
/// it slightly in front of the eyes so the player cannot see the inside of
/// their own face mesh.
/// </summary>
public sealed class FirstPersonCameraRig : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private Transform headBone;
    [SerializeField] private Animator characterAnimator;

    [Header("Placement")]
    [SerializeField] private bool createCameraRootIfMissing = true;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 0.04f, 0.09f);
    [SerializeField] private Vector3 localEulerOffset = Vector3.zero;
    [SerializeField] private float nearClipPlane = 0.01f;

    [Header("Smoothing")]
    [SerializeField] private bool smoothFollow = false;
    [SerializeField] private float followSpeed = 24f;

    private Transform _anchor;

    public Transform Anchor => _anchor;

    private void Awake()
    {
        ResolveReferences();
        ResolveAnchor();
        AttachCamera();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        if (_anchor == null)
        {
            ResolveAnchor();
            if (_anchor == null)
                return;
        }

        if (targetCamera.transform.parent != _anchor)
            AttachCamera();

        Vector3 targetLocalPosition = eyeOffset;
        Quaternion targetLocalRotation = Quaternion.Euler(localEulerOffset);

        if (smoothFollow)
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            targetCamera.transform.localPosition = Vector3.Lerp(targetCamera.transform.localPosition, targetLocalPosition, t);
            targetCamera.transform.localRotation = Quaternion.Slerp(targetCamera.transform.localRotation, targetLocalRotation, t);
        }
        else
        {
            targetCamera.transform.localPosition = targetLocalPosition;
            targetCamera.transform.localRotation = targetLocalRotation;
        }

        targetCamera.nearClipPlane = Mathf.Min(targetCamera.nearClipPlane, nearClipPlane);
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>(true);

        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>(true);

        if (headBone == null && characterAnimator != null && characterAnimator.isHuman)
            headBone = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
    }

    private void ResolveAnchor()
    {
        if (cameraRoot != null)
        {
            _anchor = cameraRoot;
            return;
        }

        if (headBone == null)
            headBone = FindNamedChild(transform, "CameraRoot", "Head", "head", "mixamorig:Head", "j_head", "neck", "Neck");

        if (headBone == null)
            return;

        if (!createCameraRootIfMissing)
        {
            _anchor = headBone;
            return;
        }

        Transform existingRoot = headBone.Find("CameraRoot");
        if (existingRoot != null)
        {
            cameraRoot = existingRoot;
            _anchor = existingRoot;
            return;
        }

        GameObject rootObject = new GameObject("CameraRoot");
        rootObject.transform.SetParent(headBone, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        cameraRoot = rootObject.transform;
        _anchor = cameraRoot;
    }

    private void AttachCamera()
    {
        if (targetCamera == null || _anchor == null)
            return;

        Transform camTransform = targetCamera.transform;
        camTransform.SetParent(_anchor, false);
        camTransform.localPosition = eyeOffset;
        camTransform.localRotation = Quaternion.Euler(localEulerOffset);
        targetCamera.nearClipPlane = Mathf.Min(targetCamera.nearClipPlane, nearClipPlane);
    }

    private static Transform FindNamedChild(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            for (int n = 0; n < names.Length; n++)
            {
                if (child.name == names[n])
                    return child;
            }
        }

        return null;
    }
}
