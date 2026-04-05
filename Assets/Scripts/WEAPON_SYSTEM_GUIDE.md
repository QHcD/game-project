# PRISM-7 Weapon System Guide

## Overview
The weapon system has been completely fixed to ensure weapons are visible in the player's hand and deal damage properly across all 20 levels.

## Key Components

### 1. PlayerController.cs
- **Location**: `Assets/FirstPersonMelee/Scripts/PlayerController.cs`
- **Function**: Main weapon management system
- **Key Methods**:
  - `EquipWeaponForLevel(int level)` - Equips the correct weapon for each level
  - `AttachWeaponToHand(GameObject body, int weaponLevel)` - Attaches weapon to player's hand
  - `BuildWeaponModel(int level, Transform attachPoint)` - Creates weapon models

### 2. WeaponBase.cs
- **Location**: `Assets/Scripts/WeaponBase.cs`
- **Function**: Handles damage dealing for weapons
- **Key Features**:
  - Automatic damage detection via overlap sphere
  - Configurable damage, range, and attack rate
  - Special effects support

### 3. WeaponVisibilityFix.cs
- **Location**: `Assets/Scripts/WeaponVisibilityFix.cs`
- **Function**: Ensures weapons are always visible
- **Features**:
  - Auto-enables mesh renderers
  - Creates fallback materials if needed
  - Generates primitive meshes if none exist

### 4. WeaponSystemTester.cs
- **Location**: `Assets/Scripts/WeaponSystemTester.cs`
- **Function**: Testing and verification tool
- **Usage**: Attach to any GameObject and run tests from context menu

## Weapon Assets

### Level 1 - Combat Knife
- **Prefab**: `Assets/Resources/Weapons/TacticalKnife/TacticalKnife.prefab`
- **Material**: `Assets/Resources/Weapons/TacticalKnife/Materials/TacticalKnifeMat.mat`
- **Damage**: 32
- **Range**: 2.0

### Level 2 - Knuckle Duster
- **Prefab**: `Assets/Resources/Weapons/KnuckleDuster.prefab`
- **Material**: `Assets/Resources/Weapons/KnuckleDuster.mat`
- **Damage**: 36
- **Range**: 2.3

### Levels 3-20 - Primitive Weapons
- **Generated**: Procedurally created primitive weapons
- **Design**: Unique shapes and colors for each level
- **Stats**: Scaled according to GameManager level data

## How It Works

1. **Level Start**: GameManager sets current level
2. **Weapon Equipping**: PlayerController.EquipWeaponForLevel() is called
3. **Weapon Creation**: 
   - Levels 1-2: Load prefabs from Resources
   - Levels 3-20: Generate primitive weapons
4. **Attachment**: Weapon is attached to player's right hand bone
5. **Components Added**: WeaponBase, WeaponVisibilityFix, and colliders
6. **Damage**: When player attacks, both raycast and weapon damage systems activate

## Testing

To test the weapon system:

1. Add `WeaponSystemTester.cs` to any GameObject in the scene
2. Set `runTestsOnStart = true` and `logResults = true`
3. Play the scene - tests will run automatically
4. Check console for results

Or manually test via context menu:
1. Select GameObject with WeaponSystemTester
2. Right-click → "Weapon System Tester" → "Run Weapon System Tests"

## Troubleshooting

### Weapon Not Visible
- Check if WeaponVisibilityFix component is added
- Verify mesh renderer is enabled
- Ensure materials are properly assigned

### Weapon Not Dealing Damage
- Verify WeaponBase component exists
- Check if collider is present and enabled
- Ensure attack range is sufficient

### Wrong Weapon for Level
- Verify GameManager.currentLevel is correct
- Check EquipWeaponForLevel() is being called
- Validate LevelWeaponNames array in GameManager

## File Structure
```
Assets/
├── Resources/Weapons/
│   ├── TacticalKnife/
│   │   ├── TacticalKnife.prefab
│   │   ├── TacticalKnife.fbx
│   │   └── Materials/
│   │       └── TacticalKnifeMat.mat
│   ├── KnuckleDuster.prefab
│   ├── KnuckleDuster.mat
│   └── BlinkDaggerPack/
└── Scripts/
    ├── WeaponBase.cs
    ├── WeaponVisibilityFix.cs
    ├── WeaponSystemTester.cs
    └── FirstPersonMelee/Scripts/PlayerController.cs
```

## Notes
- All weapons are automatically scaled and positioned for optimal visibility
- The system supports both first-person and third-person perspectives
- Weapon stats are pulled from GameManager level arrays
- Primitive weapons are generated with unique colors and shapes for each level
