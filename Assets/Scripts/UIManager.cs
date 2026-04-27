using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central UI bootstrap: Input System–safe EventSystem, end-game menu, and menu keyboard navigation hooks.
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager _runtime;
    private static int _mainMenuApplyFramesRemaining;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (_runtime != null) return;
        GameObject host = new GameObject("__UIManagerRuntime");
        DontDestroyOnLoad(host);
        _runtime = host.AddComponent<UIManager>();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!IsMainMenuLikeScene()) return;

        // Only a short stabilization window; do not keep reshuffling forever.
        if (_mainMenuApplyFramesRemaining > 0)
        {
            _mainMenuApplyFramesRemaining--;
            CompactifyMainMenuCanvas();
        }
    }

    private sealed class MainMenuCleanupRunner : MonoBehaviour
    {
        internal void Run()
        {
            StopAllCoroutines();
            StartCoroutine(Co());
        }

        private static IEnumerator Co()
        {
            // Menu is built in RuntimeMenuBuilder.Start(); wait a couple frames
            // so ProfileHeader definitely exists, then delete it + compact stack.
            yield return null;
            yield return null;
            CompactifyMainMenuCanvas();
            yield return null;
            CompactifyMainMenuCanvas(); // second pass for safety if menu rebuilt mid-frame
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapMainMenuCleanupHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying) return;
        // Gameplay / pause overlays need a consistent EventSystem in every scene.
        EnsureInputSystemEventSystem();
        if (!IsMainMenuLikeScene()) return;
        CompactifyMainMenuCanvas();
        _mainMenuApplyFramesRemaining = 60;

        // Delayed cleanup so it runs AFTER RuntimeMenuBuilder.Start() as well.
        GameObject host = GameObject.Find("__MainMenuCleanupRunner");
        if (host == null)
        {
            host = new GameObject("__MainMenuCleanupRunner");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
        }
        MainMenuCleanupRunner runner = host.GetComponent<MainMenuCleanupRunner>();
        if (runner == null) runner = host.AddComponent<MainMenuCleanupRunner>();
        runner.Run();
    }

    private static bool IsMainMenuLikeScene()
    {
        // Do not rely on a specific scene name; some projects rename it.
        GameObject canvas = GameObject.Find("NeonCanvas");
        if (canvas == null) return false;

        // Heuristic: main menu has the profile header and/or Continue/Start button.
        if (canvas.transform.Find("ProfileHeader") != null) return true;

        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;
            var t = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            string txt = t != null ? (t.text ?? string.Empty).Trim().ToUpperInvariant() : string.Empty;
            if (txt == "CONTINUE" || txt == "START") return true;
        }

        return false;
    }

    public void ShowGameFinishedMenu()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureInputSystemEventSystem();

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

    /// <summary>
    /// Ensures an <see cref="EventSystem"/> exists and uses <see cref="InputSystemUIInputModule"/> only
    /// (legacy <see cref="StandaloneInputModule"/> is removed to avoid InvalidOperationException).
    /// </summary>
    public static void EnsureInputSystemEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        GameObject go = eventSystem.gameObject;

        StandaloneInputModule legacy = go.GetComponent<StandaloneInputModule>();
        if (legacy != null)
            Object.Destroy(legacy);

        InputSystemUIInputModule uiModule = go.GetComponent<InputSystemUIInputModule>();
        if (uiModule == null)
            uiModule = go.AddComponent<InputSystemUIInputModule>();
        uiModule.enabled = true;

        go.SetActive(true);
    }

    /// <summary>Vertical list: Arrow Up/Down + Enter / Submit. Blue outline focus ring when applicable.</summary>
    public static MenuNavigationManager AttachLinearMenuNavigation(GameObject host, IList<Selectable> items)
        => MenuNavigationManager.AttachLinear(host, items);

    /// <summary>Main-menu 2D grid (Continue + pairs + column).</summary>
    public static MenuNavigationManager AttachMainMenuGrid(GameObject host, IList<Button> buttons)
        => MenuNavigationManager.AttachMainMenu(host, buttons);

    /// <summary>Forces session JSON + <see cref="PlayerPrefs"/> to disk (scene changes hook this automatically).</summary>
    public static void FlushSessionToDisk()
    {
        if (SessionManager.Instance != null)
            SessionManager.Instance.FlushPersistence();
        else
            PlayerPrefs.Save();
    }

    /// <summary>
    /// IMG_6880.mov final cleanup:
    /// - remove the top-left ProfileHeader (name/credits)
    /// - rebuild a compact button stack with zero spacing
    /// - create paired rows using HorizontalLayoutGroup (spacing/padding 0)
    /// </summary>
    public static void CompactifyMainMenuCanvas()
    {
        GameObject canvas = GameObject.Find("NeonCanvas");
        if (canvas == null) return;

        // 1) Keep name/credits, but remove the rectangle panel visuals.
        Transform profile = canvas.transform.Find("ProfileHeader");
        if (profile != null)
            StripProfilePanelVisuals(profile);

        // If overlays are currently open, do not reshuffle under them.
        if (canvas.transform.Find("LevelSelectOverlay") != null) return;
        if (canvas.transform.Find("CustomMatchOverlay") != null) return;
        if (canvas.transform.Find("StoreOverlay") != null) return;
        if (canvas.transform.Find("ChallengesOverlay") != null) return;
        if (canvas.transform.Find("NameEntryOverlay") != null) return;

        // RuntimeMenuBuilder may own the whole layout under a stretched Panel (two-column main menu).
        // Reparenting would break that hierarchy — only strip profile visuals above and stop.
        Transform menuPanel = canvas.transform.Find("Panel");
        if (menuPanel != null && menuPanel.GetComponent<VerticalLayoutGroup>() != null)
            return;

        // 2) Collect main menu buttons by their label (created by RuntimeMenuBuilder: <Label>_Btn).
        Button continueBtn    = FindButtonByLabel(canvas.transform, "CONTINUE") ?? FindButtonByLabel(canvas.transform, "START");
        Button selectLevelBtn = FindButtonByLabel(canvas.transform, "SELECT LEVEL");
        Button storeBtn       = FindButtonByLabel(canvas.transform, "PRISM STORE");
        Button challengesBtn  = FindButtonByLabel(canvas.transform, "CHALLENGES");
        Button optionsBtn     = FindButtonByLabel(canvas.transform, "OPTIONS");
        Button settingsBtn    = FindButtonByLabel(canvas.transform, "SETTINGS");
        Button creditsBtn     = FindButtonByLabel(canvas.transform, "CREDITS");
        Button quitBtn        = FindButtonByLabel(canvas.transform, "QUIT");

        // If we can't find the core buttons, bail safely (menu may be rebuilding).
        if (continueBtn == null || selectLevelBtn == null || storeBtn == null ||
            challengesBtn == null || optionsBtn == null || settingsBtn == null ||
            creditsBtn == null || quitBtn == null)
            return;

        // Destroy any previous stack if present.
        Transform existingStack = canvas.transform.Find("MainMenuButtonStack");
        if (existingStack != null) Object.Destroy(existingStack.gameObject);

        GameObject stackGo = new GameObject("MainMenuButtonStack", typeof(RectTransform));
        stackGo.transform.SetParent(canvas.transform, false);
        RectTransform stackRT = (RectTransform)stackGo.transform;
        // Give the stack more vertical room and lift it slightly so bottom buttons never clip.
        stackRT.anchorMin = new Vector2(0.18f, 0.10f);
        stackRT.anchorMax = new Vector2(0.82f, 0.82f);
        stackRT.offsetMin = stackRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = stackGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 0f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter fit = stackGo.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Row 0: Continue
        ReparentAndLayout(continueBtn, stackRT, preferredHeight: 88f, fullWidth: true);

        // Row 1: Pair 1 (Select Level | Prism Store) with | separator
        Transform pair1 = CreatePairRow(stackRT, "Pair1_Row");
        ReparentAndLayout(selectLevelBtn, pair1, preferredHeight: 88f, fullWidth: false);
        CreatePairSeparator(pair1);
        ReparentAndLayout(storeBtn, pair1, preferredHeight: 88f, fullWidth: false);

        // Row 2: Pair 2 (Challenges | Options)
        Transform pair2 = CreatePairRow(stackRT, "Pair2_Row");
        ReparentAndLayout(challengesBtn, pair2, preferredHeight: 88f, fullWidth: false);
        CreatePairSeparator(pair2);
        ReparentAndLayout(optionsBtn, pair2, preferredHeight: 88f, fullWidth: false);

        // Rows 3-5: Settings, Credits, Quit
        ReparentAndLayout(settingsBtn, stackRT, preferredHeight: 88f, fullWidth: true);
        ReparentAndLayout(creditsBtn, stackRT, preferredHeight: 88f, fullWidth: true);
        ReparentAndLayout(quitBtn, stackRT, preferredHeight: 88f, fullWidth: true);

        // Disable any stray copies of these buttons still under the canvas
        // (prevents text overlap from duplicate instances).
        DisableStrayMenuButtons(canvas.transform, stackRT);

        // 3) Navigation based on the new hierarchy order.
        MenuNavigationManager.AttachMainMenuCompact(canvas, stackRT);
    }

    private static Transform CreatePairRow(Transform parent, string name)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 0f;
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        RectTransform rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 88f);
        return row.transform;
    }

    private static void ReparentAndLayout(Button btn, Transform parent, float preferredHeight, bool fullWidth)
    {
        if (btn == null) return;
        RectTransform rt = btn.transform as RectTransform;
        if (rt == null) return;

        btn.transform.SetParent(parent, false);

        LayoutElement le = btn.GetComponent<LayoutElement>();
        if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight       = preferredHeight;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight  = 0f;

        // Width: in VerticalLayout full rows fill; in HorizontalLayout split evenly.
        le.minWidth        = 0f;
        le.preferredWidth  = 0f;
        le.flexibleWidth   = 1f;

        // Reset any previous “pill” sizing / anchors so layout groups fully control it.
        rt.localScale = Vector3.one;
        rt.pivot      = new Vector2(0.5f, 0.5f);
        rt.anchorMin  = new Vector2(0.5f, 0.5f);
        rt.anchorMax  = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private static void CreatePairSeparator(Transform pairRow)
    {
        if (pairRow == null) return;
        GameObject sep = new GameObject("PairSeparator", typeof(RectTransform));
        sep.transform.SetParent(pairRow, false);
        LayoutElement le = sep.AddComponent<LayoutElement>();
        le.minWidth = 26f;
        le.preferredWidth = 26f;
        le.flexibleWidth = 0f;
        le.minHeight = 88f;
        le.preferredHeight = 88f;
        le.flexibleHeight = 0f;

        var tmp = sep.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "|";
        tmp.fontSize = 44f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.92f, 0.94f, 1f, 0.65f);
        TMPro.TMP_FontAsset f = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Azonix SDF");
        if (f != null) tmp.font = f;

        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Button FindButtonByLabel(Transform root, string labelUpper)
    {
        if (root == null || string.IsNullOrEmpty(labelUpper)) return null;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;
            TMPro.TextMeshProUGUI t = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (t == null) continue;
            if (string.Equals(t.text?.Trim(), labelUpper, System.StringComparison.OrdinalIgnoreCase))
                return b;
        }
        return null;
    }

    private static void StripProfilePanelVisuals(Transform profileHeader)
    {
        if (profileHeader == null) return;

        // Remove any rectangle/background visuals, but never hide text.
        // In some variants, the rectangle is a separate child object; in others, it's on ProfileHeader itself.
        // We prefer disabling components (robust vs re-adding) and only deactivating whole objects that contain NO TMP text.
        Transform[] all = profileHeader.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;

            bool hasText = ContainsTMPText(t);

            Image img = t.GetComponent<Image>();
            RawImage raw = t.GetComponent<RawImage>();
            Outline outline = t.GetComponent<Outline>();

            bool hasPlateComponent = (img != null) || (raw != null) || (outline != null);
            if (!hasPlateComponent) continue;

            if (!hasText)
            {
                // Pure plate: safest is to turn it off entirely.
                t.gameObject.SetActive(false);
                continue;
            }

            // Mixed object (rare): keep it alive for text, but disable visuals.
            if (img != null)
            {
                img.enabled = false;
                img.raycastTarget = false;
            }
            if (raw != null)
            {
                raw.enabled = false;
                raw.raycastTarget = false;
            }
            if (outline != null) outline.enabled = false;
        }
    }

    private static bool ContainsTMPText(Transform root)
    {
        if (root == null) return false;
        if (root.GetComponent<TMPro.TextMeshProUGUI>() != null) return true;

        for (int i = 0; i < root.childCount; i++)
        {
            if (ContainsTMPText(root.GetChild(i))) return true;
        }
        return false;
    }

    private static void DisableStrayMenuButtons(Transform canvasRoot, Transform stackRoot)
    {
        if (canvasRoot == null || stackRoot == null) return;
        Button[] buttons = canvasRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;
            if (b.transform.IsChildOf(stackRoot)) continue;

            TMPro.TextMeshProUGUI t = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            string txt = t != null ? (t.text ?? string.Empty).Trim().ToUpperInvariant() : string.Empty;
            if (txt == "CONTINUE" || txt == "START" || txt == "SELECT LEVEL" ||
                txt == "PRISM STORE" || txt == "CHALLENGES" || txt == "OPTIONS" || txt == "SETTINGS" ||
                txt == "CREDITS" || txt == "QUIT")
            {
                b.gameObject.SetActive(false);
            }
        }
    }
}
