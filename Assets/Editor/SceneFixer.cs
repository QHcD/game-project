using UnityEngine;
using UnityEditor;

public class SceneFixer
{
    [MenuItem("PRISM-7/Fix Scene (Run Before Build)")]
    public static void FixScene()
    {
        int fixedPositions = 0;
        int removedScripts = 0;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject go in allObjects)
        {
            if (go == null) continue;

            // Fix invalid positions (NaN or Infinity)
            Vector3 pos = go.transform.position;
            bool posInvalid = float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z)
                           || float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z);

            if (posInvalid)
            {
                go.transform.position = Vector3.zero;
                fixedPositions++;
                Debug.Log($"[SceneFixer] Fixed invalid position on: {go.name}");
            }

            // Fix invalid rotations
            Quaternion rot = go.transform.rotation;
            bool rotInvalid = float.IsNaN(rot.x) || float.IsNaN(rot.y) || float.IsNaN(rot.z) || float.IsNaN(rot.w);
            if (rotInvalid)
            {
                go.transform.rotation = Quaternion.identity;
                Debug.Log($"[SceneFixer] Fixed invalid rotation on: {go.name}");
            }

            // Remove missing scripts
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removedScripts++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[SceneFixer] Done! Fixed {fixedPositions} invalid positions, cleaned missing scripts.");
        EditorUtility.DisplayDialog("Scene Fixed",
            $"Fixed {fixedPositions} invalid positions.\nCleaned missing scripts.\nScene saved.\n\nNow do: File → Build Profiles → Build",
            "OK");
    }
}
