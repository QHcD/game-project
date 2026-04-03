using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class GameSceneRebuilder
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private static readonly string[] BuildScenePaths =
    {
        "Assets/Scenes/MainMenu/MainMenu.unity",
        "Assets/Scenes/Credits.unity",
        "Assets/Scenes/Settings.unity",
        "Assets/Scenes/Options.unity",
        GameScenePath
    };

    [MenuItem("Tools/PRISM7/Rebuild Game Scene")]
    public static void RebuildGameScene()
    {
        EnsureScenesFolderExists();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateDirectionalLight();
        CreateGlobalVolume();
        CreateLevelBootstrap();

        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), GameScenePath))
        {
            throw new System.InvalidOperationException("Failed to save the rebuilt GameScene.");
        }

        RefreshBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PRISM-7 GameScene rebuilt successfully.");
    }

    private static void EnsureScenesFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }

    private static void CreateDirectionalLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light lightComponent = lightObject.AddComponent<Light>();
        lightComponent.type = LightType.Directional;
        lightComponent.intensity = 1.3f;
        lightComponent.color = new Color(0.92f, 0.95f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
    }

    private static void CreateGlobalVolume()
    {
        GameObject volumeObject = new GameObject("Global Volume");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;

        VolumeProfile defaultProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
        if (defaultProfile == null)
        {
            defaultProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        }

        volume.sharedProfile = defaultProfile;
    }

    private static void CreateLevelBootstrap()
    {
        GameObject bootstrap = new GameObject("LevelBuilder");
        bootstrap.AddComponent<LevelBuilder>();
    }

    private static void RefreshBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[BuildScenePaths.Length];
        for (int i = 0; i < BuildScenePaths.Length; i++)
        {
            scenes[i] = new EditorBuildSettingsScene(BuildScenePaths[i], true);
        }

        EditorBuildSettings.scenes = scenes;
    }
}
