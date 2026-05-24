using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build utilities for Windows and macOS standalone players.
/// macOS release builds: Build > Build macOS Release (or batchmode -executeMethod AutoBuilder.BuildMacOSRelease).
/// </summary>
[InitializeOnLoad]
public static class AutoBuilder
{
    private const string TriggerFile = "Assets/Editor/TRIGGER_BUILD.txt";
    private const string MacTriggerFile = "Assets/Editor/TRIGGER_MAC_BUILD.txt";
    private const string BuildDir = "Builds";
    private const string WindowsExe = "Builds/PRISM-7.exe";
    private const string MacApp = "Builds/macOS/PRISM-7.app";
    private const string MacBuildProfilePath = "Assets/_Shared/Project_Core/Settings/Build Profiles/macOS.asset";

    private static readonly string[] Scenes =
    {
        "Assets/_Shared/Maps/Scenes/MainMenu.unity",
        "Assets/_Shared/Maps/Scenes/Credits.unity",
        "Assets/_Shared/Maps/Scenes/Settings.unity",
        "Assets/_Shared/Maps/Scenes/Options.unity",
        "Assets/_Shared/Maps/Scenes/GameScene.unity",
        "Assets/_Shared/Maps/Scenes/MultiplayerGameScene.unity",
    };

    static AutoBuilder()
    {
        if (File.Exists(MacTriggerFile))
        {
            File.Delete(MacTriggerFile);
            Debug.Log("[AutoBuilder] macOS trigger file detected — scheduling release build after editor init.");
            EditorApplication.delayCall += BuildMacOSRelease;
            return;
        }

        if (!File.Exists(TriggerFile))
            return;

        File.Delete(TriggerFile);
        Debug.Log("[AutoBuilder] Trigger file detected — scheduling Windows build after editor init.");
        EditorApplication.delayCall += BuildWindows64;
    }

    [MenuItem("PRISM-7/Fix + Build EXE")]
    public static void FixScenesAndBuild()
    {
        Debug.Log("[AutoBuilder] Fixing all scenes before build...");

        string[] scenePaths = Scenes;
        foreach (string scenePath in scenePaths)
        {
            if (!File.Exists(scenePath)) continue;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            int fixed2 = 0, removed = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null) continue;
                    var go = t.gameObject;

                    Vector3 p = go.transform.localPosition;
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                        float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    { go.transform.localPosition = Vector3.zero; fixed2++; }

                    Quaternion r = go.transform.localRotation;
                    if (float.IsNaN(r.x) || float.IsNaN(r.y) || float.IsNaN(r.z) || float.IsNaN(r.w))
                    { go.transform.localRotation = Quaternion.identity; fixed2++; }

                    Vector3 s = go.transform.localScale;
                    if (float.IsNaN(s.x) || float.IsNaN(s.y) || float.IsNaN(s.z) ||
                        float.IsInfinity(s.x) || float.IsInfinity(s.y) || float.IsInfinity(s.z))
                    { go.transform.localScale = Vector3.one; fixed2++; }

                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AutoBuilder] Scene '{scenePath}' fixed: {fixed2} transforms, {removed} missing scripts removed.");
        }

        Debug.Log("[AutoBuilder] All scenes fixed. Starting build...");
        BuildWindows64();
    }

    [MenuItem("Build/Build Windows64 EXE")]
    public static void BuildWindows64()
    {
        PrepareReleaseBuildSettings();
        SyncEditorBuildScenes();

        string projectRoot = Path.GetFullPath(".");
        string absOutput = Path.Combine(projectRoot, WindowsExe);
        Directory.CreateDirectory(Path.GetDirectoryName(absOutput)!);

        Debug.Log($"[AutoBuilder] Starting Windows64 build → {absOutput}");

        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = absOutput,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        ReportBuildResult(BuildPipeline.BuildPlayer(options), absOutput);
    }

    [MenuItem("Build/Build macOS Release")]
    public static void BuildMacOSRelease()
    {
        PrepareReleaseBuildSettings();
        SyncEditorBuildScenes();
        ClearBuildCaches();
        ActivateMacOSBuildProfile();

        string projectRoot = Path.GetFullPath(".");
        string absOutput = Path.Combine(projectRoot, MacApp);
        string absDir = Path.GetDirectoryName(absOutput)!;

        if (Directory.Exists(absDir))
        {
            Debug.Log($"[AutoBuilder] Removing previous macOS build folder: {absDir}");
            Directory.Delete(absDir, recursive: true);
        }

        Directory.CreateDirectory(absDir);
        Debug.Log($"[AutoBuilder] Starting macOS release build → {absOutput}");

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            throw new BuildFailedException("[AutoBuilder] Failed to switch active build target to StandaloneOSX.");

        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = absOutput,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        ReportBuildResult(BuildPipeline.BuildPlayer(options), absOutput);
    }

    private static void PrepareReleaseBuildSettings()
    {
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
        EditorUserBuildSettings.waitForManagedDebugger = false;
    }

    private static void SyncEditorBuildScenes()
    {
        foreach (string scenePath in Scenes)
        {
            if (!File.Exists(scenePath))
                throw new BuildFailedException($"[AutoBuilder] Missing build scene: {scenePath}");
        }

        var removed = EditorBuildSettings.scenes
            .Where(s => !Scenes.Contains(s.path))
            .Select(s => s.path)
            .ToArray();

        foreach (string path in removed)
            Debug.LogWarning($"[AutoBuilder] Removing stale build scene entry: {path}");

        EditorBuildSettings.scenes = Scenes
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        Debug.Log($"[AutoBuilder] Build scenes synced ({Scenes.Length}): {string.Join(", ", Scenes.Select(Path.GetFileNameWithoutExtension))}");
    }

    private static void ClearBuildCaches()
    {
        string projectRoot = Path.GetFullPath(".");
        string[] cachePaths =
        {
            "Library/Bee/artifacts",
            "Library/BuildCache",
            "Library/PlayerDataCache",
            "Library/BuildPlayerData",
            "Library/ShaderCache",
            "Temp/BurstOutput",
        };

        foreach (string relativePath in cachePaths)
        {
            string absPath = Path.Combine(projectRoot, relativePath);
            if (!Directory.Exists(absPath))
                continue;

            Debug.Log($"[AutoBuilder] Clearing cache: {relativePath}");
            Directory.Delete(absPath, recursive: true);
        }
    }

    private static void ActivateMacOSBuildProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(MacBuildProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[AutoBuilder] macOS build profile not found at {MacBuildProfilePath}; using global scene list and release settings.");
            return;
        }

        BuildProfile.SetActiveBuildProfile(profile);
        Debug.Log($"[AutoBuilder] Active build profile set to: {profile.name}");
    }

    private static void ReportBuildResult(BuildReport report, string outputPath)
    {
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Debug.Log($"[AutoBuilder] BUILD SUCCEEDED — {summary.totalSize / 1048576f:F1} MB  →  {outputPath}");
        else
            Debug.LogError($"[AutoBuilder] BUILD FAILED: {summary.result}  errors={summary.totalErrors}");

        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
