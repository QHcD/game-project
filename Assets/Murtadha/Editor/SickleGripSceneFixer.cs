using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SickleGripSceneFixer
{
    private const string SocketName = "__PlayerWeaponSocket";
    private static readonly Vector3 SickleLocalPosition = new Vector3(0.006f, 0.002f, 0.026f);
    private static readonly Vector3 SickleLocalEuler = new Vector3(86f, 5f, 98f);
    private static bool queued;

    static SickleGripSceneFixer()
    {
        EditorApplication.delayCall += QueueFix;
        EditorSceneManager.sceneOpened += (_, _) => QueueFix();
        EditorApplication.hierarchyChanged += QueueFix;
    }

    [MenuItem("PRISM/Fix Player Sickle Grip In Scene")]
    public static void FixNow()
    {
        FixFloatingSickleInOpenScene(force: true);
    }

    private static void QueueFix()
    {
        if (queued || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            FixFloatingSickleInOpenScene(force: false);
        };
    }

    private static void FixFloatingSickleInOpenScene(bool force)
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        GameObject player = GameObject.Find("Player");
        if (player == null)
            return;

        Transform hand = FindRightHand(player.transform);
        if (hand == null)
            return;

        Transform socket = GetOrCreateSocket(hand);
        bool changed = false;

        foreach (Transform sickle in player.GetComponentsInChildren<Transform>(true))
        {
            if (sickle == player.transform || !LooksLikeSickle(sickle.name))
                continue;

            if (!force && sickle.parent == socket && IsClosePose(sickle))
                continue;

            Undo.SetTransformParent(sickle, socket, "Fix Sickle Grip");
            sickle.localPosition = SickleLocalPosition;
            sickle.localRotation = Quaternion.Euler(SickleLocalEuler);
            ForceVisible(sickle.gameObject);
            EnsureFingerPose(hand, sickle);
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }

    private static bool LooksLikeSickle(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.ToLowerInvariant().Contains("sickle");
    }

    private static bool IsClosePose(Transform sickle)
    {
        return Vector3.Distance(sickle.localPosition, SickleLocalPosition) < 0.001f
            && Quaternion.Angle(sickle.localRotation, Quaternion.Euler(SickleLocalEuler)) < 0.5f;
    }

    private static Transform GetOrCreateSocket(Transform hand)
    {
        Transform socket = hand.Find(SocketName);
        if (socket == null)
        {
            GameObject socketObject = new GameObject(SocketName);
            Undo.RegisterCreatedObjectUndo(socketObject, "Create Player Weapon Socket");
            socket = socketObject.transform;
            Undo.SetTransformParent(socket, hand, "Parent Player Weapon Socket");
        }

        socket.localPosition = Vector3.zero;
        socket.localRotation = Quaternion.identity;

        Vector3 handLossy = hand.lossyScale;
        socket.localScale = new Vector3(
            1f / Mathf.Max(Mathf.Abs(handLossy.x), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(handLossy.y), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(handLossy.z), 0.0001f));

        return socket;
    }

    private static Transform FindRightHand(Transform root)
    {
        string[] exact =
        {
            "Hand.R",
            "Wrist.R",
            "Palm.R",
            "RightHand",
            "mixamorig:RightHand",
            "j_wrist_ri",
            "weapon_bone_R",
            "bip_hand_R",
            "Hand_R",
            "hand_R",
            "hand_r",
            "Wrist_R",
            "wrist_R"
        };

        for (int i = 0; i < exact.Length; i++)
        {
            Transform found = FindDeepExact(root, exact[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDeepExact(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }

        return null;
    }

    private static void ForceVisible(GameObject obj)
    {
        obj.SetActive(true);
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.SetActive(true);

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].enabled = true;
            renderers[i].forceRenderingOff = false;
        }
    }

    private static void EnsureFingerPose(Transform hand, Transform weapon)
    {
        if (hand == null || weapon == null)
            return;

        SickleGripPoseDriver driver = hand.GetComponent<SickleGripPoseDriver>();
        if (driver == null)
        {
            driver = Undo.AddComponent<SickleGripPoseDriver>(hand.gameObject);
        }

        driver.Configure(hand, weapon);
        EditorUtility.SetDirty(driver);
    }
}
