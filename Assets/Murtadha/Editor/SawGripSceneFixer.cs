using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SawGripSceneFixer
{
    private const float TargetSawWorldSize = 0.78f;
    private static bool queued;

    static SawGripSceneFixer()
    {
        EditorApplication.delayCall += QueueFix;
        EditorSceneManager.sceneOpened += (_, _) => QueueFix();
        EditorApplication.hierarchyChanged += QueueFix;
    }

    [MenuItem("PRISM/Fix Saw Grip In Scene")]
    public static void FixNow()
    {
        FixSawGripInOpenScene(force: true);
    }

    private static void QueueFix()
    {
        if (queued || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            FixSawGripInOpenScene(force: false);
        };
    }

    private static void FixSawGripInOpenScene(bool force)
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        HashSet<Transform> sawRoots = new HashSet<Transform>();
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || !LooksLikeSaw(candidate.name))
                continue;

            Transform root = FindWeaponModelRoot(candidate);
            if (root != null)
                sawRoots.Add(root);
        }

        if (sawRoots.Count == 0)
            return;

        GameObject sourceMarker = new GameObject("saw_scene_fix_source");
        bool changed = false;

        foreach (Transform sawRoot in sawRoots)
        {
            if (sawRoot == null)
                continue;

            NormalizeSawWorldSize(sawRoot.gameObject);

            bool isPlayerSaw = IsUnderNamedRoot(sawRoot, "Player");
            if (isPlayerSaw)
                WeaponLoadoutCatalog.ApplyPlayerRuntimeGripPose(12, sourceMarker, sawRoot);
            else
                WeaponLoadoutCatalog.ApplyRuntimeGripPose(12, sourceMarker, sawRoot);

            ForceVisible(sawRoot.gameObject);
            changed = true;
        }

        Object.DestroyImmediate(sourceMarker);

        if (changed || force)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }

    private static bool LooksLikeSaw(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.ToLowerInvariant().Contains("saw");
    }

    private static Transform FindWeaponModelRoot(Transform child)
    {
        Transform current = child;
        while (current != null)
        {
            string lower = current.name.ToLowerInvariant();
            if (lower == "weaponmodel"
                || lower.Contains("saw_low")
                || lower.Contains("saw"))
            {
                Transform parent = current.parent;
                while (parent != null)
                {
                    string parentLower = parent.name.ToLowerInvariant();
                    if (parentLower.Contains("sawsocketanchor")
                        || parentLower.Contains("weapon socket")
                        || parentLower.Contains("__playerweaponsocket")
                        || parentLower.Contains("enemyweaponsocket"))
                        return current;

                    if (parentLower == "weaponmodel")
                        current = parent;

                    parent = parent.parent;
                }

                return current;
            }

            current = current.parent;
        }

        return child;
    }

    private static bool IsUnderNamedRoot(Transform child, string rootName)
    {
        Transform current = child;
        while (current != null)
        {
            if (current.name == rootName)
                return true;
            current = current.parent;
        }

        return false;
    }

    private static void NormalizeSawWorldSize(GameObject sawRoot)
    {
        if (sawRoot == null)
            return;

        float maxExtent = GetMaxRendererExtent(sawRoot);
        if (maxExtent <= 0.001f)
            return;

        float scaleFactor = TargetSawWorldSize / maxExtent;
        if (scaleFactor <= 0.001f || float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor))
            return;

        sawRoot.transform.localScale *= scaleFactor;
    }

    private static float GetMaxRendererExtent(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return 0f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
    }

    private static void ForceVisible(GameObject obj)
    {
        obj.SetActive(true);
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.SetActive(true);

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].enabled = true;
            renderers[i].forceRenderingOff = false;
        }
    }
}
