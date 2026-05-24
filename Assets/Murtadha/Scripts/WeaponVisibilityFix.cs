using UnityEngine;

/// <summary>
/// Ensures weapon models are properly visible and have correct materials.
/// Attach this to weapon prefabs to fix visibility issues.
/// </summary>
public class WeaponVisibilityFix : MonoBehaviour
{
    void Start()
    {
        EnsureVisibility();
    }

    void EnsureVisibility()
    {
        // Get all mesh renderers in this weapon
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        
        foreach (MeshRenderer renderer in renderers)
        {
            // Ensure renderer is enabled
            if (!renderer.enabled)
                renderer.enabled = true;
            
            // Ensure the gameObject is active
            if (!renderer.gameObject.activeInHierarchy)
                renderer.gameObject.SetActive(true);
        }

        // Handle SkinnedMeshRenderers used by imported FBXs
        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            if (!renderer.enabled)
                renderer.enabled = true;
            
            if (!renderer.gameObject.activeInHierarchy)
                renderer.gameObject.SetActive(true);
        }
        
        // Get all mesh filters
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            // Ensure mesh is assigned
            if (filter.sharedMesh == null)
                Debug.LogWarning($"[WeaponFix] Weapon mesh missing on '{filter.name}'. Leaving prefab data unchanged.", filter);
        }
    }
}
