using UnityEngine;

public readonly struct WeaponLoadout
{
    public readonly string[] ResourcePaths;
    public readonly float TargetSize;
    public readonly Vector3 PlayerLocalPosition;
    public readonly Vector3 PlayerLocalEuler;
    public readonly Vector3 EnemyLocalPosition;
    public readonly Vector3 EnemyLocalEuler;

    public WeaponLoadout(
        string[] resourcePaths,
        float targetSize,
        Vector3 playerLocalPosition,
        Vector3 playerLocalEuler,
        Vector3 enemyLocalPosition,
        Vector3 enemyLocalEuler)
    {
        ResourcePaths = resourcePaths;
        TargetSize = targetSize;
        PlayerLocalPosition = playerLocalPosition;
        PlayerLocalEuler = playerLocalEuler;
        EnemyLocalPosition = enemyLocalPosition;
        EnemyLocalEuler = enemyLocalEuler;
    }

    public GameObject LoadPrefab()
    {
        if (ResourcePaths == null)
            return null;

        for (int i = 0; i < ResourcePaths.Length; i++)
        {
            string path = ResourcePaths[i];
            if (string.IsNullOrWhiteSpace(path))
                continue;

            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
                return prefab;
        }

        return null;
    }
}

public static class WeaponLoadoutCatalog
{
    private static readonly Vector3 OneHandedGripEuler = new Vector3(0f, 0f, 90f);
    private static readonly Vector3 ReversedOneHandedGripEuler = new Vector3(0f, 180f, 90f);
    private static readonly Vector3 DefaultPlayerLocalPosition = new Vector3(-0.015f, -0.005f, 0f);
    private static readonly Vector3 DefaultPlayerLocalEuler = OneHandedGripEuler;
    private static readonly Vector3 DefaultEnemyLocalPosition = new Vector3(-0.01f, -0.0025f, 0f);
    private static readonly Vector3 DefaultEnemyLocalEuler = OneHandedGripEuler;
    // Level 1 knife keeps its grip pivot very close to the blade center on the
    // imported FBX, so the generic Crosby one-handed preset tucks the mesh into
    // the palm. The original enemy-only knife basis uses the real hand bone and
    // a forward Y offset / X-axis quarter-turn so the blade clears the wrist
    // from the first frame.
    private static readonly Vector3 Level1EnemyLocalPosition = new Vector3(0f, 0.05f, 0f);
    private static readonly Vector3 Level1EnemyLocalEuler = new Vector3(-90f, 0f, 0f);
    private static readonly Vector3 MediumPlayerLocalPosition = new Vector3(-0.03f, -0.005f, 0f);
    private static readonly Vector3 MediumEnemyLocalPosition = new Vector3(-0.025f, -0.0025f, 0f);
    private static readonly Vector3 LongPlayerLocalPosition = new Vector3(-0.045f, -0.005f, 0f);
    private static readonly Vector3 LongEnemyLocalPosition = new Vector3(-0.035f, -0.0025f, 0f);
    // Crosby's right-hand basis inverts the katana end-for-end relative to
    // the Ronin wrist basis, so level 2 needs an enemy-only grip reversal
    // while keeping the player pose unchanged.
    private static readonly Vector3 KatanaEnemyLocalEuler = ReversedOneHandedGripEuler;
    // Same Crosby hand basis issue as the katana: the bat needs an enemy-only
    // end-for-end correction while preserving the player grip basis.
    private static readonly Vector3 BaseballBatEnemyLocalEuler = ReversedOneHandedGripEuler;
    // Level 15 shares the same enemy hand-basis inversion, so it also needs
    // an enemy-only end-for-end correction to match the player's forward grip.
    private static readonly Vector3 Level15EnemyLocalEuler = ReversedOneHandedGripEuler;
    // Level 16 shield also inherits the enemy hand-basis inversion, so it
    // needs an enemy-only forward-facing correction to match the player grip.
    private static readonly Vector3 Level16EnemyLocalEuler = ReversedOneHandedGripEuler;
    private static readonly Vector3 PolePlayerLocalPosition = new Vector3(-0.06f, -0.0075f, 0f);
    private static readonly Vector3 PoleEnemyLocalPosition = new Vector3(-0.05f, -0.003f, 0f);
    // Level 6 wrench: dedicated exact grip values. The imported FBX long axis
    // is local Y with bounds roughly [-25.1559 .. +18.9399]. After the 0.40m
    // autoscale, the handle butt sits ~0.228m from the pivot on the -Y end.
    // With the project-wide Z=90 one-handed convention that butt end maps to
    // +X in socket space, so the weapon root must move in -X for the palm to
    // close around the start of the handle instead of the middle/head.
    private static readonly Vector3 WrenchPlayerLocalPosition = new Vector3(-0.208f, -0.0125f, 0.0025f);
    private static readonly Vector3 WrenchPlayerLocalEuler = new Vector3(10f, 0f, 90f);
    private static readonly Vector3 WrenchEnemyLocalPosition = new Vector3(-0.182f, -0.0085f, 0.0025f);
    private static readonly Vector3 WrenchEnemyLocalEuler = new Vector3(12f, 0f, 90f);
    private static readonly Vector3 CrowbarPlayerLocalPosition = new Vector3(-0.135f, -0.006f, 0.006f);
    private static readonly Vector3 CrowbarPlayerLocalEuler = new Vector3(0f, 180f, 104f);
    private static readonly Vector3 CrowbarEnemyLocalPosition = new Vector3(-0.115f, -0.0035f, 0.006f);
    private static readonly Vector3 CrowbarEnemyLocalEuler = new Vector3(0f, 180f, 104f);
    private static readonly Vector3 HammerPlayerLocalPosition = new Vector3(-0.156f, -0.0085f, -0.03f);
    private static readonly Vector3 HammerPlayerLocalEuler = new Vector3(8f, 0f, 90f);
    private static readonly Vector3 HammerEnemyLocalPosition = new Vector3(-0.156f, -0.007f, -0.04f);
    private static readonly Vector3 HammerEnemyLocalEuler = new Vector3(8f, 0f, 90f);
    private static readonly Vector3 HammerEnemySocketLocalEuler = new Vector3(15.2525f, -24.1971f, -111.6271f);
    // Crosby's hand basis points the pole shaft backward relative to the
    // player wrist basis, so pole levels need a pre-grip socket flip on the
    // enemy side instead of more per-weapon position nudges.
    private static readonly Vector3 PoleEnemySocketLocalEuler = new Vector3(0f, 180f, 0f);
    // Level 12 saw: the imported mesh keeps the blade on negative X and the
    // actual hand grip on the positive-X handle shell. With the project-wide
    // Z=90 one-handed convention, that handle pocket maps to large +X / -Y
    // socket offsets, so a generic short-grip preset lands the palm on the
    // blade body instead of the usable handle.
    private static readonly Vector3 SawPlayerLocalPosition = new Vector3(0.246f, -0.270f, 0.002f);
    private static readonly Vector3 SawPlayerLocalEuler = new Vector3(0f, 0f, 90f);
    private static readonly Vector3 SawEnemyLocalPosition = new Vector3(0.244f, -0.264f, 0.002f);
    private static readonly Vector3 SawEnemyLocalEuler = new Vector3(0f, 0f, 90f);
    private static readonly Vector3 SawRuntimeMeshLocalEuler = OneHandedGripEuler;
    // The socket-side helper only neutralizes the hand socket. The actual
    // source-of-truth grip lives on the weapon as a handle anchor derived
    // from the saw mesh bounds so both player and enemy grab the real rear
    // handle opening instead of the blade body.
    private const string SawRuntimeSocketAnchorName = "Level12SawSocketAnchor";
    private const string SawRuntimeHandleAnchorName = "Level12SawHandleAnchor";
    private static readonly Vector3 SawRuntimeFallbackGripPoint = new Vector3(0.332f, 0.124f, 0.002f);
    private const float SawHandleGripXNormalized = 0.835f;
    private const float SawHandleGripYNormalized = 0.295f;
    private const float SawHandleGripZNormalized = 0.50f;
    // ── Level 9 axe (single source of truth) ──
    // Empirically verified on the real Crosby body (bip_hand_R) with the
    // runtime 0.70m autoscale applied. The axe FBX has an extremely
    // asymmetric pivot: long axis is X with mesh range [-0.0094..+0.0431]
    // and the dense head bulk sitting at X≈-0.0094. After autoscale
    // (uniformScale = 0.70 / 0.0525 ≈ 13.33) the head is 0.125m from the
    // pivot and the handle butt is 0.575m from the pivot.
    //
    // The Z=90 component preserves the project-wide one-handed grip
    // convention; under Unity's intrinsic ZXY order Z=90 maps mesh +X to
    // socket -Y, which is why the offset lives on the socket Y axis (NOT
    // X like the wrench/crowbar/hammer). The 180° Y flip swaps the model
    // end-for-end so the wooden handle sits in the palm and the head
    // extends outward away from the body.
    //
    // Player Y = -0.475 places the hand 0.10m from the butt and 0.60m
    // from the head — the natural grip section of the handle, not at the
    // extreme butt edge. Enemy uses a slightly reduced magnitude per the
    // existing wrench/crowbar/hammer player-vs-enemy convention.
    private static readonly Vector3 AxePlayerLocalPosition = new Vector3(0f, -0.475f, 0f);
    private static readonly Vector3 AxePlayerLocalEuler = new Vector3(0f, 180f, 90f);
    private static readonly Vector3 AxeEnemyLocalPosition = new Vector3(0f, -0.405f, 0f);
    private static readonly Vector3 AxeEnemyLocalEuler = new Vector3(0f, 180f, 90f);

