using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemySpawner : MonoBehaviour
{
    public GameObject gruntPrefab;
    public GameObject soldierPrefab;
    public GameObject elitePrefab;
    public Transform[] spawnPoints;
    public float spawnDelay = 1.25f;

    void Start()
    {
        EnsureEnemyPrefabs();

        int count = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 8;
        if (GameManager.Instance != null)
            GameManager.Instance.enemiesRemaining = count;

        Invoke(nameof(SpawnEnemies), spawnDelay);
    }

    void SpawnEnemies()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            spawnPoints = CreateFallbackSpawnPoints();

        int level = GameManager.Instance != null ? GameManager.Instance.currentLevel : 1;
        int requestedCount = GameManager.Instance != null ? GameManager.Instance.GetEnemyCount() : 8;
        int count = Mathf.Min(requestedCount, spawnPoints.Length);

        if (GameManager.Instance != null)
            GameManager.Instance.enemiesRemaining = count;

        for (int i = 0; i < count; i++)
        {
            Transform spawnPoint = spawnPoints[i % spawnPoints.Length];
            Vector3 spawnPosition = GetGroundedSpawnPosition(spawnPoint.position) + Vector3.up * 0.05f;
            GameObject prefab = GetEnemyPrefab(level);
            GameObject enemy = prefab != null
                ? Instantiate(prefab, spawnPosition, spawnPoint.rotation)
                : BuildFallbackEnemy(level, spawnPosition);

            PrepareSpawnedEnemy(enemy, level, i);
        }

        HUDManager.Instance?.UpdateEnemyCount(count);
    }

    void EnsureEnemyPrefabs()
    {
        if (gruntPrefab != null && soldierPrefab != null && elitePrefab != null)
            return;

        GameObject editorPrefab = LoadEditorEnemyPrefab();
        if (editorPrefab == null)
            return;

        gruntPrefab ??= editorPrefab;
        soldierPrefab ??= editorPrefab;
        elitePrefab ??= editorPrefab;
    }

    GameObject GetEnemyPrefab(int level)
    {
        if (level >= 12 && elitePrefab != null) return elitePrefab;
        if (level >= 5 && soldierPrefab != null) return soldierPrefab;
        return gruntPrefab;
    }

    Transform[] CreateFallbackSpawnPoints()
    {
        Vector3[] defaultPoints = {
            new Vector3(0f, 0f, 28f),
            new Vector3(12f, 0f, 24f),
            new Vector3(24f, 0f, 12f),
            new Vector3(28f, 0f, 0f),
            new Vector3(24f, 0f, -12f),
            new Vector3(12f, 0f, -24f),
            new Vector3(0f, 0f, -28f),
            new Vector3(-12f, 0f, -24f),
            new Vector3(-24f, 0f, -12f),
            new Vector3(-28f, 0f, 0f),
            new Vector3(-24f, 0f, 12f),
            new Vector3(-12f, 0f, 24f)
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

    void PrepareSpawnedEnemy(GameObject enemy, int level, int index)
    {
        if (enemy == null)
            return;

        enemy.name = ResolveEnemyType(level) + "_Enemy_" + (index + 1);
        enemy.transform.position = GetGroundedSpawnPosition(enemy.transform.position) + Vector3.up * 0.05f;
        enemy.transform.localScale = Vector3.one;
        enemy.tag = "Untagged";
        enemy.layer = 0;

        foreach (NavMeshAgent agent in enemy.GetComponentsInChildren<NavMeshAgent>(true))
            agent.enabled = false;

        foreach (EnemyAI legacyAI in enemy.GetComponentsInChildren<EnemyAI>(true))
            legacyAI.enabled = false;

        foreach (EnemyWeaponAttach weaponAttach in enemy.GetComponentsInChildren<EnemyWeaponAttach>(true))
            weaponAttach.enabled = false;

        foreach (Animator animator in enemy.GetComponentsInChildren<Animator>(true))
            animator.applyRootMotion = false;

        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        CapsuleCollider collider = enemy.GetComponent<CapsuleCollider>();
        if (collider == null)
            collider = enemy.AddComponent<CapsuleCollider>();
        collider.height = 1.8f;
        collider.radius = 0.38f;
        collider.center = new Vector3(0f, 0.9f, 0f);

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller == null)
            controller = enemy.AddComponent<EnemyController>();
        controller.enemyType = ResolveEnemyType(level);

        TintEnemyVisual(enemy, controller.enemyType);
    }

    EnemyController.EnemyType ResolveEnemyType(int level)
    {
        if (level >= 12) return EnemyController.EnemyType.Elite;
        if (level >= 5) return EnemyController.EnemyType.Soldier;
        return EnemyController.EnemyType.Grunt;
    }

    Vector3 GetGroundedSpawnPosition(Vector3 candidate)
    {
        Vector3 rayOrigin = candidate + Vector3.up * 24f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return new Vector3(candidate.x, 0f, candidate.z);
    }

    GameObject BuildFallbackEnemy(int level, Vector3 spawnPosition)
    {
        GameObject enemy = new GameObject(level >= 12 ? "EliteEnemy" : level >= 5 ? "SoldierEnemy" : "GruntEnemy");
        enemy.transform.position = spawnPosition;

        GameObject visual = CloneHumanoidVisual(level);
        if (visual != null)
        {
            visual.transform.SetParent(enemy.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            return enemy;
        }

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.SetParent(enemy.transform, false);
        capsule.transform.localPosition = Vector3.zero;
        capsule.transform.localScale = new Vector3(0.85f, 1f, 0.85f);

        Renderer renderer = capsule.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.20f, 0.24f, 0.30f);
            renderer.material = mat;
        }

        Collider capsuleCollider = capsule.GetComponent<Collider>();
        if (capsuleCollider != null)
            Object.Destroy(capsuleCollider);

        return enemy;
    }

    GameObject CloneHumanoidVisual(int level)
    {
        Transform visualRoot = FindHumanoidTemplate();
        if (visualRoot == null)
            return null;

        GameObject visual = Instantiate(visualRoot.gameObject);
        visual.name = "EnemyVisual";

        foreach (Animator animator in visual.GetComponentsInChildren<Animator>(true))
            animator.applyRootMotion = false;

        TintEnemyVisual(visual, ResolveEnemyType(level));
        return visual;
    }

    Transform FindHumanoidTemplate()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Player_Base")
                    return child;
            }
        }

        Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Animator animator in animators)
        {
            if (animator.transform.name == "Player_Base")
                return animator.transform;
        }

        return null;
    }

    void TintEnemyVisual(GameObject root, EnemyController.EnemyType enemyType)
    {
        Color accent = enemyType == EnemyController.EnemyType.Elite
            ? new Color(0.86f, 0.42f, 0.40f)
            : enemyType == EnemyController.EnemyType.Soldier
                ? new Color(0.44f, 0.70f, 0.92f)
                : new Color(0.58f, 0.82f, 0.58f);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer.sharedMaterial == null)
                continue;

            Material mat = new Material(renderer.sharedMaterial);
            if (mat.HasProperty("_BaseColor"))
            {
                Color baseColor = mat.GetColor("_BaseColor");
                mat.SetColor("_BaseColor", Color.Lerp(baseColor, accent, 0.18f));
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.color = Color.Lerp(mat.color, accent, 0.18f);
            }

            renderer.material = mat;
        }
    }

#if UNITY_EDITOR
    GameObject LoadEditorEnemyPrefab()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
    }
#else
    GameObject LoadEditorEnemyPrefab()
    {
        return null;
    }
#endif
}
