using UnityEngine;

public sealed class HoldTabMinimapCamera : MonoBehaviour
{
    [SerializeField] private Camera minimapCamera;

    private void Reset()
    {
        GameObject obj = GameObject.Find("MinimapCamera");
        minimapCamera = obj != null ? obj.GetComponent<Camera>() : null;
    }

    private void Awake()
    {
        if (minimapCamera != null)
            minimapCamera.enabled = false;
    }

    private void Update()
    {
    }
}

