using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class AutoSetupGame
{
    [MenuItem("Tools/PRISM7/Auto Setup Player And Enemy")]
    public static void AutoSetup()
    {
        GameObject player = GameObject.Find("Player");
        GameObject enemy = GameObject.Find("Enemy");

        if (player == null)
        {
            Debug.LogError("AutoSetup: Could not find GameObject named 'Player'");
            return;
        }

        if (enemy == null)
        {
            Debug.LogError("AutoSetup: Could not find GameObject named 'Enemy'");
            return;
        }

        SetupPlayer(player);
        SetupEnemy(enemy);

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(enemy);

        Debug.Log("Auto setup complete.");
    }

    private static void SetupPlayer(GameObject player)
    {
        if (player.GetComponent<PlayerHealth>() == null)
            player.AddComponent<PlayerHealth>();

        if (player.GetComponent<CharacterController>() == null)
            player.AddComponent<CharacterController>();

        Transform playerBody = FindChildRecursive(player.transform, "Player_Base");
        if (playerBody == null)
            playerBody = player.transform;

        var playerController = player.GetComponent("PlayerController");
        if (playerController != null)
        {
            SerializedObject so = new SerializedObject(playerController);

            SetObjectReferenceIfExists(so, "playerBody", playerBody);
            SetObjectReferenceIfExists(so, "firstPersonCam", FindCameraByName(player, "FirstPersonCam"));
            SetObjectReferenceIfExists(so, "thirdPersonCam", FindCameraByName(player, "ThirdPersonCam"));

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupEnemy(GameObject enemy)
    {
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = enemy.AddComponent<NavMeshAgent>();

        agent.radius = 0.5f;
        agent.height = 2f;
        agent.speed = 3.5f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 1.5f;

        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI == null)
            enemyAI = enemy.AddComponent<EnemyAI>();

        enemyAI.player = GameObject.Find("Player")?.transform;
        enemyAI.agent = agent;
        enemyAI.animator = enemy.GetComponentInChildren<Animator>();

        EnemyWeaponAttach weaponAttach = enemy.GetComponent<EnemyWeaponAttach>();
        if (weaponAttach == null)
            weaponAttach = enemy.AddComponent<EnemyWeaponAttach>();

        weaponAttach.weaponObjectName = "knife";
        weaponAttach.localPosition = Vector3.zero;
        weaponAttach.localRotation = new Vector3(0f, 90f, 0f);
        weaponAttach.localScale = Vector3.one;
    }

    private static Transform FindChildRecursive(Transform parent, string containsName)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLower().Contains(containsName.ToLower()))
                return t;
        }
        return null;
    }

    private static Camera FindCameraByName(GameObject parent, string nameContains)
    {
        Camera[] cams = parent.GetComponentsInChildren<Camera>(true);
        foreach (Camera cam in cams)
        {
            if (cam.name.ToLower().Contains(nameContains.ToLower()))
                return cam;
        }
        return null;
    }

    private static void SetObjectReferenceIfExists(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            prop.objectReferenceValue = value;
        }
    }
}
