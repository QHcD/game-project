using UnityEngine;

/// <summary>
/// Closes the industrial arena visually: floor backfill, perimeter walls, building foundations.
/// Map-only pass — does not alter gameplay systems.
/// </summary>
[DisallowMultipleComponent]
public class ArenaVisualBounds : MonoBehaviour
{
    private const string RootName = "ArenaVisualClosure";
    private const string FloorName = "ArenaFloor_Backfill";
    private const string PerimeterRootName = "ArenaPerimeterWalls";
    private const string FoundationRootName = "ArenaBuildingFoundations";

    [Header("Sizing")]
    public float arenaHalfSize = 80f;
    [Tooltip("Extra metres beyond arena half-size for floor/backfill coverage.")]
    public float floorMargin = 6f;
    [Tooltip("Perimeter placed at this fraction of arenaHalfSize (keeps spawn lanes clear).")]
    [Range(0.82f, 0.98f)] public float perimeterInsetFactor = 0.93f;

    [Header("Debug")]
    public bool debugVisualBounds;

    private static Material _asphaltMaterial;
    private static Material _concreteWallMaterial;
    private static Material _containerMaterial;
    private static bool _loggedInstall;

    public static ArenaVisualBounds Instance { get; private set; }

    /// <summary>
    /// Builds visible floor + perimeter + foundation skirts after FBX map load.
    /// </summary>
    public static void Install(Transform arenaRoot, float halfSize, Transform fbxMapRoot, bool debug = false)
    {
        if (arenaRoot == null) return;

        ArenaVisualBounds bounds = arenaRoot.GetComponentInChildren<ArenaVisualBounds>(true);
        if (bounds == null)
        {
            GameObject host = new GameObject(RootName);
            host.transform.SetParent(arenaRoot, false);
            bounds = host.AddComponent<ArenaVisualBounds>();
        }

        bounds.arenaHalfSize = Mathf.Max(30f, halfSize);
        bounds.debugVisualBounds = debug;
        bounds.Rebuild(fbxMapRoot);

        Instance = bounds;

        if (!_loggedInstall || debug)
        {
            _loggedInstall = true;
            Debug.Log(
                $"[ArenaVisualBounds] Installed closure halfSize={bounds.arenaHalfSize:F1} " +
                $"floorMargin={bounds.floorMargin:F1} perimeterInset={bounds.arenaHalfSize * bounds.perimeterInsetFactor:F1}");
        }
    }

