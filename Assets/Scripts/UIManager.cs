using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Thin UIManager wrapper for projects that already use HUDManager internally.
/// Guarantees the end-game menu unlocks the cursor, pauses time, and has
/// a live EventSystem before delegating to HUDManager.
/// </summary>
public class UIManager : MonoBehaviour
{
    public void ShowGameFinishedMenu()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();

        HUDManager hud = HUDManager.Instance;
        if (hud == null)
            hud = FindFirstObjectByType<HUDManager>();

        if (hud != null)
        {
            hud.ShowGameFinishedMenu();
            return;
        }

        Debug.LogWarning("[UIManager] No HUDManager found. Game is paused and cursor is unlocked, but no finish menu was shown.");
    }

    private static void EnsureEventSystem()
    {
        // The project uses the Input System Package (new). The legacy
        // StandaloneInputModule reads UnityEngine.Input.* internally and
        // throws InvalidOperationException under the new Input System,
        // which prevents any UI button from receiving click events. We
        // must use InputSystemUIInputModule instead, and strip any stale
        // legacy module that may already be attached to an existing
        // EventSystem (e.g. from a scene import or older code path).
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        GameObject go = eventSystem.gameObject;

        StandaloneInputModule legacy = go.GetComponent<StandaloneInputModule>();
        if (legacy != null)
            Destroy(legacy);

        InputSystemUIInputModule uiModule = go.GetComponent<InputSystemUIInputModule>();
        if (uiModule == null)
            uiModule = go.AddComponent<InputSystemUIInputModule>();
        uiModule.enabled = true;

        go.SetActive(true);
    }
}
