// Editor/SetupIndustrialMap.cs
// Auto-runs after every compile. Also available via:  PRISM-7 ▸ Setup Industrial Map
//
// Steps:
//  1. Opens Map_v1.unity additively
//  2. Force-activates every object & renderer
//  3. Converts ALL materials from Standard → URP/Lit (preserving every texture)
//  4. Saves converted materials as assets
//  5. Wraps map under one root "IndustrialMap"
//  6. Saves as prefab → MohamedAltajer/Prefabs/Environment/Resources/Maps/IndustrialMap/IndustrialMap.prefab
//  7. Removes old Map1/Map2 folders

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SetupIndustrialMap
{
    private const string IndustrialScenePath =
        "Assets/_Shared/Imports/RPG_FPS_game_assets_industrial/Map_v1.unity";

    private const string PrefabSavePath =
        "Assets/MohamedAltajer/Prefabs/Environment/Resources/Maps/IndustrialMap/IndustrialMap.prefab";

    private const string MaterialSavePath =
        "Assets/MohamedAltajer/Prefabs/Environment/Resources/Maps/IndustrialMap/Materials";

    private static readonly string[] OldMapFolders =
    {
        "Assets/MohamedAltajer/Prefabs/Environment/Resources/Maps/Map1",
        "Assets/MohamedAltajer/Prefabs/Environment/Resources/Maps/Map2"
    };

    // ── Auto-run on every compile ──────────────────────────────────────────
    static SetupIndustrialMap()
    {
        EditorApplication.delayCall += AutoSetup;
    }

    private static void AutoSetup()
    {
        if (Application.isPlaying) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(PrefabSavePath)) return;   // already done

        Debug.Log("[SetupIndustrialMap] Auto-generating industrial map prefab...");
        Run(silent: true);
    }

    [MenuItem("PRISM-7/Setup Industrial Map")]
    public static void RunFromMenu() => Run(silent: false);

    // ── Core ───────────────────────────────────────────────────────────────
    public static void Run(bool silent = false)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[SetupIndustrialMap] Exit Play mode first.");
            return;
        }

        // Ensure folders exist
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabSavePath));
        if (!AssetDatabase.IsValidFolder(MaterialSavePath))
            AssetDatabase.CreateFolder("Assets/MohamedAltajer/Prefabs/Environment/Resources/Maps/IndustrialMap", "Materials");

        // 1. Open Map_v1 additively
        Scene scene = EditorSceneManager.OpenScene(IndustrialScenePath, OpenSceneMode.Additive);
        if (!scene.IsValid())
        {
            Warn(silent, "Could not open Map_v1.unity. Make sure the industrial pack is imported.");
            return;
        }

        // 2. Force-activate geometry; strip all non-visual scene overhead
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            root.SetActive(true);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
        }

        // ── Strip objects that cause visual noise or conflicts ─────────────
        StripUnwantedComponents(scene);

        // 3. Convert all Standard materials → URP/Lit (save as assets, keep textures)
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            Warn(silent, "URP/Lit shader not found — materials may still appear pink.");
        else
            ConvertAllMaterials(scene, urpLit);

        AssetDatabase.SaveAssets();

        // 4. Create wrapper root
        GameObject wrapper = new GameObject("IndustrialMap");
        SceneManager.MoveGameObjectToScene(wrapper, scene);
        wrapper.SetActive(true);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == wrapper) continue;
            if (root.GetComponent<Camera>()        != null) continue;
            if (root.GetComponent<AudioListener>() != null) continue;
            root.transform.SetParent(wrapper.transform, worldPositionStays: true);
        }

        // 5. Save prefab
        bool ok;
        PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabSavePath, out ok);
        EditorSceneManager.CloseScene(scene, true);

        if (!ok)
        {
            Warn(silent, "Failed to save prefab — check the Console.");
            return;
        }

        // 6. Remove old map folders
        foreach (string folder in OldMapFolders)
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);

        AssetDatabase.Refresh();

        Debug.Log("[SetupIndustrialMap] ✓ Done — prefab: " + PrefabSavePath);
        if (!silent)
            EditorUtility.DisplayDialog("Done!",
                "Industrial map ready.\nOld maps removed.\nPress Play to start.", "OK");
    }

    // ── Material Conversion ────────────────────────────────────────────────
    /// <summary>
    /// Iterates every Renderer in every root object of the scene.
    /// For each material that is NOT already URP, creates a new URP/Lit material,
    /// copies every texture and property, saves it as an asset, and assigns it.
    /// Uses a dictionary so the same source material is only converted once.
    /// </summary>
    private static void ConvertAllMaterials(Scene scene, Shader urpLit)
    {
        var cache = new Dictionary<Material, Material>(); // src → converted
        int count = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer rend in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = rend.sharedMaterials;
                bool dirty = false;

                for (int i = 0; i < shared.Length; i++)
                {
                    Material src = shared[i];
                    if (src == null)               continue;
                    if (src.shader == urpLit)      continue; // already URP

                    if (!cache.TryGetValue(src, out Material dst))
                    {
                        dst = BuildUrpMaterial(src, urpLit);

                        // Save as a real asset so prefab references it correctly
                        string safeName = src.name.Replace("/", "_").Replace("\\", "_");
                        string assetPath = MaterialSavePath + "/" + safeName + "_URP.mat";
                        // Avoid path collisions
                        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                        AssetDatabase.CreateAsset(dst, assetPath);

                        cache[src] = dst;
                        count++;
                    }

                    shared[i] = dst;
                    dirty = true;
                }

                if (dirty)
                    rend.sharedMaterials = shared;
            }
        }

        Debug.Log($"[SetupIndustrialMap] Converted {count} materials to URP/Lit.");
    }

    /// <summary>
    /// Creates a new URP/Lit material that mirrors the source Standard material,
    /// copying albedo, normal, metallic, smoothness, emission, and occlusion maps.
    /// </summary>
    private static Material BuildUrpMaterial(Material src, Shader urpLit)
    {
        Material dst = new Material(urpLit);
        dst.name = src.name + "_URP";

        // ── Albedo / Base ──────────────────────────────────────────────────
        Color baseColor = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
        dst.SetColor("_BaseColor", baseColor);

        if (src.HasProperty("_MainTex"))
        {
            Texture mainTex = src.GetTexture("_MainTex");
            if (mainTex != null)
            {
                dst.SetTexture("_BaseMap", mainTex);
                dst.SetTextureScale("_BaseMap",  src.GetTextureScale("_MainTex"));
                dst.SetTextureOffset("_BaseMap", src.GetTextureOffset("_MainTex"));
            }
        }

        // ── Normal map ─────────────────────────────────────────────────────
        if (src.HasProperty("_BumpMap"))
        {
            Texture bump = src.GetTexture("_BumpMap");
            if (bump != null)
            {
                dst.SetTexture("_BumpMap", bump);
                dst.EnableKeyword("_NORMALMAP");
                if (src.HasProperty("_BumpScale"))
                    dst.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
            }
        }

        // ── Metallic / Smoothness ──────────────────────────────────────────
        if (src.HasProperty("_Metallic"))
            dst.SetFloat("_Metallic", src.GetFloat("_Metallic"));

        if (src.HasProperty("_MetallicGlossMap"))
        {
            Texture mGloss = src.GetTexture("_MetallicGlossMap");
            if (mGloss != null)
            {
                dst.SetTexture("_MetallicGlossMap", mGloss);
                dst.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
        }

        float smoothness = src.HasProperty("_Glossiness")
            ? src.GetFloat("_Glossiness") : 0.5f;
        dst.SetFloat("_Smoothness", smoothness);

        // ── Emission ───────────────────────────────────────────────────────
        if (src.IsKeywordEnabled("_EMISSION"))
        {
            dst.EnableKeyword("_EMISSION");
            if (src.HasProperty("_EmissionColor"))
                dst.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
            if (src.HasProperty("_EmissionMap"))
            {
                Texture em = src.GetTexture("_EmissionMap");
                if (em != null) dst.SetTexture("_EmissionMap", em);
            }
        }

        // ── Occlusion ──────────────────────────────────────────────────────
        if (src.HasProperty("_OcclusionMap"))
        {
            Texture ao = src.GetTexture("_OcclusionMap");
            if (ao != null)
            {
                dst.SetTexture("_OcclusionMap", ao);
                if (src.HasProperty("_OcclusionStrength"))
                    dst.SetFloat("_OcclusionStrength", src.GetFloat("_OcclusionStrength"));
            }
        }

        // ── Transparency ───────────────────────────────────────────────────
        if (src.HasProperty("_Mode") && src.GetFloat("_Mode") > 0f)
        {
            dst.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
            dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return dst;
    }

    // ── Strip unwanted components ──────────────────────────────────────────
    /// <summary>
    /// Removes components that either conflict with PRISM-7 runtime systems
    /// or create unwanted visual artefacts (cyan reflection probe boxes,
    /// duplicate audio listeners, light probe volumes, etc.).
    /// </summary>
    private static void StripUnwantedComponents(Scene scene)
    {
        int removed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Reflection probes — the large CYAN boxes you see floating everywhere.
            // They don't work properly inside a prefab and must be removed.
            foreach (ReflectionProbe rp in root.GetComponentsInChildren<ReflectionProbe>(true))
            {
                Object.DestroyImmediate(rp);
                removed++;
            }

            // Light probe groups — not needed in the prefab
            foreach (LightProbeGroup lpg in root.GetComponentsInChildren<LightProbeGroup>(true))
            {
                Object.DestroyImmediate(lpg);
                removed++;
            }

            // Cameras baked into the scene
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(cam.gameObject);
                removed++;
            }

            // Duplicate audio listeners
            foreach (AudioListener al in root.GetComponentsInChildren<AudioListener>(true))
            {
                Object.DestroyImmediate(al);
                removed++;
            }

            // Occluders / occlusion area components (can cause invisible geometry)
            foreach (OcclusionArea oa in root.GetComponentsInChildren<OcclusionArea>(true))
            {
                Object.DestroyImmediate(oa);
                removed++;
            }

            // Particle systems — remove all dust, smoke, and any particle effects
            // This removes the entire GameObject so nothing floats in the scene
            var toDestroy = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t.gameObject == null) continue;
                string n = t.gameObject.name.ToLower();
                bool isParticleObject = t.GetComponent<ParticleSystem>() != null;
                bool isDustOrSmoke    = n.Contains("dust") || n.Contains("smoke")
                                     || n.Contains("particle") || n.Contains("fog")
                                     || n.Contains("steam") || n.Contains("spark");
                bool isParticlesRoot  = n == "particles";

                if (isParticleObject || isDustOrSmoke || isParticlesRoot)
                    toDestroy.Add(t.gameObject);
            }
            // Remove duplicates (child listed after parent)
            toDestroy.RemoveAll(go => go == null);
            foreach (GameObject go in toDestroy)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
        }

        Debug.Log($"[SetupIndustrialMap] Stripped {removed} unwanted components (reflection probes, cameras, etc.)");
    }

    private static void Warn(bool silent, string msg)
    {
        if (!silent) EditorUtility.DisplayDialog("Error", msg, "OK");
        else Debug.LogWarning("[SetupIndustrialMap] " + msg);
    }
}
