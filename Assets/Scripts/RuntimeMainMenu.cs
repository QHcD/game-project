using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class RuntimeMainMenu : MonoBehaviour
{
    private void Start()
    {
        BuildMenu();
    }

    private void BuildMenu()
    {
        // Canvas
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.12f, 1f);
        StretchFull(bgObj.GetComponent<RectTransform>());

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "PRISM-7";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 120;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.7f);
        titleRect.anchorMax = new Vector2(1f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // Subtitle
        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI subText = subObj.AddComponent<TextMeshProUGUI>();
        subText.text = "WEAPON TRIALS";
        subText.alignment = TextAlignmentOptions.Center;
        subText.fontSize = 45;
        subText.color = new Color(0.6f, 0.4f, 1f, 1f);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0.62f);
        subRect.anchorMax = new Vector2(1f, 0.72f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = Vector2.zero;

        // Buttons container
        GameObject containerObj = new GameObject("ButtonContainer");
        containerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.3f, 0.1f);
        containerRect.anchorMax = new Vector2(0.7f, 0.6f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(0, 0, 10, 10);

        // Create buttons
        CreateButton(containerObj.transform, "START", () => SceneManager.LoadScene("Level_01"));
        CreateButton(containerObj.transform, "INSTRUCTIONS", () => SceneManager.LoadScene("Instructions"));
        CreateButton(containerObj.transform, "CREDITS", () => SceneManager.LoadScene("Credits"));
        CreateButton(containerObj.transform, "SETTINGS", () => SceneManager.LoadScene("Settings"));
        CreateButton(containerObj.transform, "QUIT", () => Application.Quit());
    }

    private void CreateButton(Transform parent, string label,
                               UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(label + "Btn");
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.07f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0.07f);
        cb.highlightedColor = new Color(0.55f, 0.3f, 1f, 0.4f);
        cb.pressedColor = new Color(0.4f, 0.15f, 0.9f, 0.6f);
        btn.colors = cb;
        btn.onClick.AddListener(action);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.5f, 0.25f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 38;
        tmp.color = Color.white;
        StretchFull(textObj.GetComponent<RectTransform>());
    }

    private void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.localScale = Vector3.one;
    }
}