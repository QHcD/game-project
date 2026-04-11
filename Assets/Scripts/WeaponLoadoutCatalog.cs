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
                return CreateLongGrip(0.95f,
                    "Weapons/Imported/Katana(level2)/source/Katana_low",
                    "Weapons/Imported/Katana(level2)/source/melee");
            case 3:
                return CreatePoleGrip(1.00f,
                    "Weapons/Imported/shovel(level3)/source/Shovel/Shovel");
            case 4:
                return CreateLongGrip(0.85f,
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
                return CreateExactGrip(
                    0.55f,
                    CrowbarPlayerLocalPosition,
                    CrowbarPlayerLocalEuler,
                    CrowbarEnemyLocalPosition,
                    CrowbarEnemyLocalEuler,
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
                return CreatePoleGrip(1.40f,
                    "Weapons/Imported/Spear(level10)/source/Spear/Spear");
            case 11:
                return CreateLongGrip(1.00f,
                    "Weapons/Imported/nailed-plank(level11)/source/NailedPlank/NailedPlank");
            case 12:
                return CreateShortGrip(0.40f,
                    "Weapons/Imported/saw(level12)/source/extracted/saw_low");
            case 13:
                return CreateShortGrip(0.35f,
                    "Weapons/Imported/sickle(level13)/source/Sickle");
            case 14:
                return CreateMediumGrip(0.50f,
                    "Weapons/Imported/medieval(level14)/source/Medieval_morgenstern_low2 scene");
            case 15:
                return CreateMediumGrip(0.60f,
                    "Weapons/Imported/l3fte(level15)/source/L3FT_E");
            case 16:
                return Create(0.90f,
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
}
