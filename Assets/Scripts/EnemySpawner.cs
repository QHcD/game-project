using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject[] enemyPrefabs;     // 0=Grunt, 1=Soldier, 2=Elite
    public Transform[] spawnPoints;

    public void SpawnEnemies()
    {
        int count = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 10;
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        if (GameManager.Instance != null)
            GameManager.Instance.enemiesRemaining = count;

        for (int i = 0; i < count; i++)
        {
            Transform spawnPoint = spawnPoints[i % spawnPoints.Length];
            GameObject prefab = GetEnemyPrefab(level);
            Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        }
    }

    GameObject GetEnemyPrefab(int level)
    {
        // Elite enemies appear in later levels
        if (level >= 12 && enemyPrefabs.Length > 2) return enemyPrefabs[2];
        if (level >= 5 && enemyPrefabs.Length > 1) return enemyPrefabs[1];
        return enemyPrefabs[0];
    }
}