    // ── Guaranteed fallback paths (tried in order when the level weapon is missing) ──
    private static readonly string[] FallbackPaths =
    {
        "Weapons/Imported/tactical-knife(level1)/source/TacticalKnife/Tactical Knife",
        "Weapons/TacticalKnife/TacticalKnife",
        "Weapons/KnuckleDuster",
        "Weapons/BlinkDaggerPack/_PrefabsDaggers/Dagger1_3_5",
        "Weapons/BlinkDaggerPack/Meshes_Dagger/Dagger1_3",
    };

    /// <summary>
    /// Loads the weapon prefab for the given level with a guaranteed fallback chain.
    /// If the level's weapon is missing, tries: Level 1 knife → KnuckleDuster → BlinkDagger.
    /// <paramref name="targetSize"/> is set to the appropriate size for whichever weapon loaded.
    /// </summary>
    public static GameObject LoadPrefabWithFallback(int level, out float targetSize)
    {
        WeaponLoadout loadout = Get(level);
        GameObject prefab = loadout.LoadPrefab();
        if (prefab != null)
        {
            targetSize = loadout.TargetSize;
            return prefab;
        }

        Debug.LogWarning($"[WeaponLoadoutCatalog] Level {level} weapon missing — trying fallbacks.");

        for (int i = 0; i < FallbackPaths.Length; i++)
        {
            prefab = Resources.Load<GameObject>(FallbackPaths[i]);
            if (prefab != null)
            {
                Debug.LogWarning($"[WeaponLoadoutCatalog] Fallback loaded: '{FallbackPaths[i]}'");
                targetSize = 0.30f; // safe knife-sized default
                return prefab;
            }
        }

        Debug.LogError($"[WeaponLoadoutCatalog] ALL weapon fallbacks failed for level {level}!");
        targetSize = 0.30f;
        return null;
    }

