using UnityEngine;
using UnityEngine.EventSystems;

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
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            eventSystem.gameObject.SetActive(true);
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }
}
