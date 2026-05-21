using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runtime arena envelope: fog/sky masking, outer blockers, and fall kill volume.
/// Installed by <see cref="LevelBuilder"/> — does not alter map layout.
/// </summary>
[DisallowMultipleComponent]
public class WorldArenaStabilizer : MonoBehaviour
{
    private const string RootName = "WorldArenaStabilizer";
    private const string KillVolumeName = "WorldKillVolume";
    private const string EdgeBlockerRootName = "ArenaEdgeBlockers";

    [Header("Arena envelope")]
    public float arenaHalfSize = 80f;
    [Tooltip("Y below which entities are recovered to the nearest NavMesh point.")]
    public float killYThreshold = -12f;
    [Tooltip("Seconds between recovery warps per entity (prevents warp spam).")]
    public float recoveryCooldown = 2.5f;

    [Header("Fog / sky (hides outside void)")]
    public bool enableFog = true;
    public Color fogColor = new Color(0.42f, 0.44f, 0.48f, 1f);
    [Range(0.0005f, 0.05f)] public float fogDensity = 0.012f;
    public float fogStartDistance = 28f;
    public float fogEndDistance = 72f;

    [Header("Debug")]
    public bool debugRecovery;

    private static WorldArenaStabilizer _instance;
    private static Material _edgeBlockerMaterial;
    private readonly System.Collections.Generic.Dictionary<int, float> _nextRecoveryTime =
        new System.Collections.Generic.Dictionary<int, float>(64);

    public static WorldArenaStabilizer Instance => _instance;