    public static WeaponLoadout Get(int level)
    {
        switch (Mathf.Clamp(level, 1, 16))
        {
            case 1:
                return CreateExactGrip(
                    0.32f,
                    DefaultPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    Level1EnemyLocalPosition,
                    Level1EnemyLocalEuler,
                    "Weapons/Imported/tactical-knife(level1)/source/TacticalKnife/Tactical Knife",
                    "Weapons/TacticalKnife/TacticalKnife");
            case 2:
                return CreateExactGrip(
                    0.95f,
                    LongPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    LongEnemyLocalPosition,
                    KatanaEnemyLocalEuler,
                    "Weapons/Imported/Katana(level2)/source/Katana_low",
                    "Weapons/Imported/Katana(level2)/source/melee");
            case 3:
                return CreatePlayerMatchedGrip(
                    1.00f,
                    PolePlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    "Weapons/Imported/shovel(level3)/source/Shovel/Shovel");
            case 4:
                return CreateExactGrip(
                    0.85f,
                    LongPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    LongEnemyLocalPosition,
                    BaseballBatEnemyLocalEuler,
                    "Weapons/Imported/baseball-bat(level4)/source/baseball_bat_1k");
            case 5:
                return CreateShortGrip(0.30f,
                    "Weapons/Imported/nunchucks(level5)/Nunchucks");
            case 6:
                return CreateExactGrip(
                    0.40f,
                    WrenchPlayerLocalPosition,
                    WrenchPlayerLocalEuler,
                    WrenchEnemyLocalPosition,
                    WrenchEnemyLocalEuler,
                    "Weapons/Imported/Wrench(level6)/source/PipeWrenchUnreal");
            case 7:
                return CreatePlayerMatchedGrip(
                    0.55f,
                    CrowbarPlayerLocalPosition,
                    CrowbarPlayerLocalEuler,
                    "Weapons/Imported/crowbar(level7)/source/CrowbarV2");
            case 8:
                return CreateExactGrip(
                    0.85f,
                    HammerPlayerLocalPosition,
                    HammerPlayerLocalEuler,
                    HammerEnemyLocalPosition,
                    HammerEnemyLocalEuler,
                    "Weapons/Imported/Hammer(level8)l/source/Sledgehammer/Sledge hammer");
            case 9:
                return CreateExactGrip(
                    0.70f,
                    AxePlayerLocalPosition,
                    AxePlayerLocalEuler,
                    AxeEnemyLocalPosition,
                    AxeEnemyLocalEuler,
                    "Weapons/Imported/axe(level9)/source/axe");
            case 10:
                return CreatePlayerMatchedGrip(
                    1.40f,
                    PolePlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    "Weapons/Imported/Spear(level10)/source/Spear/Spear");
            case 11:
                return CreatePlayerMatchedGrip(
                    1.00f,
                    LongPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    "Weapons/Imported/nailed-plank(level11)/source/NailedPlank/NailedPlank");
            case 12:
                return CreateExactGrip(
                    0.40f,
                    SawPlayerLocalPosition,
                    SawPlayerLocalEuler,
                    SawEnemyLocalPosition,
                    SawEnemyLocalEuler,
                    "Weapons/Imported/saw(level12)/source/extracted/saw_low");
            case 13:
                return CreateShortGrip(0.35f,
                    "Weapons/Imported/sickle(level13)/source/Sickle");
            case 14:
                return CreateMediumGrip(0.50f,
                    "Weapons/Imported/medieval(level14)/source/Medieval_morgenstern_low2 scene");
            case 15:
                return CreateExactGrip(
                    0.60f,
                    MediumPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    MediumEnemyLocalPosition,
                    Level15EnemyLocalEuler,
                    "Weapons/Imported/l3fte(level15)/source/L3FT_E");
            case 16:
                return CreateExactGrip(
                    0.90f,
                    DefaultPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    DefaultEnemyLocalPosition,
                    Level16EnemyLocalEuler,
                    "Weapons/Imported/shield(level16)/source/RiotShield/Riot Shield");
            default:
                // No fallback FBX — primitive knife generated in code.
                return Create(0.30f);
        }
    }

