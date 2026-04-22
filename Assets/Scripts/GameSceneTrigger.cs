using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local safety net for LevelBuilder.
///
/// Auto-creates itself in every loaded GameScene via sceneLoaded callback
/// registered from a [RuntimeInitializeOnLoadMethod]. Because this object
/// is created INSIDE the GameScene (not DDOL), its Start() is guaranteed
/// to fire — unlike lifecycle methods on DontDestroyOnLoad objects, which
/// can silently stop executing after scene transitions in certain Unity versions.
/// </summary>
public class GameSceneTrigger : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Register once; fires for every scene load.
        SceneManager.sceneLoaded += OnAnySceneLoaded;
        // Also handle the current scene if it's already GameScene.
        CheckAndInject(SceneManager.GetActiveScene());
    }

    private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndInject(scene);
    }

    private static void CheckAndInject(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "GameScene") return;

        // Don't add duplicates
        if (Object.FindFirstObjectByType<GameSceneTrigger>() != null) return;

        GameObject trigger = new GameObject("__GameSceneTrigger");
        // Move into the GameScene so it is NOT in DDOL
        SceneManager.MoveGameObjectToScene(trigger, scene);
        trigger.AddComponent<GameSceneTrigger>();
    }

    private int _retryFrames = 0;

    private void Update()
    {
        // Wait a couple of frames to let the DDOL build attempt finish first.
        _retryFrames++;

        // Check if the build already succeeded
        if (GameObject.Find("GameplayRoot") != null)
        {
            Debug.Log("[GameSceneTrigger] Build already completed — nothing to do.");
            Destroy(gameObject);
            return;
        }

        // Give it 2 frames before retrying (avoids frame-guard collision)
        if (_retryFrames < 3) return;

        LevelBuilder builder = LevelBuilder.Instance;
        if (builder != null)
        {
            Debug.Log("[GameSceneTrigger] GameplayRoot missing after " + _retryFrames +
                " frames — invoking TriggerBuild...");
            builder.TriggerBuild();
        }
        else
        {
            Debug.LogWarning("[GameSceneTrigger] LevelBuilder.Instance is null; scene preview/build will retry from LevelBuilder hooks.");
        }

        // Self-destruct after doing our job (whether it worked or not).
        Destroy(gameObject);
    }
}
