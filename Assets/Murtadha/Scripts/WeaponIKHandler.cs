using UnityEngine;

/// <summary>
/// Two-handed weapon IK handler. IK is disabled for all melee weapons
/// (one-handed grip). Kept for future expansion.
/// Attach to the ThirdPersonBody GameObject.
/// </summary>
public class WeaponIKHandler : MonoBehaviour
{
    [Header("IK Settings")]
    [Tooltip("How strongly the left hand follows the IK target (0=none, 1=full).")]
    [Range(0f, 1f)]
    public float ikWeight = 1f;

    [Tooltip("Speed at which IK blends in/out.")]
    public float ikBlendSpeed = 8f;

    [Header("Weapon Grip Points")]
    [Tooltip("If assigned, left hand will reach for this transform on the weapon.")]
    public Transform leftHandTarget;

    [Tooltip("If assigned, right hand position is overridden to this grip point.")]
    public Transform rightHandTarget;

    // ── Internal state ──
    private Animator _animator;
    private float _currentIKWeight;
    private bool _ikEnabled;

    // Bone references for manual IK (PlayableGraph fallback)
    private Transform _leftHand;
    private Transform _leftLowerArm;
    private Transform _leftUpperArm;
    private Transform _rightHand;

    // Per-weapon type grip offsets (used when no explicit grip transform exists)
    private Vector3 _leftHandLocalOffset;
    private Quaternion _leftHandLocalRotation;
    private bool _isInitialized;

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call when a weapon is equipped. Creates IK grip targets on the weapon model.
    /// </summary>
    public void SetupForWeapon(GameObject weaponModel, GameManager.WeaponType weaponType)
    {
        if (weaponModel == null)
        {
            _ikEnabled = false;
            return;
        }

        // Enable two-handed IK for two-handed and ultimate melee weapons
        bool isTwoHanded = weaponType == GameManager.WeaponType.TwoHandedMelee
                        || weaponType == GameManager.WeaponType.UltimateMelee;

        if (!isTwoHanded)
        {
            _ikEnabled = false;
            return;
        }

        _ikEnabled = true;
        EnsureBoneReferences();

        // Find or create left hand grip point on weapon
        leftHandTarget = weaponModel.transform.Find("LeftHandGrip");
        if (leftHandTarget == null)
        {
            // Auto-create grip point based on weapon type
            GameObject gripObj = new GameObject("LeftHandGrip");
            gripObj.transform.SetParent(weaponModel.transform, false);

            // Position the grip at the front of the weapon (foregrip area)
            ConfigureGripForWeaponType(gripObj.transform, weaponModel, weaponType);

            leftHandTarget = gripObj.transform;
        }

        // Find or create right hand grip point
        rightHandTarget = weaponModel.transform.Find("RightHandGrip");
        if (rightHandTarget == null)
        {
            GameObject gripObj = new GameObject("RightHandGrip");
            gripObj.transform.SetParent(weaponModel.transform, false);
            // Right hand stays at the weapon's parent (hand bone position)
            gripObj.transform.localPosition = Vector3.zero;
            gripObj.transform.localRotation = Quaternion.identity;
            rightHandTarget = gripObj.transform;
        }
    }

    /// <summary>
    /// Disables two-handed IK (e.g., when switching to melee weapon).
    /// </summary>
    public void DisableIK()
    {
        _ikEnabled = false;
        leftHandTarget = null;
        rightHandTarget = null;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  GRIP CONFIGURATION PER WEAPON TYPE
    // ════════════════════════════════════════════════════════════════════════

    private void ConfigureGripForWeaponType(Transform grip, GameObject weapon, GameManager.WeaponType wType)
    {
        // Melee-only project — IK is disabled for all weapon types.
        // This method is kept as a no-op for compatibility.
        Bounds weaponBounds = CalculateWeaponBounds(weapon);
        float weaponLength = Mathf.Max(weaponBounds.size.x, Mathf.Max(weaponBounds.size.y, weaponBounds.size.z));
        grip.localPosition = new Vector3(0f, -weaponLength * 0.25f, 0f);
        grip.localRotation = Quaternion.identity;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LATE UPDATE — Apply IK after animation
    // ════════════════════════════════════════════════════════════════════════

    private void LateUpdate()
    {
        // Smooth blend IK weight
        float targetWeight = (_ikEnabled && leftHandTarget != null) ? ikWeight : 0f;
        _currentIKWeight = Mathf.Lerp(_currentIKWeight, targetWeight, ikBlendSpeed * Time.deltaTime);

        if (_currentIKWeight < 0.01f) return;

        EnsureBoneReferences();
        if (_leftHand == null) return;

        // Apply left hand IK: move hand to foregrip position
        if (leftHandTarget != null)
        {
            // Smoothly move left hand to grip target
            _leftHand.position = Vector3.Lerp(
                _leftHand.position,
                leftHandTarget.position,
                _currentIKWeight
            );

            _leftHand.rotation = Quaternion.Slerp(
                _leftHand.rotation,
                leftHandTarget.rotation,
                _currentIKWeight
            );

            // Solve the arm chain (simple two-bone IK)
            if (_leftLowerArm != null && _leftUpperArm != null)
                SolveTwoBoneIK(_leftUpperArm, _leftLowerArm, _leftHand, leftHandTarget.position);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TWO-BONE IK SOLVER
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simple analytical two-bone IK: positions the shoulder→elbow→hand chain
    /// so the hand reaches the target position. Uses law of cosines.
    /// </summary>
    private void SolveTwoBoneIK(Transform upper, Transform lower, Transform hand, Vector3 target)
    {
        Vector3 upperPos = upper.position;
        float upperLen = Vector3.Distance(upperPos, lower.position);
        float lowerLen = Vector3.Distance(lower.position, hand.position);
        float targetDist = Vector3.Distance(upperPos, target);

        // Clamp target distance to arm reach
        float maxReach = (upperLen + lowerLen) * 0.999f;
        float minReach = Mathf.Abs(upperLen - lowerLen) * 1.001f;
        targetDist = Mathf.Clamp(targetDist, minReach, maxReach);

        // Direction from shoulder to target
        Vector3 toTarget = (target - upperPos).normalized;

        // Elbow angle via law of cosines
        float cosAngle = (upperLen * upperLen + targetDist * targetDist - lowerLen * lowerLen)
                       / (2f * upperLen * targetDist);
        cosAngle = Mathf.Clamp(cosAngle, -1f, 1f);
        float angle = Mathf.Acos(cosAngle) * Mathf.Rad2Deg;

        // Determine bend direction (elbow hint — prefer bending downward/forward)
        Vector3 bendHint = Vector3.Cross(toTarget, Vector3.up).normalized;
        if (bendHint.sqrMagnitude < 0.001f)
            bendHint = Vector3.forward;

        // Rotate upper arm to point toward target with elbow bend
        Quaternion lookRot = Quaternion.LookRotation(toTarget, bendHint);
        upper.rotation = lookRot * Quaternion.Euler(-angle, 0f, 0f);

        // Point lower arm toward hand target
        Vector3 toLower = (target - lower.position).normalized;
        if (toLower.sqrMagnitude > 0.001f)
            lower.rotation = Quaternion.LookRotation(toLower, bendHint);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UTILITIES
    // ════════════════════════════════════════════════════════════════════════

    private void EnsureBoneReferences()
    {
        if (_isInitialized) return;

        _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null || !_animator.isHuman) return;

        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        _isInitialized = (_leftHand != null);
    }

    private Bounds CalculateWeaponBounds(GameObject weapon)
    {
        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(weapon.transform.position, Vector3.one * 0.5f);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
