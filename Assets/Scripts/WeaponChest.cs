using UnityEngine;
using TMPro;

public class WeaponChest : MonoBehaviour
{
    public GameObject weaponPrefab;       // Assign weapon model in Inspector
    public TextMeshProUGUI promptText;    // "Press E to open"
    public ParticleSystem openEffect;

    private bool isOpened = false;
    private bool playerNearby = false;

    void Update()
    {
        if (playerNearby && !isOpened && Input.GetKeyDown(KeyCode.E))
            OpenChest();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            if (promptText != null)
                promptText.gameObject.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            if (promptText != null)
                promptText.gameObject.SetActive(false);
        }
    }

    void OpenChest()
    {
        isOpened = true;

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        if (openEffect != null)
            openEffect.Play();

        if (weaponPrefab != null)
            Instantiate(weaponPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);

        // Play animation if available
        Animator anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetTrigger("Open");

        // Spawn enemies after chest is opened
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
            spawner.SpawnEnemies();
    }
}
