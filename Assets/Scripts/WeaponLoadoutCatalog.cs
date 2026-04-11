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
    private static readonly Vector3 MediumPlayerLocalPosition = new Vector3(-0.03f, -0.005f, 0f);
    private static readonly Vector3 MediumEnemyLocalPosition = new Vector3(-0.025f, -0.0025f, 0f);
    private static readonly Vector3 LongPlayerLocalPosition = new Vector3(-0.045f, -0.005f, 0f);
    private static readonly Vector3 LongEnemyLocalPosition = new Vector3(-0.035f, -0.0025f, 0f);
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
    // Level 12 saw: the imported mesh keeps the blade on negative X and the
    // actual hand grip on the positive-X handle shell. With the project-wide
    // Z=90 one-handed convention, that handle pocket maps to large +X / -Y
    // socket offsets, so a generic short-grip preset lands the palm on the
    // blade body instead of the usable handle.
    private static readonly Vector3 SawPlayerLocalPosition = new Vector3(0.246f, -0.270f, 0.002f);
    private static readonly Vector3 SawPlayerLocalEuler = new Vector3(0f, 0f, 90f);
    private static readonly Vector3 SawEnemyLocalPosition = new Vector3(0.244f, -0.264f, 0.002f);
    private static readonly Vector3 SawEnemyLocalEuler = new Vector3(0f, 0f, 90f);
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
                return CreateShortGrip(0.32f,
                    "Weapons/Imported/tactical-knife(level1)/source/TacticalKnife/Tactical Knife");
            case 2:
                return CreatePlayerMatchedGrip(
                    0.95f,
                    LongPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    "Weapons/Imported/Katana(level2)/source/Katana_low",
                    "Weapons/Imported/Katana(level2)/source/melee");
            case 3:
                return CreatePlayerMatchedGrip(
                    1.00f,
                    PolePlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    "Weapons/Imported/shovel(level3)/source/Shovel/Shovel");
            case 4:
                return CreatePlayerMatchedGrip(
                    0.85f,
                    LongPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
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
                return CreatePlayerMatchedGrip(
                    0.60f,
                    MediumPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
                    "Weapons/Imported/l3fte(level15)/source/L3FT_E");
            case 16:
                return CreatePlayerMatchedGrip(
                    0.90f,
                    DefaultPlayerLocalPosition,
                    DefaultPlayerLocalEuler,
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
