using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SciFiSlidingDoor : MonoBehaviour
{
    public Transform leftPanel;
    public Transform rightPanel;
    public float panelSlide = 1.9f;
    public float slideDuration = 0.55f;

    public bool interactiveToggle = true;
    public string playerTag = "Player";
    public float interactRange = 3.5f;

    private Vector3 _leftClosedLocal;
    private Vector3 _rightClosedLocal;
    private Vector3 _leftOpenLocal;
    private Vector3 _rightOpenLocal;
    private bool _isOpen;
    private bool _isTransitioning;
    private readonly HashSet<Transform> _occupants = new HashSet<Transform>();

    private void Awake()
    {
        EnsurePanels();
        CapturePoses();
        StripLegacyPrompt();
        DestroyLegacyOverlay();
        EnsureTrigger();
    }

    private void OnDisable()
    {
        _occupants.Clear();
    }

    private void EnsurePanels()
    {
        if (leftPanel == null)
        {
            Transform t = transform.Find("LeftPanel");
            if (t != null) leftPanel = t;
        }
        if (rightPanel == null)
        {
            Transform t = transform.Find("RightPanel");
            if (t != null) rightPanel = t;
        }
    }

    private void StripLegacyPrompt()
    {
        Transform legacy = transform.Find("DoorPromptCanvas");
        if (legacy != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(legacy.gameObject);
            else Destroy(legacy.gameObject);
#else
            Destroy(legacy.gameObject);
#endif
        }
    }

    private static void DestroyLegacyOverlay()
    {
        GameObject overlay = GameObject.Find("DoorPromptOverlay");
        if (overlay != null) Destroy(overlay);
    }

    private void EnsureTrigger()
    {
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = Mathf.Max(sc.radius, interactRange);
    }

    private void CapturePoses()
    {
        if (leftPanel != null)
        {
            _leftClosedLocal = leftPanel.localPosition;
            float dir = _leftClosedLocal.z < 0f ? -1f : (_leftClosedLocal.z > 0f ? 1f : -1f);
            _leftOpenLocal = _leftClosedLocal + Vector3.forward * (panelSlide * dir);
        }
        if (rightPanel != null)
        {
            _rightClosedLocal = rightPanel.localPosition;
            float dir = _rightClosedLocal.z > 0f ? 1f : (_rightClosedLocal.z < 0f ? -1f : 1f);
            _rightOpenLocal = _rightClosedLocal + Vector3.forward * (panelSlide * dir);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform root = ResolveTriggerRoot(other);
        if (root == null) return;
        _occupants.Add(root);
        EvaluateOpenState();
    }

    private void OnTriggerStay(Collider other)
    {
        Transform root = ResolveTriggerRoot(other);
        if (root == null) return;
        if (_occupants.Add(root))
            EvaluateOpenState();
    }

    private void OnTriggerExit(Collider other)
    {
        Transform root = ResolveTriggerRoot(other);
        if (root == null) return;
        _occupants.Remove(root);
        EvaluateOpenState();
    }

    private Transform ResolveTriggerRoot(Collider other)
    {
        if (other == null) return null;

        if (other.attachedRigidbody != null)
        {
            Transform rbT = other.attachedRigidbody.transform;
            if (rbT.CompareTag(playerTag)) return rbT;
        }
        for (Transform t = other.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(playerTag)) return t;
        }

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null && enemy.IsAlive)
            return enemy.transform;

        return null;
    }

    private void Update()
    {
        if (_occupants.Count == 0) return;
        bool changed = false;
        var iter = new List<Transform>(_occupants);
        for (int i = 0; i < iter.Count; i++)
        {
            Transform t = iter[i];
            if (t == null) { _occupants.Remove(iter[i]); changed = true; continue; }
            float d = Vector3.Distance(t.position, transform.position);
            if (d > interactRange * 1.25f) { _occupants.Remove(t); changed = true; continue; }
            EnemyController e = t.GetComponent<EnemyController>();
            if (e != null && !e.IsAlive) { _occupants.Remove(t); changed = true; }
        }
        if (changed) EvaluateOpenState();
    }

    private void EvaluateOpenState()
    {
        bool wantOpen = _occupants.Count > 0;
        if (wantOpen && !_isOpen) Open();
        else if (!wantOpen && _isOpen) Close();
    }

    public void Toggle()
    {
        if (_isTransitioning) return;
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_isOpen || _isTransitioning) return;
        EnsurePanels();
        StartCoroutine(SlideRoutine(true));
    }

    public void Close()
    {
        if (!_isOpen || _isTransitioning) return;
        EnsurePanels();
        StartCoroutine(SlideRoutine(false));
    }

    private IEnumerator SlideRoutine(bool targetOpen)
    {
        _isTransitioning = true;
        Vector3 leftFrom = leftPanel != null ? leftPanel.localPosition : Vector3.zero;
        Vector3 rightFrom = rightPanel != null ? rightPanel.localPosition : Vector3.zero;
        Vector3 leftTo = targetOpen ? _leftOpenLocal : _leftClosedLocal;
        Vector3 rightTo = targetOpen ? _rightOpenLocal : _rightClosedLocal;
        float dur = Mathf.Max(0.05f, slideDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            float e = n * n * (3f - 2f * n);
            if (leftPanel != null) leftPanel.localPosition = Vector3.Lerp(leftFrom, leftTo, e);
            if (rightPanel != null) rightPanel.localPosition = Vector3.Lerp(rightFrom, rightTo, e);
            yield return null;
        }
        if (leftPanel != null) leftPanel.localPosition = leftTo;
        if (rightPanel != null) rightPanel.localPosition = rightTo;
        _isOpen = targetOpen;
        _isTransitioning = false;
        EvaluateOpenState();
    }
}