    public static void Install(Transform arenaRoot, float halfSize, bool debugLog = false)
    {
        if (arenaRoot == null) return;

        WorldArenaStabilizer stabilizer = arenaRoot.GetComponentInChildren<WorldArenaStabilizer>(true);
        if (stabilizer == null)
        {
            GameObject host = new GameObject(RootName);
            host.transform.SetParent(arenaRoot, false);
            stabilizer = host.AddComponent<WorldArenaStabilizer>();
        }

        stabilizer.arenaHalfSize = Mathf.Max(30f, halfSize);
        stabilizer.debugRecovery = debugLog;
        stabilizer.ApplyEnvironmentMasking();
        stabilizer.EnsurePhysicsBounds(arenaRoot);
        // Visual perimeter is built by ArenaVisualBounds; keep a thin interior curtain as backup.
        stabilizer.EnsureEdgeBlockers(arenaRoot);
        stabilizer.EnsureKillVolume(arenaRoot);
        _instance = stabilizer;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void ApplyEnvironmentMasking()
    {
        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = Mathf.Clamp(fogDensity, 0.0005f, 0.05f);
        }
        else
        {
            RenderSettings.fog = false;
        }

        // Linear fog distances help third-person cameras that sit farther from the player.
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.fogEndDistance = Mathf.Max(fogStartDistance + 10f, fogEndDistance);

        if (RenderSettings.skybox == null)
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material procedural = new Material(skyShader);
                procedural.SetColor("_SkyTint", fogColor * 1.1f);
                procedural.SetColor("_GroundColor", fogColor * 0.65f);
                procedural.SetFloat("_SunSize", 0.02f);
                procedural.SetFloat("_AtmosphereThickness", 0.75f);
                RenderSettings.skybox = procedural;
            }
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(fogColor, Color.white, 0.15f);
        RenderSettings.ambientEquatorColor = fogColor;
        RenderSettings.ambientGroundColor = fogColor * 0.55f;
    }

    private void EnsurePhysicsBounds(Transform arenaRoot)
    {
        float full = arenaHalfSize * 2f;
        float wallHeight = 42f;
        float wallInset = 0.35f;

        EnsureBoxColliderWall(arenaRoot, "PhysicsWall_N", new Vector3(0f, wallHeight * 0.5f, arenaHalfSize + wallInset),
            new Vector3(full + 4f, wallHeight, 2f));
        EnsureBoxColliderWall(arenaRoot, "PhysicsWall_S", new Vector3(0f, wallHeight * 0.5f, -arenaHalfSize - wallInset),
            new Vector3(full + 4f, wallHeight, 2f));
        EnsureBoxColliderWall(arenaRoot, "PhysicsWall_E", new Vector3(arenaHalfSize + wallInset, wallHeight * 0.5f, 0f),
            new Vector3(2f, wallHeight, full + 4f));
        EnsureBoxColliderWall(arenaRoot, "PhysicsWall_W", new Vector3(-arenaHalfSize - wallInset, wallHeight * 0.5f, 0f),
            new Vector3(2f, wallHeight, full + 4f));

        // Corner pillars close diagonal sight-lines into the void.
        float corner = arenaHalfSize * 0.98f;
        Vector3 cornerScale = new Vector3(6f, wallHeight, 6f);
        EnsureBoxColliderWall(arenaRoot, "PhysicsCorner_NE", new Vector3(corner, wallHeight * 0.5f, corner), cornerScale);
        EnsureBoxColliderWall(arenaRoot, "PhysicsCorner_NW", new Vector3(-corner, wallHeight * 0.5f, corner), cornerScale);
        EnsureBoxColliderWall(arenaRoot, "PhysicsCorner_SE", new Vector3(corner, wallHeight * 0.5f, -corner), cornerScale);
        EnsureBoxColliderWall(arenaRoot, "PhysicsCorner_SW", new Vector3(-corner, wallHeight * 0.5f, -corner), cornerScale);
    }

    private static void EnsureBoxColliderWall(Transform parent, string objectName, Vector3 localPos, Vector3 localScale)
    {
        Transform existing = parent.Find(objectName);
        GameObject wall = existing != null ? existing.gameObject : new GameObject(objectName);
        if (existing == null)
            wall.transform.SetParent(parent, false);

        wall.transform.localPosition = localPos;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = Vector3.one;

        BoxCollider box = wall.GetComponent<BoxCollider>();
        if (box == null)
            box = wall.AddComponent<BoxCollider>();

        box.size = localScale;
        box.center = Vector3.zero;
        box.isTrigger = false;

        MeshFilter mf = wall.GetComponent<MeshFilter>();
        MeshRenderer mr = wall.GetComponent<MeshRenderer>();
        if (mf != null) Destroy(mf);
        if (mr != null) Destroy(mr);
    }

    private void EnsureEdgeBlockers(Transform arenaRoot)
    {
        Transform root = arenaRoot.Find(EdgeBlockerRootName);
        if (root == null)
        {
            GameObject go = new GameObject(EdgeBlockerRootName);
            go.transform.SetParent(arenaRoot, false);
            root = go.transform;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);

        float full = arenaHalfSize * 2f;
        float wallHeight = 18f;
        float inset = Mathf.Max(0.5f, arenaHalfSize * 0.9f);
        Material mat = GetEdgeBlockerMaterial();

        CreateVisibleBlocker(root, "EdgeBlocker_N", new Vector3(0f, wallHeight * 0.5f, inset),
            new Vector3(full + 2f, wallHeight, 1.5f), mat, colliderEnabled: true);
        CreateVisibleBlocker(root, "EdgeBlocker_S", new Vector3(0f, wallHeight * 0.5f, -inset),
            new Vector3(full + 2f, wallHeight, 1.5f), mat, colliderEnabled: true);
        CreateVisibleBlocker(root, "EdgeBlocker_E", new Vector3(inset, wallHeight * 0.5f, 0f),
            new Vector3(1.5f, wallHeight, full + 2f), mat, colliderEnabled: true);
        CreateVisibleBlocker(root, "EdgeBlocker_W", new Vector3(-inset, wallHeight * 0.5f, 0f),
            new Vector3(1.5f, wallHeight, full + 2f), mat, colliderEnabled: true);
    }

    private static void CreateVisibleBlocker(Transform parent, string name, Vector3 localPos, Vector3 scale,
        Material mat, bool colliderEnabled)
    {
        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = name;
        blocker.transform.SetParent(parent, false);
        blocker.transform.localPosition = localPos;
        blocker.transform.localScale = scale;

        Collider col = blocker.GetComponent<Collider>();
        if (col != null)
            col.enabled = colliderEnabled;

        Renderer rend = blocker.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        int envLayer = LayerMask.NameToLayer("Environment");
        if (envLayer >= 0)
            blocker.layer = envLayer;

        try { blocker.tag = "Map"; }
        catch { /* optional */ }
    }

    private static Material GetEdgeBlockerMaterial()
    {
        if (_edgeBlockerMaterial != null)
            return _edgeBlockerMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        _edgeBlockerMaterial = new Material(shader);
        Color c = new Color(0.22f, 0.24f, 0.27f, 1f);
        if (_edgeBlockerMaterial.HasProperty("_BaseColor"))
            _edgeBlockerMaterial.SetColor("_BaseColor", c);
        else if (_edgeBlockerMaterial.HasProperty("_Color"))
            _edgeBlockerMaterial.SetColor("_Color", c);

        return _edgeBlockerMaterial;
    }

    private void EnsureKillVolume(Transform arenaRoot)
    {
        Transform existing = arenaRoot.Find(KillVolumeName);
        GameObject killGo = existing != null ? existing.gameObject : new GameObject(KillVolumeName);
        if (existing == null)
            killGo.transform.SetParent(arenaRoot, false);

        killGo.transform.localPosition = new Vector3(0f, killYThreshold - 5f, 0f);
        killGo.transform.localRotation = Quaternion.identity;
        killGo.transform.localScale = Vector3.one;

        BoxCollider trigger = killGo.GetComponent<BoxCollider>();
        if (trigger == null)
            trigger = killGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(arenaHalfSize * 2.2f, 12f, arenaHalfSize * 2.2f);
        trigger.center = Vector3.zero;

        WorldKillVolumeHandler handler = killGo.GetComponent<WorldKillVolumeHandler>();
        if (handler == null)
            handler = killGo.AddComponent<WorldKillVolumeHandler>();
        handler.stabilizer = this;
        handler.killYThreshold = killYThreshold;
        handler.debugRecovery = debugRecovery;
    }

    public bool TryRecoverTransform(Transform target, string reason)
    {
        if (target == null)
            return false;

        int id = target.GetInstanceID();
        float now = Time.time;
        if (_nextRecoveryTime.TryGetValue(id, out float nextAllowed) && now < nextAllowed)
            return false;

        Vector3 sampleOrigin = target.position;
        if (sampleOrigin.y < killYThreshold)
            sampleOrigin.y = 0.5f;

        if (!NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, 30f, NavMesh.AllAreas))
            return false;

        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.Warp(hit.position);
            agent.ResetPath();
        }
        else
        {
            CharacterController cc = target.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;
                target.position = hit.position;
                cc.enabled = true;
            }
            else
            {
                target.position = hit.position;
            }
        }

        _nextRecoveryTime[id] = now + Mathf.Max(0.5f, recoveryCooldown);

        if (debugRecovery)
            Debug.Log($"[WorldArena] Recovered {target.name} ({reason}) -> {hit.position}");

        return true;
    }

    public bool IsInsidePlayableBounds(Vector3 worldPos)
    {
        float limit = arenaHalfSize * 0.98f;
        return Mathf.Abs(worldPos.x) <= limit && Mathf.Abs(worldPos.z) <= limit;
    }
}

/// <summary>Trigger below the arena — warps fallen player/enemies back to NavMesh.</summary>
public class WorldKillVolumeHandler : MonoBehaviour
{
    public WorldArenaStabilizer stabilizer;
    public float killYThreshold = -12f;
    public bool debugRecovery;

    private void OnTriggerStay(Collider other)
    {
        if (stabilizer == null)
            stabilizer = WorldArenaStabilizer.Instance;
        if (stabilizer == null)
            return;

        Transform root = other.transform.root;
        if (root == null)
            return;

        if (root.position.y > killYThreshold + 1f)
            return;

        bool isPlayer = root.GetComponentInParent<PlayerController>() != null;
        bool isEnemy = root.GetComponentInParent<EnemyController>() != null;
        if (!isPlayer && !isEnemy)
            return;

        stabilizer.TryRecoverTransform(root, "kill_volume");
    }
}