    public void Rebuild(Transform fbxMapRoot)
    {
        ClearChildren(transform);

        EnsureMaterials();
        BuildFloorBackfill();
        BuildPerimeterEnclosure();
        SealFloatingStructures(fbxMapRoot);

        if (debugVisualBounds)
            Debug.Log($"[ArenaVisualBounds] Rebuild complete on {name}", this);
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    private static void EnsureMaterials()
    {
        if (_asphaltMaterial == null)
            _asphaltMaterial = LoadArenaMaterial(
                "Maps/IndustrialMap/Materials/Seamless_asphalt_v1_URP",
                new Color(0.28f, 0.29f, 0.30f, 1f));

        if (_concreteWallMaterial == null)
            _concreteWallMaterial = LoadArenaMaterial(
                "Maps/IndustrialMap/Materials/UNIConcrete_wall_v1_URP",
                new Color(0.35f, 0.36f, 0.38f, 1f));

        if (_containerMaterial == null)
            _containerMaterial = LoadArenaMaterial(
                "Maps/IndustrialMap/Materials/Cargo_container_v1_URP",
                new Color(0.32f, 0.34f, 0.36f, 1f));
    }

    private static Material LoadArenaMaterial(string resourcesPath, Color fallbackColor)
    {
        Material source = Resources.Load<Material>(resourcesPath);
        if (source != null)
            return new Material(source);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", fallbackColor);
        else
            mat.color = fallbackColor;
        return mat;
    }

    private void BuildFloorBackfill()
    {
        float extent = (arenaHalfSize + floorMargin) * 2f;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = FloorName;
        floor.transform.SetParent(transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.14f, 0f);
        floor.transform.localScale = new Vector3(extent, 0.08f, extent);

        ApplyArenaSurface(floor, _asphaltMaterial, receiveShadows: true);
        TagAndLayer(floor, "Map");
    }

    private void BuildPerimeterEnclosure()
    {
        GameObject root = new GameObject(PerimeterRootName);
        root.transform.SetParent(transform, false);

        float inset = arenaHalfSize * perimeterInsetFactor;
        float full = inset * 2f;
        float wallHeight = 14f;
        float wallThickness = 2.2f;
        float segmentSpacing = 7f;

        BuildWallRun(root.transform, "Perimeter_N",
            new Vector3(0f, wallHeight * 0.5f, inset),
            new Vector3(full + wallThickness, wallHeight, wallThickness),
            Vector3.forward, segmentSpacing, wallHeight);

        BuildWallRun(root.transform, "Perimeter_S",
            new Vector3(0f, wallHeight * 0.5f, -inset),
            new Vector3(full + wallThickness, wallHeight, wallThickness),
            Vector3.back, segmentSpacing, wallHeight);

        BuildWallRun(root.transform, "Perimeter_E",
            new Vector3(inset, wallHeight * 0.5f, 0f),
            new Vector3(wallThickness, wallHeight, full + wallThickness),
            Vector3.right, segmentSpacing, wallHeight);

        BuildWallRun(root.transform, "Perimeter_W",
            new Vector3(-inset, wallHeight * 0.5f, 0f),
            new Vector3(wallThickness, wallHeight, full + wallThickness),
            Vector3.left, segmentSpacing, wallHeight);

        float corner = inset * 0.98f;
        Vector3 cornerScale = new Vector3(10f, wallHeight + 2f, 10f);
        CreatePerimeterBlock(root.transform, "Corner_NE", new Vector3(corner, (wallHeight + 2f) * 0.5f, corner), cornerScale, _concreteWallMaterial);
        CreatePerimeterBlock(root.transform, "Corner_NW", new Vector3(-corner, (wallHeight + 2f) * 0.5f, corner), cornerScale, _concreteWallMaterial);
        CreatePerimeterBlock(root.transform, "Corner_SE", new Vector3(corner, (wallHeight + 2f) * 0.5f, -corner), cornerScale, _concreteWallMaterial);
        CreatePerimeterBlock(root.transform, "Corner_SW", new Vector3(-corner, (wallHeight + 2f) * 0.5f, -corner), cornerScale, _concreteWallMaterial);

        // Low skirt ring — hides ground gaps at the outer rim.
        float skirtY = 0.35f;
        float skirtH = 0.7f;
        CreatePerimeterBlock(root.transform, "Skirt_N",
            new Vector3(0f, skirtY, inset + wallThickness * 0.35f),
            new Vector3(full + 4f, skirtH, 1.6f), _concreteWallMaterial);
        CreatePerimeterBlock(root.transform, "Skirt_S",
            new Vector3(0f, skirtY, -(inset + wallThickness * 0.35f)),
            new Vector3(full + 4f, skirtH, 1.6f), _concreteWallMaterial);
        CreatePerimeterBlock(root.transform, "Skirt_E",
            new Vector3(inset + wallThickness * 0.35f, skirtY, 0f),
            new Vector3(1.6f, skirtH, full + 4f), _concreteWallMaterial);
        CreatePerimeterBlock(root.transform, "Skirt_W",
            new Vector3(-(inset + wallThickness * 0.35f), skirtY, 0f),
            new Vector3(1.6f, skirtH, full + 4f), _concreteWallMaterial);
    }

    private void BuildWallRun(Transform parent, string baseName, Vector3 center, Vector3 mainScale,
        Vector3 alongAxis, float spacing, float height)
    {
        CreatePerimeterBlock(parent, baseName + "_Core", center, mainScale, _concreteWallMaterial);

        bool alongX = Mathf.Abs(alongAxis.x) > 0.5f;
        float runLength = alongX ? mainScale.x : mainScale.z;
        int count = Mathf.Max(1, Mathf.FloorToInt(runLength / spacing));
        float start = -(count - 1) * 0.5f * spacing;

        for (int i = 0; i < count; i++)
        {
            float offset = start + i * spacing;
            Vector3 pos = center;
            if (alongX)
                pos.x += offset;
            else
                pos.z += offset;

            Vector3 moduleScale = alongX
                ? new Vector3(2.4f, height * 0.92f, 2.6f)
                : new Vector3(2.6f, height * 0.92f, 2.4f);

            CreatePerimeterBlock(parent, $"{baseName}_Module_{i}", pos, moduleScale, _containerMaterial);
        }
    }

    private static void CreatePerimeterBlock(Transform parent, string blockName, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = blockName;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPos;
        block.transform.localScale = scale;
        ApplyArenaSurface(block, mat, receiveShadows: true);
        TagAndLayer(block, "Map");
    }

    private void SealFloatingStructures(Transform fbxMapRoot)
    {
        if (fbxMapRoot == null) return;

        EnvironmentGroundAnchor.Install(fbxMapRoot, debugVisualBounds);

        GameObject foundationRoot = new GameObject(FoundationRootName);
        foundationRoot.transform.SetParent(transform, false);

        Renderer[] renderers = fbxMapRoot.GetComponentsInChildren<Renderer>(true);
        int skirtsAdded = 0;
        const int maxSkirts = 48;

        for (int i = 0; i < renderers.Length && skirtsAdded < maxSkirts; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null || !rend.enabled) continue;

            string n = rend.gameObject.name.ToLowerInvariant();
            if (!LooksLikeFloatingCandidate(n)) continue;

            Bounds b = rend.bounds;
            if (!EnvironmentGroundAnchor.TrySampleGroundAt(b, rend.transform, out float groundY))
                continue;

            float gap = b.min.y - groundY;
            if (gap > 0.55f && b.size.y > 2f)
            {
                AddFoundationSkirt(foundationRoot.transform, rend.transform, b, groundY);
                skirtsAdded++;
                continue;
            }

            if (gap > 0.45f && gap < 1.2f && (b.size.x > 4f || b.size.z > 4f))
            {
                AddFoundationSkirt(foundationRoot.transform, rend.transform, b, groundY);
                skirtsAdded++;
            }
        }

        if (debugVisualBounds && skirtsAdded > 0)
            Debug.Log($"[ArenaVisualBounds] Added {skirtsAdded} foundation skirt(s).");
    }

