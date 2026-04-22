using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapExpansionGenerator
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string ContainerName = "_MapExpansion_Container";
    private const string EnvironmentPrefabRoot = "Assets/StarterAssets/Environment/Prefabs/";

    [MenuItem("Tools/PRISM/Generate Starter Assets Map Expansion")]
    public static void GenerateFromMenu()
    {
        Generate(ScenePath);
    }

    public static void GenerateFromCommandLine()
    {
        Generate(ScenePath);
    }

    private static void Generate(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject container = RecreateContainer(scene);

        Bounds existingBounds = CalculateSceneBounds(container.transform);
        float startX = existingBounds.max.x + 4f;
        float laneZ = Mathf.Clamp(existingBounds.center.z, -2f, 2f);

        GameObject largeBox = LoadPrefab("Box_350x250x300_Prefab.prefab");
        GameObject smallBox = LoadPrefab("Box_100x100x100_Prefab.prefab");
        GameObject ramp = LoadPrefab("Ramp_Prefab.prefab");
        GameObject stairsLarge = LoadPrefab("Stairs_650_400_300_Prefab.prefab");
        GameObject tunnel = LoadPrefab("Tunnel_Prefab.prefab");
        GameObject wall = LoadPrefab("Wall_Prefab.prefab");
        if (largeBox == null || smallBox == null || ramp == null || stairsLarge == null || tunnel == null || wall == null)
        {
            Debug.LogWarning("[MapExpansionGenerator] Map expansion skipped because one or more environment prefabs are missing.");
            Object.DestroyImmediate(container);
            return;
        }

        var placed = new List<GameObject>();

        GameObject baseA = PlaceAtBoundsCenter(largeBox, container.transform, new Vector3(startX, 0f, laneZ), Quaternion.identity, alignMinY: 0f);
        GameObject baseB = PlaceFlushX(largeBox, container.transform, baseA, laneZ, 0f, gap: 0.2f);
        GameObject baseC = PlaceFlushX(largeBox, container.transform, baseB, laneZ, 0f, gap: 0.2f);
        placed.Add(baseA);
        placed.Add(baseB);
        placed.Add(baseC);

        GameObject leftRailA = PlaceAtBoundsCenter(wall, container.transform, GetBounds(baseA).center + new Vector3(0f, 0f, -2.8f), Quaternion.Euler(0f, 90f, 0f), alignMinY: 0f);
        GameObject rightRailA = PlaceAtBoundsCenter(wall, container.transform, GetBounds(baseA).center + new Vector3(0f, 0f, 2.8f), Quaternion.Euler(0f, 90f, 0f), alignMinY: 0f);
        placed.Add(leftRailA);
        placed.Add(rightRailA);

        GameObject stairs = PlaceFlushX(stairsLarge, container.transform, baseC, laneZ, 0f, gap: 0.6f);
        placed.Add(stairs);

        float upperY = GetBounds(stairs).max.y;

        GameObject upperA = PlaceFlushX(smallBox, container.transform, stairs, laneZ, upperY, gap: 0.15f);
        GameObject upperB = PlaceFlushX(smallBox, container.transform, upperA, laneZ, upperY, gap: 0.05f);
        GameObject upperC = PlaceFlushX(smallBox, container.transform, upperB, laneZ, upperY, gap: 0.05f);
        placed.Add(upperA);
        placed.Add(upperB);
        placed.Add(upperC);

        Bounds upperBounds = Encapsulate(upperA, upperB, upperC);
        GameObject tunnelCover = PlaceAtBoundsCenter(tunnel, container.transform, upperBounds.center + new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), alignMinY: upperY);
        placed.Add(tunnelCover);

        GameObject overlook = PlaceAtBoundsCenter(smallBox, container.transform, upperBounds.center + new Vector3(0f, 0f, 4.2f), Quaternion.identity, alignMinY: upperY);
        placed.Add(overlook);

        GameObject rampDown = PlaceFlushX(ramp, container.transform, upperC, laneZ + 4.2f, 0f, gap: 0.4f, alignMaxY: upperY + 1.0f);
        placed.Add(rampDown);

        GameObject landingA = PlaceFlushX(largeBox, container.transform, rampDown, laneZ + 4.2f, 0f, gap: 0.1f);
        GameObject landingB = PlaceFlushZ(largeBox, container.transform, landingA, 3.4f, 0f, gap: 0.2f);
        placed.Add(landingA);
        placed.Add(landingB);

        GameObject wallEnd = PlaceFlushX(wall, container.transform, landingA, laneZ + 6.9f, 0f, gap: -0.6f, rotation: Quaternion.Euler(0f, 90f, 0f));
        GameObject wallSide = PlaceFlushZ(wall, container.transform, landingB, 0f, 0f, gap: 0.3f, positiveDirection: true, rotation: Quaternion.identity);
        placed.Add(wallEnd);
        placed.Add(wallSide);

        foreach (GameObject go in placed)
        {
            ConfigureEnvironmentObject(go);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MapExpansionGenerator] Generated {placed.Count} environment pieces in {scene.path} under {ContainerName}.");
    }

    private static GameObject RecreateContainer(Scene scene)
    {
        GameObject existing = FindRoot(scene, ContainerName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject container = new GameObject(ContainerName);
        SceneManager.MoveGameObjectToScene(container, scene);
        container.transform.position = Vector3.zero;
        return container;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }

    private static GameObject LoadPrefab(string fileName)
    {
        string path = EnvironmentPrefabRoot + fileName;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[MapExpansionGenerator] Missing environment prefab at {path}");
            return null;
        }

        return prefab;
    }

    private static Bounds CalculateSceneBounds(Transform ignoredRoot)
    {
        bool hasBounds = false;
        Bounds combined = new Bounds(Vector3.zero, Vector3.one);

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer.transform.IsChildOf(ignoredRoot) || renderer is ParticleSystemRenderer || renderer.gameObject.hideFlags != HideFlags.None)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            combined = new Bounds(Vector3.zero, new Vector3(8f, 2f, 8f));
        }

        return combined;
    }

    private static GameObject PlaceAtBoundsCenter(GameObject prefab, Transform parent, Vector3 boundsCenterTarget, Quaternion rotation, float? alignMinY = null, float? alignMaxY = null)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetPositionAndRotation(Vector3.zero, rotation);
        MoveBoundsCenterTo(instance, boundsCenterTarget);

        if (alignMinY.HasValue)
        {
            ShiftY(instance, alignMinY.Value - GetBounds(instance).min.y);
        }

        if (alignMaxY.HasValue)
        {
            ShiftY(instance, alignMaxY.Value - GetBounds(instance).max.y);
        }

        instance.name = prefab.name.Replace("_Prefab", string.Empty);
        return instance;
    }

    private static GameObject PlaceFlushX(GameObject prefab, Transform parent, GameObject previous, float z, float baseMinY, float gap, bool positiveDirection = true, Quaternion? rotation = null, float? alignMaxY = null)
    {
        Quaternion actualRotation = rotation ?? Quaternion.identity;
        GameObject instance = PlaceAtBoundsCenter(prefab, parent, Vector3.zero, actualRotation, alignMinY: baseMinY, alignMaxY: alignMaxY);

        Bounds prevBounds = GetBounds(previous);
        Bounds nextBounds = GetBounds(instance);
        float x = positiveDirection
            ? prevBounds.max.x + gap + nextBounds.extents.x
            : prevBounds.min.x - gap - nextBounds.extents.x;

        MoveBoundsCenterTo(instance, new Vector3(x, nextBounds.center.y, z));

        if (alignMaxY.HasValue)
        {
            ShiftY(instance, alignMaxY.Value - GetBounds(instance).max.y);
        }
        else
        {
            ShiftY(instance, baseMinY - GetBounds(instance).min.y);
        }

        return instance;
    }

    private static GameObject PlaceFlushZ(GameObject prefab, Transform parent, GameObject previous, float xOffset, float baseMinY, float gap, bool positiveDirection = true, Quaternion? rotation = null)
    {
        Quaternion actualRotation = rotation ?? Quaternion.identity;
        GameObject instance = PlaceAtBoundsCenter(prefab, parent, Vector3.zero, actualRotation, alignMinY: baseMinY);

        Bounds prevBounds = GetBounds(previous);
        Bounds nextBounds = GetBounds(instance);
        float z = positiveDirection
            ? prevBounds.max.z + gap + nextBounds.extents.z
            : prevBounds.min.z - gap - nextBounds.extents.z;

        MoveBoundsCenterTo(instance, new Vector3(prevBounds.center.x + xOffset, nextBounds.center.y, z));
        ShiftY(instance, baseMinY - GetBounds(instance).min.y);
        return instance;
    }

    private static void MoveBoundsCenterTo(GameObject go, Vector3 targetCenter)
    {
        Bounds bounds = GetBounds(go);
        Vector3 delta = targetCenter - bounds.center;
        go.transform.position += delta;
    }

    private static void ShiftY(GameObject go, float deltaY)
    {
        go.transform.position += new Vector3(0f, deltaY, 0f);
    }

    private static Bounds Encapsulate(params GameObject[] gameObjects)
    {
        Bounds bounds = GetBounds(gameObjects[0]);
        for (int i = 1; i < gameObjects.Length; i++)
        {
            bounds.Encapsulate(GetBounds(gameObjects[i]));
        }

        return bounds;
    }

    private static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        var colliders = go.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds;
        }

        return new Bounds(go.transform.position, Vector3.one);
    }

    private static void ConfigureEnvironmentObject(GameObject root)
    {
        SetLayerRecursively(root, 0);
        root.tag = "Untagged";
        GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
