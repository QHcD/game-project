using UnityEngine;

public static class MeleeBodyTargeting
{
    public const string MeleeBodyChildName = "__MeleeBody";

    public static Vector3 GetTorsoWorldPoint(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null)
            return target.TransformPoint(cc.center);

        CapsuleCollider cap = target.GetComponent<CapsuleCollider>();
        if (cap != null)
            return target.TransformPoint(cap.center);

        Transform body = target.Find(MeleeBodyChildName);
        if (body != null)
        {
            CapsuleCollider bodyCap = body.GetComponent<CapsuleCollider>();
            if (bodyCap != null)
                return body.TransformPoint(bodyCap.center);
        }

        return target.position + Vector3.up * 1.2f;
    }

    public static bool TryGetBodyCapsule(Transform target, out Vector3 pointA, out Vector3 pointB, out float radius)
    {
        pointA = pointB = Vector3.zero;
        radius = 0.45f;

        if (target == null)
            return false;

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null)
        {
            float half = Mathf.Max(0.05f, (cc.height * 0.5f) - cc.radius);
            Vector3 center = target.TransformPoint(cc.center);
            pointA = center + Vector3.up * half;
            pointB = center - Vector3.up * half;
            radius = cc.radius;
            return true;
        }

        CapsuleCollider cap = target.GetComponent<CapsuleCollider>();
        if (cap == null)
        {
            Transform body = target.Find(MeleeBodyChildName);
            if (body != null)
                cap = body.GetComponent<CapsuleCollider>();
        }

        if (cap != null)
        {
            Vector3 center = cap.transform.TransformPoint(cap.center);
            float half = Mathf.Max(0.05f, (cap.height * 0.5f) - cap.radius);
            Vector3 axis = cap.transform.up;
            pointA = center + axis * half;
            pointB = center - axis * half;
            radius = cap.radius;
            return true;
        }

        Vector3 torso = GetTorsoWorldPoint(target);
        pointA = torso + Vector3.up * 0.75f;
        pointB = torso - Vector3.up * 0.75f;
        return true;
    }

    public static float GetClosestBodyDistance(Vector3 worldPoint, Transform target)
    {
        if (!TryGetBodyCapsule(target, out Vector3 pointA, out Vector3 pointB, out float radius))
            return Vector3.Distance(worldPoint, GetTorsoWorldPoint(target));

        Vector3 ab = pointB - pointA;
        float abSqr = ab.sqrMagnitude;
        if (abSqr < 0.0001f)
            return Mathf.Max(0f, Vector3.Distance(worldPoint, pointA) - radius);

        float t = Mathf.Clamp01(Vector3.Dot(worldPoint - pointA, ab) / abSqr);
        Vector3 closest = pointA + ab * t;
        return Mathf.Max(0f, Vector3.Distance(worldPoint, closest) - radius);
    }

    public static void EnsureMeleeBodyCollider(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        if (playerRoot.Find(MeleeBodyChildName) != null)
            return;

        GameObject body = new GameObject(MeleeBodyChildName);
        body.transform.SetParent(playerRoot, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.layer = playerRoot.gameObject.layer;

        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        CapsuleCollider cap = body.AddComponent<CapsuleCollider>();
        if (cc != null)
        {
            cap.center = cc.center;
            cap.height = cc.height;
            cap.radius = cc.radius + 0.05f;
        }
        else
        {
            cap.center = new Vector3(0f, 1f, 0f);
            cap.height = 2f;
            cap.radius = 0.45f;
        }

        cap.isTrigger = false;
    }
}