    private static bool LooksLikeFloatingCandidate(string lowerName)
    {
        if (string.IsNullOrEmpty(lowerName)) return false;
        if (lowerName.Contains("road") || lowerName.Contains("asphalt") || lowerName.Contains("ground")
            || lowerName.Contains("floor") || lowerName.Contains("pavement") || lowerName.Contains("street"))
            return false;
        if (lowerName.Contains("fence") || lowerName.Contains("barrel") || lowerName.Contains("lamp")
            || lowerName.Contains("light") || lowerName.Contains("wire") || lowerName.Contains("pipe"))
            return false;

        return lowerName.Contains("building") || lowerName.Contains("hangar") || lowerName.Contains("warehouse")
            || lowerName.Contains("container") || lowerName.Contains("office") || lowerName.Contains("structure")
            || lowerName.Contains("wall") || lowerName.Contains("shed") || lowerName.Contains("hall")
            || lowerName.Contains("silo") || lowerName.Contains("tank") || lowerName.Contains("storage");
    }

    private static void AddFoundationSkirt(Transform parent, Transform anchor, Bounds worldBounds, float groundY)
    {
        float bottomY = worldBounds.min.y;
        float skirtHeight = Mathf.Max(0.35f, bottomY - groundY);
        if (skirtHeight < 0.2f) return;

        Vector3 center = new Vector3(worldBounds.center.x, groundY + skirtHeight * 0.5f, worldBounds.center.z);
        Vector3 scale = new Vector3(
            Mathf.Clamp(worldBounds.size.x + 0.6f, 2f, 40f),
            skirtHeight,
            Mathf.Clamp(worldBounds.size.z + 0.6f, 2f, 40f));

        GameObject skirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        skirt.name = $"Foundation_{anchor.name}";
        skirt.transform.SetParent(parent, true);
        skirt.transform.position = center;
        skirt.transform.localScale = scale;
        ApplyArenaSurface(skirt, _concreteWallMaterial, receiveShadows: true);
        TagAndLayer(skirt, "Map");
    }

    private static void ApplyArenaSurface(GameObject go, Material mat, bool receiveShadows)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rend.receiveShadows = receiveShadows;
        }

        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        TagAndLayer(go, "Map");
    }

    private static void TagAndLayer(GameObject go, string tagName)
    {
        if (!string.IsNullOrEmpty(tagName))
        {
            try { go.tag = tagName; }
            catch { /* tag may be undefined in some scenes */ }
        }

        int env = LayerMask.NameToLayer("Environment");
        if (env < 0) env = LayerMask.NameToLayer("Default");
        if (env >= 0)
            SetLayerRecursive(go, env);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugVisualBounds) return;

        Gizmos.color = new Color(0.2f, 0.85f, 0.4f, 0.85f);
        float inset = arenaHalfSize * perimeterInsetFactor;
        Vector3 c = transform.position;
        Gizmos.DrawWireCube(c, new Vector3(inset * 2f, 12f, inset * 2f));

        Gizmos.color = new Color(0.85f, 0.55f, 0.1f, 0.75f);
        float floor = (arenaHalfSize + floorMargin) * 2f;
        Gizmos.DrawWireCube(c + Vector3.up * -0.14f, new Vector3(floor, 0.08f, floor));
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
