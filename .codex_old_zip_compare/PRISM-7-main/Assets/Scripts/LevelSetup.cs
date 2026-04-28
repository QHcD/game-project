using UnityEngine;

/// <summary>
/// Scene-level safety net. Attach this to LevelManager if the generated
/// LevelBuilder path is noisy or package tooling fails during startup.
/// </summary>
public class LevelSetup : MonoBehaviour
{
    public Vector3 fallbackSpawn = new Vector3(0f, 1f, 0f);
    public PlayerController player;
    public Camera gameplayCamera;

    private void Start()
    {
        try
        {
            EnsurePlayer();
            EnsureCamera();
            EnsureGroundVisible();
            ForceFallbackSpawnIfNeeded();
            TryInitializeOptionalAISystems();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LevelSetup] Non-critical setup failed; forcing fallback spawn. {e.GetType().Name}: {e.Message}");
            EnsureGroundVisible();
            ForceFallbackSpawnIfNeeded();
        }
    }

    private void EnsurePlayer()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player == null)
        {
            Debug.LogWarning("[LevelSetup] PlayerController not found; fallback setup will continue.");
            return;
        }

        if (!player.CompareTag("Player"))
            player.tag = "Player";

        if (player.GetComponent<CharacterController>() == null)
            player.gameObject.AddComponent<CharacterController>();

        if (player.GetComponent<PlayerHealth>() == null)
            player.gameObject.AddComponent<PlayerHealth>();
    }

    private void EnsureCamera()
    {
        if (gameplayCamera == null && player != null)
            gameplayCamera = player.ActiveCamera;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (gameplayCamera == null)
        {
            GameObject cameraObject = new GameObject("FallbackGameplayCamera");
            gameplayCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        if (player != null)
        {
            CameraController cameraController = gameplayCamera.GetComponent<CameraController>();
            if (cameraController == null)
                cameraController = gameplayCamera.gameObject.AddComponent<CameraController>();

            cameraController.target = player.transform;
            cameraController.SnapToTarget();
        }
    }

    private void ForceFallbackSpawnIfNeeded()
    {
        if (player == null)
            return;

        Vector3 position = player.transform.position;
        bool unsafePosition = float.IsNaN(position.x)
            || float.IsNaN(position.y)
            || float.IsNaN(position.z)
            || position.y < -0.5f;

        if (unsafePosition || position.y < fallbackSpawn.y)
            player.TeleportTo(fallbackSpawn);
    }

    private void EnsureGroundVisible()
    {
        bool foundGround = false;
        string[] names = { "Plane", "Ground", "ground", "PhysicsFloor", "Ground_PhysicsFloor", "ArenaFloor" };

        for (int i = 0; i < names.Length; i++)
        {
            GameObject ground = GameObject.Find(names[i]);
            if (ground == null)
                continue;

            ground.SetActive(true);
            Renderer[] renderers = ground.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                    continue;

                renderers[r].enabled = true;
                foundGround = true;
            }
        }

        if (foundGround)
            return;

        GameObject fallbackGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallbackGround.name = "VisibleGround_Fallback";
        fallbackGround.transform.position = new Vector3(0f, -0.05f, 0f);
        fallbackGround.transform.localScale = new Vector3(44f, 0.1f, 44f);

        Renderer fallbackRenderer = fallbackGround.GetComponent<Renderer>();
        if (fallbackRenderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader != null)
            {
                Material material = new Material(shader);
                Color groundColor = new Color(0.42f, 0.44f, 0.39f, 1f);
                material.color = groundColor;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", groundColor);
                fallbackRenderer.material = material;
            }
        }

        Debug.LogWarning("[LevelSetup] No ground renderer found; created VisibleGround_Fallback.");
    }

    private void TryInitializeOptionalAISystems()
    {
        try
        {
            // Optional AI/Sentis/npm-backed editor tooling is intentionally not
            // required for movement, camera, or melee combat.
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LevelSetup] Optional AI/Sentis initialization skipped. {e.GetType().Name}: {e.Message}");
        }
    }
}
