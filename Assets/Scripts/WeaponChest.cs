using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WeaponChest : MonoBehaviour
{
    public float interactRange = 2.5f;

    private bool opened = false;
    private Transform player;
    private TextMeshProUGUI promptText;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        CreatePrompt();
    }

    void Update()
    {
        if (opened)
        {
            if (promptText != null)
                promptText.gameObject.SetActive(false);
            return;
        }

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool canOpen = dist <= interactRange;

        if (promptText != null)
            promptText.gameObject.SetActive(canOpen);

        if (canOpen && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            OpenChest();
    }

    void OpenChest()
    {
        opened = true;

        PlayerController playerController = player != null ? player.GetComponent<PlayerController>() : null;
        if (playerController != null && GameManager.Instance != null)
            playerController.EquipWeaponForLevel(GameManager.Instance.currentLevel);

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.2f, 1f, 0.6f);

        Invoke(nameof(DestroyChest), 0.8f);
    }

    void DestroyChest()
    {
        if (promptText != null)
            Destroy(promptText.transform.parent.gameObject);

        Destroy(gameObject);
    }

    void CreatePrompt()
    {
        GameObject canvasObj = new GameObject("ChestPromptCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20f;
        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220f, 60f);

        GameObject textObj = new GameObject("Prompt");
        textObj.transform.SetParent(canvasObj.transform, false);
        promptText = textObj.AddComponent<TextMeshProUGUI>();
        promptText.text = "Press E to open chest";
        promptText.fontSize = 18f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        promptText.gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
