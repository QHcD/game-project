using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LevelBuilder : MonoBehaviour
{
    private const float ArenaRadius = 26f;
    private const int ArenaSegments = 20;

    private enum ArenaTheme
    {
        BlacksiteFacility,
        CyberRuinsNeon,
        ContainerPortYard
    }

    private void Start()
    {
        SetupManagers();
        BuildArena();
        SetupPlayer();
        SetupEnemies();
        SetupCameras();
        SetupMinimap();
        SetupLighting();
    }

    private ArenaTheme GetTheme()
    {
        if (GameManager.Instance == null)
        {
            return ArenaTheme.BlacksiteFacility;
        }

        switch (GameManager.Instance.GetSelectedMap())
        {
            case GameManager.ArenaMap.CyberRuinsNeon:
                return ArenaTheme.CyberRuinsNeon;
            case GameManager.ArenaMap.ContainerPortYard:
                return ArenaTheme.ContainerPortYard;
            default:
                return ArenaTheme.BlacksiteFacility;
        }
    }

    private void BuildArena()
    {
        GameObject existingRoot = GameObject.Find("ArenaRoot");
        if (existingRoot != null)
        {
            Destroy(existingRoot);
        }

        GameObject arenaRoot = new GameObject("ArenaRoot");

        BuildGround(arenaRoot.transform);
        BuildBoundary(arenaRoot.transform);
        BuildInnerCover(arenaRoot.transform);
        BuildThemeProps(arenaRoot.transform);
    }

    private void BuildGround(Transform root)
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Ground";
        }

        ground.transform.SetParent(root, false);
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.rotation = Quaternion.identity;
        ground.transform.localScale = new Vector3(ArenaRadius * 2.2f, 0.5f, ArenaRadius * 2.2f);

        SetLitColor(ground, GetGroundColor(GetTheme()));

        GameObject centerDisk = CreateArenaProp(root, "ArenaCenterDisk", PrimitiveType.Cylinder,
            new Vector3(0f, -0.44f, 0f), new Vector3(2.4f, 0.18f, 2.4f), GetLaneColor(GetTheme()));
        centerDisk.transform.rotation = Quaternion.identity;

        GameObject ringDisk = CreateArenaProp(root, "ArenaRingDisk", PrimitiveType.Cylinder,
            new Vector3(0f, -0.46f, 0f), new Vector3(4.6f, 0.08f, 4.6f), new Color(0.32f, 0.36f, 0.42f));
        ringDisk.transform.rotation = Quaternion.identity;
    }

    private void BuildBoundary(Transform root)
    {
        ArenaTheme theme = GetTheme();
        Color wallColor   = GetWallColor(theme);
        Color pillarColor = GetPlatformColor(theme);
        Color accentColor = GetAccentColor(theme);

        for (int i = 0; i < ArenaSegments; i++)
        {
            float angle = (Mathf.PI * 2f / ArenaSegments) * i;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // Main wall segment — taller and thicker
            GameObject wallSegment = CreateArenaProp(root, "ArenaWall_" + i, PrimitiveType.Cube,
                direction * ArenaRadius + Vector3.up * 3.2f,
                new Vector3(7.2f, 6.4f, 1.6f), wallColor);
            wallSegment.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Wall top cap / parapet
            GameObject wallTop = CreateArenaProp(root, "ArenaWallTop_" + i, PrimitiveType.Cube,
                direction * ArenaRadius + Vector3.up * 6.6f,
                new Vector3(7.4f, 0.5f, 1.9f), pillarColor);
            wallTop.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Accent strip at mid-height (every other segment gets theme color)
            if (i % 2 == 0)
            {
                GameObject accent = CreateArenaProp(root, "ArenaWallAccent_" + i, PrimitiveType.Cube,
                    direction * (ArenaRadius + 0.05f) + Vector3.up * 4.8f,
                    new Vector3(6.8f, 0.22f, 0.12f), accentColor);
                accent.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            // Pillar between segments
            GameObject pillar = CreateArenaProp(root, "ArenaPillar_" + i, PrimitiveType.Cylinder,
                direction * (ArenaRadius - 1.5f) + Vector3.up * 3.4f,
                new Vector3(0.85f, 3.4f, 0.85f), pillarColor);
            pillar.transform.rotation = Quaternion.identity;
        }
    }

    private void BuildInnerCover(Transform root)
    {
        ArenaTheme theme = GetTheme();
        Color coverColor = GetCoverColor(theme);
        Color accentColor = GetAccentColor(theme);

        // 8 main cover blocks — vary height and depth for gameplay interest
        float[] heights = { 2.3f, 1.4f, 2.8f, 1.6f, 2.3f, 1.4f, 2.8f, 1.6f };
        float[] depths  = { 2.2f, 3.0f, 1.8f, 2.8f, 2.2f, 3.0f, 1.8f, 2.8f };
        for (int i = 0; i < 8; i++)
        {
            float angle = (Mathf.PI * 2f / 8f) * i;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float radius = i % 2 == 0 ? 11f : 14f;
            float h = heights[i];

            GameObject cover = CreateArenaProp(root, "ArenaCover_" + i, PrimitiveType.Cube,
                direction * radius + Vector3.up * (h * 0.5f),
                new Vector3(4.2f, h, depths[i]), coverColor);
            cover.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Accent stripe on top of tall covers
            if (h > 2f)
            {
                GameObject stripe = CreateArenaProp(root, "CoverStripe_" + i, PrimitiveType.Cube,
                    direction * radius + Vector3.up * (h + 0.1f),
                    new Vector3(4.2f, 0.18f, depths[i] + 0.05f), accentColor);
                stripe.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        // Central T-wall structure — more interesting than 2 plain blocks
        CreateArenaProp(root, "CentralBlock_A", PrimitiveType.Cube,
            new Vector3(-4.5f, 1.35f, 0f), new Vector3(2.8f, 2.7f, 9.5f), new Color(0.27f, 0.30f, 0.36f));
        CreateArenaProp(root, "CentralBlock_B", PrimitiveType.Cube,
            new Vector3(4.5f, 1.35f, 0f), new Vector3(2.8f, 2.7f, 9.5f), new Color(0.27f, 0.30f, 0.36f));
        // Connecting crossbar
        CreateArenaProp(root, "CentralBlock_Cross", PrimitiveType.Cube,
            new Vector3(0f, 2.6f, 0f), new Vector3(11.8f, 0.5f, 2.0f), new Color(0.22f, 0.25f, 0.30f));
    }

    private void BuildThemeProps(Transform root)
    {
        ArenaTheme theme = GetTheme();

        if (theme == ArenaTheme.BlacksiteFacility)
        {
            BuildBlacksiteProps(root);
        }
        else if (theme == ArenaTheme.CyberRuinsNeon)
        {
            BuildCyberRuinsProps(root);
        }
        else
        {
            BuildContainerPortProps(root);
        }
    }

    // ─── BLACKSITE FACILITY ────────────────────────────────────────────────────

    private void BuildBlacksiteProps(Transform root)
    {
        // Wall accent beams
        CreateArenaProp(root, "AccentN", PrimitiveType.Cube,
            new Vector3(0f, 5.0f, 18f), new Vector3(12f, 0.4f, 1.4f), new Color(0.88f, 0.22f, 0.20f));
        CreateArenaProp(root, "AccentS", PrimitiveType.Cube,
            new Vector3(0f, 5.0f, -18f), new Vector3(12f, 0.4f, 1.4f), new Color(0.22f, 0.72f, 1.0f));
        CreateArenaProp(root, "AccentE", PrimitiveType.Cube,
            new Vector3(18f, 5.0f, 0f), new Vector3(1.4f, 0.4f, 12f), new Color(0.88f, 0.22f, 0.20f));
        CreateArenaProp(root, "AccentW", PrimitiveType.Cube,
            new Vector3(-18f, 5.0f, 0f), new Vector3(1.4f, 0.4f, 12f), new Color(0.22f, 0.72f, 1.0f));

        // Destroyed vehicles
        BuildDestroyedCar(root, "Wreck_NW", new Vector3(-11f, 0f, 9f), 22f);
        BuildDestroyedCar(root, "Wreck_SE", new Vector3(13f, 0f, -7f), -38f);
        BuildDestroyedCar(root, "Wreck_NE", new Vector3(10f, 0f, 12f), 65f);

        // Concrete barriers / sandbag positions
        BuildBarricade(root, "Barricade_A", new Vector3(7f, 0f, 16f), 18f);
        BuildBarricade(root, "Barricade_B", new Vector3(-14f, 0f, -11f), -24f);
        BuildBarricade(root, "Barricade_C", new Vector3(-7f, 0f, -16f), 5f);
        BuildBarricade(root, "Barricade_D", new Vector3(15f, 0f, 6f), -72f);

        // Military crate stacks
        BuildCrateStack(root, "Crates_NE", new Vector3(17f, 0f, 12f), 15f, 3);
        BuildCrateStack(root, "Crates_SW", new Vector3(-17f, 0f, -13f), -30f, 2);
        BuildCrateStack(root, "Crates_NW", new Vector3(-16f, 0f, 10f), 50f, 3);

        // Watchtower stubs at cardinal points (partial towers on boundary inside)
        BuildWatchtower(root, "Tower_N", new Vector3(0f, 0f, 22f));
        BuildWatchtower(root, "Tower_S", new Vector3(0f, 0f, -22f));

        // Floor warning stripes
        CreateArenaProp(root, "FloorStripe_N", PrimitiveType.Cube,
            new Vector3(0f, 0.02f, 10f), new Vector3(8f, 0.06f, 0.5f), new Color(0.95f, 0.75f, 0.05f));
        CreateArenaProp(root, "FloorStripe_S", PrimitiveType.Cube,
            new Vector3(0f, 0.02f, -10f), new Vector3(8f, 0.06f, 0.5f), new Color(0.95f, 0.75f, 0.05f));
        CreateArenaProp(root, "FloorStripe_E", PrimitiveType.Cube,
            new Vector3(10f, 0.02f, 0f), new Vector3(0.5f, 0.06f, 8f), new Color(0.95f, 0.75f, 0.05f));
        CreateArenaProp(root, "FloorStripe_W", PrimitiveType.Cube,
            new Vector3(-10f, 0.02f, 0f), new Vector3(0.5f, 0.06f, 8f), new Color(0.95f, 0.75f, 0.05f));

        // Ventilation units on ground
        BuildVentUnit(root, "Vent_A", new Vector3(19f, 0f, 7f), 0f);
        BuildVentUnit(root, "Vent_B", new Vector3(-20f, 0f, -8f), 90f);

        // Extra destroyed vehicles for realism
        BuildDestroyedCar(root, "Wreck_SW", new Vector3(-13f, 0f, -14f), 142f);
        BuildDestroyedCar(root, "Wreck_Center", new Vector3(3f, 0f, -4f), -12f);

        // Scattered oil barrels
        BuildOilBarrelCluster(root, "Barrels_NE", new Vector3(16f, 0f, 15f));
        BuildOilBarrelCluster(root, "Barrels_SW", new Vector3(-15f, 0f, -17f));
        BuildOilBarrelCluster(root, "Barrels_E", new Vector3(21f, 0f, -3f));

        // Concrete debris piles
        BuildDebrisPile(root, "Debris_NW", new Vector3(-9f, 0f, 16f));
        BuildDebrisPile(root, "Debris_SE", new Vector3(8f, 0f, -18f));
        BuildDebrisPile(root, "Debris_Center", new Vector3(-3f, 0f, 5f));

        // Sandbag walls
        BuildSandbagWall(root, "Sandbags_N", new Vector3(5f, 0f, 12f), 0f);
        BuildSandbagWall(root, "Sandbags_S", new Vector3(-6f, 0f, -13f), 90f);

        // Razor wire barricades
        CreateArenaProp(root, "Wire_A", PrimitiveType.Cylinder,
            new Vector3(15f, 0.35f, -3f), new Vector3(0.7f, 0.35f, 0.7f), new Color(0.35f, 0.32f, 0.30f));
        CreateArenaProp(root, "Wire_B", PrimitiveType.Cylinder,
            new Vector3(-16f, 0.35f, 4f), new Vector3(0.7f, 0.35f, 0.7f), new Color(0.35f, 0.32f, 0.30f));
    }

    private void BuildCrateStack(Transform root, string name, Vector3 pos, float rot, int layers)
    {
        GameObject stack = new GameObject(name);
        stack.transform.SetParent(root, false);
        stack.transform.position = pos;
        stack.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        Color base1 = new Color(0.48f, 0.42f, 0.30f);
        Color base2 = new Color(0.38f, 0.34f, 0.26f);
        for (int row = 0; row < layers; row++)
        {
            int cols = layers - row;
            for (int c = 0; c < cols; c++)
            {
                CreatePrimitiveChild(stack.transform, "Crate_" + row + "_" + c, PrimitiveType.Cube,
                    new Vector3((c - cols * 0.5f + 0.5f) * 1.05f, 0.55f + row * 1.05f, 0f),
                    new Vector3(1.0f, 1.0f, 1.0f),
                    row % 2 == 0 ? base1 : base2);
            }
        }
    }

    private void BuildWatchtower(Transform root, string name, Vector3 pos)
    {
        GameObject tower = new GameObject(name);
        tower.transform.SetParent(root, false);
        tower.transform.position = pos;

        // Legs
        for (int i = 0; i < 4; i++)
        {
            float lx = i < 2 ? -0.7f : 0.7f;
            float lz = i % 2 == 0 ? -0.7f : 0.7f;
            CreatePrimitiveChild(tower.transform, "Leg_" + i, PrimitiveType.Cylinder,
                new Vector3(lx, 2.5f, lz), new Vector3(0.22f, 2.5f, 0.22f),
                new Color(0.22f, 0.22f, 0.26f));
        }

        // Platform
        CreatePrimitiveChild(tower.transform, "Platform", PrimitiveType.Cube,
            new Vector3(0f, 5.2f, 0f), new Vector3(3.2f, 0.3f, 3.2f),
            new Color(0.30f, 0.30f, 0.34f));

        // Railing
        CreatePrimitiveChild(tower.transform, "Rail_F", PrimitiveType.Cube,
            new Vector3(0f, 5.7f, 1.6f), new Vector3(3.0f, 0.6f, 0.12f),
            new Color(0.18f, 0.18f, 0.22f));
        CreatePrimitiveChild(tower.transform, "Rail_B", PrimitiveType.Cube,
            new Vector3(0f, 5.7f, -1.6f), new Vector3(3.0f, 0.6f, 0.12f),
            new Color(0.18f, 0.18f, 0.22f));
    }

    private void BuildVentUnit(Transform root, string name, Vector3 pos, float rot)
    {
        GameObject vent = new GameObject(name);
        vent.transform.SetParent(root, false);
        vent.transform.position = pos;
        vent.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        CreatePrimitiveChild(vent.transform, "Box", PrimitiveType.Cube,
            new Vector3(0f, 0.6f, 0f), new Vector3(2.4f, 1.2f, 1.4f),
            new Color(0.28f, 0.30f, 0.34f));
        CreatePrimitiveChild(vent.transform, "Fan", PrimitiveType.Cylinder,
            new Vector3(0f, 1.25f, 0f), new Vector3(0.9f, 0.08f, 0.9f),
            new Color(0.18f, 0.20f, 0.24f));
    }

    // ─── SHARED PROP BUILDERS ─────────────────────────────────────────────────

    private void BuildOilBarrelCluster(Transform root, string name, Vector3 pos)
    {
        GameObject cluster = new GameObject(name);
        cluster.transform.SetParent(root, false);
        cluster.transform.position = pos;

        Color[] colors = {
            new Color(0.62f, 0.14f, 0.10f),
            new Color(0.20f, 0.24f, 0.28f),
            new Color(0.45f, 0.42f, 0.10f),
            new Color(0.55f, 0.18f, 0.14f)
        };
        Vector3[] offsets = {
            new Vector3(-0.55f, 0f, 0.3f),
            new Vector3(0.55f, 0f, -0.2f),
            new Vector3(0f, 0f, -0.65f),
            new Vector3(0.1f, 0f, 0.65f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            CreatePrimitiveChild(cluster.transform, "Barrel_" + i, PrimitiveType.Cylinder,
                offsets[i] + Vector3.up * 0.55f, new Vector3(0.5f, 0.55f, 0.5f), colors[i]);
        }
        // One tipped barrel
        CreatePrimitiveChild(cluster.transform, "Barrel_tipped", PrimitiveType.Cylinder,
            new Vector3(1.2f, 0.25f, 0.4f), new Vector3(0.5f, 0.55f, 0.5f), colors[0],
            new Vector3(0f, 0f, 70f));
    }

    private void BuildDebrisPile(Transform root, string name, Vector3 pos)
    {
        GameObject pile = new GameObject(name);
        pile.transform.SetParent(root, false);
        pile.transform.position = pos;

        Color concreteLight = new Color(0.52f, 0.50f, 0.48f);
        Color concreteDark = new Color(0.35f, 0.33f, 0.32f);

        CreatePrimitiveChild(pile.transform, "Slab_A", PrimitiveType.Cube,
            new Vector3(0f, 0.15f, 0f), new Vector3(2.4f, 0.3f, 1.6f), concreteLight);
        CreatePrimitiveChild(pile.transform, "Slab_B", PrimitiveType.Cube,
            new Vector3(0.6f, 0.4f, 0.3f), new Vector3(1.6f, 0.25f, 1.0f), concreteDark,
            new Vector3(0f, 25f, 8f));
        CreatePrimitiveChild(pile.transform, "Chunk_A", PrimitiveType.Cube,
            new Vector3(-0.8f, 0.25f, -0.5f), new Vector3(0.7f, 0.5f, 0.6f), concreteDark,
            new Vector3(0f, 42f, 0f));
        CreatePrimitiveChild(pile.transform, "Chunk_B", PrimitiveType.Cube,
            new Vector3(1.3f, 0.2f, -0.4f), new Vector3(0.5f, 0.4f, 0.5f), concreteLight,
            new Vector3(12f, -15f, 0f));
        // Rebar sticking out
        CreatePrimitiveChild(pile.transform, "Rebar", PrimitiveType.Cylinder,
            new Vector3(0.2f, 0.6f, 0.1f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.45f, 0.30f, 0.22f), new Vector3(0f, 0f, 32f));
    }

    private void BuildSandbagWall(Transform root, string name, Vector3 pos, float rot)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(root, false);
        wall.transform.position = pos;
        wall.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        Color sandbag = new Color(0.60f, 0.55f, 0.40f);
        // Bottom row
        for (int i = -2; i <= 2; i++)
        {
            CreatePrimitiveChild(wall.transform, "Bag_B" + i, PrimitiveType.Cube,
                new Vector3(i * 0.65f, 0.2f, 0f), new Vector3(0.6f, 0.35f, 0.55f), sandbag);
        }
        // Top row (staggered)
        for (int i = -1; i <= 1; i++)
        {
            CreatePrimitiveChild(wall.transform, "Bag_T" + i, PrimitiveType.Cube,
                new Vector3(i * 0.65f, 0.55f, 0f), new Vector3(0.6f, 0.35f, 0.55f), sandbag);
        }
    }

    private void BuildTyreStack(Transform root, string name, Vector3 pos)
    {
        GameObject stack = new GameObject(name);
        stack.transform.SetParent(root, false);
        stack.transform.position = pos;

        Color rubber = new Color(0.12f, 0.12f, 0.14f);
        for (int row = 0; row < 3; row++)
        {
            int count = 3 - row;
            for (int c = 0; c < count; c++)
            {
                float x = (c - count * 0.5f + 0.5f) * 0.7f;
                CreatePrimitiveChild(stack.transform, "Tyre_" + row + "_" + c, PrimitiveType.Cylinder,
                    new Vector3(x, 0.18f + row * 0.36f, 0f), new Vector3(0.6f, 0.18f, 0.6f), rubber);
            }
        }
    }

    // ─── CYBER RUINS NEON ─────────────────────────────────────────────────────

    private void BuildCyberRuinsProps(Transform root)
    {
        Color neonMagenta = new Color(1f, 0.18f, 0.82f);
        Color neonCyan    = new Color(0.12f, 0.90f, 1f);
        Color ruinGrey    = new Color(0.20f, 0.22f, 0.28f);
        Color darkSlate   = new Color(0.12f, 0.14f, 0.20f);

        // Wall accent neon strips
        CreateArenaProp(root, "NeonE_H", PrimitiveType.Cube,
            new Vector3(18f, 4.6f, 0f), new Vector3(1.2f, 0.32f, 12f), neonMagenta);
        CreateArenaProp(root, "NeonW_H", PrimitiveType.Cube,
            new Vector3(-18f, 4.6f, 0f), new Vector3(1.2f, 0.32f, 12f), neonCyan);
        CreateArenaProp(root, "NeonN_H", PrimitiveType.Cube,
            new Vector3(0f, 4.6f, 18f), new Vector3(12f, 0.32f, 1.2f), neonCyan);
        CreateArenaProp(root, "NeonS_H", PrimitiveType.Cube,
            new Vector3(0f, 4.6f, -18f), new Vector3(12f, 0.32f, 1.2f), neonMagenta);

        // Neon floor grid lines
        for (int i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            CreateArenaProp(root, "GridH_" + i, PrimitiveType.Cube,
                new Vector3(i * 5f, 0.025f, 0f), new Vector3(0.12f, 0.05f, 40f),
                i < 0 ? neonCyan : neonMagenta);
            CreateArenaProp(root, "GridV_" + i, PrimitiveType.Cube,
                new Vector3(0f, 0.025f, i * 5f), new Vector3(40f, 0.05f, 0.12f),
                i < 0 ? neonMagenta : neonCyan);
        }

        // Ruined building walls
        BuildRuinWall(root, "Ruin_NE", new Vector3(14f, 0f, 10f), 30f, ruinGrey, neonMagenta);
        BuildRuinWall(root, "Ruin_SW", new Vector3(-14f, 0f, -10f), 210f, ruinGrey, neonCyan);
        BuildRuinWall(root, "Ruin_NW", new Vector3(-10f, 0f, 14f), 120f, ruinGrey, neonMagenta);
        BuildRuinWall(root, "Ruin_SE", new Vector3(10f, 0f, -14f), -45f, ruinGrey, neonCyan);

        // Holo-pillars (glowing cylinders)
        Vector3[] pillarPositions = {
            new Vector3(7f, 0f, 7f), new Vector3(-7f, 0f, 7f),
            new Vector3(7f, 0f, -7f), new Vector3(-7f, 0f, -7f)
        };
        Color[] pillarColors = { neonMagenta, neonCyan, neonCyan, neonMagenta };
        for (int i = 0; i < pillarPositions.Length; i++)
        {
            CreateArenaProp(root, "HoloPillar_" + i, PrimitiveType.Cylinder,
                pillarPositions[i] + Vector3.up * 2.2f,
                new Vector3(0.28f, 2.2f, 0.28f), pillarColors[i]);
            // Glowing base ring
            CreateArenaProp(root, "HoloPillarBase_" + i, PrimitiveType.Cylinder,
                pillarPositions[i] + Vector3.up * 0.06f,
                new Vector3(0.9f, 0.06f, 0.9f), pillarColors[i]);
        }

        // Elevated cyber platforms
        BuildCyberPlatform(root, "Platform_N", new Vector3(0f, 0f, 16f), neonCyan);
        BuildCyberPlatform(root, "Platform_S", new Vector3(0f, 0f, -16f), neonMagenta);

        // Data obelisks
        BuildDataObelisk(root, "Obelisk_E", new Vector3(18f, 0f, -8f), darkSlate, neonCyan);
        BuildDataObelisk(root, "Obelisk_W", new Vector3(-18f, 0f, 9f), darkSlate, neonMagenta);

        // Rubble clusters
        BuildRubbleCluster(root, "Rubble_A", new Vector3(15f, 0f, -15f), ruinGrey);
        BuildRubbleCluster(root, "Rubble_B", new Vector3(-15f, 0f, 15f), ruinGrey);
        BuildRubbleCluster(root, "Rubble_C", new Vector3(5f, 0f, -18f), ruinGrey);
        BuildRubbleCluster(root, "Rubble_D", new Vector3(-5f, 0f, 18f), ruinGrey);

        // Toppled server racks (scattered debris)
        CreateArenaProp(root, "ServerRack_A", PrimitiveType.Cube,
            new Vector3(18f, 0.4f, 5f), new Vector3(1.0f, 0.8f, 2.4f), darkSlate);
        CreateArenaProp(root, "ServerRack_B", PrimitiveType.Cube,
            new Vector3(-17f, 0.3f, -6f), new Vector3(0.8f, 0.6f, 2.0f), darkSlate);

        // Broken neon tubes on ground
        CreateArenaProp(root, "BrokenNeon_A", PrimitiveType.Cube,
            new Vector3(8f, 0.05f, 15f), new Vector3(3.5f, 0.08f, 0.12f), neonMagenta);
        CreateArenaProp(root, "BrokenNeon_B", PrimitiveType.Cube,
            new Vector3(-12f, 0.05f, -5f), new Vector3(2.8f, 0.08f, 0.12f), neonCyan);

        // Wrecked hover-car shells
        BuildDestroyedCar(root, "HoverWreck_A", new Vector3(12f, 0f, 17f), 55f);
        BuildDestroyedCar(root, "HoverWreck_B", new Vector3(-16f, 0f, -16f), -30f);

        // Scattered barrel-like energy cells
        BuildOilBarrelCluster(root, "EnergyCells_A", new Vector3(20f, 0f, 12f));
        BuildOilBarrelCluster(root, "EnergyCells_B", new Vector3(-19f, 0f, -14f));
    }

    private void BuildRuinWall(Transform root, string name, Vector3 pos, float rot, Color wallColor, Color neonColor)
    {
        GameObject ruin = new GameObject(name);
        ruin.transform.SetParent(root, false);
        ruin.transform.position = pos;
        ruin.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        // Main wall section
        CreatePrimitiveChild(ruin.transform, "Wall", PrimitiveType.Cube,
            new Vector3(0f, 2.5f, 0f), new Vector3(6f, 5f, 0.8f), wallColor);
        // Broken top — offset to simulate damage
        CreatePrimitiveChild(ruin.transform, "WallTop", PrimitiveType.Cube,
            new Vector3(1.8f, 5.5f, 0f), new Vector3(2.4f, 1.0f, 0.8f), wallColor);
        // Neon strip on wall
        CreatePrimitiveChild(ruin.transform, "NeonStrip", PrimitiveType.Cube,
            new Vector3(0f, 1.2f, -0.45f), new Vector3(5.5f, 0.18f, 0.1f), neonColor);
    }

    private void BuildCyberPlatform(Transform root, string name, Vector3 pos, Color neonColor)
    {
        Color platformColor = new Color(0.16f, 0.18f, 0.26f);
        GameObject plat = new GameObject(name);
        plat.transform.SetParent(root, false);
        plat.transform.position = pos;

        CreatePrimitiveChild(plat.transform, "Surface", PrimitiveType.Cube,
            new Vector3(0f, 1.0f, 0f), new Vector3(6f, 0.28f, 3.5f), platformColor);
        // Support legs
        CreatePrimitiveChild(plat.transform, "SupportL", PrimitiveType.Cube,
            new Vector3(-2.5f, 0.5f, 0f), new Vector3(0.3f, 1.0f, 3.5f), platformColor);
        CreatePrimitiveChild(plat.transform, "SupportR", PrimitiveType.Cube,
            new Vector3(2.5f, 0.5f, 0f), new Vector3(0.3f, 1.0f, 3.5f), platformColor);
        // Neon edge
        CreatePrimitiveChild(plat.transform, "EdgeFront", PrimitiveType.Cube,
            new Vector3(0f, 1.15f, 1.76f), new Vector3(6.1f, 0.08f, 0.08f), neonColor);
        CreatePrimitiveChild(plat.transform, "EdgeBack", PrimitiveType.Cube,
            new Vector3(0f, 1.15f, -1.76f), new Vector3(6.1f, 0.08f, 0.08f), neonColor);
    }

    private void BuildDataObelisk(Transform root, string name, Vector3 pos, Color bodyColor, Color neonColor)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        obj.transform.position = pos;

        CreatePrimitiveChild(obj.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 3f, 0f), new Vector3(1.2f, 6f, 1.2f), bodyColor);
        CreatePrimitiveChild(obj.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 6.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), neonColor);
        // Screen panels
        CreatePrimitiveChild(obj.transform, "Screen_F", PrimitiveType.Cube,
            new Vector3(0f, 2.5f, -0.62f), new Vector3(0.9f, 1.8f, 0.05f), neonColor);
        CreatePrimitiveChild(obj.transform, "Screen_B", PrimitiveType.Cube,
            new Vector3(0f, 2.5f, 0.62f), new Vector3(0.9f, 1.8f, 0.05f), neonColor);
    }

    private void BuildRubbleCluster(Transform root, string name, Vector3 pos, Color color)
    {
        GameObject cluster = new GameObject(name);
        cluster.transform.SetParent(root, false);
        cluster.transform.position = pos;

        float[] rots = { 0f, 34f, 72f, 110f };
        Vector3[] offsets = { Vector3.zero, new Vector3(1.2f, 0f, 0.4f), new Vector3(-0.8f, 0f, 0.9f), new Vector3(0.5f, 0f, -0.8f) };
        Vector3[] scales = { new Vector3(1.8f, 0.5f, 1.2f), new Vector3(1.0f, 0.8f, 0.7f), new Vector3(1.4f, 0.4f, 0.9f), new Vector3(0.6f, 0.6f, 0.6f) };

        for (int i = 0; i < offsets.Length; i++)
        {
            CreatePrimitiveChild(cluster.transform, "Chunk_" + i, PrimitiveType.Cube,
                offsets[i] + Vector3.up * (scales[i].y * 0.5f),
                scales[i],
                new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f),
                new Vector3(0f, rots[i], 0f));
        }
    }

    // ─── CONTAINER PORT YARD ──────────────────────────────────────────────────

    private void BuildContainerPortProps(Transform root)
    {
        // Container stacks
        BuildContainerStack(root, "Stack_NE", new Vector3(12f, 0f, 12f),  20f, new Color(0.72f, 0.32f, 0.16f), 2);
        BuildContainerStack(root, "Stack_NW", new Vector3(-12f, 0f, 12f), 340f, new Color(0.18f, 0.44f, 0.60f), 3);
        BuildContainerStack(root, "Stack_SE", new Vector3(12f, 0f, -12f), 160f, new Color(0.50f, 0.52f, 0.18f), 2);
        BuildContainerStack(root, "Stack_SW", new Vector3(-12f, 0f, -12f), 200f, new Color(0.70f, 0.65f, 0.15f), 3);
        BuildContainerStack(root, "Stack_E",  new Vector3(20f, 0f, 0f),    90f, new Color(0.58f, 0.25f, 0.22f), 2);
        BuildContainerStack(root, "Stack_W",  new Vector3(-20f, 0f, 0f),  270f, new Color(0.22f, 0.48f, 0.36f), 2);

        // Crane structure (north side)
        BuildCrane(root, "Crane_Main", new Vector3(0f, 0f, 20f));

        // Dock bollards along southern edge
        for (int i = -3; i <= 3; i++)
        {
            CreateArenaProp(root, "Bollard_" + i, PrimitiveType.Cylinder,
                new Vector3(i * 4.5f, 0.55f, -21f), new Vector3(0.4f, 1.1f, 0.4f),
                new Color(0.22f, 0.22f, 0.24f));
        }

        // Oil drum clusters
        BuildDrumCluster(root, "Drums_E", new Vector3(20f, 0f, -10f));
        BuildDrumCluster(root, "Drums_W", new Vector3(-20f, 0f, 8f));

        // Pallet stacks
        BuildPalletStack(root, "Pallets_A", new Vector3(16f, 0f, -18f), 15f);
        BuildPalletStack(root, "Pallets_B", new Vector3(-16f, 0f, 16f), -30f);

        // Floor paint lines (dock markings)
        CreateArenaProp(root, "DockLine_H", PrimitiveType.Cube,
            new Vector3(0f, 0.02f, -8f), new Vector3(30f, 0.05f, 0.45f), new Color(0.95f, 0.80f, 0.10f));
        CreateArenaProp(root, "DockLine_V", PrimitiveType.Cube,
            new Vector3(-8f, 0.02f, 0f), new Vector3(0.45f, 0.05f, 30f), new Color(0.95f, 0.80f, 0.10f));

        // Fuel tank
        BuildFuelTank(root, "FuelTank", new Vector3(-19f, 0f, -14f), 45f);

        // Warehouse wall sections
        BuildWarehouseSection(root, "Warehouse_N", new Vector3(0f, 0f, 22f), 0f);

        // Extra oil drum clusters for realism
        BuildDrumCluster(root, "Drums_NE", new Vector3(16f, 0f, 14f));
        BuildDrumCluster(root, "Drums_SW", new Vector3(-15f, 0f, -15f));

        // Scattered debris / broken pallets
        BuildDebrisPile(root, "Debris_E", new Vector3(18f, 0f, 5f));
        BuildDebrisPile(root, "Debris_W", new Vector3(-18f, 0f, -5f));

        // Tyre stacks
        BuildTyreStack(root, "Tyres_NW", new Vector3(-14f, 0f, 18f));
        BuildTyreStack(root, "Tyres_SE", new Vector3(14f, 0f, -18f));

        // Loose containers (single, on ground, tilted for cover)
        CreateArenaProp(root, "LooseContainer_A", PrimitiveType.Cube,
            new Vector3(6f, 1.55f, 18f), new Vector3(7.0f, 3.1f, 3.2f), new Color(0.35f, 0.55f, 0.30f));
        CreateArenaProp(root, "LooseContainer_B", PrimitiveType.Cube,
            new Vector3(-6f, 1.55f, -19f), new Vector3(7.0f, 3.1f, 3.2f), new Color(0.60f, 0.28f, 0.18f));
    }

    private void BuildContainerStack(Transform root, string name, Vector3 pos, float rot, Color color, int layers)
    {
        GameObject stack = new GameObject(name);
        stack.transform.SetParent(root, false);
        stack.transform.position = pos;
        stack.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        for (int layer = 0; layer < layers; layer++)
        {
            Color c = new Color(
                Mathf.Clamp01(color.r + (layer % 2 == 0 ? 0f : 0.12f)),
                Mathf.Clamp01(color.g + (layer % 2 == 0 ? 0f : 0.10f)),
                Mathf.Clamp01(color.b + (layer % 2 == 0 ? 0f : 0.08f)));
            CreatePrimitiveChild(stack.transform, "Container_" + layer, PrimitiveType.Cube,
                new Vector3(0f, 1.55f + layer * 3.1f, 0f),
                new Vector3(7.0f, 3.1f, 3.2f), c);
        }
    }

    private void BuildCrane(Transform root, string name, Vector3 pos)
    {
        GameObject crane = new GameObject(name);
        crane.transform.SetParent(root, false);
        crane.transform.position = pos;

        Color steelDark = new Color(0.22f, 0.24f, 0.28f);
        Color steelMid  = new Color(0.32f, 0.34f, 0.40f);

        // Vertical tower
        CreatePrimitiveChild(crane.transform, "Tower", PrimitiveType.Cube,
            new Vector3(0f, 7f, 0f), new Vector3(1.4f, 14f, 1.4f), steelDark);
        // Horizontal boom
        CreatePrimitiveChild(crane.transform, "Boom", PrimitiveType.Cube,
            new Vector3(-7f, 13.5f, 0f), new Vector3(14f, 0.7f, 0.7f), steelMid);
        // Counter weight
        CreatePrimitiveChild(crane.transform, "CounterWeight", PrimitiveType.Cube,
            new Vector3(5f, 13.5f, 0f), new Vector3(2.5f, 1.5f, 1.5f), steelDark);
        // Cable
        CreatePrimitiveChild(crane.transform, "Cable", PrimitiveType.Cylinder,
            new Vector3(-7f, 9.5f, 0f), new Vector3(0.12f, 4f, 0.12f), steelDark);
        // Hook block
        CreatePrimitiveChild(crane.transform, "Hook", PrimitiveType.Cube,
            new Vector3(-7f, 5.2f, 0f), new Vector3(1.0f, 0.8f, 1.0f), steelMid);
        // Base feet
        CreatePrimitiveChild(crane.transform, "FootL", PrimitiveType.Cube,
            new Vector3(-2f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 3f), steelDark);
        CreatePrimitiveChild(crane.transform, "FootR", PrimitiveType.Cube,
            new Vector3(2f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 3f), steelDark);
    }

    private void BuildDrumCluster(Transform root, string name, Vector3 pos)
    {
        GameObject cluster = new GameObject(name);
        cluster.transform.SetParent(root, false);
        cluster.transform.position = pos;

        Color[] drumColors = {
            new Color(0.70f, 0.16f, 0.12f),
            new Color(0.22f, 0.28f, 0.32f),
            new Color(0.50f, 0.46f, 0.12f)
        };
        Vector3[] offsets = { new Vector3(-0.6f, 0f, 0f), new Vector3(0.6f, 0f, 0.2f), new Vector3(0f, 0f, -0.7f) };

        for (int i = 0; i < offsets.Length; i++)
        {
            CreatePrimitiveChild(cluster.transform, "Drum_" + i, PrimitiveType.Cylinder,
                offsets[i] + Vector3.up * 0.6f, new Vector3(0.55f, 0.6f, 0.55f), drumColors[i]);
        }
        // Second row on top
        CreatePrimitiveChild(cluster.transform, "DrumTop", PrimitiveType.Cylinder,
            new Vector3(0f, 1.6f, -0.3f), new Vector3(0.55f, 0.6f, 0.55f), drumColors[0]);
    }

    private void BuildPalletStack(Transform root, string name, Vector3 pos, float rot)
    {
        GameObject stack = new GameObject(name);
        stack.transform.SetParent(root, false);
        stack.transform.position = pos;
        stack.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        Color palletColor = new Color(0.62f, 0.48f, 0.28f);
        for (int i = 0; i < 4; i++)
        {
            CreatePrimitiveChild(stack.transform, "Pallet_" + i, PrimitiveType.Cube,
                new Vector3(0f, 0.07f + i * 0.16f, 0f),
                new Vector3(2.4f, 0.12f, 1.6f), palletColor);
        }
    }

    private void BuildFuelTank(Transform root, string name, Vector3 pos, float rot)
    {
        GameObject tank = new GameObject(name);
        tank.transform.SetParent(root, false);
        tank.transform.position = pos;
        tank.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        Color tankColor = new Color(0.55f, 0.52f, 0.42f);
        // Horizontal tank body
        CreatePrimitiveChild(tank.transform, "Body", PrimitiveType.Cylinder,
            new Vector3(0f, 1.2f, 0f), new Vector3(1.8f, 2.2f, 1.8f), tankColor,
            new Vector3(90f, 0f, 0f));
        // End caps
        CreatePrimitiveChild(tank.transform, "CapA", PrimitiveType.Sphere,
            new Vector3(0f, 1.2f, 2.2f), new Vector3(1.8f, 1.8f, 0.6f), tankColor);
        CreatePrimitiveChild(tank.transform, "CapB", PrimitiveType.Sphere,
            new Vector3(0f, 1.2f, -2.2f), new Vector3(1.8f, 1.8f, 0.6f), tankColor);
        // Legs
        CreatePrimitiveChild(tank.transform, "LegL", PrimitiveType.Cube,
            new Vector3(-0.7f, 0.4f, 0f), new Vector3(0.25f, 0.8f, 4.5f),
            new Color(0.30f, 0.30f, 0.32f));
        CreatePrimitiveChild(tank.transform, "LegR", PrimitiveType.Cube,
            new Vector3(0.7f, 0.4f, 0f), new Vector3(0.25f, 0.8f, 4.5f),
            new Color(0.30f, 0.30f, 0.32f));
    }

    private void BuildWarehouseSection(Transform root, string name, Vector3 pos, float rot)
    {
        GameObject wh = new GameObject(name);
        wh.transform.SetParent(root, false);
        wh.transform.position = pos;
        wh.transform.rotation = Quaternion.Euler(0f, rot, 0f);

        Color wallColor = new Color(0.42f, 0.38f, 0.32f);
        Color roofColor = new Color(0.32f, 0.28f, 0.24f);

        CreatePrimitiveChild(wh.transform, "WallL", PrimitiveType.Cube,
            new Vector3(-5f, 2.5f, 0f), new Vector3(1.0f, 5.0f, 6f), wallColor);
        CreatePrimitiveChild(wh.transform, "WallR", PrimitiveType.Cube,
            new Vector3(5f, 2.5f, 0f), new Vector3(1.0f, 5.0f, 6f), wallColor);
        CreatePrimitiveChild(wh.transform, "Roof", PrimitiveType.Cube,
            new Vector3(0f, 5.2f, 0f), new Vector3(11f, 0.4f, 6.2f), roofColor);
    }

    private void BuildDestroyedCar(Transform root, string name, Vector3 position, float yRotation)
    {
        GameObject carRoot = new GameObject(name);
        carRoot.transform.SetParent(root, false);
        carRoot.transform.position = position;
        carRoot.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        CreatePrimitiveChild(carRoot.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 0.55f, 0f), new Vector3(2.4f, 0.7f, 1.2f), new Color(0.26f, 0.23f, 0.22f));
        CreatePrimitiveChild(carRoot.transform, "Cabin", PrimitiveType.Cube,
            new Vector3(-0.2f, 1.05f, 0f), new Vector3(1.15f, 0.55f, 1.05f), new Color(0.35f, 0.34f, 0.36f));
        CreatePrimitiveChild(carRoot.transform, "Hood", PrimitiveType.Cube,
            new Vector3(0.95f, 0.82f, 0f), new Vector3(0.8f, 0.22f, 1.0f), new Color(0.42f, 0.18f, 0.16f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_FL", PrimitiveType.Cylinder,
            new Vector3(0.8f, 0.3f, 0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_FR", PrimitiveType.Cylinder,
            new Vector3(0.8f, 0.3f, -0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_BL", PrimitiveType.Cylinder,
            new Vector3(-0.8f, 0.3f, 0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
        CreatePrimitiveChild(carRoot.transform, "Wheel_BR", PrimitiveType.Cylinder,
            new Vector3(-0.8f, 0.3f, -0.58f), new Vector3(0.38f, 0.18f, 0.38f), new Color(0.05f, 0.05f, 0.07f), new Vector3(90f, 0f, 0f));
    }

    private void BuildBarricade(Transform root, string name, Vector3 position, float yRotation)
    {
        GameObject barricade = new GameObject(name);
        barricade.transform.SetParent(root, false);
        barricade.transform.position = position;
        barricade.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        CreatePrimitiveChild(barricade.transform, "Base", PrimitiveType.Cube,
            new Vector3(0f, 0.35f, 0f), new Vector3(2.4f, 0.5f, 0.5f), new Color(0.54f, 0.50f, 0.42f));
        CreatePrimitiveChild(barricade.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 0.95f, 0f), new Vector3(2.0f, 0.22f, 0.42f), new Color(0.80f, 0.64f, 0.18f));
    }

    private void SetupPlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player == null)
        {
            player = SpawnFirstPersonPlayer();
        }

        if (player == null)
        {
            return;
        }

        player.tag = "Player";
        player.transform.localScale = Vector3.one;

        // Disable CharacterController before teleporting — it blocks transform.position changes
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // Spawn safely on the arena floor and away from the raised cover blocks
        player.transform.position = new Vector3(0f, 0.1f, -8.5f);
        player.transform.rotation = Quaternion.identity;

        if (controller == null)
        {
            controller = player.AddComponent<CharacterController>();
        }

        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.04f;
        controller.slopeLimit = 22f;
        controller.skinWidth = 0.03f;
        controller.minMoveDistance = 0f;
        controller.enabled = true;

        if (player.GetComponent<PlayerHealth>() == null)
        {
            player.AddComponent<PlayerHealth>();
        }

        foreach (Animator animator in player.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.arenaBoundaryRadius = ArenaRadius - 2.8f;
        }
    }

    private void SetupCameras()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player == null)
        {
            return;
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        playerController.cam = playerCamera;
        playerController.firstPersonCam = playerCamera;
        playerController.thirdPersonCam = null;
    }

    private void SetupMinimap()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        GameObject minimapCameraObject = GameObject.Find("MinimapCamera");
        if (minimapCameraObject == null)
        {
            minimapCameraObject = new GameObject("MinimapCamera");
        }

        Camera minimapCamera = minimapCameraObject.GetComponent<Camera>();
        if (minimapCamera == null)
        {
            minimapCamera = minimapCameraObject.AddComponent<Camera>();
        }

        MinimapCameraFollow minimapFollow = minimapCameraObject.GetComponent<MinimapCameraFollow>();
        if (minimapFollow == null)
        {
            minimapFollow = minimapCameraObject.AddComponent<MinimapCameraFollow>();
        }

        minimapFollow.target = player.transform;
        minimapFollow.height = 28f;
        minimapFollow.orthographicSize = 16f;   // tighter view so movement is obvious
        minimapFollow.offset = Vector3.zero;
        minimapFollow.lockToArenaCenter = false; // follows the player
        minimapFollow.EnsureRenderTexture();
    }

    private void SetupManagers()
    {
        GameObject gameManager = GameObject.Find("GameManager");
        if (gameManager == null)
        {
            gameManager = new GameObject("GameManager");
        }

        if (gameManager.GetComponent<GameManager>() == null)
        {
            gameManager.AddComponent<GameManager>();
        }

        GameObject hud = GameObject.Find("HUDManager");
        if (hud == null)
        {
            hud = new GameObject("HUDManager");
        }

        if (hud.GetComponent<HUDManager>() == null)
        {
            hud.AddComponent<HUDManager>();
        }

        if (hud.GetComponent<PauseMenuController>() == null)
        {
            hud.AddComponent<PauseMenuController>();
        }
    }

    private void SetupEnemies()
    {
        GameObject existingRoot = GameObject.Find("EnemyRoot");
        if (existingRoot != null)
        {
            Destroy(existingRoot);
        }

        GameObject enemyRoot = new GameObject("EnemyRoot");
        int enemyCount = GameManager.Instance != null ? Mathf.Max(8, GameManager.Instance.GetEnemyCount()) : 10;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPosition = player != null ? player.transform.position : new Vector3(0f, 0.1f, -8.5f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.enemiesRemaining = enemyCount;
        }

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPoint = GetEnemySpawnPoint(i, enemyCount, playerPosition);
            CreateEnemy(enemyRoot.transform, i, spawnPoint);
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateEnemyCount(enemyCount);
        }
    }

    private Vector3 GetEnemySpawnPoint(int index, int enemyCount, Vector3 playerPosition)
    {
        const float minPlayerDistance = 11.5f;
        const float closeRingRadius = 15.5f;
        const float farRingRadius = 20.5f;
        const float angleOffset = 0.35f;

        float baseAngle = (Mathf.PI * 2f / enemyCount) * index;
        float[] angleCandidates =
        {
            baseAngle + angleOffset,
            baseAngle - angleOffset,
            baseAngle + angleOffset + Mathf.PI,
            baseAngle - angleOffset + Mathf.PI
        };
        float[] radiusCandidates =
        {
            index % 2 == 0 ? closeRingRadius : farRingRadius,
            index % 2 == 0 ? farRingRadius : closeRingRadius
        };

        for (int radiusIndex = 0; radiusIndex < radiusCandidates.Length; radiusIndex++)
        {
            for (int angleIndex = 0; angleIndex < angleCandidates.Length; angleIndex++)
            {
                float radius = radiusCandidates[radiusIndex];
                float angle = angleCandidates[angleIndex];
                Vector3 candidate = new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle)) * radius;
                if (Vector3.Distance(candidate, playerPosition) >= minPlayerDistance)
                {
                    return candidate;
                }
            }
        }

        Vector3 fallbackDirection = (-new Vector3(playerPosition.x, 0f, playerPosition.z)).normalized;
        if (fallbackDirection.sqrMagnitude < 0.001f)
        {
            fallbackDirection = Vector3.forward;
        }

        return new Vector3(fallbackDirection.x, 0.1f, fallbackDirection.z) * farRingRadius;
    }

    private void CreateEnemy(Transform root, int index, Vector3 position)
    {
        GameObject enemy = new GameObject("Enemy_" + index);
        enemy.transform.SetParent(root, false);
        // Place on arena floor directly — GetGroundPosition can hit overhead platforms
        enemy.transform.position = new Vector3(position.x, 0.1f, position.z);

        CapsuleCollider collider = enemy.AddComponent<CapsuleCollider>();
        collider.height = 1.9f;
        collider.radius = 0.38f;
        collider.center = new Vector3(0f, 0.95f, 0f);

        Rigidbody body = enemy.AddComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.mass = 55f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        PrototypeEnemy prototypeEnemy = enemy.AddComponent<PrototypeEnemy>();
        prototypeEnemy.maxHealth = 90;

        BuildEnemyVisual(enemy.transform);
    }

    private void BuildEnemyVisual(Transform root)
    {
        GameObject knightPrefab = Resources.Load<GameObject>("ThirdPersonKnight/Paladin WProp J Nordstrom");
        if (knightPrefab != null)
        {
            GameObject visual = Instantiate(knightPrefab, root);
            visual.name = "EnemyVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);

            Animator importedAnimator = visual.GetComponentInChildren<Animator>(true);
            if (importedAnimator != null)
            {
                CharacterVisualAnimationPlayer animationPlayer = visual.AddComponent<CharacterVisualAnimationPlayer>();
                animationPlayer.Setup(importedAnimator,
                    Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/SwordIdle"),
                    Resources.Load<AnimationClip>("ThirdPersonKnight/Animations/Attack1"));
            }

            visual.AddComponent<CharacterVisualGrounder>();
            visual.AddComponent<CharacterVisualBob>();

            // Tint enemy knight red so it's visually hostile / distinct from player
            TintRenderers(visual, new Color(0.85f, 0.35f, 0.30f));

            return;
        }

        CreatePrimitiveChild(root, "Torso", PrimitiveType.Capsule,
            new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 1.0f, 0.72f), new Color(0.46f, 0.18f, 0.18f));
        CreatePrimitiveChild(root, "Head", PrimitiveType.Sphere,
            new Vector3(0f, 1.82f, 0f), new Vector3(0.42f, 0.42f, 0.42f), new Color(0.86f, 0.76f, 0.66f));
        CreatePrimitiveChild(root, "LeftArm", PrimitiveType.Cylinder,
            new Vector3(-0.52f, 1.08f, 0f), new Vector3(0.16f, 0.52f, 0.16f), new Color(0.30f, 0.12f, 0.12f), new Vector3(0f, 0f, 26f));
        CreatePrimitiveChild(root, "RightArm", PrimitiveType.Cylinder,
            new Vector3(0.52f, 1.08f, 0f), new Vector3(0.16f, 0.52f, 0.16f), new Color(0.30f, 0.12f, 0.12f), new Vector3(0f, 0f, -26f));
        CreatePrimitiveChild(root, "LeftLeg", PrimitiveType.Cylinder,
            new Vector3(-0.18f, 0.3f, 0f), new Vector3(0.18f, 0.62f, 0.18f), new Color(0.08f, 0.08f, 0.10f));
        CreatePrimitiveChild(root, "RightLeg", PrimitiveType.Cylinder,
            new Vector3(0.18f, 0.3f, 0f), new Vector3(0.18f, 0.62f, 0.18f), new Color(0.08f, 0.08f, 0.10f));
    }

    private GameObject SpawnFirstPersonPlayer()
    {
        GameObject playerPrefab = Resources.Load<GameObject>("FirstPersonMelee/Player");
        if (playerPrefab == null)
        {
            return null;
        }

        GameObject player = Instantiate(playerPrefab);
        player.name = "Player";
        return player;
    }

    private void SetupLighting()
    {
        ArenaTheme theme = GetTheme();

        GameObject lightObject = GameObject.Find("Directional Light");
        if (lightObject != null)
        {
            Light lightComponent = lightObject.GetComponent<Light>();
            if (lightComponent != null)
            {
                switch (theme)
                {
                    case ArenaTheme.CyberRuinsNeon:
                        lightObject.transform.rotation = Quaternion.Euler(28f, 60f, 0f);
                        lightComponent.intensity = 0.7f;
                        lightComponent.color = new Color(0.72f, 0.80f, 1.0f);
                        break;
                    case ArenaTheme.ContainerPortYard:
                        lightObject.transform.rotation = Quaternion.Euler(42f, -20f, 0f);
                        lightComponent.intensity = 1.5f;
                        lightComponent.color = new Color(1.0f, 0.96f, 0.88f);
                        break;
                    default: // BlacksiteFacility
                        lightObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
                        lightComponent.intensity = 1.3f;
                        lightComponent.color = new Color(0.92f, 0.95f, 1.0f);
                        break;
                }
            }
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        switch (theme)
        {
            case ArenaTheme.CyberRuinsNeon:
                RenderSettings.fogColor = new Color(0.08f, 0.06f, 0.16f);
                RenderSettings.fogDensity = 0.008f;
                RenderSettings.ambientLight = new Color(0.26f, 0.20f, 0.44f);
                break;
            case ArenaTheme.ContainerPortYard:
                RenderSettings.fogColor = new Color(0.52f, 0.58f, 0.62f);
                RenderSettings.fogDensity = 0.004f;
                RenderSettings.ambientLight = new Color(0.56f, 0.58f, 0.62f);
                break;
            default:
                RenderSettings.fogColor = new Color(0.10f, 0.12f, 0.16f);
                RenderSettings.fogDensity = 0.0065f;
                RenderSettings.ambientLight = new Color(0.48f, 0.54f, 0.62f);
                break;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    private GameObject CreateArenaProp(Transform root, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(root, false);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        SetLitColor(obj, color);
        return obj;
    }

    private GameObject CreatePrimitiveChild(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
    {
        return CreatePrimitiveChild(parent, name, type, localPosition, localScale, color, Vector3.zero);
    }

    private GameObject CreatePrimitiveChild(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color, Vector3 localRotationEuler)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.Euler(localRotationEuler);
        obj.transform.localScale = localScale;
        SetLitColor(obj, color);

        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        return obj;
    }

    private Vector3 GetGroundPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * 20f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return new Vector3(desiredPosition.x, 0f, desiredPosition.z);
    }

    private void TintRenderers(GameObject obj, Color tint)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null)
                {
                    mats[m].color = Color.Lerp(mats[m].color, tint, 0.55f);
                }
            }
            renderers[i].materials = mats;
        }
    }

    private void SetLitColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        renderer.material = mat;
    }

    private Color GetAccentColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.88f, 0.22f, 0.20f);
        if (theme == ArenaTheme.CyberRuinsNeon)   return new Color(1f, 0.18f, 0.82f);
        return new Color(0.95f, 0.80f, 0.10f);
    }

    private Color GetGroundColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.18f, 0.20f, 0.24f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.12f, 0.16f, 0.22f);
        return new Color(0.20f, 0.20f, 0.18f);
    }

    private Color GetLaneColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.32f, 0.36f, 0.42f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.20f, 0.28f, 0.40f);
        return new Color(0.34f, 0.28f, 0.22f);
    }

    private Color GetWallColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.10f, 0.11f, 0.14f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.11f, 0.12f, 0.18f);
        return new Color(0.22f, 0.20f, 0.18f);
    }

    private Color GetPlatformColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.26f, 0.30f, 0.36f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.24f, 0.22f, 0.34f);
        return new Color(0.36f, 0.28f, 0.20f);
    }

    private Color GetCoverColor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.BlacksiteFacility) return new Color(0.28f, 0.24f, 0.24f);
        if (theme == ArenaTheme.CyberRuinsNeon) return new Color(0.22f, 0.24f, 0.32f);
        return new Color(0.40f, 0.30f, 0.22f);
    }
}

