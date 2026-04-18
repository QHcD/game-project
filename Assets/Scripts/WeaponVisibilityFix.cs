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
            
            // Ensure materials are assigned
            if (renderer.materials == null || renderer.materials.Length == 0)
            {
                Material defaultMat = new Material(Shader.Find("Standard"));
                defaultMat.color = Color.white;
                renderer.material = defaultMat;
            }
            
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
            
            if (renderer.materials == null || renderer.materials.Length == 0)
            {
                Material defaultMat = new Material(Shader.Find("Standard"));
                defaultMat.color = Color.white;
                renderer.material = defaultMat;
            }
            
            if (!renderer.gameObject.activeInHierarchy)
                renderer.gameObject.SetActive(true);
        }
        
        // Get all mesh filters
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            // Ensure mesh is assigned
            if (filter.sharedMesh == null)
            {
                // Create a simple cube as fallback
                filter.sharedMesh = CreateCubeMesh();
            }
        }
    }
    
    private Mesh CreateCubeMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "FallbackCube";
        
        // Vertices
        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        };
        
        // Triangles
        int[] triangles = new int[36]
        {
            // Front face
            0, 2, 1, 0, 3, 2,
            // Back face
            4, 6, 5, 4, 7, 6,
            // Left face
            0, 7, 3, 0, 4, 7,
            // Right face
            1, 6, 2, 1, 5, 6,
            // Top face
            3, 6, 7, 3, 2, 6,
            // Bottom face
            0, 5, 4, 0, 1, 5
        };
        
        // UVs
        Vector2[] uvs = new Vector2[8]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
}
