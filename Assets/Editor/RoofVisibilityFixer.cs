#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoofVisibilityFixer
{
    private static readonly string[] RoofKeywords =
    {
        "roof", "ceiling", "ceil", "rooftop", "overhead", "canopy", "skylight",
        "top_cover", "topcover", "topcap", "top_cap"
    };

    [MenuItem("Tools/PRISM-7/Fix Roof Visibility (Scene)")]
    public static void FixSceneRoofs()
    {
        int rendererCount = 0;
        int materialCount = 0;
        int reactivated = 0;
        int staticFlagged = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
                ProcessRecursive(root.transform, ref rendererCount, ref materialCount, ref reactivated, ref staticFlagged);
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[RoofVisibilityFixer] Scene pass complete. Renderers={rendererCount} Materials={materialCount} Reactivated={reactivated} StaticFlagged={staticFlagged}");
    }

    [MenuItem("Tools/PRISM-7/Fix Roof Visibility (Selected Prefab)")]
    public static void FixSelectedPrefab()
    {
        GameObject sel = Selection.activeGameObject;
        if (sel == null)
        {
            Debug.LogWarning("[RoofVisibilityFixer] Select a prefab or scene GameObject first.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(sel);
        bool isPrefabAsset = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab");

        GameObject instance = isPrefabAsset
            ? PrefabUtility.LoadPrefabContents(path)
            : sel;

        int rendererCount = 0, materialCount = 0, reactivated = 0, staticFlagged = 0;
        ProcessRecursive(instance.transform, ref rendererCount, ref materialCount, ref reactivated, ref staticFlagged);

        if (isPrefabAsset)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            PrefabUtility.UnloadPrefabContents(instance);
        }
        else
        {
            EditorSceneManager.MarkAllScenesDirty();
        }

        Debug.Log($"[RoofVisibilityFixer] Prefab pass complete on '{sel.name}'. Renderers={rendererCount} Materials={materialCount} Reactivated={reactivated} StaticFlagged={staticFlagged}");
    }

    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null) return;
        int a = 0, b = 0, c = 0, d = 0;
        ProcessRecursive(root, ref a, ref b, ref c, ref d);
    }

    private static void ProcessRecursive(Transform t, ref int rendererCount, ref int materialCount, ref int reactivated, ref int staticFlagged)
    {
        if (t == null) return;

        bool isRoofBranch = IsRoofLike(t);

        if (isRoofBranch)
        {
            if (!t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "Reactivate roof");
                t.gameObject.SetActive(true);
                reactivated++;
            }

            MeshRenderer[] renderers = t.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in renderers)
            {
                if (r == null) continue;
                if (!r.enabled)
                {
                    Undo.RecordObject(r, "Enable roof renderer");
                    r.enabled = true;
                }
                if (!r.gameObject.activeSelf)
                {
                    Undo.RecordObject(r.gameObject, "Reactivate roof renderer");
                    r.gameObject.SetActive(true);
                    reactivated++;
                }

                rendererCount++;

                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    Material dup = ForceDoubleSided(m);
                    if (dup != m)
                    {
                        mats[i] = dup;
                        changed = true;
                    }
                    materialCount++;
                }
                if (changed) r.sharedMaterials = mats;

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                StaticEditorFlags desired = flags
                    | StaticEditorFlags.ContributeGI
                    | StaticEditorFlags.BatchingStatic
                    | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic
                    | StaticEditorFlags.ReflectionProbeStatic;
                if (desired != flags)
                {
                    Undo.RecordObject(r.gameObject, "Set roof static flags");
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, desired);
                    r.receiveGI = ReceiveGI.Lightmaps;
                    staticFlagged++;
                }

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
        }

        for (int i = 0; i < t.childCount; i++)
            ProcessRecursive(t.GetChild(i), ref rendererCount, ref materialCount, ref reactivated, ref staticFlagged);
    }

    private static bool IsRoofLike(Transform t)
    {
        if (t == null) return false;
        string lower = t.name.ToLowerInvariant();
        foreach (string k in RoofKeywords)
            if (lower.Contains(k)) return true;
        return false;
    }

    private static Material ForceDoubleSided(Material original)
    {
        if (original == null) return original;

        string path = AssetDatabase.GetAssetPath(original);
        Material target = original;
        if (!string.IsNullOrEmpty(path))
        {
            Undo.RecordObject(target, "Force double-sided roof material");
        }

        bool dirty = false;

        if (target.HasProperty("_Cull"))
        {
            if (Mathf.Abs(target.GetFloat("_Cull")) > 0.001f)
            {
                target.SetFloat("_Cull", 0f);
                dirty = true;
            }
        }
        if (target.HasProperty("_CullMode"))
        {
            if (Mathf.Abs(target.GetFloat("_CullMode")) > 0.001f)
            {
                target.SetFloat("_CullMode", 0f);
                dirty = true;
            }
        }
        if (target.HasProperty("_RenderFace"))
        {
            if (Mathf.Abs(target.GetFloat("_RenderFace")) > 0.001f)
            {
                target.SetFloat("_RenderFace", 0f);
                dirty = true;
            }
        }
        if (target.HasProperty("_DoubleSidedEnable"))
        {
            if (target.GetFloat("_DoubleSidedEnable") < 0.5f)
            {
                target.SetFloat("_DoubleSidedEnable", 1f);
                dirty = true;
            }
        }

        target.doubleSidedGI = true;

        if (target.IsKeywordEnabled("_DOUBLESIDED_ON") == false)
        {
            target.EnableKeyword("_DOUBLESIDED_ON");
            dirty = true;
        }

        if (dirty)
        {
            EditorUtility.SetDirty(target);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.SaveAssetIfDirty(target);
        }

        return target;
    }
}

[InitializeOnLoad]
public static class RoofVisibilityAutoPass
{
    private const string PrefKey = "PRISM7.RoofVisibility.LastRun";

    static RoofVisibilityAutoPass()
    {
        EditorApplication.delayCall += AutoRunOnce;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () => RunIfChanged(scene.path);
    }

    private static void AutoRunOnce()
    {
        if (Application.isPlaying) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (scene.IsValid() && scene.isLoaded)
                RunIfChanged(scene.path);
        }
    }

    private static void RunIfChanged(string scenePath)
    {
        if (Application.isPlaying) return;
        string key = PrefKey + ":" + scenePath;
        string last = EditorPrefs.GetString(key, "");
        string stamp = scenePath + "|v1";
        if (last == stamp) return;
        RoofVisibilityFixer.FixSceneRoofs();
        EditorPrefs.SetString(key, stamp);
    }
}
#endif
