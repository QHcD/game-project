using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EquipmentManager — OOD weapon-equipping utility (static class, no MonoBehaviour needed).
///
/// Responsibilities (Single-Responsibility Principle):
///   • Locate the correct right-hand bone on any humanoid (Ronin or Crosby).
///   • Instantiate a weapon prefab at that bone with auto-normalised scale.
///   • Wire up WeaponHitbox so the blade itself is the damage source.
///
/// Usage:
///   // Player
///   GameObject knife = EquipmentManager.Equip(thirdPersonBody, knifePrefab, damage: 25);
///
///   // Enemy
///   GameObject knife = EquipmentManager.Equip(enemyObject, knifePrefab, damage: 10);
/// </summary>
public static class EquipmentManager
{
    // ── Model-specific right-hand bone names ──────────────────────────────────
    // Priority order: most-specific first.
    private static readonly string[] RightHandBoneNames =
    {
        // Ronin (player) — CoD-style j_ prefix
        "j_wrist_ri",

        // Crosby (enemy) — bip_ prefix, also has dedicated weapon sockets
        "weapon_bone_R",        // CoD weapon socket — best attach point for enemies
        "bip_hand_R",
        "bip_hand_r",

        // Generic Mixamo / standard rigs
        "mixamorig:RightHand",
        "RightHand",
        "Hand_R", "hand_R", "hand_r",
        "Wrist_R", "wrist_R",

        // Legacy / custom
        "RIGHT_HAND_COMBAT",
        "RIGHT_HAND_REST",
        "jointItemR",
        "r_Hand", "R_Hand",
    };

    // ── Target display sizes (longest axis in world-space metres) ─────────────
    // Keeps the knife looking correct at any import scale.
    private const float TargetKnifeLength  = 0.32f;   // ~32 cm
    private const float TargetMeleeLength  = 0.55f;   // generic melee fallback

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attach <paramref name="weaponPrefab"/> to the right hand of <paramref name="body"/>.
    /// Returns the live weapon instance or null if the prefab is null.
    /// </summary>
    /// <param name="body">Root of the humanoid (player or enemy).</param>
    /// <param name="weaponPrefab">Weapon prefab to instantiate.</param>
    /// <param name="damage">Damage written to WeaponHitbox and WeaponBase.</param>
    /// <param name="weaponName">Display name (shown in HUD).</param>
    /// <param name="targetLength">Desired world-space length of the weapon in metres.
    ///   Pass 0 to use the default size for the prefab's longest axis.</param>
    public static GameObject Equip(
        GameObject body,
        GameObject weaponPrefab,
        int        damage,
        string     weaponName   = "Weapon",
        float      targetLength = 0f)
    {
        if (body == null)
        {
            Debug.LogError("[EquipmentManager] Equip() called with null body.");
            return null;
        }
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[EquipmentManager] Weapon prefab is null — '{body.name}' will be unarmed.");
            return null;
        }

        // 1. Find the hand bone
        Transform hand = FindRightHand(body);
        if (hand == null)
        {
            Debug.LogWarning($"[EquipmentManager] No right-hand bone found on '{body.name}'. " +
                             "Attaching to root.");
            hand = body.transform;
        }
        else
        {
            Debug.Log($"[EquipmentManager] '{body.name}' → hand bone = '{hand.name}'");
        }

        // 2. Instantiate and parent to hand bone
        GameObject weapon = Object.Instantiate(weaponPrefab, hand, false);
        weapon.name = "WeaponModel";

        // 3. Auto-scale so the weapon looks correct at any import unit
        float length = targetLength > 0f ? targetLength : TargetKnifeLength;
        ApplyAutoScale(weapon, length);

        // 4. Zero local transform so the blade sits at the palm
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;

        // Make every child visible
        foreach (Transform t in weapon.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);

        // 5. Wire WeaponBase (stats)
        WeaponBase wb = weapon.GetComponent<WeaponBase>();
        if (wb == null) wb = weapon.AddComponent<WeaponBase>();
        wb.weaponName  = weaponName;
        wb.damage      = damage;
        wb.attackRange = 2.5f;
        wb.isRanged    = false;

        // 6. Wire WeaponHitbox (physical trigger that deals damage)
        WeaponHitbox hitbox = weapon.GetComponent<WeaponHitbox>();
        if (hitbox == null) hitbox = weapon.AddComponent<WeaponHitbox>();
        hitbox.damage = damage;

