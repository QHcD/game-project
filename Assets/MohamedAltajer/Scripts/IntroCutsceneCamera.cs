using System;
using UnityEngine;

[DisallowMultipleComponent]
public class IntroCutsceneCamera : MonoBehaviour
{
    [Serializable]
    public struct CutsceneWaypoint
    {
        public Vector3 position;
        public Vector3 eulerAngles;
        public Quaternion Rotation => Quaternion.Euler(eulerAngles);
    }

    [HideInInspector] public CutsceneWaypoint[] waypoints = Array.Empty<CutsceneWaypoint>();
    [HideInInspector] public float[] segmentDurations = Array.Empty<float>();
    [HideInInspector] public float totalDuration = 0f;
    [HideInInspector] public float positionSmoothTime = 0.85f;
    [HideInInspector] public float rotationSlerpSpeed = 3.5f;
    [HideInInspector] public float positionArriveEpsilon = 0.04f;

    public event Action SequenceFinished;
    public bool IsPlaying => false;
    public float TotalDuration => 0f;

    void Awake()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
            cam.enabled = false;
        enabled = false;
        gameObject.SetActive(false);
    }

    public void SnapToStart() { }

    public void Play()
    {
        SequenceFinished?.Invoke();
    }

    public void Stop() { }
}
