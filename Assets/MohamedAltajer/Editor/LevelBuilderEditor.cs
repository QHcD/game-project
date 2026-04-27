using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelBuilder))]
public class LevelBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(12f);
        EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use these tools to rebuild the map preview, clear stale level geometry, and prepare the scene for NavMesh baking without entering Play Mode.",
            MessageType.Info);

        LevelBuilder builder = (LevelBuilder)target;
        if (builder == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Preview", GUILayout.Height(32f)))
            {
                builder.BuildEditorScenePreview(force: true);
                EditorUtility.SetDirty(builder);
            }

            if (GUILayout.Button("Clear Existing Level", GUILayout.Height(32f)))
            {
                builder.ClearExistingLevel();
                EditorUtility.SetDirty(builder);
            }
        }

        if (GUILayout.Button("Prepare For Bake", GUILayout.Height(34f)))
        {
            builder.PrepareForBake();
            EditorUtility.SetDirty(builder);
        }
    }
}
