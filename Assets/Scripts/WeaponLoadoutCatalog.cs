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
    private static readonly Vector3 DefaultPlayerLocalPosition = new Vector3(0f, 0.08f, 0.02f);
    private static readonly Vector3 DefaultPlayerLocalEuler = new Vector3(0f, 0f, -90f);
    private static readonly Vector3 DefaultEnemyLocalPosition = new Vector3(0f, 0.05f, 0f);
    private static readonly Vector3 DefaultEnemyLocalEuler = new Vector3(-90f, 0f, 0f);

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
                return Create(0.32f,
                    "Weapons/Imported/tactical-knife(level1)/source/TacticalKnife/Tactical Knife");
            case 2:
                return Create(0.95f,
                    "Weapons/Imported/Katana(level2)/source/Katana_low",
                    "Weapons/Imported/Katana(level2)/source/melee");
            case 3:
                return Create(1.00f,
                    "Weapons/Imported/shovel(level3)/source/Shovel/Shovel");
            case 4:
                return Create(0.85f,
                    "Weapons/Imported/baseball-bat(level4)/source/baseball_bat_1k");
            case 5:
                return Create(0.30f,
                    "Weapons/Imported/nunchucks(level5)/Nunchucks");
            case 6:
                return Create(0.40f,
                    "Weapons/Imported/Wrench(level6)/source/PipeWrenchUnreal");
            case 7:
                return Create(0.55f,
                    "Weapons/Imported/crowbar(level7)/source/CrowbarV2");
            case 8:
                return Create(0.85f,
                    "Weapons/Imported/Hammer(level8)l/source/Sledgehammer/Sledge hammer");
            case 9:
                return Create(0.70f,
                    "Weapons/Imported/axe(level9)/source/axe");
            case 10:
                return Create(1.40f,
                    "Weapons/Imported/Spear(level10)/source/Spear/Spear");
            case 11:
                return Create(1.00f,
                    "Weapons/Imported/nailed-plank(level11)/source/NailedPlank/NailedPlank");
            case 12:
                return Create(0.40f,
                    "Weapons/Imported/saw(level12)/source/extracted/saw_low");
            case 13:
                return Create(0.35f,
                    "Weapons/Imported/sickle(level13)/source/Sickle");
            case 14:
                return Create(0.50f,
                    "Weapons/Imported/medieval(level14)/source/Medieval_morgenstern_low2 scene");
            case 15:
                return Create(0.60f,
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
