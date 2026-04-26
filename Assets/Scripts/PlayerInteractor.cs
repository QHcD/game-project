using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        Camera cam = Camera.main;
        if (cam == null) { HideReticle(); return; }

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactionMask, QueryTriggerInteraction.Collide))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanInteract)
            {
                _currentTarget = interactable;
                ShowReticle("[" + interactKey + "]  " + interactable.GetPrompt());
                if (Input.GetKeyDown(interactKey))
                    interactable.Interact(gameObject);
                return;
            }
        }

        _currentTarget = null;
        HideReticle();
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
