using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven weapon attachment/grip system for both player and enemy.
/// Supports all 16 weapon levels via serialized entries.
/// </summary>
public class WeaponGripSystem : MonoBehaviour
{
    [Serializable]
    public class GripProfile
    {
        [Range(1, 16)] public int level = 1;
        [Tooltip("Optional matcher against prefab name (e.g. sickle, chainsaw).")]
        public string weaponNameKey;
        public Vector3 playerLocalPosition;
        public Vector3 playerLocalEuler;
        public Vector3 enemyLocalPosition;
        public Vector3 enemyLocalEuler;
        public float playerScale = 1f;
        public float enemyScale = 1f;
    }

    [Header("Profiles (16 levels)")]
    public List<GripProfile> profiles = new List<GripProfile>(16);

    [Header("Right Hand Bone Name Fallbacks")]
    public string[] handBoneNames =
    {
        "weapon_bone_R", "bip_hand_R", "j_wrist_ri",
        "mixamorig:RightHand", "RightHand", "Hand_R"
    };

    [Header("Defaults")]
    public Vector3 defaultPlayerLocalPosition = Vector3.zero;
    public Vector3 defaultPlayerLocalEuler = new Vector3(0f, 0f, 90f);
    public Vector3 defaultEnemyLocalPosition = Vector3.zero;
    public Vector3 defaultEnemyLocalEuler = new Vector3(0f, 0f, 90f);

    private Dictionary<int, List<GripProfile>> _byLevel;

    private void Awake()
    {
        EnsureDefaultProfiles();
        BuildLookup();
    }

    private void OnValidate()
    {
        EnsureDefaultProfiles();
        BuildLookup();
    }

    public void BuildLookup()
    {
        _byLevel = new Dictionary<int, List<GripProfile>>(16);
        for (int i = 0; i < profiles.Count; i++)
        {
            GripProfile p = profiles[i];
            if (!_byLevel.TryGetValue(p.level, out List<GripProfile> list))
            {
                list = new List<GripProfile>(2);
                _byLevel[p.level] = list;
            }
            list.Add(p);
        }
    }

    private void EnsureDefaultProfiles()
    {
        if (profiles == null)
            profiles = new List<GripProfile>(16);

        for (int level = 1; level <= 16; level++)
        {
            bool exists = false;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].level == level)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                profiles.Add(new GripProfile
                {
                    level = level,
                    weaponNameKey = string.Empty,
                    playerLocalPosition = defaultPlayerLocalPosition,
                    playerLocalEuler = defaultPlayerLocalEuler,
                    enemyLocalPosition = defaultEnemyLocalPosition,
                    enemyLocalEuler = defaultEnemyLocalEuler,
                    playerScale = 1f,
                    enemyScale = 1f
                });
            }
        }
    }

    public GameObject AttachWeapon(
        GameObject characterRoot,
        GameObject weaponPrefab,
        bool isPlayer,
        int level,
        int damage = 25)
    {
        if (characterRoot == null || weaponPrefab == null)
            return null;

        Transform hand = FindRightHand(characterRoot);
        if (hand == null)
            hand = characterRoot.transform;

        GameObject weapon = Instantiate(weaponPrefab, hand, false);
        weapon.name = "WeaponModel";
        weapon.SetActive(true);

        GripProfile profile = ResolveProfile(level, weaponPrefab.name);
        ApplyGrip(weapon.transform, profile, isPlayer);

        WeaponBase wb = weapon.GetComponent<WeaponBase>();
        if (wb == null) wb = weapon.AddComponent<WeaponBase>();
        wb.damage = damage;
        wb.isRanged = false;

        WeaponHitbox hitbox = weapon.GetComponent<WeaponHitbox>();
        if (hitbox == null) hitbox = weapon.AddComponent<WeaponHitbox>();
        hitbox.damage = damage;
        hitbox.DisableHitbox();

        if (weapon.GetComponent<WeaponVisibilityFix>() == null)
            weapon.AddComponent<WeaponVisibilityFix>();

        return weapon;
    }

    public Transform FindRightHand(GameObject root)
    {
        Animator anim = root.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            Transform humanHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (humanHand != null)
                return humanHand;
        }

        for (int i = 0; i < handBoneNames.Length; i++)
        {
            Transform t = FindBoneExact(root.transform, handBoneNames[i]);
            if (t != null)
                return t;
        }

        return null;
    }

    private GripProfile ResolveProfile(int level, string weaponName)
    {
        if (_byLevel == null || _byLevel.Count == 0)
            BuildLookup();

        if (_byLevel.TryGetValue(level, out List<GripProfile> list))
        {
            string lower = weaponName != null ? weaponName.ToLowerInvariant() : string.Empty;
            for (int i = 0; i < list.Count; i++)
            {
                GripProfile p = list[i];
                if (!string.IsNullOrWhiteSpace(p.weaponNameKey) && lower.Contains(p.weaponNameKey.ToLowerInvariant()))
                    return p;
            }
            if (list.Count > 0) return list[0];
        }
        return null;
    }

    private void ApplyGrip(Transform weapon, GripProfile profile, bool isPlayer)
    {
        // Enemies must grab weapons exactly like the player — same corner,
        // direction, and grip pose. Always apply the player's grip values
        // regardless of whether this is the player or an enemy.
        if (profile == null)
        {
            weapon.localPosition = defaultPlayerLocalPosition;
            weapon.localRotation = Quaternion.Euler(defaultPlayerLocalEuler);
            return;
        }

        weapon.localPosition = profile.playerLocalPosition;
        weapon.localRotation = Quaternion.Euler(profile.playerLocalEuler);
        weapon.localScale = Vector3.one * Mathf.Max(0.01f, profile.playerScale);
    }

    private static Transform FindBoneExact(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneExact(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
