using UnityEngine;

/// <summary>
/// Simple test script to verify weapon system functionality.
/// Attach to player or run in scene to test weapon visibility and damage.
/// </summary>
public class WeaponSystemTester : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTestsOnStart = false;
    public bool logResults = true;
    
    void Start()
    {
        if (runTestsOnStart)
        {
            RunTests();
        }
    }
    
    [ContextMenu("Run Weapon System Tests")]
    public void RunTests()
    {
        if (logResults)
            Debug.Log("=== Starting Weapon System Tests ===");
        
        TestPlayerController();
        TestWeaponVisibility();
        TestWeaponDamage();
        
        if (logResults)
            Debug.Log("=== Weapon System Tests Complete ===");
    }
    
    void TestPlayerController()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            if (logResults)
                Debug.LogWarning("PlayerController not found in scene.");
            return;
        }
        
        if (logResults)
            Debug.Log("✅ PlayerController found");
        
        // Test weapon equipping for current level
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        player.EquipWeaponForLevel(currentLevel);
        
        if (logResults)
            Debug.Log($"✅ Weapon equipped for level {currentLevel}: {player.equippedWeaponName}");
    }
    
    void TestWeaponVisibility()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        
        // Check if weapon object exists
        Transform weaponTransform = player.transform.Find("ThirdPersonBody/WeaponModel");
        if (weaponTransform == null)
        {
            // Try alternative paths
            weaponTransform = player.transform.Find("WeaponModel");
        }
        
        if (weaponTransform != null)
        {
            MeshRenderer renderer = weaponTransform.GetComponentInChildren<MeshRenderer>();
            if (renderer != null && renderer.enabled)
            {
                if (logResults)
                    Debug.Log("✅ Weapon model is visible");
            }
            else
            {
                if (logResults)
                    Debug.LogWarning("⚠️ Weapon model found but renderer may be disabled");
            }
            
            // Check for WeaponVisibilityFix
            WeaponVisibilityFix visibilityFix = weaponTransform.GetComponent<WeaponVisibilityFix>();
            if (visibilityFix != null)
            {
                if (logResults)
                    Debug.Log("✅ WeaponVisibilityFix component found");
            }
        }
        else
        {
            if (logResults)
                Debug.LogWarning("⚠️ Weapon model not found in expected locations");
        }
    }
    
    void TestWeaponDamage()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        
        // Find the weapon object
        GameObject weaponObject = null;
        Transform[] transforms = player.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform t in transforms)
        {
            if (t.name == "WeaponModel")
            {
                weaponObject = t.gameObject;
                break;
            }
        }
        
        if (weaponObject != null)
        {
            WeaponBase weapon = weaponObject.GetComponent<WeaponBase>();
            if (weapon != null)
            {
                if (logResults)
                {
                    Debug.Log($"✅ WeaponBase component found");
                    Debug.Log($"   - Weapon Name: {weapon.weaponName}");
                    Debug.Log($"   - Damage: {weapon.damage}");
                    Debug.Log($"   - Attack Range: {weapon.attackRange}");
                    Debug.Log($"   - Is Ranged: {weapon.isRanged}");
                }
                
                // Check for collider
                Collider collider = weaponObject.GetComponent<Collider>();
                if (collider != null)
                {
                    if (logResults)
                        Debug.Log("✅ Weapon collider found");
                }
                else
                {
                    if (logResults)
                        Debug.LogWarning("⚠️ No collider found on weapon");
                }
            }
            else
            {
                if (logResults)
                    Debug.LogWarning("WeaponBase component not found on weapon.");
            }
        }
        else
        {
            if (logResults)
                Debug.LogWarning("Weapon object not found.");
        }
    }
    
    [ContextMenu("Test Current Level Weapon")]
    public void TestCurrentLevelWeapon()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
            player.EquipWeaponForLevel(level);
            
            if (logResults)
                Debug.Log($"🔄 Re-equipped weapon for level {level}: {player.equippedWeaponName}");
        }
    }
}
