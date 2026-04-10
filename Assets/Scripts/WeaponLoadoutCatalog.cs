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
                return CreateShortGrip(0.40f,
                    "Weapons/Imported/Wrench(level6)/source/PipeWrenchUnreal");
            case 7:
                return CreateMediumGrip(0.55f,
                    "Weapons/Imported/crowbar(level7)/source/CrowbarV2");
            case 8:
                return CreatePoleGrip(0.85f,
                    "Weapons/Imported/Hammer(level8)l/source/Sledgehammer/Sledge hammer");
            case 9:
                return CreateLongGrip(0.70f,
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
