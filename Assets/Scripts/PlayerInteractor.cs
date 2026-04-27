using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Raycast-based interaction controller for the player. Each frame it casts
/// a ray forward from the active camera; if the ray hits a collider whose
/// parent chain contains an <see cref="IInteractable"/>, a "[E] PROMPT"
/// reticle is shown and pressing E fires <see cref="IInteractable.Interact"/>.
///
/// The reticle UI is built procedurally on first use — no scene wiring
/// required. The component auto-attaches to the player on Start so any
/// existing scene works without prefab edits.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("Maximum interaction distance in meters.")]
    public float maxDistance = 3.5f;

    [Tooltip("Interact key.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Also use primary mouse button when the pointer is not over UI.")]
    public bool useMouseClick = true;

    [Tooltip("When the cursor is visible/unlocked, cast from the mouse instead of screen center.")]
    public bool rayFromMouseWhenCursorUnlocked = true;

    [Tooltip("Layers considered for interaction. Default = everything except UI/IgnoreRaycast.")]
    public LayerMask interactionMask = ~0;

    private IInteractable    _currentTarget;
    private GameObject       _reticleRoot;
    private TextMeshProUGUI  _reticleLabel;
    private Image            _reticleBg;

    /// <summary>
    /// Auto-bootstrap so any scene with a player tagged "Player" gets an
    /// interactor without requiring inspector-driven wiring.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, _) =>
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            if (p.GetComponent<PlayerInteractor>() == null)
                p.AddComponent<PlayerInteractor>();
        };
    }

    private void Update()
    {
        if (EndMatchCinematic.GameplayLocked)
        {
            HideReticle();
            return;
        }

        Camera cam = ResolveInteractCamera();
        if (cam == null) { HideReticle(); return; }

        Vector2 screenPoint = GetInteractionScreenPoint();
        Ray ray = cam.ScreenPointToRay(screenPoint);

        if (TryGetInteractableHit(ray, out RaycastHit hit, out IInteractable interactable))
        {
            _currentTarget = interactable;
            string clickHint = useMouseClick ? " / CLICK" : string.Empty;
            ShowReticle("[" + interactKey + "]" + clickHint + "  " + interactable.GetPrompt());

            bool useUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool pressed = WasInteractPressedThisFrame();
            if (useMouseClick && !pressed && !useUi)
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    pressed = true;
#else
                if (!pressed)
                    pressed = UnityEngine.Input.GetMouseButtonDown(0);
#endif
            }

            if (pressed)
                interactable.Interact(gameObject);
            return;
        }

        _currentTarget = null;
        HideReticle();
    }

    private static Camera ResolveInteractCamera()
    {
        if (CameraController.Instance != null)
        {
            Camera c = CameraController.Instance.GetComponent<Camera>();
            if (c != null && c.enabled) return c;
        }

        return Camera.main;
    }

    private Vector2 GetInteractionScreenPoint()
    {
        bool freeCursor = Cursor.lockState != CursorLockMode.Locked || Cursor.visible;
        if (rayFromMouseWhenCursorUnlocked && freeCursor)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
#else
            return UnityEngine.Input.mousePosition;
#endif
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    /// <summary>
    /// Third-person rays from the camera often hit the player mesh first. Walk
    /// sorted hits until we find an <see cref="IInteractable"/> on another body.
    /// </summary>
    private bool TryGetInteractableHit(Ray ray, out RaycastHit bestHit, out IInteractable interactable)
    {
        bestHit      = default;
        interactable = null;

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactionMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Transform playerRoot = transform;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (IsPlayerOwnedCollider(col, playerRoot)) continue;

            IInteractable found = FindInteractable(col);
            if (found != null && found.CanInteract)
            {
                bestHit      = hits[i];
                interactable = found;
                return true;
            }
        }

        return false;
    }

    private static bool IsPlayerOwnedCollider(Collider col, Transform playerRoot)
    {
        if (playerRoot == null || col == null) return false;
        Transform t = col.transform;
        return t == playerRoot || t.IsChildOf(playerRoot);
    }

    /// <summary>
    /// Walks parents and checks each <see cref="MonoBehaviour"/> for <see cref="IInteractable"/>.
    /// More reliable than <c>GetComponentInParent&lt;IInteractable&gt;()</c> across Unity versions.
    /// </summary>
    private static IInteractable FindInteractable(Collider col)
    {
        if (col == null) return null;
        for (Transform tr = col.transform; tr != null; tr = tr.parent)
        {
            MonoBehaviour[] mbs = tr.GetComponents<MonoBehaviour>();
            for (int i = 0; i < mbs.Length; i++)
            {
                if (mbs[i] == null || !mbs[i].enabled) continue;
                if (mbs[i] is IInteractable intr)
                    return intr;
            }
        }

        return null;
    }

    private bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (interactKey)
            {
                case KeyCode.E: return Keyboard.current.eKey.wasPressedThisFrame;
                case KeyCode.F: return Keyboard.current.fKey.wasPressedThisFrame;
                case KeyCode.Q: return Keyboard.current.qKey.wasPressedThisFrame;
                case KeyCode.R: return Keyboard.current.rKey.wasPressedThisFrame;
                case KeyCode.G: return Keyboard.current.gKey.wasPressedThisFrame;
                case KeyCode.Space: return Keyboard.current.spaceKey.wasPressedThisFrame;
                case KeyCode.LeftShift: return Keyboard.current.leftShiftKey.wasPressedThisFrame;
                case KeyCode.RightShift: return Keyboard.current.rightShiftKey.wasPressedThisFrame;
                case KeyCode.Return: return Keyboard.current.enterKey.wasPressedThisFrame;
            }
        }
        return false;
