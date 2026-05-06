using UnityEngine;

/// <summary>
/// Small runtime weapon equipper that keeps active melee weapons parented to a
/// hand socket and applies explicit per-weapon grip offsets.
/// </summary>
public class WeaponEquipper : MonoBehaviour
{
    public Transform weaponSocket;
    public Transform activeWeapon;

    [Header("Default Grip Offsets")]
    public Vector3 katanaLocalPosition = new Vector3(-0.035f, -0.0025f, 0f);
    public Vector3 katanaLocalEuler = new Vector3(0f, 180f, 90f);
    public Vector3 katanaLocalScale = Vector3.one;

    public Vector3 knifeLocalPosition = new Vector3(-0.015f, -0.005f, 0f);
    public Vector3 knifeLocalEuler = new Vector3(0f, 0f, 90f);
    public Vector3 knifeLocalScale = Vector3.one;

    public Vector3 batLocalPosition = new Vector3(-0.045f, -0.005f, 0f);
    public Vector3 batLocalEuler = new Vector3(0f, 180f, 90f);
    public Vector3 batLocalScale = Vector3.one;

    public Vector3 hammerLocalPosition = new Vector3(0f, 0.04f, 0f);
    public Vector3 hammerLocalEuler = new Vector3(90f, 0f, 90f);
    public Vector3 hammerLocalScale = Vector3.one;

    public void EquipWeapon(Transform weapon, Vector3 localPosition, Vector3 localEuler, Vector3 localScale, string weaponName)
    {
        if (weaponSocket == null || weapon == null)
            return;

        activeWeapon = weapon;
        activeWeapon.SetParent(weaponSocket, worldPositionStays: false);
        activeWeapon.localPosition = localPosition;
        activeWeapon.localRotation = Quaternion.Euler(localEuler);
        activeWeapon.localScale = localScale;

        Debug.Log($"[WeaponEquip] weapon={weaponName} parent={weaponSocket.name} localPos={activeWeapon.localPosition} localRot={activeWeapon.localEulerAngles} localScale={activeWeapon.localScale}");
    }
}