public class MinimapCameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 32f;
    public float orthographicSize = 18f;
    public Vector3 offset = Vector3.zero;
    public bool lockToArenaCenter = true;

    private Camera minimapCamera;
    private RenderTexture minimapTexture;

    public RenderTexture MinimapTexture => minimapTexture;

    private void Awake()
    {
        minimapCamera = GetComponent<Camera>();
        if (minimapCamera == null)
        {
            minimapCamera = gameObject.AddComponent<Camera>();
        }

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthographicSize;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.74f, 0.66f, 0.48f, 1f);
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = 100f;
        minimapCamera.cullingMask = ~0;

        EnsureRenderTexture();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                return;
            }
        }

        Vector3 targetPosition = lockToArenaCenter ? offset : target.position + offset;
        transform.position = new Vector3(targetPosition.x, height, targetPosition.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void OnDestroy()
    {
        if (minimapTexture != null)
        {
            minimapTexture.Release();
            Destroy(minimapTexture);
        }
    }

    public RenderTexture EnsureRenderTexture()
    {
        if (minimapTexture == null)
        {
            minimapTexture = new RenderTexture(256, 256, 16)
            {
                name = "RuntimeMinimapTexture"
            };
        }

        if (minimapCamera != null)
        {
            minimapCamera.targetTexture = minimapTexture;
        }

        return minimapTexture;
    }
}

