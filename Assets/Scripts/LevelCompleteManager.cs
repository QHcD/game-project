using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelCompleteManager : MonoBehaviour
{
    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        Image bg = new GameObject("BG").AddComponent<Image>();
        bg.transform.SetParent(canvasObj.transform, false);
        bg.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);
        Stretch(bg.GetComponent<RectTransform>());

        MakeText(canvasObj.transform, "LEVEL COMPLETE!", 80, Color.white,
            new Vector2(0f, 0.75f), new Vector2(1f, 1f));

        int stars = GameManager.Instance != null
            ? GameManager.Instance.CalculateStars(120f)
            : 3;

        string starStr = stars == 3 ? "★★★" : stars == 2 ? "★★☆" : "★☆☆";
        MakeText(canvasObj.transform, starStr, 90, Color.yellow,
            new Vector2(0.2f, 0.55f), new Vector2(0.8f, 0.75f));

        int score = GameManager.Instance != null ? GameManager.Instance.score : 0;
        MakeText(canvasObj.transform, "Score: " + score, 45, Color.white,
            new Vector2(0.2f, 0.43f), new Vector2(0.8f, 0.55f));

        MakeButton(canvasObj.transform, "NEXT LEVEL",
            new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.38f),
            () => GameManager.Instance?.LoadNextLevel());

        MakeButton(canvasObj.transform, "MAIN MENU",
            new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.21f),
            () => SceneManager.LoadScene("MainMenu"));
    }

    void MakeText(Transform parent, string text, float size, Color color,
        Vector2 aMin, Vector2 aMax)
    {
        var obj = new GameObject("T"); obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax,
        UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject(label); obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>(); img.color = new Color(0.5f, 0.25f, 1f, 0.5f);
        var btn = obj.AddComponent<Button>(); btn.onClick.AddListener(action);
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = r.offsetMax = Vector2.zero;
        var t = new GameObject("L"); t.transform.SetParent(obj.transform, false);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 36; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        Stretch(t.GetComponent<RectTransform>());
    }

    void Stretch(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}