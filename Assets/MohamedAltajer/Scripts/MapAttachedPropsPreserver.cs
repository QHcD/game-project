using System.Collections.Generic;
using UnityEngine;

public static class MapAttachedPropsPreserver
{
    private struct WorldPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;
    }

    private static Dictionary<int, WorldPose> _poses;

    public static void Capture(Transform mapRoot)
    {
        _poses = new Dictionary<int, WorldPose>(256);
        if (mapRoot == null) return;

        Transform[] all = mapRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == mapRoot) continue;
            if (!IsPreservedPropName(t.name)) continue;

            _poses[t.GetInstanceID()] = new WorldPose
            {
                Position = t.position,
                Rotation = t.rotation,
                LocalScale = t.localScale
            };
        }
    }

    public static void Restore(Transform mapRoot)
    {
        if (_poses == null || _poses.Count == 0 || mapRoot == null) return;

        Transform[] all = mapRoot.GetComponentsInChildren<Transform>(true);
        int restored = 0;

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            if (!_poses.TryGetValue(t.GetInstanceID(), out WorldPose pose)) continue;

            t.SetPositionAndRotation(pose.Position, pose.Rotation);
            t.localScale = pose.LocalScale;
            restored++;
        }

        _poses.Clear();
        _poses = null;
    }

    public static bool IsPreservedPropName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();

        if (n.Contains("pipes_set") || n.Contains("pipe_set")) return true;
        if (n.Contains("h_set") || n.Contains("v_set")) return true;
        if (n.Contains("vent_") || n.Contains("tile_d")) return true;
        if (n.Contains("ladder") || n.Contains("wires") || n.Contains("cable")) return true;

        return false;
    }
}
