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
            spawnPoints = CreateFallbackSpawnPoints();

        int count = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 5;
        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;

        for (int i = 0; i < count; i++)
        {
            Transform sp = spawnPoints[i % spawnPoints.Length];
            GameObject prefab = GetEnemyPrefab(level);
            GameObject enemy;

            if (prefab != null)
            {
                prefab.SetActive(true);
                enemy = Instantiate(prefab, sp.position + Vector3.up, sp.rotation);
                enemy.SetActive(true);
                prefab.SetActive(false);
            }
            else
            {
                enemy = BuildFallbackEnemy(level, sp.position + Vector3.up);
            }

            enemy.tag = "Enemy";
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

    private Transform[] CreateFallbackSpawnPoints()
    {
        Vector3[] defaultPoints = {
            new Vector3(18f, 0f, 18f),
            new Vector3(-18f, 0f, 18f),
            new Vector3(18f, 0f, -18f),
            new Vector3(-18f, 0f, -18f),
            new Vector3(0f, 0f, 22f),
            new Vector3(0f, 0f, -22f)
        };

        Transform[] generated = new Transform[defaultPoints.Length];
        for (int i = 0; i < defaultPoints.Length; i++)
        {
            GameObject point = new GameObject("RuntimeSpawnPoint_" + (i + 1));
            point.transform.position = defaultPoints[i];
            generated[i] = point.transform;
        }

        return generated;
    }

    private GameObject BuildFallbackEnemy(int level, Vector3 spawnPosition)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = level >= 12 ? "EliteEnemy" : level >= 5 ? "SoldierEnemy" : "GruntEnemy";
        enemy.transform.position = spawnPosition;

        Renderer renderer = enemy.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = level >= 12 ? new Color(0.8f, 0.2f, 0.2f) :
                level >= 5 ? new Color(0.2f, 0.4f, 0.9f) :
                new Color(0.2f, 0.8f, 0.3f);
            renderer.material = mat;
        }

        Rigidbody rb = enemy.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        EnemyController controller = enemy.AddComponent<EnemyController>();
        controller.enemyType = level >= 12 ? EnemyController.EnemyType.Elite :
            level >= 5 ? EnemyController.EnemyType.Soldier :
            EnemyController.EnemyType.Grunt;

        return enemy;
    }
}
