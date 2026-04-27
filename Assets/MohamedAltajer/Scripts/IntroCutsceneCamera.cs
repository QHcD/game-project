using System;
using UnityEngine;

/// <summary>
/// Drives a dedicated <see cref="Camera"/> through an ordered list of waypoints.
/// Position blends with <see cref="Vector3.SmoothDamp"/>; rotation blends with <see cref="Quaternion.Slerp"/>.
/// Pair this with <see cref="IntroSequenceFlowManager"/> for game-start flow.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class IntroCutsceneCamera : MonoBehaviour
{
    [Serializable]
    public struct CutsceneWaypoint
    {
        [Tooltip("World position for this keyframe.")]
        public Vector3 position;

        [Tooltip("World-space Euler angles (degrees) at this keyframe.")]
        public Vector3 eulerAngles;

        public Quaternion Rotation => Quaternion.Euler(eulerAngles);
    }

    [Header("Waypoints")]
    [Tooltip("At least two points. The camera starts at waypoints[0] when Play() is called.")]
    public CutsceneWaypoint[] waypoints =
    {
        new CutsceneWaypoint { position = new Vector3(0f, 5f, -12f), eulerAngles = new Vector3(12f, 0f, 0f) },
        new CutsceneWaypoint { position = new Vector3(8f, 6f, -6f), eulerAngles = new Vector3(10f, -35f, 0f) },
        new CutsceneWaypoint { position = new Vector3(0f, 3f, -4f), eulerAngles = new Vector3(5f, 0f, 0f) },
    };

    [Header("Timing")]
    [Tooltip("Seconds spent travelling between each consecutive pair of waypoints. Length should be waypoints.Length - 1, or leave empty to split totalDuration evenly.")]
    public float[] segmentDurations = { 3f, 3f };

    [Tooltip("If segmentDurations is null or wrong length, total time is split evenly across segments.")]
    public float totalDuration = 6f;

    [Header("Smoothing")]
    [Tooltip("SmoothDamp time for position (smaller = snappier). Per Unity docs, roughly time to reach ~63% of target.")]
    [Min(0.01f)]
    public float positionSmoothTime = 0.85f;

    [Tooltip("How fast rotation catches up each second (1 = very slow, 6+ = tight).")]
    [Min(0.1f)]
    public float rotationSlerpSpeed = 3.5f;

    [Tooltip("Stop a segment early when position is within this distance of the target.")]
    public float positionArriveEpsilon = 0.04f;

    /// <summary>Fires once when the last segment completes.</summary>
    public event Action SequenceFinished;

    /// <summary>True while a sequence is running.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Total expected play time (sum of effective segment lengths).</summary>
    public float TotalDuration => ComputeTotalDuration();

    Camera _cam;
    int _segmentIndex;
    float _segmentElapsed;
    float _segmentLength;
    Vector3 _smoothVelocity;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    /// <summary>Reset transform to first waypoint and stop playback.</summary>
    public void SnapToStart()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;
        transform.position = waypoints[0].position;
        transform.rotation = waypoints[0].Rotation;
        _smoothVelocity = Vector3.zero;
    }

    /// <summary>Begin (or restart) the cutscene from waypoint 0.</summary>
    public void Play()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning("[IntroCutsceneCamera] Need at least 2 waypoints.");
            IsPlaying = false;
            SequenceFinished?.Invoke();
            return;
        }

        SnapToStart();
        _segmentIndex = 0;
        _segmentElapsed = 0f;
        _smoothVelocity = Vector3.zero;
        BeginSegment(0);
        IsPlaying = true;
    }

    /// <summary>Stop motion immediately (does not fire <see cref="SequenceFinished"/>).</summary>
    public void Stop()
    {
        IsPlaying = false;
        _smoothVelocity = Vector3.zero;
    }

    void Update()
    {
        if (!IsPlaying) return;

        int next = _segmentIndex + 1;
        if (next >= waypoints.Length)
        {
            Finish();
            return;
        }

        CutsceneWaypoint to = waypoints[next];

        _segmentElapsed += Time.deltaTime;

        // Position: SmoothDamp toward this segment's target (velocity resets at each segment boundary).
        transform.position = Vector3.SmoothDamp(transform.position, to.position, ref _smoothVelocity, positionSmoothTime);

        // Rotation: Slerp toward target orientation (per-frame, time-independent speed factor).
        transform.rotation = Quaternion.Slerp(transform.rotation, to.Rotation,
            Mathf.Clamp01(rotationSlerpSpeed * Time.deltaTime));

        bool timeUp = _segmentElapsed >= _segmentLength;
        bool posArrived = Vector3.Distance(transform.position, to.position) <= positionArriveEpsilon;

        if (timeUp || posArrived)
        {
            transform.position = to.position;
            transform.rotation = to.Rotation;
            _segmentIndex = next;
            _smoothVelocity = Vector3.zero;

            if (_segmentIndex >= waypoints.Length - 1)
            {
                Finish();
                return;
            }

            BeginSegment(_segmentIndex);
        }
    }

    void BeginSegment(int fromIndex)
    {
        _segmentElapsed = 0f;
        _segmentLength = GetSegmentDuration(fromIndex);
    }

    float GetSegmentDuration(int fromIndex)
    {
        float[] eff = EffectiveSegmentDurations();
        if (fromIndex >= 0 && fromIndex < eff.Length)
            return Mathf.Max(0.05f, eff[fromIndex]);
        return 1f;
    }

    float[] EffectiveSegmentDurations()
    {
        int segments = Mathf.Max(0, waypoints.Length - 1);
        if (segments <= 0) return Array.Empty<float>();

        if (segmentDurations != null && segmentDurations.Length == segments)
        {
            float[] copy = new float[segments];
            for (int i = 0; i < segments; i++)
                copy[i] = Mathf.Max(0.05f, segmentDurations[i]);
            return copy;
        }

        float each = Mathf.Max(0.05f, totalDuration / segments);
        float[] even = new float[segments];
        for (int i = 0; i < segments; i++)
            even[i] = each;
        return even;
    }

    float ComputeTotalDuration()
    {
        if (waypoints == null || waypoints.Length < 2) return 0f;
        float sum = 0f;
        float[] eff = EffectiveSegmentDurations();
        for (int i = 0; i < eff.Length; i++)
            sum += eff[i];
        return sum;
    }

    void Finish()
    {
        IsPlaying = false;
        _smoothVelocity = Vector3.zero;
        SequenceFinished?.Invoke();
    }
}
