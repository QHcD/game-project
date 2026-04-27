using UnityEngine;

/// <summary>
/// Hides the player's head / helmet from the first-person camera by assigning
/// those meshes to a dedicated layer and removing that layer from the camera's
/// culling mask.
/// </summary>
public sealed class FirstPersonLayerCulling : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private string hiddenLayerName = "PlayerBody";
    [SerializeField] private Transform[] headAndHelmetRoots;
    [SerializeField] private bool assignLayerOnStart = true;
    [SerializeField] private bool excludeLayerFromCamera = true;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        int layer = LayerMask.NameToLayer(hiddenLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[FirstPersonLayerCulling] Layer '{hiddenLayerName}' does not exist.");
            return;
        }

        if (assignLayerOnStart)
        {
            for (int i = 0; i < headAndHelmetRoots.Length; i++)
                SetLayerRecursive(headAndHelmetRoots[i], layer);
        }

        if (excludeLayerFromCamera && targetCamera != null)
            targetCamera.cullingMask &= ~(1 << layer);
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null || layer < 0)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
    }
}