public class PauseMenuController : MonoBehaviour
{
    private GameObject pauseCanvas;
    private GameObject mainPanel;
    private GameObject optionsPanel;
    private GameObject settingsPanel;
    private bool isPaused;
    private TMP_FontAsset prismFont;

    private readonly float[] volumeLevels = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    private readonly string[] volumeLabels = { "MUTE", "25%", "50%", "75%", "100%" };
    private readonly string[] graphicsLabels = { "LOW", "MEDIUM", "HIGH" };

    private void Update()
    {
        HUDManager hudManager = HUDManager.Instance;
        if (hudManager != null && hudManager.IsMatchFinished)
        {
            return;
        }

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            ShowPauseMenu();
        }
    }

    private void ShowPauseMenu()
    {
        EnsureEventSystem();
        BuildPauseMenu();
        ShowPanel(mainPanel);

        Time.timeScale = 0f;
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseCanvas != null)
        {
            Destroy(pauseCanvas);
            pauseCanvas = null;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        GameManager.Instance?.ReplayCurrentLevel();
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        GameManager.Instance?.GoToMainMenu();
    }

    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildPauseMenu()
    {
        if (pauseCanvas != null)
        {
            Destroy(pauseCanvas);
        }

        prismFont = ResolvePrismFont();

        pauseCanvas = new GameObject("PauseCanvas");
        Canvas canvas = pauseCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = pauseCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        pauseCanvas.AddComponent<GraphicRaycaster>();

        Image overlay = new GameObject("PauseOverlay").AddComponent<Image>();
        overlay.transform.SetParent(pauseCanvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.color = new Color(0.02f, 0.02f, 0.06f, 0.72f);

        mainPanel = CreatePausePanel("PausePanel_Main", new Vector2(760f, 700f));
        optionsPanel = CreatePausePanel("PausePanel_Options", new Vector2(860f, 720f));
        settingsPanel = CreatePausePanel("PausePanel_Settings", new Vector2(860f, 720f));

        BuildMainPanel(mainPanel.transform);
        BuildOptionsPanel(optionsPanel.transform);
        BuildSettingsPanel(settingsPanel.transform);
    }

    private GameObject CreatePausePanel(string name, Vector2 size)
    {
        Image panel = new GameObject(name).AddComponent<Image>();
        panel.transform.SetParent(pauseCanvas.transform, false);
        panel.color = new Color(0.16f, 0.20f, 0.30f, 0.36f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = new Vector2(0f, -10f);

        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.26f, 0.42f, 0.68f, 0.22f);
        panelOutline.effectDistance = new Vector2(2f, -2f);
        return panel.gameObject;
    }

    private void BuildMainPanel(Transform parent)
    {
        CreateLabel(parent, "PAUSED", 62f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 250f), new Vector2(420f, 80f), true);
        CreateLabel(parent, "TAKE A BREATH. JUMP BACK IN WHEN YOU'RE READY.", 22f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 198f), new Vector2(620f, 36f), false);

        CreateButton(parent, "RESUME", new Vector2(0f, 92f), ResumeGame);
        CreateButton(parent, "RESTART", new Vector2(0f, 2f), RestartGame);
        CreateButton(parent, "OPTIONS", new Vector2(0f, -88f), () => ShowPanel(optionsPanel));
        CreateButton(parent, "SETTINGS", new Vector2(0f, -178f), () => ShowPanel(settingsPanel));
        CreateButton(parent, "QUIT", new Vector2(0f, -268f), QuitGame);
    }

    private void BuildOptionsPanel(Transform parent)
    {
        CreateLabel(parent, "OPTIONS", 56f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 258f), new Vector2(500f, 76f), true);
        CreateLabel(parent, "ADJUST THE CURRENT MATCH WITHOUT LEAVING GAMEPLAY.", 20f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 208f), new Vector2(620f, 34f), false);

        CreateCycleRow(parent, "DIFFICULTY", new Vector2(0f, 92f), GetDifficultyLabel, CycleDifficulty);
        CreateCycleRow(parent, "CAMERA VIEW", new Vector2(0f, 4f), GetPerspectiveLabel, CyclePerspective);
        CreateCycleRow(parent, "MOVE STYLE", new Vector2(0f, -84f), GetMovementLabel, CycleMovement);

        CreateButton(parent, "RETURN", new Vector2(0f, -238f), () => ShowPanel(mainPanel));
    }

    private void BuildSettingsPanel(Transform parent)
    {
        CreateLabel(parent, "SETTINGS", 56f, new Color(0.78f, 0.84f, 1f, 1f), new Vector2(0f, 258f), new Vector2(500f, 76f), true);
        CreateLabel(parent, "TUNE DISPLAY AND AUDIO, THEN DROP RIGHT BACK INTO THE MATCH.", 20f, new Color(0.72f, 0.84f, 1f, 0.88f), new Vector2(0f, 208f), new Vector2(690f, 34f), false);

        CreateCycleRow(parent, "MASTER VOL", new Vector2(0f, 92f), GetMasterVolumeLabel, CycleMasterVolume);
        CreateCycleRow(parent, "GRAPHICS", new Vector2(0f, 4f), GetGraphicsLabel, CycleGraphics);
        CreateCycleRow(parent, "FULLSCREEN", new Vector2(0f, -84f), GetFullscreenLabel, ToggleFullscreen);

        CreateButton(parent, "RETURN", new Vector2(0f, -238f), () => ShowPanel(mainPanel));
    }

    private void CreateCycleRow(Transform parent, string label, Vector2 position, System.Func<string> getValue, UnityEngine.Events.UnityAction onCycle)
    {
        GameObject row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(700f, 64f);
        rowRect.anchoredPosition = position;

        CreateLabel(row.transform, label, 26f, Color.white, new Vector2(-190f, 0f), new Vector2(320f, 42f), false, TextAlignmentOptions.MidlineRight);
        CreateButton(row.transform, getValue(), new Vector2(148f, 0f), onCycle, new Vector2(320f, 60f), getValue);
    }

    private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        CreateButton(parent, text, position, action, new Vector2(300f, 72f), null);
    }

    private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action, Vector2 size, System.Func<string> dynamicText)
    {
        Image buttonImage = new GameObject("Btn_" + text).AddComponent<Image>();
        buttonImage.transform.SetParent(parent, false);
        buttonImage.color = Color.white;

        RectTransform rect = buttonImage.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.38f, 0.30f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);

        TextMeshProUGUI label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(buttonImage.transform, false);
        label.text = dynamicText != null ? dynamicText() : text;
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.10f, 0.10f, 0.14f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        if (prismFont != null)
        {
            label.font = prismFont;
        }

        RectTransform labelRect = label.GetComponent<RectTransform>();
        Stretch(labelRect);

        if (dynamicText != null)
        {
            PauseDynamicLabel dynamicLabel = buttonImage.gameObject.AddComponent<PauseDynamicLabel>();
            dynamicLabel.label = label;
            dynamicLabel.getText = dynamicText;
        }
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size, bool bold)
    {
        CreateLabel(parent, text, fontSize, color, position, size, bold, TextAlignmentOptions.Center);
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size, bool bold, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = new GameObject("Txt_" + text).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        if (prismFont != null)
        {
            label.font = prismFont;
        }

        if (bold)
        {
            label.fontStyle = FontStyles.Bold;
        }

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void ShowPanel(GameObject targetPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(targetPanel == mainPanel);
        if (optionsPanel != null) optionsPanel.SetActive(targetPanel == optionsPanel);
        if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);
    }

    private string GetDifficultyLabel()
    {
        return (GameManager.Instance != null ? GameManager.Instance.difficulty : PlayerPrefs.GetString("Difficulty", "Normal")).ToUpperInvariant();
    }

    private void CycleDifficulty()
    {
        string current = GameManager.Instance != null ? GameManager.Instance.difficulty : PlayerPrefs.GetString("Difficulty", "Normal");
        string next = current == "Easy" ? "Normal" : current == "Normal" ? "Hard" : "Easy";
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDifficulty(next);
        }
        else
        {
            PlayerPrefs.SetString("Difficulty", next);
            PlayerPrefs.Save();
        }
    }

    private string GetPerspectiveLabel()
    {
        GameManager.PerspectiveMode mode = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : (GameManager.PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", 0), 0, 1);
        return mode == GameManager.PerspectiveMode.ThirdPerson ? "THIRD PERSON" : "FIRST PERSON";
    }

    private void CyclePerspective()
    {
        GameManager.PerspectiveMode current = GameManager.Instance != null
            ? GameManager.Instance.GetPerspectiveMode()
            : (GameManager.PerspectiveMode)Mathf.Clamp(PlayerPrefs.GetInt("PerspectiveMode", 0), 0, 1);
        GameManager.PerspectiveMode next = current == GameManager.PerspectiveMode.FirstPerson
            ? GameManager.PerspectiveMode.ThirdPerson
            : GameManager.PerspectiveMode.FirstPerson;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPerspectiveMode(next);
        }
        else
        {
            PlayerPrefs.SetInt("PerspectiveMode", (int)next);
            PlayerPrefs.Save();
        }

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.RefreshGameplayPreferences();
        }
    }

    private string GetMovementLabel()
    {
        GameManager.MovementScheme scheme = GameManager.Instance != null
            ? GameManager.Instance.GetMovementScheme()
            : (GameManager.MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
        return scheme == GameManager.MovementScheme.ArrowKeys ? "ARROWS + MOUSE" : "WASD + MOUSE";
    }

    private void CycleMovement()
    {
        GameManager.MovementScheme current = GameManager.Instance != null
            ? GameManager.Instance.GetMovementScheme()
            : (GameManager.MovementScheme)Mathf.Clamp(PlayerPrefs.GetInt("MovementScheme", 0), 0, 1);
        GameManager.MovementScheme next = current == GameManager.MovementScheme.Wasd
            ? GameManager.MovementScheme.ArrowKeys
            : GameManager.MovementScheme.Wasd;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMovementScheme(next);
        }
        else
        {
            PlayerPrefs.SetInt("MovementScheme", (int)next);
            PlayerPrefs.Save();
        }
    }

    private string GetMasterVolumeLabel()
    {
        float volume = PlayerPrefs.GetFloat("MasterVol", 0.8f);
        int index = 0;
        float smallestDifference = float.MaxValue;
        for (int i = 0; i < volumeLevels.Length; i++)
        {
            float difference = Mathf.Abs(volume - volumeLevels[i]);
            if (difference < smallestDifference)
            {
                smallestDifference = difference;
                index = i;
            }
        }

        return volumeLabels[index];
    }

    private void CycleMasterVolume()
    {
        float current = PlayerPrefs.GetFloat("MasterVol", 0.8f);
        int index = 0;
        for (int i = 0; i < volumeLevels.Length; i++)
        {
            if (Mathf.Abs(current - volumeLevels[i]) < 0.01f)
            {
                index = i;
                break;
            }
        }

        int nextIndex = (index + 1) % volumeLevels.Length;
        float next = volumeLevels[nextIndex];
        PlayerPrefs.SetFloat("MasterVol", next);
        PlayerPrefs.Save();
        AudioListener.volume = next;
    }

    private string GetGraphicsLabel()
    {
        int tier = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, graphicsLabels.Length - 1);
        return graphicsLabels[tier];
    }

    private void CycleGraphics()
    {
        int tier = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsTier", 2), 0, graphicsLabels.Length - 1);
        int nextTier = (tier + 1) % graphicsLabels.Length;
        PlayerPrefs.SetInt("GraphicsTier", nextTier);
        PlayerPrefs.Save();

        int qualityLevel = nextTier == 0 ? 0 :
            nextTier == 1 ? Mathf.Max(0, (QualitySettings.names.Length - 1) / 2) :
            Mathf.Max(0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel);
    }

    private string GetFullscreenLabel()
    {
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        return fullscreen ? "ON" : "OFF";
    }

    private void ToggleFullscreen()
    {
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        fullscreen = !fullscreen;
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        Screen.fullScreen = fullscreen;
    }

    private TMP_FontAsset ResolvePrismFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            TMP_FontAsset font = fonts[i];
            if (font != null && font.name.Contains("Azonix"))
            {
                return font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        isPaused = false;
    }
}

