using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes the root cause of the Z-key ground-clipping bug.
///
/// The three tactical FBX clips (Jump Over, Running Slide, Prone Idle) are
/// Humanoid animations whose "Bake Into Pose" (keepOriginalPositionY) flag is
/// not set. Because of this Unity evaluates the full body-Y position from the
/// clip every frame, placing the character mesh's center of mass underground
/// even when applyRootMotion = false.  Root motion blocking only prevents the
/// ROOT TRANSFORM from moving; it does not stop the Humanoid avatar from
/// computing its body position from the clip data.
///
/// This script enables keepOriginalPositionY = true (and keepOriginalPositionXZ
/// = true, since movement is handled by CharacterController anyway).  The
/// animation body position is then held at the Avatar's reference height, and
/// the bones still animate the correct pose without the mesh diving into ground.
///
/// Run once: Tools > PRISM-7 > Fix Tactical Animation Clipping (Bake Y)
/// </summary>
public static class FixTacticalAnimations
{
    private static readonly string[] Paths =
    {
        "Assets/MohamedAltajer/Materials/Jump Over.fbx",
        "Assets/MohamedAltajer/Materials/Running Slide.fbx",
        "Assets/MohamedAltajer/Materials/Prone Idle.fbx",
    };

    [MenuItem("Tools/PRISM-7/Fix Tactical Animation Clipping (Bake Y)")]
    public static void Run()
    {
        int reimportCount = 0;
        foreach (string path in Paths)
            if (ProcessFbx(path)) reimportCount++;

        EditorUtility.DisplayDialog(
            "Fix Tactical Animation Clipping",
            reimportCount > 0
                ? "Done. Re-imported " + reimportCount + " file(s).\n\nEnter Play Mode and press Z — the character should no longer sink into the ground."
                : "All files were already correctly configured.",
            "OK");
    }

    private static bool ProcessFbx(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[FixTacticalAnimations] Could not load ModelImporter at: " + path);
            return false;
        }

        // When clipAnimations is empty Unity uses defaultClipAnimations (the raw
        // clips from the FBX with all settings at their defaults).  We must read
        // defaultClipAnimations, mutate them, then assign back to clipAnimations
        // so the settings become explicit and survive reimport.
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (var clip in clips)
        {
            // keepOriginalPositionY = true  →  "Bake Into Pose" for body Y.
            // Keeps the avatar's center of mass at the reference height so the
            // body does not descend below the ground plane during the animation.
            if (!clip.keepOriginalPositionY)
            {
                clip.keepOriginalPositionY = true;
                changed = true;
            }

            // keepOriginalPositionXZ = true  →  "Bake Into Pose" for body XZ.
            // Movement is driven by CharacterController, so baking XZ is also
            // correct; it prevents any residual horizontal root drift in the pose.
            if (!clip.keepOriginalPositionXZ)
            {
                clip.keepOriginalPositionXZ = true;
                changed = true;
            }
        }

        if (!changed)
        {
            Debug.Log("[FixTacticalAnimations] Already correct: " + path);
            return false;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log("[FixTacticalAnimations] Fixed and reimported: " + path);
        return true;
    }
}
