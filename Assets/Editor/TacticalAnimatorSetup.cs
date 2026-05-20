using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-click wiring of Z/X/C tactical actions into Animator Controllers.
///
/// Menus:
///   Tools > PRISM-7 > Setup Tactical Animator (Player)
///   Tools > PRISM-7 > Setup Tactical Animator (Enemy / Crosby)
///   Tools > PRISM-7 > Setup Tactical Animator (Both)
///
/// Idempotent: re-running never duplicates parameters, states, or
/// transitions. Backs up the target .controller file before any edits.
/// </summary>
public static class TacticalAnimatorSetup
{
    private const string PlayerControllerPath = "Assets/Murtadha/Prefabs/FirstPersonMelee/Controllers/Player Controller.controller";
    private const string EnemyControllerPath  = "Assets/AliAlhawaj/Prefabs/Enemies/Resources/Enemy/CrosbyAnimator.controller";

    private const string JumpOverFbx = "Assets/MohamedAltajer/Materials/Jump Over.fbx";
    private const string SlideFbx    = "Assets/MohamedAltajer/Materials/Running Slide.fbx";
    private const string ProneFbx    = "Assets/MohamedAltajer/Materials/Prone Idle.fbx";

    private const string PJumpOver = "JumpOver";
    private const string PSlide    = "Slide";
    private const string PIsProne  = "IsProne";

    private const string SJumpOver = "JumpOver";
    private const string SSlide    = "Slide";
    private const string SProne    = "Prone Idle";

    [MenuItem("Tools/PRISM-7/Setup Tactical Animator (Player)")]
    public static void RunPlayer() => Run(PlayerControllerPath, askConfirm: true);

    [MenuItem("Tools/PRISM-7/Setup Tactical Animator (Enemy Crosby)")]
    public static void RunEnemy()  => Run(EnemyControllerPath, askConfirm: true);

    [MenuItem("Tools/PRISM-7/Setup Tactical Animator (Both)")]
    public static void RunBoth()
    {
        if (!EditorUtility.DisplayDialog("Tactical Animator Setup",
            "Wire BOTH Animator Controllers (Player + Enemy)?\n\n"
            + "Both controllers will be backed up first.",
            "Proceed", "Cancel")) return;
        Run(PlayerControllerPath, askConfirm: false);
        Run(EnemyControllerPath,  askConfirm: false);
    }

