using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    private static LevelManager _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("LevelManager_Runtime");
            _instance = go.AddComponent<LevelManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            DestroyImmediate(this.gameObject);
            return;
        }
        _instance = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StabilizeEnvironment();
    }

    private void StabilizeEnvironment()
    {
        int envLayer = LayerMask.NameToLayer("Environment");
        int mapLayer = LayerMask.NameToLayer("Map");

        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        
        int count = 0;
        foreach (Transform t in allTransforms)
        {
            GameObject obj = t.gameObject;
            
            // Check if object belongs to Environment or Map layer, or has Environment tag
            bool isEnvironment = false;
            if (envLayer >= 0 && obj.layer == envLayer) isEnvironment = true;
            if (mapLayer >= 0 && obj.layer == mapLayer) isEnvironment = true;
            if (obj.CompareTag("Environment") || obj.CompareTag("Map")) isEnvironment = true;

            if (isEnvironment)
            {
                // Skip if it is part of a character/damageable entity
                if (obj.GetComponentInParent<IDamageable>() != null) continue;

                // Make object static
                obj.isStatic = true;
                
                // Remove Rigidbody to prevent the floor/walls from falling
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Destroy(rb);
                }
                
                count++;
            }
        }
        
        Debug.Log($"[LevelManager] Programmatic Scene Cleanup: Stabilized {count} environment objects (isStatic = true, Rigidbody removed).");
    }
}
