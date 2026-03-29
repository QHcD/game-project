using UnityEngine;

public class EnemyWeaponAttach : MonoBehaviour
{
    [Header("Auto Find Weapon In Scene/Project")]
    public string weaponObjectName = "knife";

    [Header("Offsets")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = new Vector3(0f, 90f, 0f);
    public Vector3 localScale = Vector3.one;

    private Transform rightHand;
    private GameObject spawnedWeapon;

    void Start()
    {
        rightHand = FindRightHand(transform);

        if (rightHand == null)
        {
            Debug.LogError("Right hand bone not found.");
            return;
        }

        GameObject sourceWeapon = FindWeaponObject();

        if (sourceWeapon == null)
        {
            Debug.LogError("Weapon not found. Make sure there is an object named like: " + weaponObjectName);
            return;
        }

        AttachWeapon(sourceWeapon);
    }

    Transform FindRightHand(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();

            if (n == "mixamorig9:righthand" ||
                n == "mixamorig:righthand" ||
                n.Contains("righthand"))
            {
                return t;
            }
        }

        return null;
    }

    GameObject FindWeaponObject()
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains(weaponObjectName.ToLower()))
            {
                if (obj.scene.IsValid())
                    continue;

                return obj;
            }
        }

        GameObject sceneObj = GameObject.Find(weaponObjectName);
        if (sceneObj != null) return sceneObj;

        return null;
    }

    void AttachWeapon(GameObject sourceWeapon)
    {
        if (spawnedWeapon != null)
            Destroy(spawnedWeapon);

        spawnedWeapon = Instantiate(sourceWeapon, rightHand);
        spawnedWeapon.name = sourceWeapon.name;

        spawnedWeapon.transform.localPosition = localPosition;
        spawnedWeapon.transform.localEulerAngles = localRotation;
        spawnedWeapon.transform.localScale = localScale;

        RemoveBadComponents(spawnedWeapon);

        Debug.Log("Weapon attached to " + rightHand.name);
    }

    void RemoveBadComponents(GameObject obj)
    {
        Animator[] animators = obj.GetComponentsInChildren<Animator>(true);
        foreach (Animator a in animators)
        {
            Destroy(a);
        }

        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            Destroy(rb);
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
        {
            c.enabled = false;
        }
    }
}