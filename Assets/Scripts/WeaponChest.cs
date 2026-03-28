using UnityEngine;

public class WeaponChest : MonoBehaviour
{
    public bool isHeavyGunCrate = false;
    public GameObject[] weaponPrefabs;
    public float interactRange = 2f;

    private bool opened = false;
    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (opened || player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= interactRange && Input.GetKeyDown(KeyCode.E))
            OpenChest();
    }

    void OpenChest()
    {
        opened = true;

        if (weaponPrefabs == null || weaponPrefabs.Length == 0) return;

        GameObject weaponPrefab;
        if (isHeavyGunCrate)
        {
            int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
            int index = Mathf.Clamp(level - 16, 0, weaponPrefabs.Length - 1);
            weaponPrefab = weaponPrefabs[index];
        }
        else
        {
            weaponPrefab = weaponPrefabs[Random.Range(0, weaponPrefabs.Length)];
        }

        Transform holdPoint = player.Find("WeaponHoldPoint");
        if (holdPoint == null)
        {
            GameObject hp = new GameObject("WeaponHoldPoint");
            hp.transform.SetParent(player);
            hp.transform.localPosition = new Vector3(0.5f, 1.2f, 0.8f);
            holdPoint = hp.transform;
        }

        foreach (Transform child in holdPoint)
            Destroy(child.gameObject);

        Instantiate(weaponPrefab, holdPoint.position, holdPoint.rotation, holdPoint);

        // Destroy chest after opening
        GetComponent<Animator>()?.SetTrigger("Open");
        Invoke(nameof(DestroyChest), 1f);
    }

    void DestroyChest() => Destroy(gameObject);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}