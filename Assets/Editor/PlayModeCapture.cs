using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically dumps runtime diagnostics when Play Mode reaches GameScene.
/// Triggered by TRIGGER_DIAG.txt sentinel file.
/// Uses EditorApplication.update (runs every editor frame, even in Play Mode)
/// so it can inspect live game objects without any runtime-assembly tricks.
/// </summary>
[InitializeOnLoad]
public static class PlayModeCapture
{
    private const string SentinelFile = "Assets/Editor/TRIGGER_DIAG.txt";
    private const string OutputFile   = "Assets/Editor/runtime_diag.json";
    private const string GameSceneName = "GameScene";

    private static bool  _armed;
    private static float _captureAt;   // Time.realtimeSinceStartup target

    static PlayModeCapture()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update               += OnEditorUpdate;
    }

    // ── Play-mode state machine ───────────────────────────────────────────────

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Arm the capture regardless of sentinel — we'll check it in
            // OnEditorUpdate so the sentinel can be written at any time.
            _armed      = true;
            _captureAt  = Time.realtimeSinceStartup + 6f;  // wait 6s for LevelBuilder
            Debug.Log("[PlayModeCapture] Play Mode entered — will capture in ~6s if GameScene is loaded.");
        }

        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            _armed = false;
            EditorSceneManager.playModeStartScene = null;   // restore default start scene
        }
    }

    private static void OnEditorUpdate()
    {
        // ── Edit-mode trigger: sentinel → open GameScene → enter Play Mode ──────
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (File.Exists(SentinelFile))
            {
                File.Delete(SentinelFile);
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/_Shared/Maps/Scenes/GameScene.unity");
                if (sceneAsset != null)
                {
                    EditorSceneManager.playModeStartScene = sceneAsset;
                    Debug.Log("[PlayModeCapture] Sentinel detected — launching GameScene in Play Mode.");
                }
                else
                {
                    Debug.LogWarning("[PlayModeCapture] GameScene.unity not found; using default start scene.");
                }
                _armed    = true;
                _captureAt = 0f;   // will be reset in EnteredPlayMode
                EditorApplication.isPlaying = true;
            }
            return;
        }

        // ── Play-mode capture ────────────────────────────────────────────────────
        if (!EditorApplication.isPlaying) { _armed = false; return; }

        // Immediate capture if manual sentinel is dropped while already in Play Mode.
        if (File.Exists(SentinelFile))
        {
            File.Delete(SentinelFile);
            _armed = true;
            _captureAt = 0f;
        }

        if (!_armed) return;
        if (Time.realtimeSinceStartup < _captureAt) return;

        string scene = SceneManager.GetActiveScene().name;
        if (scene != GameSceneName)
        {
            Debug.Log($"[PlayModeCapture] Scene is '{scene}' (not GameScene) — disarming.");
            _armed = false;
            return;
        }

        _armed = false;
        CaptureAndWrite();
    }

    // ── Capture ───────────────────────────────────────────────────────────────

    // Starts Play Mode directly from GameScene (overrides start scene temporarily).
    [MenuItem("Diagnostics/Start Play Mode from GameScene + Capture %#g")]
    public static void StartFromGameScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlayModeCapture] Already in Play Mode — use Dump instead.");
            return;
        }
        var gameScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(
            "Assets/_Shared/Maps/Scenes/GameScene.unity");
        if (gameScene != null)
            EditorSceneManager.playModeStartScene = gameScene;
        else
            Debug.LogWarning("[PlayModeCapture] GameScene.unity not found — will use default start scene.");

        _armed     = true;
        _captureAt = 0f;   // will be set when EnteredPlayMode fires
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Diagnostics/Dump Runtime State (Play Mode) %#d")]
    public static void TriggerManual()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlayModeCapture] Must be in Play Mode.");
            return;
        }
        CaptureAndWrite();
    }

    private static void CaptureAndWrite()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        // ── Time / pause ─────────────────────────────────────────────────────
        sb.AppendLine($"  \"timeScale\": {Time.timeScale},");
        sb.AppendLine($"  \"isPlaying\": {EditorApplication.isPlaying.ToString().ToLower()},");
        sb.AppendLine($"  \"activeScene\": \"{SceneManager.GetActiveScene().name}\",");
        sb.AppendLine($"  \"frameCount\": {Time.frameCount},");

        // ── Hierarchy overview ────────────────────────────────────────────────
        var roots = new List<string>();
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            roots.Add(go.name);
        sb.AppendLine($"  \"rootObjects\": [{string.Join(", ", roots.ConvertAll(r => $"\"{r}\""))}],");

        // ── GameManager ───────────────────────────────────────────────────────
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            sb.AppendLine("  \"gameManager\": {");
            sb.AppendLine($"    \"enabled\": {gm.enabled.ToString().ToLower()},");
            sb.AppendLine($"    \"currentLevel\": {gm.currentLevel},");
            var pauseF = gm.GetType().GetField("_isPaused",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stateF = gm.GetType().GetField("_matchState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pauseF != null) sb.AppendLine($"    \"_isPaused\": \"{pauseF.GetValue(gm)}\",");
            if (stateF != null) sb.AppendLine($"    \"_matchState\": \"{stateF.GetValue(gm)}\"");
            else                sb.AppendLine($"    \"note\": \"no _matchState field found\"");
            sb.AppendLine("  },");
        }
        else
        {
            sb.AppendLine("  \"gameManager\": null,");
        }

        // ── Player ────────────────────────────────────────────────────────────
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            var cc   = pc.GetComponent<CharacterController>();
            var rb   = pc.GetComponent<Rigidbody>();
            var anim = pc.GetComponentInChildren<Animator>(true);
            var pcT  = pc.GetType();

            sb.AppendLine("  \"player\": {");
            sb.AppendLine($"    \"name\": \"{pc.gameObject.name}\",");
            sb.AppendLine($"    \"enabled\": {pc.enabled.ToString().ToLower()},");
            sb.AppendLine($"    \"active\": {pc.gameObject.activeInHierarchy.ToString().ToLower()},");
            sb.AppendLine($"    \"position\": \"{V3(pc.transform.position)}\",");
            sb.AppendLine($"    \"eulerY\": {pc.transform.eulerAngles.y:F1},");
            sb.AppendLine($"    \"layer\": {LayerMask.LayerToName(pc.gameObject.layer)},");
            sb.AppendLine($"    \"tag\": \"{pc.gameObject.tag}\",");

            if (cc != null)
                sb.AppendLine($"    \"cc_enabled\": {cc.enabled.ToString().ToLower()}, \"cc_isGrounded\": {cc.isGrounded.ToString().ToLower()}, \"cc_velocity\": \"{V3(cc.velocity)}\",");
            if (rb != null)
                sb.AppendLine($"    \"rb_isKinematic\": {rb.isKinematic.ToString().ToLower()}, \"rb_velocity\": \"{V3(rb.linearVelocity)}\",");

            if (anim != null)
            {
                sb.AppendLine($"    \"anim_enabled\": {anim.enabled.ToString().ToLower()},");
                sb.AppendLine($"    \"anim_controller\": \"{(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")}\",");
                sb.AppendLine($"    \"anim_state\": \"{(anim.GetCurrentAnimatorStateInfo(0).IsName("") ? "unknown" : anim.GetCurrentAnimatorClipInfo(0).Length > 0 ? anim.GetCurrentAnimatorClipInfo(0)[0].clip?.name ?? "?" : "?"  )}\",");
                var plines = new List<string>();
                foreach (var p in anim.parameters)
                {
                    string v = p.type switch
                    {
                        AnimatorControllerParameterType.Float   => anim.GetFloat(p.name).ToString("F4"),
                        AnimatorControllerParameterType.Int     => anim.GetInteger(p.name).ToString(),
                        AnimatorControllerParameterType.Bool    => anim.GetBool(p.name).ToString().ToLower(),
                        AnimatorControllerParameterType.Trigger => "(trigger)",
                        _ => "?"
                    };
                    plines.Add($"      \"{p.name}\": \"{v}\"");
                }
                sb.AppendLine($"    \"animParams\": {{\n{string.Join(",\n", plines)}\n    }},");
            }

            // Key PlayerController fields via reflection
            var fieldsToCapture = new[]
            {
                "moveInputRaw", "moveInputSmoothed", "horizontalVelocity", "actualHorizontalVelocity",
                "isGrounded", "isSprinting", "isCrouching", "isProne", "isSliding", "isFlipping",
                "isMantling", "isAttacking", "isThirdPersonActive", "moveSpeed", "turnSpeed",
                "deceleration", "acceleration", "gamepadMoveDeadzone",
                "equippedWeaponName", "equippedWeaponLevel"
            };
            var fl = new List<string>();
            foreach (var fn in fieldsToCapture)
            {
                var f = pcT.GetField(fn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) fl.Add($"      \"{fn}\": \"{f.GetValue(pc)}\"");
            }
            sb.AppendLine($"    \"pcFields\": {{\n{string.Join(",\n", fl)}\n    }}");
            sb.AppendLine("  },");
        }
        else
        {
            sb.AppendLine("  \"player\": null,");
        }

        // ── Enemies ───────────────────────────────────────────────────────────
        var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        sb.AppendLine($"  \"enemyCount\": {enemies.Length},");
        sb.AppendLine("  \"enemies\": [");
        int cap = Mathf.Min(enemies.Length, 8);
        for (int i = 0; i < cap; i++)
        {
            var e     = enemies[i];
            var agent = e.GetComponent<NavMeshAgent>();
            var ecT   = e.GetType();

            string target  = GetPrivate(ecT, e, "_target")  is Transform t ? t.name : "null";
            string state   = GetPrivate(ecT, e, "_state")?.ToString() ?? "?";
            float  lockExp = GetPrivate(ecT, e, "_targetLockTimer") is float lf ? lf : -1f;
            string wpn     = GetPublic(ecT, e, "equippedWeaponObject") is GameObject w && w != null
                             ? $"{w.name} active={w.activeInHierarchy}" : "null";

            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{e.name}\",");
            sb.AppendLine($"      \"state\": \"{state}\",");
            sb.AppendLine($"      \"target\": \"{target}\",");
            sb.AppendLine($"      \"lockExpiry\": {lockExp:F2},");
            sb.AppendLine($"      \"weapon\": \"{wpn}\",");
            sb.AppendLine($"      \"pos\": \"{V3(e.transform.position)}\",");
            if (agent != null)
            {
                sb.AppendLine($"      \"nav_enabled\": {agent.enabled.ToString().ToLower()},");
                sb.AppendLine($"      \"nav_onMesh\": {agent.isOnNavMesh.ToString().ToLower()},");
                sb.AppendLine($"      \"nav_stopped\": {agent.isStopped.ToString().ToLower()},");
                sb.AppendLine($"      \"nav_speed\": {agent.speed:F2},");
                sb.AppendLine($"      \"nav_dest\": \"{V3(agent.destination)}\",");
                sb.AppendLine($"      \"nav_remDist\": {(agent.isOnNavMesh ? agent.remainingDistance.ToString("F2") : "-1")},");
                sb.AppendLine($"      \"nav_pathStatus\": \"{(agent.isOnNavMesh ? agent.pathStatus.ToString() : "offMesh")}\"");
            }
            sb.Append(i < cap - 1 ? "    }," : "    }");
            sb.AppendLine();
        }
        sb.AppendLine("  ],");

        // ── Console errors (last 30 matching lines in Editor.log) ─────────────
        string logPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)
                         + "/Unity/Editor/Editor.log";
        var errors = new List<string>();
        if (File.Exists(logPath))
        {
            try
            {
                // Unity holds Editor.log open, so we must share the file handle.
                using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new System.IO.StreamReader(fs);
                var allLines = sr.ReadToEnd().Split('\n');
                for (int i = allLines.Length - 1; i >= 0 && errors.Count < 30; i--)
                {
                    var l = allLines[i];
                    if (l.Contains("Exception") || l.Contains("NullReference") ||
                        l.StartsWith("Error") || l.Contains("error CS"))
                        errors.Add(l.Replace("\"", "'").Replace("\\", "/").Trim());
                }
            }
            catch (System.Exception ex)
            {
                errors.Add($"[log read failed: {ex.Message}]");
            }
        }
        sb.AppendLine("  \"consoleErrors\": [");
        for (int i = 0; i < errors.Count; i++)
            sb.AppendLine($"    \"{errors[i]}\"{(i < errors.Count - 1 ? "," : "")}");
        sb.AppendLine("  ]");

        sb.AppendLine("}");

        string absOut = Path.GetFullPath(OutputFile);
        File.WriteAllText(absOut, sb.ToString());
        Debug.Log($"[PlayModeCapture] Diagnostics written → {absOut}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object GetPrivate(System.Type t, object obj, string name)
        => t.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(obj);

    private static object GetPublic(System.Type t, object obj, string name)
        => t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(obj);

    private static string V3(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
}
