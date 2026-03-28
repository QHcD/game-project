using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject gruntPrefab;
    public GameObject soldierPrefab;
    public GameObject elitePrefab;
    public Transform[] spawnPoints;
    public float spawnDelay = 2f;

    void Start()
    {
        int count = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 5;
        if (GameManager.Instance != null)
            GameManager.Instance.enemiesRemaining = count;

        Invoke(nameof(SpawnEnemies), spawnDelay);
    }

    void SpawnEnemies()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("EnemySpawner: No spawn points assigned!");
            return;
        }

        int count = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 5;
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        for (int i = 0; i < count; i++)
        {
            Transform sp = spawnPoints[i % spawnPoints.Length];
            GameObject prefab = GetEnemyPrefab(level);
            if (prefab == null) continue;

            // Activate, instantiate, deactivate template again
            prefab.SetActive(true);
            GameObject enemy = Instantiate(prefab, sp.position + Vector3.up, sp.rotation);
            enemy.SetActive(true);
            prefab.SetActive(false);
        }

        // Update HUD
        HUDManager.Instance?.UpdateEnemyCount(count);
    }

    GameObject GetEnemyPrefab(int level)
    {
        if (level >= 12 && elitePrefab != null) return elitePrefab;
        if (level >= 5 && soldierPrefab != null) return soldierPrefab;
        return gruntPrefab;
    }
}
