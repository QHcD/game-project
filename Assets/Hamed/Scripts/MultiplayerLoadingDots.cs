using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animates a "..." loading indicator by watching a reference button's
/// interactable state. When the button becomes non-interactable (i.e. Photon
/// is doing work), the dots cycle. When it becomes interactable again, they stop.
/// No changes to PhotonLauncher are required.
/// </summary>
public class MultiplayerLoadingDots : MonoBehaviour
{
    [Tooltip("Label that shows the animated dots. Can be separate from the status text.")]
    public TextMeshProUGUI dotsLabel;

    [Tooltip("Watch this button — dots animate when it is non-interactable.")]
    public Button watchButton;

    bool _wasInteractable = true;
    Coroutine _routine;

    void Update()
    {
        if (watchButton == null || dotsLabel == null) return;
        bool nowInteractable = watchButton.interactable;
        if (!nowInteractable && _wasInteractable)
        {
            _wasInteractable = false;
            StartDots();
        }
        else if (nowInteractable && !_wasInteractable)
        {
            _wasInteractable = true;
            StopDots();
        }
    }

    void StartDots()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Animate());
    }

    void StopDots()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        if (dotsLabel != null) dotsLabel.text = string.Empty;
    }

    IEnumerator Animate()
    {
        int dots = 0;
        while (true)
        {
            dots = (dots % 3) + 1;
            if (dotsLabel != null)
                dotsLabel.text = new string('.', dots);
            yield return new WaitForSecondsRealtime(0.40f);
        }
    }
}