    public static void Run(string controllerPath, bool askConfirm)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Tactical Animator Setup",
                "Could not find Animator Controller at:\n" + controllerPath, "OK");
            return;
        }

        if (askConfirm && !EditorUtility.DisplayDialog("Tactical Animator Setup",
            "This will:\n" +
            "  1) Back up '" + Path.GetFileName(controllerPath) + "'\n" +
            "  2) Add parameters JumpOver / Slide / IsProne (if missing)\n" +
            "  3) Add states JumpOver / Slide / Prone Idle (if missing)\n" +
            "  4) Add Any-State -> state transitions (if missing)\n\n" +
            "Existing locomotion / attack / blend trees are NOT touched.\n\nProceed?",
            "Proceed", "Cancel"))
            return;

        string backup = BackupController(controllerPath);
        Debug.Log("[TacticalAnimatorSetup] Backup written: " + backup);

        AddParameter(controller, PJumpOver, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, PSlide,    AnimatorControllerParameterType.Trigger);
        AddParameter(controller, PIsProne,  AnimatorControllerParameterType.Bool);

        if (controller.layers == null || controller.layers.Length == 0)
        {
            Debug.LogError("[TacticalAnimatorSetup] Controller has no layers: " + controllerPath);
            return;
        }
        var rootSM = controller.layers[0].stateMachine;

        var jumpOverClip = LoadFirstClip(JumpOverFbx);
        var slideClip    = LoadFirstClip(SlideFbx);
        var proneClip    = LoadFirstClip(ProneFbx);

        if (jumpOverClip == null) Debug.LogWarning("[TacticalAnimatorSetup] No AnimationClip in " + JumpOverFbx);
        if (slideClip    == null) Debug.LogWarning("[TacticalAnimatorSetup] No AnimationClip in " + SlideFbx);
        if (proneClip    == null) Debug.LogWarning("[TacticalAnimatorSetup] No AnimationClip in " + ProneFbx);

        var jumpOverState = GetOrCreateState(rootSM, SJumpOver, jumpOverClip, new Vector3(400, -120, 0));
        var slideState    = GetOrCreateState(rootSM, SSlide,    slideClip,    new Vector3(400,    0, 0));
        var proneState    = GetOrCreateState(rootSM, SProne,    proneClip,    new Vector3(400,  120, 0));

        EnsureAnyStateTransition(rootSM, jumpOverState, PJumpOver, AnimatorConditionMode.If, 0f,
            hasExitTime: false, duration: 0.05f);
        EnsureExitTransition(jumpOverState, exitTimeNormalized: 0.85f, duration: 0.1f);

        EnsureAnyStateTransition(rootSM, slideState, PSlide, AnimatorConditionMode.If, 0f,
            hasExitTime: false, duration: 0.05f);
        EnsureExitTransition(slideState, exitTimeNormalized: 0.85f, duration: 0.1f);

        EnsureAnyStateTransition(rootSM, proneState, PIsProne, AnimatorConditionMode.If, 0f,
            hasExitTime: false, duration: 0.15f);
        EnsureExitTransitionWithCondition(proneState, PIsProne, AnimatorConditionMode.IfNot, 0f, 0.15f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TacticalAnimatorSetup] Wired " + Path.GetFileName(controllerPath)
                  + " — JumpOver / Slide / IsProne states + transitions in place.");
    }

    private static string BackupController(string controllerPath)
    {
        string dir = Path.GetDirectoryName(controllerPath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(controllerPath);
        string backupDir = dir + "/Backups";
        if (!AssetDatabase.IsValidFolder(backupDir))
            AssetDatabase.CreateFolder(dir, "Backups");
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string target = backupDir + "/" + name + "_backup_" + stamp + ".controller";
        AssetDatabase.CopyAsset(controllerPath, target);
        return target;
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
            if (p.name == name) return;
        controller.AddParameter(name, type);
    }

    private static AnimationClip LoadFirstClip(string assetPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (var a in assets)
        {
            var clip = a as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__"))
                return clip;
        }
        return null;
    }

    private static AnimatorState GetOrCreateState(AnimatorStateMachine sm, string stateName, AnimationClip clip, Vector3 pos)
    {
        foreach (var cs in sm.states)
            if (cs.state.name == stateName)
            {
                if (clip != null && cs.state.motion == null)
                    cs.state.motion = clip;
                return cs.state;
            }
        var state = sm.AddState(stateName, pos);
        state.motion = clip;
        return state;
    }

    private static void EnsureAnyStateTransition(AnimatorStateMachine sm, AnimatorState dest,
        string param, AnimatorConditionMode mode, float threshold, bool hasExitTime, float duration)
    {
        foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationState == dest && HasCondition(t, param, mode))
                return;
        }
        var trans = sm.AddAnyStateTransition(dest);
        trans.hasExitTime = hasExitTime;
        trans.duration = duration;
        trans.canTransitionToSelf = false;
        trans.AddCondition(mode, threshold, param);
    }

    private static void EnsureExitTransition(AnimatorState state, float exitTimeNormalized, float duration)
    {
        foreach (var t in state.transitions)
            if (t.isExit && t.conditions.Length == 0) return;
        var trans = state.AddExitTransition();
        trans.hasExitTime = true;
        trans.exitTime = exitTimeNormalized;
        trans.duration = duration;
    }

    private static void EnsureExitTransitionWithCondition(AnimatorState state, string param,
        AnimatorConditionMode mode, float threshold, float duration)
    {
        foreach (var t in state.transitions)
            if (t.isExit && HasCondition(t, param, mode)) return;
        var trans = state.AddExitTransition();
        trans.hasExitTime = false;
        trans.duration = duration;
        trans.AddCondition(mode, threshold, param);
    }

    private static bool HasCondition(AnimatorTransitionBase t, string param, AnimatorConditionMode mode)
    {
        foreach (var c in t.conditions)
            if (c.parameter == param && c.mode == mode) return true;
        return false;
    }
}
