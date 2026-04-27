#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 6000.3+: Scene view helpers. Direct use of NavMeshVisualizationSettings.showNavigation
/// is obsolete in source; we optionally touch it via reflection so the editor still compiles cleanly.
/// If reflection fails, use Scene view Gizmos → NavMesh / AI Navigation overlays.
/// </summary>
public static class SceneViewDisplayTools
{
    private const string MenuRoot = "PRISM/Scene View/";

    private static readonly string NavMeshVisualizationTypeName = "UnityEditor.AI.NavMeshVisualizationSettings, UnityEditor";

    [MenuItem(MenuRoot + "Hide NavMesh Overlay", false, 1)]
    public static void HideNavMeshOverlay()
    {
        TrySetShowNavigationCounter(0);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuRoot + "Show NavMesh Overlay", false, 2)]
    public static void ShowNavMeshOverlay()
    {
        int current = TryGetShowNavigationCounter();
        TrySetShowNavigationCounter(current > 0 ? current : 1);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuRoot + "Use Shaded Textured Scene Mode", false, 11)]
    public static void SetAllSceneViewsShadedTextured()
    {
        foreach (SceneView sv in SceneView.sceneViews)
        {
            if (sv == null) continue;
            sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
            // Unity 6000.x: signature is (Shader shader, string replaceString) — not Color.
            sv.SetSceneViewShaderReplace(null, null);
            sv.sceneLighting = true;
        }

        SceneView.RepaintAll();
    }

    private static int TryGetShowNavigationCounter()
    {
        try
        {
            Type t = Type.GetType(NavMeshVisualizationTypeName);
            if (t == null) return 0;

            PropertyInfo p = t.GetProperty("showNavigation", BindingFlags.Public | BindingFlags.Static);
            if (p == null) return 0;

            object v = p.GetValue(null);
            return v is int i ? i : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void TrySetShowNavigationCounter(int value)
    {
        try
        {
            Type t = Type.GetType(NavMeshVisualizationTypeName);
            if (t == null)
            {
                Debug.LogWarning("[SceneViewDisplayTools] NavMeshVisualizationSettings not found. Use Scene → Gizmos to toggle NavMesh.");
                return;
            }

            PropertyInfo p = t.GetProperty("showNavigation", BindingFlags.Public | BindingFlags.Static);
            if (p == null || !p.CanWrite)
            {
                Debug.LogWarning("[SceneViewDisplayTools] showNavigation not available. In Unity 6.3+ use Scene overlays / Gizmos (AI Navigation).");
                return;
            }

            p.SetValue(null, value);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SceneViewDisplayTools] Could not change NavMesh overlay: {e.Message}");
        }
    }
}
#endif
