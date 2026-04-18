// InputSystemAutoFix.cs
// Keeps a manual repair command for the Input System settings asset.
// Avoid auto-running this on editor load because asset operations and
// InputSystem.settings assignment can happen during background validation.

using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InputSystemAutoFix
{
    private const string SettingsPath = "Assets/Settings/InputSettings.asset";

    private static InputSettings FindExistingSettings()
    {
        // Search by asset type.
        string[] guids = AssetDatabase.FindAssets("t:InputSettings");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            InputSettings found = AssetDatabase.LoadAssetAtPath<InputSettings>(path);
            if (found != null) return found;
        }
        return null;
    }

    // ── Menu item for manual one-click repair ──────────────────────────────
    [MenuItem("PRISM/Fix Input System Settings")]
    private static void FixManually()
    {
        // Force-run even if settings appears assigned (useful after a bad state).
        InputSettings settings = FindExistingSettings();

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<InputSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
        }

        InputSystem.settings = settings;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[InputSystemAutoFix] Manual fix applied. InputSystem.settings = " + settings);
        EditorUtility.DisplayDialog("PRISM", "Input System settings fixed!\nCheck Console — errors should be gone.", "OK");
    }
}
