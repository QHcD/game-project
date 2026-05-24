using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SciFiSlidingDoor : MonoBehaviour
{
    public Transform leftPanel;
    public Transform rightPanel;
    public float panelSlide = 1.5f;
    public float slideDuration = 0.55f;

    public bool interactiveToggle = true;
    public string openPromptText = "[E] / CLICK OPEN DOOR";
    public string closePromptText = "[E] / CLICK CLOSE DOOR";
    public string playerTag = "Player";

    public Canvas promptCanvas;
    public TMP_Text promptText;

    private Vector3 _leftClosedLocal;
    private Vector3 _rightClosedLocal;
    private Vector3 _leftOpenLocal;
    private Vector3 _rightOpenLocal;
    private bool _isOpen;
    private bool _isTransitioning;
    private bool _playerInRange;
    private Transform _player;

    private void Awake()
    {
        EnsurePanels();
        EnsureCanvas();
        CapturePoses();
        SetCanvasActive(false);
    }

    private void OnEnable()
    {
        EnsurePanels();
        EnsureCanvas();
        SetCanvasActive(_playerInRange);
        if (_playerInRange) RefreshPromptText();
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

    private void EnsureCanvas()
    {
        if (promptCanvas == null)
        {
            Transform t = transform.Find("DoorPromptCanvas");
            if (t != null) promptCanvas = t.GetComponent<Canvas>();
        }
        if (promptText == null && promptCanvas != null)
            promptText = promptCanvas.GetComponentInChildren<TMP_Text>(true);
    }

    private void CapturePoses()
    {
        if (leftPanel != null)
        {
            _leftClosedLocal = leftPanel.localPosition;
            _leftOpenLocal = _leftClosedLocal + Vector3.forward * panelSlide;
        }
        if (rightPanel != null)
        {
            _rightClosedLocal = rightPanel.localPosition;
            _rightOpenLocal = _rightClosedLocal - Vector3.forward * panelSlide;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        _playerInRange = true;
        _player = other.transform;
        RefreshPromptText();
        SetCanvasActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        _playerInRange = false;
        _player = null;
        SetCanvasActive(false);
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            return true;
        for (Transform t = other.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(playerTag)) return true;
        }
        return false;
    }

    private void SetCanvasActive(bool active)
    {
        if (promptCanvas == null) return;
        if (promptCanvas.gameObject.activeSelf != active)
            promptCanvas.gameObject.SetActive(active);
    }

    private void RefreshPromptText()
    {
        if (promptText == null) return;
        promptText.text = _isOpen ? closePromptText : openPromptText;
    }

    private void Update()
    {
        if (!_playerInRange) return;
        if (_isTransitioning) return;
        if (!interactiveToggle) return;
        if (promptCanvas != null && _player != null)
            FacePlayer();
        if (WasInteractPressedThisFrame())
            Toggle();
    }

    private void FacePlayer()
    {
        if (promptCanvas == null || _player == null) return;
        Vector3 toPlayer = _player.position - promptCanvas.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        promptCanvas.transform.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
    }

    private static bool WasInteractPressedThisFrame()
    {
        bool e = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return e || click;
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
        if (_playerInRange) RefreshPromptText();
    }
}
