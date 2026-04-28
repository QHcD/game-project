using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class SickleGripPoseDriver : MonoBehaviour
{
    private const string FingerWrapRootName = "__SickleFingerWrap";

    [Header("Grip Pose")]
    public Transform handRoot;
    public Transform weaponRoot;
    public bool enableGripPose = true;
    public bool createVisualFingerWrap = true;

    private readonly Dictionary<Transform, Quaternion> defaultLocalRotations = new Dictionary<Transform, Quaternion>();

    private static readonly string[] RightFingerKeywords =
    {
        "thumb.r", "index.r", "middle.r", "ring.r", "pinky.r",
        "thumb_r", "index_r", "middle_r", "ring_r", "pinky_r",
        "thumbri", "indexri", "middleri", "ringri", "pinkyri",
        "finger", "f_index", "f_middle", "f_ring", "f_pinky", "f_thumb"
    };

    private void OnEnable()
    {
        RefreshPose();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            RefreshPose();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
            RefreshPose();
    }

    public void Configure(Transform hand, Transform weapon)
    {
        handRoot = hand;
        weaponRoot = weapon;
        RefreshPose();
    }

    public void RefreshPose()
    {
        if (!enableGripPose || handRoot == null)
            return;

        CaptureDefaults();
        CurlFingerBones();

        if (createVisualFingerWrap)
            EnsureVisualFingerWrap();
    }

    private void CaptureDefaults()
    {
        Transform[] bones = handRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone == null || bone == handRoot || !LooksLikeRightFingerBone(bone.name))
                continue;

            if (!defaultLocalRotations.ContainsKey(bone))
                defaultLocalRotations.Add(bone, bone.localRotation);
        }
    }

    private void CurlFingerBones()
    {
        foreach (KeyValuePair<Transform, Quaternion> entry in defaultLocalRotations)
        {
            Transform bone = entry.Key;
            if (bone == null)
                continue;

            Vector3 curlEuler = GetCurlEuler(bone.name);
            bone.localRotation = entry.Value * Quaternion.Euler(curlEuler);
        }
    }

    private static Vector3 GetCurlEuler(string boneName)
    {
        string lower = boneName.ToLowerInvariant();

        if (lower.Contains("thumb"))
            return new Vector3(12f, -18f, 34f);

        if (lower.Contains("index"))
            return new Vector3(0f, 0f, 46f);

        if (lower.Contains("middle"))
            return new Vector3(0f, 0f, 58f);

        if (lower.Contains("ring"))
            return new Vector3(0f, 0f, 54f);

        if (lower.Contains("pinky") || lower.Contains("little"))
            return new Vector3(0f, 0f, 48f);

        return new Vector3(0f, 0f, 38f);
    }

    private static bool LooksLikeRightFingerBone(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
            return false;

        string lower = boneName.ToLowerInvariant().Replace(" ", string.Empty);
        bool rightSide = lower.Contains(".r")
            || lower.Contains("_r")
            || lower.Contains("right")
            || lower.Contains("ri");

        if (!rightSide)
            return false;

        for (int i = 0; i < RightFingerKeywords.Length; i++)
        {
            if (lower.Contains(RightFingerKeywords[i]))
                return true;
        }

        return false;
    }

    private void EnsureVisualFingerWrap()
    {
        Transform wrapRoot = handRoot.Find(FingerWrapRootName);
        if (wrapRoot == null)
        {
            GameObject rootObject = new GameObject(FingerWrapRootName);
            wrapRoot = rootObject.transform;
            wrapRoot.SetParent(handRoot, false);
        }

        wrapRoot.localPosition = new Vector3(0.02f, 0.018f, 0.045f);
        wrapRoot.localRotation = Quaternion.Euler(88f, 2f, 96f);
        wrapRoot.localScale = Vector3.one;

        EnsureWrapSegment(wrapRoot, "FingerWrap_Index", new Vector3(-0.012f, 0.000f, 0.000f), 0.045f);
        EnsureWrapSegment(wrapRoot, "FingerWrap_Middle", new Vector3(0.000f, 0.000f, 0.000f), 0.050f);
        EnsureWrapSegment(wrapRoot, "FingerWrap_Ring", new Vector3(0.012f, 0.000f, 0.000f), 0.045f);
    }

    private static void EnsureWrapSegment(Transform parent, string objectName, Vector3 localPosition, float length)
    {
        Transform existing = parent.Find(objectName);
        GameObject segment = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        segment.name = objectName;
        segment.transform.SetParent(parent, false);
        segment.transform.localPosition = localPosition;
        segment.transform.localRotation = Quaternion.identity;
        segment.transform.localScale = new Vector3(0.009f, 0.018f, length);

        Collider col = segment.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Renderer renderer = segment.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader != null && renderer.sharedMaterial == null)
            {
                Material mat = new Material(shader);
                Color gloveColor = new Color(0.46f, 0.37f, 0.28f, 1f);
                mat.color = gloveColor;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", gloveColor);
                renderer.sharedMaterial = mat;
            }
        }
    }
}
