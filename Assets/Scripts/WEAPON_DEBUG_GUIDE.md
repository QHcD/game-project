# Weapon Debugging Guide - دليل تصحيح الأسلحة

## 🔍 How to Debug Weapon Issues - كيفية تصحيح مشاكل الأسلحة

### Step 1: Test Q Key and Check Console
1. Press **Q key** in game
2. Check Unity Console for these debug messages:

```
[PlayerController] Re-equipping weapon for level 1: Combat Knife
[PlayerController] Third person body found: [BODY_NAME]
[AttachWeaponToHand] Attaching weapon level 1 to body: [BODY_NAME]
[ResolveCombatKnifePrefab] Resolving combat knife prefab...
[ResolveCombatKnifePrefab] Loading from resource path: Weapons/TacticalKnife/TacticalKnife
[ResolveCombatKnifePrefab] Successfully loaded from Resources: [PREFAB_NAME]
[BuildWeaponModel] Building weapon level 1
[BuildWeaponModel] Knife prefab resolved: [PREFAB_NAME]
[BuildWeaponModel] Knife instantiated at position: [POSITION]
[AttachWeaponToHand] Hand bone found: [BONE_NAME]
[AttachWeaponToHand] Attach point: [ATTACH_POINT]
[AttachWeaponToHand] Weapon created successfully: WeaponModel
```

### Step 2: Look for These Specific Issues

#### 🔴 Problem: Third Person Body is NULL
**Console Message**: `[PlayerController] Third person body is NULL!`
**Cause**: Third person body not being created
**Solution**: Check if `EnsureThirdPersonBody()` is being called

#### 🔴 Problem: Prefab Not Loading
**Console Message**: `[ResolveCombatKnifePrefab] Failed to load from Resources`
**Cause**: Prefab path is wrong or prefab doesn't exist
**Solution**: Check if `TacticalKnife.prefab` exists in Resources folder

#### 🔴 Problem: Hand Bone Not Found
**Console Message**: `[AttachWeaponToHand] Hand bone found: NULL`
**Cause**: Character skeleton doesn't have expected bone names
**Solution**: Weapon will attach to body transform instead

#### 🔴 Problem: Wrong Position
**Console Message**: `[BuildWeaponModel] Knife instantiated at position: [WEIRD_VALUES]`
**Cause**: Local position/rotation values are wrong
**Solution**: Check `combatKnifeThirdPersonLocalPos` values

### Step 3: Common Fixes

#### Fix 1: Check Prefab Files
Make sure these files exist:
- `Assets/Resources/Weapons/TacticalKnife/TacticalKnife.prefab`
- `Assets/Resources/Weapons/KnuckleDuster.prefab`

#### Fix 2: Check Material Files  
Make sure these files exist:
- `Assets/Resources/Weapons/TacticalKnife/Materials/TacticalKnifeMat.mat`
- `Assets/Resources/Weapons/KnuckleDuster.mat`

#### Fix 3: Manual Weapon Test
Add this code to test weapon manually:
```csharp
// In PlayerController.Start(), add:
Invoke(nameof(TestWeaponEquip), 2f);

void TestWeaponEquip()
{
    Debug.Log("Manual weapon equip test");
    EquipWeaponForLevel(1);
}
```

### Step 4: Visual Verification

1. **Scene Hierarchy**: Look for "WeaponModel" object
2. **Inspector**: Check if WeaponModel has:
   - Transform component
   - MeshRenderer component (enabled)
   - WeaponBase component
   - WeaponVisibilityFix component
3. **Game View**: Check if weapon is visible in third person

### Step 5: Alternative Solutions

If all else fails, try this simple fix:

```csharp
// In AttachWeaponToHand, replace attachPoint:
Transform attachPoint = body.transform; // Force attach to body
equippedWeaponObject = BuildWeaponModel(level, attachPoint);

// Make weapon visible:
if (equippedWeaponObject != null)
{
    equippedWeaponObject.transform.localPosition = new Vector3(0.1f, 0.1f, 0.2f);
    equippedWeaponObject.transform.localScale = Vector3.one * 0.1f;
}
```

## 🎮 Quick Test Commands

Press these keys to test:
- **Q**: Re-equip weapon (should see debug messages)
- **V**: Toggle perspective (check third person view)
- **Left Mouse**: Attack (check if weapon animates)

## 📞 If Still Not Working

1. **Check Unity Console** for red error messages
2. **Verify Prefab Files** exist in Resources folder
3. **Test with Primitive Weapon** (comment out prefab loading)
4. **Check Character Model** has proper bone structure

**Most Common Issue**: Prefab files not properly formatted or missing GUIDs.