public class PauseDynamicLabel : MonoBehaviour
{
    public TextMeshProUGUI label;
    public System.Func<string> getText;

    private void Update()
    {
        if (label != null && getText != null)
        {
            label.text = getText();
        }
    }
}

public class PrototypeEnemy : Actor
{
    private const float ArenaRadius = 23.2f;
    private const float ArenaPadding = 0.75f;
    private const float ArenaFloorHeight = 0.1f;

    // --- Aggro system ---
    private const float AggroRadius = 12f;
    private const float DeaggroRadius = 16f;
    private const int MaxSimultaneousAttackers = 3;
    private static int currentAttackerCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { currentAttackerCount = 0; }

    public float moveSpeed = 2.2f;
    public float attackRange = 1.7f;
    public float attackDamage = 12f;
    public float attackCooldown = 1.6f;

    private Transform target;
    private float lastAttackTime;
    private float retargetTime;
    private float currentSpeed;
    private bool isAggro;
    private bool isActiveAttacker;

    // --- Hit stagger ---
    private float staggerTimer;
    private const float StaggerDuration = 0.6f;

    // --- Idle wander ---
    private Vector3 wanderTarget;
    private float wanderTimer;

    private void Start()
    {
        if (maxHealth <= 0)
        {
            maxHealth = 90;
        }

        currentHealth = maxHealth;
        if (GameManager.Instance != null)
        {
            moveSpeed = GameManager.Instance.GetEnemySpeed();
            attackDamage = GameManager.Instance.GetEnemyDamage();
        }
        currentSpeed = moveSpeed;

        // Stagger initial attack so enemies don't all hit at the same instant
        lastAttackTime = Time.time + Random.Range(0f, attackCooldown);
        wanderTarget = transform.position;
    }

