#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves project assets into the five credited team folders using
/// <see cref="AssetDatabase.MoveAsset"/> so Unity keeps .meta GUIDs and
/// scene/prefab references stay intact.
///
/// Menu: <b>PRISM / Team Credits / …</b>
///
/// <b>Important:</b> Commit or back up before running. Third-party roots such as
/// TextMesh Pro and TutorialInfo are intentionally excluded.
/// </summary>
public static class TeamCreditsAssetReorganizer
{
    public const string Hamed_Ahmed       = "Assets/Hamed_Ahmed";
    public const string Murtadha_Sarhan   = "Assets/Murtadha_Sarhan";
    public const string Mohamed_Altajer   = "Assets/Mohamed_Altajer";
    public const string Ali_Alhawaj       = "Assets/Ali_Alhawaj";
    public const string Mohamed_Aman      = "Assets/Mohamed_Aman";

    [MenuItem("PRISM/Team Credits/Create Student Folder Roots Only (No File Moves)", false, 2)]
    public static void CreateFolderRootsOnly()
    {
        EnsureFolder(Hamed_Ahmed);
        EnsureFolder($"{Hamed_Ahmed}/Scripts");
        EnsureFolder($"{Hamed_Ahmed}/Scenes");
        EnsureFolder($"{Hamed_Ahmed}/UI");

        EnsureFolder(Murtadha_Sarhan);
        EnsureFolder($"{Murtadha_Sarhan}/Scripts");
        EnsureFolder($"{Murtadha_Sarhan}/Resources");

        EnsureFolder(Mohamed_Altajer);
        EnsureFolder($"{Mohamed_Altajer}/Scripts");
        EnsureFolder($"{Mohamed_Altajer}/Scenes");
        EnsureFolder($"{Mohamed_Altajer}/Environment");
        EnsureFolder($"{Mohamed_Altajer}/Editor");

        EnsureFolder(Ali_Alhawaj);
        EnsureFolder($"{Ali_Alhawaj}/Scripts");
        EnsureFolder($"{Ali_Alhawaj}/Resources");

        EnsureFolder(Mohamed_Aman);
        EnsureFolder($"{Mohamed_Aman}/Scripts");
        EnsureFolder($"{Mohamed_Aman}/Resources");
        EnsureFolder($"{Mohamed_Aman}/VFX");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TeamReorganize] Student folder roots created under Assets/ (organizational only).");
    }

    [MenuItem("PRISM/Team Credits/Preview Reorganization (Console Only)", false, 1)]
    public static void Preview()
    {
        List<KeyValuePair<string, string>> moves = BuildMoves();
        for (int i = 0; i < moves.Count; i++)
            Debug.Log($"[TeamReorganize] {moves[i].Key}  -->  {moves[i].Value}");
        Debug.Log($"[TeamReorganize] Total operations: {moves.Count}");
    }

    [MenuItem("PRISM/Team Credits/Reorganize Assets Into Member Folders", false, 0)]
    public static void Execute()
    {
        if (!EditorUtility.DisplayDialog(
                "PRISM-7 — Team folder reorganization",
                "This moves assets into Hamed_Ahmed, Murtadha_Sarhan, Mohamed_Altajer, Ali_Alhawaj, and Mohamed_Aman using Unity's AssetDatabase (GUIDs preserved).\n\n" +
                "Make a Git commit or full backup first.\n\n" +
                "Continue?",
                "Move assets",
                "Cancel"))
            return;

        List<KeyValuePair<string, string>> moves = BuildMoves();
        AssetDatabase.StartAssetEditing();
        try
        {
            int ok = 0, skipped = 0, failed = 0;
            foreach (var pair in moves)
            {
                string from = pair.Key.Replace('\\', '/');
                string to   = pair.Value.Replace('\\', '/');

                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(from)))
                {
                    Debug.LogWarning($"[TeamReorganize] Missing (skipped): {from}");
                    skipped++;
                    continue;
                }

                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(to)))
                {
                    Debug.LogWarning($"[TeamReorganize] Destination already exists (skipped): {to}");
                    skipped++;
                    continue;
                }

                EnsureParentFolderExists(to);
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err))
                {
                    Debug.LogError($"[TeamReorganize] {err}  ({from} -> {to})");
                    failed++;
                }
                else
                {
                    ok++;
                }
            }

            Debug.Log($"[TeamReorganize] Finished. Moved: {ok}, skipped: {skipped}, failed: {failed}.");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Team reorganization",
            "Done. Run Play Mode, open MainMenu + GameScene, and verify.\n\n" +
            "GameManager / SessionManager use type names only — no script path updates required.",
            "OK");
    }

    private static void EnsureParentFolderExists(string assetPath)
    {
        string parent = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(parent)) return;
        EnsureFolder(parent.Replace('\\', '/'));
    }

    private static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath)) return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string name   = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(name)) return;

        if (!string.IsNullOrEmpty(parent) && parent != "Assets")
            EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent ?? "Assets", name);
    }

    /// <summary>Builds the full move list: each source path -> destination path (file or folder).</summary>
    private static List<KeyValuePair<string, string>> BuildMoves()
    {
        var list = new List<KeyValuePair<string, string>>();
        void To(string src, string destFolder)
        {
            src = src.Replace('\\', '/');
            destFolder = destFolder.Replace('\\', '/').TrimEnd('/');
            string leaf = Path.GetFileName(src);
            list.Add(new KeyValuePair<string, string>(src, $"{destFolder}/{leaf}"));
        }

        // ── Member root subfolders (logical grouping) ─────────────────────
        string hScripts   = $"{Hamed_Ahmed}/Scripts";
        string hScenes    = $"{Hamed_Ahmed}/Scenes";
        string hUI        = $"{Hamed_Ahmed}/UI";
        string mScripts   = $"{Murtadha_Sarhan}/Scripts";
        string mResources = $"{Murtadha_Sarhan}/Resources";
        string aScripts   = $"{Mohamed_Altajer}/Scripts";
        string aScenes    = $"{Mohamed_Altajer}/Scenes";
        string aEditor    = $"{Mohamed_Altajer}/Editor";
        string aEnv       = $"{Mohamed_Altajer}/Environment";
        string aiScripts  = $"{Ali_Alhawaj}/Scripts";
        string aiRes      = $"{Ali_Alhawaj}/Resources";
        string amScripts  = $"{Mohamed_Aman}/Scripts";
        string amRes      = $"{Mohamed_Aman}/Resources";
        string amVfx      = $"{Mohamed_Aman}/VFX";

        // ── Whole trees (Resources paths stay loadable: any …/Resources/… works) ──
        To("Assets/Resources/Weapons", mResources);
        To("Assets/Resources/Audio", amRes);
        To("Assets/Resources/Enemy", aiRes);
        To("Assets/Resources/FirstPersonMelee", $"{Mohamed_Altajer}/Resources");
        To("Assets/RPG_FPS_game_assets_industrial", $"{aEnv}/RPG_FPS_game_assets_industrial");
        To("Assets/FirstPersonMelee", Mohamed_Altajer);
        To("Assets/StarterAssets", $"{Mohamed_Altajer}/ThirdParty/StarterAssets");
        To("Assets/VisualScriptingGraphs", $"{Ali_Alhawaj}/VisualScriptingGraphs");
        To("Assets/UI", hUI);
        To("Assets/Fonts", $"{Hamed_Ahmed}/Fonts");
        To("Assets/images", $"{Hamed_Ahmed}/Images");
        To("Assets/Textures", $"{Mohamed_Altajer}/Art/Textures");
        To("Assets/loading.png", $"{Hamed_Ahmed}/Images");
        To("Assets/Editor/LevelBuilderEditor.cs", $"{aEditor}/LevelBuilderEditor.cs");
        To("Assets/Editor/PrismUnusedAssetCleanup.cs", $"{aEditor}/PrismUnusedAssetCleanup.cs");

        // ── Scenes ────────────────────────────────────────────────────────
        To("Assets/Scenes/MainMenu.unity", hScenes);
        To("Assets/Scenes/Options.unity", hScenes);
        To("Assets/Scenes/Settings.unity", hScenes);
        To("Assets/Scenes/Credits.unity", hScenes);
        To("Assets/Scenes/GameScene.unity", aScenes);
        To("Assets/Scenes/GameScene", aScenes); // folder with NavMesh sub-asset

        // ── Hamed_Ahmed — UI / menus ───────────────────────────────────────
        To("Assets/Scripts/RuntimeMenuBuilder.cs", hScripts);
        To("Assets/Scripts/SettingsBuilder.cs", hScripts);
        To("Assets/Scripts/OptionsBuilder.cs", hScripts);
        To("Assets/Scripts/LoadingScreenUI.cs", hScripts);
        To("Assets/Scripts/PauseMenuController.cs", hScripts);
        To("Assets/Scripts/MenuKeyboardNavigator.cs", hScripts);
        To("Assets/Scripts/UIClickAudio.cs", hScripts);
        To("Assets/Scripts/PlayerProfile.cs", hScripts);
        To("Assets/Scripts/LevelSelectBuilder.cs", hScripts);
        To("Assets/Scripts/WinScreenCelebration.cs", hScripts);
        To("Assets/Scripts/CreditsBuilder.cs", hScripts);
        To("Assets/Scripts/MenuButtonHoverEffect.cs", hScripts);
        To("Assets/Scripts/UIManager.cs", hScripts);
        To("Assets/Scripts/HUDManager.cs", hScripts);
        To("Assets/Scripts/CombatUIManager.cs", hScripts);
        To("Assets/Scripts/LevelCompleteManager.cs", hScripts);
        To("Assets/Scripts/MatchStartCountdownUI.cs", hScripts);
        To("Assets/Scripts/MenuManager.cs", hScripts);
        To("Assets/Scripts/MenuNavigationManager.cs", hScripts);
        To("Assets/Scripts/MenuUIManager.cs", hScripts);
        To("Assets/Scripts/SettingsManager.cs", hScripts);

        // ── Murtadha_Sarhan — weapons / combat hitboxes ─────────────────────
        To("Assets/Scripts/WeaponGripSystem.cs", mScripts);
        To("Assets/Scripts/WeaponLiveAnimator.cs", mScripts);
        To("Assets/Scripts/WeaponChest.cs", mScripts);
        To("Assets/Scripts/WeaponHitbox.cs", mScripts);
        To("Assets/Scripts/WeaponIKHandler.cs", mScripts);
        To("Assets/Scripts/WeaponBase.cs", mScripts);
        To("Assets/Scripts/WeaponLoadoutCatalog.cs", mScripts);
        To("Assets/Scripts/WeaponSpecialEffects.cs", mScripts);
        To("Assets/Scripts/WeaponVisibilityFix.cs", mScripts);
        To("Assets/Scripts/WeaponSystemTester.cs", mScripts);
        To("Assets/Scripts/MeleeOverlapAttack.cs", mScripts);
        To("Assets/Scripts/MeleeAnimationEventSink.cs", mScripts);
        To("Assets/Scripts/SickleGripPoseDriver.cs", mScripts);
        To("Assets/Scripts/EquipmentManager.cs", mScripts);
        To("Assets/Scripts/GunController.cs", mScripts);

        // ── Mohamed_Altajer — level design & core ───────────────────────────
        To("Assets/Scripts/LevelBuilder.cs", aScripts);
        To("Assets/Scripts/LevelManager.cs", aScripts);
        To("Assets/Scripts/LevelSetup.cs", aScripts);
        To("Assets/Scripts/GameManager.cs", aScripts);
        To("Assets/Scripts/GameSceneTrigger.cs", aScripts);
        To("Assets/Scripts/PlayerHealth.cs", aScripts);
        To("Assets/Scripts/CameraController.cs", aScripts);
        To("Assets/Scripts/FreeLookFollowCamera.cs", aScripts);
        To("Assets/Scripts/MinimapCameraFollow.cs", aScripts);
        To("Assets/Scripts/RobustThirdPersonMovement.cs", aScripts);
        To("Assets/Scripts/LookSensitivityRuntime.cs", aScripts);
        To("Assets/Scripts/IInteractable.cs", aScripts);
        To("Assets/Scripts/DoorController.cs", aScripts);
        To("Assets/Scripts/PlayerInteractor.cs", aScripts);
        To("Assets/Scripts/MatchStatsManager.cs", aScripts);
        To("Assets/Scripts/SessionManager.cs", aScripts);
        To("Assets/Scripts/EndMatchCinematic.cs", aScripts);
        To("Assets/Scripts/IDamageable.cs", aScripts);
        To("Assets/Scripts/AnimationEventSink.cs", aScripts);

        // ── Ali_Alhawaj — AI & enemies ───────────────────────────────────────
        To("Assets/Scripts/EnemyController.cs", aiScripts);
        To("Assets/Scripts/ActorHealth.cs", aiScripts);
        To("Assets/Scripts/FlowFieldManager.cs", aiScripts);
        To("Assets/Scripts/RagdollController.cs", aiScripts);

        // ── Mohamed_Aman — audio & VFX-oriented scripts ─────────────────────
        To("Assets/Scripts/AudioSettingsRuntime.cs", amScripts);
        To("Assets/Scripts/MatchCommentator.cs", amScripts);
        To("Assets/Scripts/VoClipAutoIndex.cs", amScripts);

        // Optional empty VFX bucket for future particle prefabs (folder only if missing)
        // Created on run via EnsureParentFolderExists when moving into amVfx later — no-op here.

        return list;
    }
}
#endif
