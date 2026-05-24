using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuButtonMissingScriptCleaner : IProcessSceneWithReport
{
    private static readonly HashSet<string> ButtonNames = new HashSet<string>
    {
        "START_BTN",
        "CONTINUE_BTN",
        "MULTIPLAYER_BTN",
        "SELECT_LEVEL_BTN",
        "SELECT LEVEL_BTN",
        "PRISM_STORE_BTN",
        "PRISM STORE_BTN",
        "CHALLENGES_BTN",
        "SETTINGS_BTN",
        "CREDITS_BTN",
        "QUIT_BTN"
    };

    private static readonly HashSet<string> ButtonLabels = new HashSet<string>
    {
        "START",
        "CONTINUE",
        "MULTIPLAYER",
        "SELECT LEVEL",
        "PRISM STORE",
        "CHALLENGES",
        "SETTINGS",
        "CREDITS",
        "QUIT"
    };

    public int callbackOrder => -1000;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        CleanScene(scene);
    }

    [MenuItem("PRISM/Clean Main Menu Button Scripts")]
    public static void CleanActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (CleanScene(scene))
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }

    [InitializeOnLoadMethod]
    private static void RegisterDeferredCleaner()
    {
        EditorApplication.delayCall -= CleanActiveMainMenuDeferred;
        EditorApplication.delayCall += CleanActiveMainMenuDeferred;
    }

    private static void CleanActiveMainMenuDeferred()
    {
        if (Application.isPlaying)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "MainMenu")
            return;

        if (CleanScene(scene))
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool CleanScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        bool changed = false;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            changed |= CleanTransformTree(roots[i].transform);

        return changed;
    }

    private static bool CleanTransformTree(Transform root)
    {
        bool changed = false;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            if (!IsTargetButton(button))
                continue;

            changed |= GameObjectUtility.RemoveMonoBehavioursWithMissingScript(button.gameObject) > 0;
        }

        return changed;
    }

    private static bool IsTargetButton(Button button)
    {
        string objectName = Normalize(button.gameObject.name);
        if (ButtonNames.Contains(objectName))
            return true;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
            return false;

        string text = (label.text ?? string.Empty).Trim().ToUpperInvariant();
        return ButtonLabels.Contains(text);
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim().Replace(' ', '_').ToUpperInvariant();
    }
}
