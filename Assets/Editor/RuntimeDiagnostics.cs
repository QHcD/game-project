using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runtime diagnostics tool. Dumps live scene state to
/// Assets/Editor/runtime_diag.json while in Play Mode.
/// Menu: Diagnostics > Dump Runtime State
/// Also auto-triggers when TRIGGER_DIAG.txt is present on domain reload.
/// </summary>
public static class RuntimeDiagnostics
{
    private const string TriggerFile = "Assets/Editor/TRIGGER_DIAG.txt";
    private const string OutputFile  = "Assets/Editor/runtime_diag.json";

    [MenuItem("Diagnostics/Dump Runtime State %#d")]   // Ctrl+Shift+D
    public static void DumpRuntimeState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Diag] Enter Play Mode first, then run Diagnostics > Dump Runtime State.");
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine("{");

        // ── Time / pause ─────────────────────────────────────────────────────
        sb.AppendLine($"  \"timeScale\": {Time.timeScale},");
        sb.AppendLine($"  \"frameCount\": {Time.frameCount},");
        sb.AppendLine($"  \"realtimeSinceStartup\": {Time.realtimeSinceStartup:F2},");

        // ── GameManager ───────────────────────────────────────────────────────
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            sb.AppendLine($"  \"gameManager\": {{");
            sb.AppendLine($"    \"enabled\": {gm.enabled.ToString().ToLower()},");
            sb.AppendLine($"    \"currentLevel\": {gm.currentLevel},");
            var gmType = gm.GetType();
            foreach (var f in gmType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if (f.Name.ToLower().Contains("pause") || f.Name.ToLower().Contains("state") || f.Name.ToLower().Contains("phase") || f.Name.ToLower().Contains("active"))
                {
                    var v = f.GetValue(gm);
                    sb.AppendLine($"    \"{f.Name}\": \"{v}\",");
                }
            }
            sb.AppendLine($"    \"gameObject\": \"{gm.gameObject.name}\"");
            sb.AppendLine($"  }},");
        }
        else
        {
            sb.AppendLine($"  \"gameManager\": null,");
        }

        // ── Player ────────────────────────────────────────────────────────────
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            var cc  = pc.GetComponent<CharacterController>();
            var rb  = pc.GetComponent<Rigidbody>();
            var anim = pc.GetComponentInChildren<Animator>(true);

            sb.AppendLine($"  \"player\": {{");
            sb.AppendLine($"    \"name\": \"{pc.gameObject.name}\",");
            sb.AppendLine($"    \"enabled\": {pc.enabled.ToString().ToLower()},");
            sb.AppendLine($"    \"active\": {pc.gameObject.activeInHierarchy.ToString().ToLower()},");
            sb.AppendLine($"    \"position\": \"{pc.transform.position}\",");
            sb.AppendLine($"    \"rotation\": \"{pc.transform.eulerAngles}\",");
            sb.AppendLine($"    \"layer\": {pc.gameObject.layer},");
            sb.AppendLine($"    \"tag\": \"{pc.gameObject.tag}\",");

            // CharacterController
            if (cc != null)
                sb.AppendLine($"    \"cc_enabled\": {cc.enabled.ToString().ToLower()}, \"cc_isGrounded\": {cc.isGrounded.ToString().ToLower()}, \"cc_velocity\": \"{cc.velocity}\",");

            // Rigidbody
            if (rb != null)
                sb.AppendLine($"    \"rb_isKinematic\": {rb.isKinematic.ToString().ToLower()}, \"rb_velocity\": \"{rb.linearVelocity}\",");

            // Animator
            if (anim != null)
            {
                sb.AppendLine($"    \"anim_enabled\": {anim.enabled.ToString().ToLower()},");
                sb.AppendLine($"    \"anim_controller\": \"{(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")}\",");
                sb.AppendLine($"    \"anim_isHuman\": {anim.isHuman.ToString().ToLower()},");
                var paramLines = new List<string>();
                foreach (var p in anim.parameters)
                {
                    string val = p.type switch
                    {
                        AnimatorControllerParameterType.Float  => anim.GetFloat(p.name).ToString("F4"),
                        AnimatorControllerParameterType.Int    => anim.GetInteger(p.name).ToString(),
                        AnimatorControllerParameterType.Bool   => anim.GetBool(p.name).ToString().ToLower(),
                        AnimatorControllerParameterType.Trigger => "(trigger)",
                        _ => "?"
                    };
                    paramLines.Add($"      \"{p.name}\": \"{val}\"");
                }
                sb.AppendLine($"    \"anim_params\": {{");
                sb.AppendLine(string.Join(",\n", paramLines));
                sb.AppendLine($"    }},");
            }

            // PlayerController debug fields via reflection
            var pcType = pc.GetType();
            var debugFields = new[] {
                "moveInputRaw","moveInputSmoothed","horizontalVelocity","actualHorizontalVelocity",
                "isGrounded","isSprinting","isCrouching","isProne","isSliding","isFlipping","isMantling",
                "isAttacking","isThirdPersonActive","moveSpeed","turnSpeed","deceleration","acceleration",
                "gamepadMoveDeadzone","equippedWeaponName","equippedWeaponLevel"
            };
            sb.AppendLine($"    \"pcFields\": {{");
            var fieldLines = new List<string>();
            foreach (var fn in debugFields)
            {
                var f = pcType.GetField(fn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) fieldLines.Add($"      \"{fn}\": \"{f.GetValue(pc)}\"");
            }
            sb.AppendLine(string.Join(",\n", fieldLines));
            sb.AppendLine($"    }}");
            sb.AppendLine($"  }},");
        }
        else
        {
            sb.AppendLine($"  \"player\": null,");
        }

        // ── Enemies ───────────────────────────────────────────────────────────
        var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        sb.AppendLine($"  \"enemyCount\": {enemies.Length},");
        sb.AppendLine($"  \"enemies\": [");
        for (int i = 0; i < Mathf.Min(enemies.Length, 6); i++)
        {
            var e   = enemies[i];
            var agent = e.GetComponent<NavMeshAgent>();
            var ecType = e.GetType();

            string targetName = "null";
            string stateName  = "?";
            string weaponInfo = "null";
            float  lockTimer  = -1f;

            // Reflection to grab private fields
            var targetF = ecType.GetField("_target",          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stateF  = ecType.GetField("_state",           System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lockF   = ecType.GetField("_targetLockTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var wpnF    = ecType.GetField("equippedWeaponObject", System.Reflection.BindingFlags.Public  | System.Reflection.BindingFlags.Instance);

            if (targetF?.GetValue(e) is Transform tgt) targetName = tgt != null ? tgt.name : "null";
            if (stateF?.GetValue(e) is object st)      stateName  = st.ToString();
            if (lockF?.GetValue(e)  is float lt)       lockTimer  = lt;
            if (wpnF?.GetValue(e)   is GameObject wpn) weaponInfo = wpn != null ? $"{wpn.name} active={wpn.activeInHierarchy}" : "null";

            sb.AppendLine($"  {{");
            sb.AppendLine($"    \"name\": \"{e.name}\",");
            sb.AppendLine($"    \"enabled\": {e.enabled.ToString().ToLower()},");
            sb.AppendLine($"    \"position\": \"{e.transform.position}\",");
            sb.AppendLine($"    \"state\": \"{stateName}\",");
            sb.AppendLine($"    \"target\": \"{targetName}\",");
            sb.AppendLine($"    \"lockTimerExpiry\": {lockTimer:F3},");
            sb.AppendLine($"    \"equippedWeapon\": \"{weaponInfo}\",");
            if (agent != null)
            {
                sb.AppendLine($"    \"agent_enabled\": {agent.enabled.ToString().ToLower()},");
                sb.AppendLine($"    \"agent_isOnNavMesh\": {agent.isOnNavMesh.ToString().ToLower()},");
                sb.AppendLine($"    \"agent_isStopped\": {agent.isStopped.ToString().ToLower()},");
                sb.AppendLine($"    \"agent_speed\": {agent.speed:F2},");
                sb.AppendLine($"    \"agent_destination\": \"{agent.destination}\",");
                sb.AppendLine($"    \"agent_remainingDist\": {(agent.isOnNavMesh ? agent.remainingDistance.ToString("F2") : "-1")},");
                sb.AppendLine($"    \"agent_pathStatus\": \"{(agent.isOnNavMesh ? agent.pathStatus.ToString() : "offMesh")}\"");
            }
            sb.Append(i < Mathf.Min(enemies.Length, 6) - 1 ? "  }," : "  }");
            sb.AppendLine();
        }
        sb.AppendLine($"  ],");

        // ── Console errors (from log file proxy) ─────────────────────────────
        // Grab the 20 most recent editor log lines that are errors/warnings
        string editorLog = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)
                           + "/Unity/Editor/Editor.log";
        var errLines = new List<string>();
        if (File.Exists(editorLog))
        {
            var allLines = File.ReadAllLines(editorLog);
            for (int i = allLines.Length - 1; i >= 0 && errLines.Count < 20; i--)
            {
                var l = allLines[i];
                if (l.Contains("Exception") || l.Contains("Error") || l.Contains("error CS") || l.Contains("NullReference"))
                    errLines.Add(l.Replace("\\", "\\\\").Replace("\"", "'"));
            }
        }
        sb.AppendLine($"  \"recentErrors\": [");
        for (int i = 0; i < errLines.Count; i++)
            sb.AppendLine($"    \"{errLines[i]}\"{(i < errLines.Count - 1 ? "," : "")}");
        sb.AppendLine($"  ]");

        sb.AppendLine("}");

        File.WriteAllText(OutputFile, sb.ToString());
        Debug.Log($"[Diag] Runtime state dumped to {OutputFile}");
        // Also log summary to console so it shows in Unity Console window
        Debug.Log($"[Diag] Player enabled={pc != null && pc.enabled}  timeScale={Time.timeScale}  enemies={enemies.Length}");
    }
}