    public static Vector3 GetEnemySocketLocalEuler(int level)
    {
        switch (Mathf.Clamp(level, 1, 16))
        {
            case 3:
            case 10:
                return PoleEnemySocketLocalEuler;
            case 8:
                return HammerEnemySocketLocalEuler;
            default:
                return Vector3.zero;
        }
    }

    public static void ApplyRuntimeOverrides(int level, GameObject sourcePrefab, GameObject weaponRoot)
    {
        if (sourcePrefab == null || weaponRoot == null)
            return;

        switch (Mathf.Clamp(level, 1, 16))
        {
            case 12:
                if (sourcePrefab.name.IndexOf("saw", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    ApplyBlackSawMaterialOverride(weaponRoot);
                break;
        }
    }

    public static bool UsesRuntimeGripAnchor(int level, GameObject sourcePrefab)
    {
        return Mathf.Clamp(level, 1, 16) == 12
            && sourcePrefab != null
            && sourcePrefab.name.IndexOf("saw", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static Transform GetOrCreateRuntimeGripAnchor(int level, GameObject sourcePrefab, Transform weaponSocket)
    {
        if (!UsesRuntimeGripAnchor(level, sourcePrefab) || weaponSocket == null)
            return weaponSocket;

        Transform gripAnchor = weaponSocket.Find(SawRuntimeSocketAnchorName);
        if (gripAnchor == null)
        {
            GameObject gripObject = new GameObject(SawRuntimeSocketAnchorName);
            gripAnchor = gripObject.transform;
            gripAnchor.SetParent(weaponSocket, worldPositionStays: false);
        }

        gripAnchor.localPosition = Vector3.zero;
        gripAnchor.localRotation = Quaternion.identity;
        gripAnchor.localScale = Vector3.one;
        return gripAnchor;
    }

    public static bool ApplyRuntimeGripPose(int level, GameObject sourcePrefab, Transform weaponRoot)
    {
        if (!UsesRuntimeGripAnchor(level, sourcePrefab) || weaponRoot == null)
            return false;

        weaponRoot.localRotation = Quaternion.Euler(SawRuntimeMeshLocalEuler);
        weaponRoot.localPosition = Vector3.zero;

        Vector3 gripPoint = TryGetSawRuntimeGripPoint(weaponRoot, out Vector3 computedGripPoint)
            ? computedGripPoint
            : SawRuntimeFallbackGripPoint;

        Transform handleAnchor = GetOrCreateSawRuntimeHandleAnchor(weaponRoot);
        handleAnchor.localPosition = gripPoint;
        handleAnchor.localRotation = Quaternion.identity;
        handleAnchor.localScale = Vector3.one;

        Vector3 scaledGripPoint = Vector3.Scale(gripPoint, weaponRoot.localScale);
        weaponRoot.localPosition = -(weaponRoot.localRotation * scaledGripPoint);
        return true;
    }

    private static Transform GetOrCreateSawRuntimeHandleAnchor(Transform weaponRoot)
    {
        Transform handleAnchor = weaponRoot.Find(SawRuntimeHandleAnchorName);
        if (handleAnchor == null)
        {
            GameObject handleAnchorObject = new GameObject(SawRuntimeHandleAnchorName);
            handleAnchor = handleAnchorObject.transform;
            handleAnchor.SetParent(weaponRoot, worldPositionStays: false);
        }

        return handleAnchor;
    }

    private static bool TryGetSawRuntimeGripPoint(Transform weaponRoot, out Vector3 gripPoint)
    {
        gripPoint = SawRuntimeFallbackGripPoint;

        if (!TryGetCombinedLocalBounds(weaponRoot, out Bounds localBounds))
            return false;

        gripPoint = new Vector3(
            Mathf.Lerp(localBounds.min.x, localBounds.max.x, SawHandleGripXNormalized),
            Mathf.Lerp(localBounds.min.y, localBounds.max.y, SawHandleGripYNormalized),
            Mathf.Lerp(localBounds.min.z, localBounds.max.z, SawHandleGripZNormalized));
        return true;
    }

    private static bool TryGetCombinedLocalBounds(Transform root, out Bounds combinedBounds)
    {
        combinedBounds = new Bounds();
        if (root == null)
            return false;

        bool hasBounds = false;
        Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            EncapsulateTransformedBounds(
                ref combinedBounds,
                ref hasBounds,
                meshFilter.sharedMesh.bounds,
                rootWorldToLocal * meshFilter.transform.localToWorldMatrix);
        }

        SkinnedMeshRenderer[] skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedMeshes.Length; i++)
        {
            SkinnedMeshRenderer skinnedMesh = skinnedMeshes[i];
            if (skinnedMesh == null)
                continue;

            Bounds sourceBounds = skinnedMesh.localBounds;
            if (sourceBounds.size.sqrMagnitude <= 0f && skinnedMesh.sharedMesh != null)
                sourceBounds = skinnedMesh.sharedMesh.bounds;

            if (sourceBounds.size.sqrMagnitude <= 0f)
                continue;

            EncapsulateTransformedBounds(
                ref combinedBounds,
                ref hasBounds,
                sourceBounds,
                rootWorldToLocal * skinnedMesh.transform.localToWorldMatrix);
        }

        return hasBounds;
    }

    private static void EncapsulateTransformedBounds(
        ref Bounds combinedBounds,
        ref bool hasBounds,
        Bounds sourceBounds,
        Matrix4x4 localToRoot)
    {
        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;

        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 point = localToRoot.MultiplyPoint3x4(corners[i]);
            if (!hasBounds)
            {
                combinedBounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(point);
            }
        }
    }

    private static WeaponLoadout Create(float targetSize, params string[] resourcePaths)
    {
        return Create(
            targetSize,
            DefaultPlayerLocalPosition,
            DefaultPlayerLocalEuler,
            DefaultEnemyLocalPosition,
            DefaultEnemyLocalEuler,
            resourcePaths);
    }

    private static WeaponLoadout CreateShortGrip(float targetSize, params string[] resourcePaths)
    {
        return Create(
            targetSize,
            DefaultPlayerLocalPosition,
            DefaultPlayerLocalEuler,
            DefaultEnemyLocalPosition,
            DefaultEnemyLocalEuler,
            resourcePaths);
    }

    private static WeaponLoadout CreateMediumGrip(float targetSize, params string[] resourcePaths)
    {
        return Create(
            targetSize,
            MediumPlayerLocalPosition,
            DefaultPlayerLocalEuler,
            MediumEnemyLocalPosition,
            DefaultEnemyLocalEuler,
            resourcePaths);
    }

    private static WeaponLoadout CreateLongGrip(float targetSize, params string[] resourcePaths)
    {
        return Create(
            targetSize,
            LongPlayerLocalPosition,
            DefaultPlayerLocalEuler,
            LongEnemyLocalPosition,
            DefaultEnemyLocalEuler,
            resourcePaths);
    }

    private static WeaponLoadout CreatePoleGrip(float targetSize, params string[] resourcePaths)
    {
        return Create(
            targetSize,
            PolePlayerLocalPosition,
            DefaultPlayerLocalEuler,
            PoleEnemyLocalPosition,
            DefaultEnemyLocalEuler,
            resourcePaths);
    }

    private static WeaponLoadout CreatePlayerMatchedGrip(
        float targetSize,
        Vector3 localPosition,
        Vector3 localEuler,
        params string[] resourcePaths)
    {
        return Create(
            targetSize,
            localPosition,
            localEuler,
            localPosition,
            localEuler,
            resourcePaths);
    }

    private static WeaponLoadout CreateExactGrip(
        float targetSize,
        Vector3 playerLocalPosition,
        Vector3 playerLocalEuler,
        Vector3 enemyLocalPosition,
        Vector3 enemyLocalEuler,
        params string[] resourcePaths)
    {
        return Create(
            targetSize,
            playerLocalPosition,
            playerLocalEuler,
            enemyLocalPosition,
            enemyLocalEuler,
            resourcePaths);
    }

    private static WeaponLoadout Create(
        float targetSize,
        Vector3 playerLocalPosition,
        Vector3 playerLocalEuler,
        Vector3 enemyLocalPosition,
        Vector3 enemyLocalEuler,
        params string[] resourcePaths)
    {
        return new WeaponLoadout(
            resourcePaths,
            targetSize,
            playerLocalPosition,
            playerLocalEuler,
            enemyLocalPosition,
            enemyLocalEuler);
    }

    private static void ApplyBlackSawMaterialOverride(GameObject weaponRoot)
    {
        Color sawBlack = new Color(0.045f, 0.045f, 0.045f, 1f);
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (fallbackShader == null)
            return;

        Renderer[] renderers = weaponRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.materials;
            if (materials == null || materials.Length == 0)
                materials = new[] { new Material(fallbackShader) };

            for (int j = 0; j < materials.Length; j++)
            {
                Material source = materials[j];
                Material material = source != null ? new Material(source) : new Material(fallbackShader);

                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", sawBlack);
                if (material.HasProperty("_Color")) material.SetColor("_Color", sawBlack);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.12f);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);

                materials[j] = material;
            }

            renderer.materials = materials;
        }
    }

}