#else
        return UnityEngine.Input.GetKeyDown(interactKey);
#endif
    }

    private void EnsureReticle()
    {
        if (_reticleRoot != null) return;
        _reticleRoot = new GameObject("InteractionReticle");
        DontDestroyOnLoad(_reticleRoot);

        Canvas canvas = _reticleRoot.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500;
        _reticleRoot.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = _reticleRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject pill = new GameObject("Pill");
        pill.transform.SetParent(_reticleRoot.transform, false);
        _reticleBg = pill.AddComponent<Image>();
        _reticleBg.color = new Color(0.05f, 0.07f, 0.12f, 0.78f);
        Outline outline = pill.AddComponent<Outline>();
        outline.effectColor    = new Color(0.30f, 0.55f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        RectTransform pillRT = pill.GetComponent<RectTransform>();
        pillRT.anchorMin = new Vector2(0.5f, 0.5f);
        pillRT.anchorMax = new Vector2(0.5f, 0.5f);
        pillRT.pivot     = new Vector2(0.5f, 0.5f);
        pillRT.sizeDelta = new Vector2(360f, 64f);
        // Sit just above the screen center.
        pillRT.anchoredPosition = new Vector2(0f, 75f);

        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(pill.transform, false);
        _reticleLabel = lbl.AddComponent<TextMeshProUGUI>();
        _reticleLabel.text       = string.Empty;
        _reticleLabel.fontSize   = 28f;
        _reticleLabel.fontStyle  = FontStyles.Bold;
        _reticleLabel.alignment  = TextAlignmentOptions.Center;
        _reticleLabel.color      = new Color(0.94f, 0.96f, 1f, 1f);
        RectTransform lblRT = lbl.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;

        _reticleRoot.SetActive(false);
    }

    private void ShowReticle(string text)
    {
        EnsureReticle();
        if (_reticleLabel != null) _reticleLabel.text = text;
        if (_reticleRoot != null && !_reticleRoot.activeSelf)
            _reticleRoot.SetActive(true);
    }

    private void HideReticle()
    {
        if (_reticleRoot != null && _reticleRoot.activeSelf)
            _reticleRoot.SetActive(false);
    }
}
