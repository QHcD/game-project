using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Build utility: creates a Windows 64-bit standalone EXE.
/// Auto-triggers once on first load when TRIGGER_BUILD.txt is present.
/// Also available as Build > Build Windows64 EXE from the Unity menu.
/// </summary>
[InitializeOnLoad]
public static class AutoBuilder
{
    private const string TriggerFile = "Assets/Editor/TRIGGER_BUILD.txt";
    private const string BuildDir    = "Builds";
    private const string BuildExe    = "Builds/PRISM-7.exe";

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
        if (!File.Exists(TriggerFile))
            return;

        File.Delete(TriggerFile);
        Debug.Log("[AutoBuilder] Trigger file detected — scheduling build after editor init.");
        EditorApplication.delayCall += BuildWindows64;
    }

    [MenuItem("Build/Build Windows64 EXE")]
    public static void BuildWindows64()
    {
        string projectRoot = Path.GetFullPath(".");
        string absOutput   = Path.Combine(projectRoot, BuildExe);
        string absDir      = Path.Combine(projectRoot, BuildDir);
        Directory.CreateDirectory(absDir);

        Debug.Log($"[AutoBuilder] Starting Windows64 build → {absOutput}");

        var options = new BuildPlayerOptions
        {
            scenes           = Scenes,
            locationPathName = absOutput,
            target           = BuildTarget.StandaloneWindows64,
            options          = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[AutoBuilder] BUILD SUCCEEDED — {summary.totalSize / 1048576f:F1} MB  →  {absOutput}");
        else
            Debug.LogError($"[AutoBuilder] BUILD FAILED: {summary.result}  errors={summary.totalErrors}");
    }
}