        // 7. Ensure every renderer on the weapon is active (fixes URP visibility)
        if (weapon.GetComponent<WeaponVisibilityFix>() == null)
            weapon.AddComponent<WeaponVisibilityFix>();

        Debug.Log($"[EquipmentManager] Equipped '{weaponName}' on '{body.name}' " +
                  $"(hand='{hand.name}', dmg={damage})");

        return weapon;
    }

    /// <summary>
    /// Remove any weapon currently parented under the right-hand bone of <paramref name="body"/>.
    /// </summary>
    public static void Unequip(GameObject body)
    {
        Transform hand = FindRightHand(body);
        if (hand == null) return;

        // Destroy all children of the hand that are named "WeaponModel"
        var toRemove = new List<GameObject>();
        foreach (Transform child in hand)
        {
            if (child.name == "WeaponModel")
                toRemove.Add(child.gameObject);
        }
        foreach (GameObject go in toRemove)
            Object.Destroy(go);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BONE FINDER
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find the right-hand bone of any humanoid.
    /// Priority:
    ///   1. Humanoid avatar (GetBoneTransform — works for any rig with a proper avatar).
    ///   2. Exact bone name from the project's known list.
    ///   3. Name contains "right" + "hand" (case-insensitive fallback).
    /// </summary>
    public static Transform FindRightHand(GameObject body)
    {
        if (body == null) return null;

        // ── Pass 1: avatar-based (100 % correct for Humanoid rigs) ───────────
        Animator anim = body.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            Transform bone = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null) return bone;
        }

        // ── Pass 2: known exact bone names ───────────────────────────────────
        foreach (string name in RightHandBoneNames)
        {
            Transform bone = FindBoneExact(body.transform, name);
            if (bone != null) return bone;
        }

        // ── Pass 3: fuzzy name search ─────────────────────────────────────────
        Transform fuzzy = FindBoneContaining(body.transform, "right", "hand");
        if (fuzzy != null) return fuzzy;

        return FindBoneContaining(body.transform, "hand", "_r");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SCALE NORMALISER
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Scales <paramref name="weapon"/> so its longest world-space axis equals
    /// <paramref name="targetMetres"/>. Works correctly at any import scale.
    /// </summary>
    public static void ApplyAutoScale(GameObject weapon, float targetMetres)
    {
        // Reset to identity first so we measure the prefab's native size
        weapon.transform.localScale = Vector3.one;

        // Measure bounds across all mesh renderers
        Bounds b = new Bounds(weapon.transform.position, Vector3.zero);
        bool any = false;

        foreach (MeshFilter mf in weapon.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            // Convert mesh bounds to world space
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 wc = mf.transform.TransformPoint(mb.center);
            Vector3 we = Vector3.Scale(mb.extents, mf.transform.lossyScale);
            if (!any) { b = new Bounds(wc, we * 2f); any = true; }
            else       b.Encapsulate(new Bounds(wc, we * 2f));
        }

        foreach (SkinnedMeshRenderer smr in weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            Bounds mb = smr.sharedMesh.bounds;
            Vector3 wc = smr.transform.TransformPoint(mb.center);
            Vector3 we = Vector3.Scale(mb.extents, smr.transform.lossyScale);
            if (!any) { b = new Bounds(wc, we * 2f); any = true; }
            else       b.Encapsulate(new Bounds(wc, we * 2f));
        }

        if (!any || b.size == Vector3.zero)
        {
            // Fallback: use a generic small scale for weapons with no mesh data at rest
            weapon.transform.localScale = Vector3.one * 0.02f;
            return;
        }

        float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (longest < 0.0001f)
        {
            weapon.transform.localScale = Vector3.one * 0.02f;
            return;
        }

        float uniformScale = targetMetres / longest;
        weapon.transform.localScale = Vector3.one * uniformScale;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private static Transform FindBoneExact(Transform root, string boneName)
    {
        if (string.Equals(root.name, boneName, System.StringComparison.Ordinal))
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneExact(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindBoneContaining(Transform root, string partA, string partB)
    {
        string lower = root.name.ToLowerInvariant();
        if (lower.Contains(partA) && lower.Contains(partB)) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneContaining(child, partA, partB);
            if (found != null) return found;
        }
        return null;
    }
}