    private void Update()
    {
        // Hit stagger — freeze in place when hit
        if (staggerTimer > 0f)
        {
            staggerTimer -= Time.deltaTime;
            return;
        }

        if (Time.time >= retargetTime || target == null)
        {
            target = FindBestTarget();
            retargetTime = Time.time + Random.Range(0.35f, 0.7f);
        }

        if (target == null)
        {
            Wander();
            ClampInsideArena();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // Aggro check — only chase if within aggro radius
        if (!isAggro && distance <= AggroRadius)
            isAggro = true;
        else if (isAggro && distance > DeaggroRadius)
        {
            isAggro = false;
            ReleaseAttackerSlot();
        }

        if (!isAggro)
        {
            Wander();
            ClampInsideArena();
            return;
        }

        // Face the player
        if (distance > 0.2f)
        {
            Vector3 direction = toTarget.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 540f * Time.deltaTime);

            if (distance > attackRange)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, 8f * Time.deltaTime);
                Vector3 destination = target.position - direction * (attackRange * 0.82f);
                transform.position = Vector3.MoveTowards(transform.position, destination, currentSpeed * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 14f * Time.deltaTime);
            }
        }

        ClampInsideArena();

        // Attack — only if attacker slot available
        if (distance <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            if (!isActiveAttacker && currentAttackerCount >= MaxSimultaneousAttackers)
                return;

            if (!isActiveAttacker)
            {
                isActiveAttacker = true;
                currentAttackerCount++;
            }

            lastAttackTime = Time.time;
            CharacterVisualAnimationPlayer visualAnimation = GetComponentInChildren<CharacterVisualAnimationPlayer>();
            visualAnimation?.PlayAttack();
            PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
            else
            {
                Actor actor = target.GetComponent<Actor>();
                if (actor != null && actor != this)
                {
                    actor.TakeDamage(Mathf.RoundToInt(attackDamage));
                }
            }
        }

        // Release slot if out of attack range for a while
        if (isActiveAttacker && distance > attackRange * 1.5f)
        {
            ReleaseAttackerSlot();
        }
    }

    public override void TakeDamage(int amount)
    {
        // Apply stagger and knockback BEFORE base call which may destroy us
        staggerTimer = StaggerDuration;
        isAggro = true;

        if (target != null)
        {
            Vector3 pushDir = (transform.position - target.position).normalized;
            pushDir.y = 0f;
            transform.position += pushDir * 0.5f;
        }

        base.TakeDamage(amount);
    }

    private void Wander()
    {
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(2f, 5f);
            wanderTarget = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            wanderTimer = Random.Range(2f, 4.5f);
        }

        float wanderSpeed = moveSpeed * 0.35f;
        transform.position = Vector3.MoveTowards(transform.position, new Vector3(wanderTarget.x, transform.position.y, wanderTarget.z), wanderSpeed * Time.deltaTime);

        Vector3 wanderDir = (wanderTarget - transform.position);
        wanderDir.y = 0f;
        if (wanderDir.sqrMagnitude > 0.1f)
        {
            Quaternion rot = Quaternion.LookRotation(wanderDir.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, 120f * Time.deltaTime);
        }
    }

    private Transform FindBestTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.transform;
        }

        return null;
    }

    private void ReleaseAttackerSlot()
    {
        if (isActiveAttacker)
        {
            isActiveAttacker = false;
            currentAttackerCount = Mathf.Max(0, currentAttackerCount - 1);
        }
    }

    private void ClampInsideArena()
    {
        Vector3 planarPosition = transform.position;
        planarPosition.y = 0f;

        float maxRadius = ArenaRadius - ArenaPadding;
        if (planarPosition.sqrMagnitude > maxRadius * maxRadius)
        {
            Vector3 clampedPlanar = planarPosition.normalized * maxRadius;
            transform.position = new Vector3(clampedPlanar.x, transform.position.y, clampedPlanar.z);
        }

        if (transform.position.y > ArenaFloorHeight + 0.35f || transform.position.y < ArenaFloorHeight - 0.05f)
        {
            transform.position = new Vector3(transform.position.x, ArenaFloorHeight, transform.position.z);
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
            }
        }
    }

    private void OnDestroy()
    {
        ReleaseAttackerSlot();
    }

    protected override void Death()
    {
        ReleaseAttackerSlot();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnemyKilled();
        }

        base.Death();
    }
}
