using UnityEditor;
using UnityEngine;

public class AutoSetupGame
{
    [MenuItem("Tools/PRISM7/Auto Setup First Person Player")]
    public static void AutoSetup()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("AutoSetup: Could not find GameObject named 'Player'");
            return;
        }

        SetupPlayer(player);
        EditorUtility.SetDirty(player);
        Debug.Log("First-person player setup complete.");
    }

    private static void SetupPlayer(GameObject player)
    {
        if (player.GetComponent<PlayerHealth>() == null)
        {
            player.AddComponent<PlayerHealth>();
        }

        if (player.GetComponent<CharacterController>() == null)
        {
            player.AddComponent<CharacterController>();
        }

        Object playerController = player.GetComponent("PlayerController");
        if (playerController == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(playerController);
        Camera playerCamera = FindCameraByName(player, "Camera");

        SetObjectReferenceIfExists(serializedObject, "cam", playerCamera);
        SetObjectReferenceIfExists(serializedObject, "firstPersonCam", playerCamera);
        SetObjectReferenceIfExists(serializedObject, "thirdPersonCam", null);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Camera FindCameraByName(GameObject parent, string nameContains)
    {
        Camera[] cameras = parent.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera.name.ToLower().Contains(nameContains.ToLower()))
            {
                return camera;
            }
        }

        return null;
    }

    private static void SetObjectReferenceIfExists(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
        {
            property.objectReferenceValue = value;
        }
    }
}
