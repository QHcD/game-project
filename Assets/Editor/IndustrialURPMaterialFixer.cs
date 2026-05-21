using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Converts Industrial Set v3 materials from Built-in Standard / Legacy shaders
// to URP/Lit so they render correctly under URP 17.x. Scoped strictly to
// Assets/_Shared/Imports/RPG_FPS_game_assets_industrial — will not touch any
// other project materials.
//
// Menu:
//   PRISM-7 ▸ Map ▸ Report Industrial Material Shaders
//     Read-only audit. Lists every .mat in the import folder and its current shader.
//
//   PRISM-7 ▸ Map ▸ Convert Industrial Materials To URP
//     For every .mat in the import folder whose shader is Standard, Standard
//     (Specular setup), or a Legacy diffuse/transparent shader, switches it to
//     Universal Render Pipeline/Lit and remaps texture + PBR properties.
//
// Standard → URP/Lit property map (only the properties Unity's own MaterialUpgrader uses):
//   _MainTex       → _BaseMap
//   _Color         → _BaseColor
//   _Glossiness    → _Smoothness
//   _Metallic      → _Metallic           (kept)
//   _MetallicGlossMap → _MetallicGlossMap (kept)
//   _BumpMap / _BumpScale → _BumpMap / _BumpScale (kept)
//   _OcclusionMap / _OcclusionStrength    (kept)
//   _EmissionMap / _EmissionColor         (kept; emission keyword preserved)
//
// Safe to run multiple times — idempotent (skips materials already on URP/Lit).
public static class IndustrialURPMaterialFixer
{
    private const string ImportRoot = "Assets/_Shared/Imports/RPG_FPS_game_assets_industrial";

    [MenuItem("PRISM-7/Map/Report Industrial Material Shaders")]
    private static void Report()
    {
        var mats = FindMaterials();
        var byShader = new Dictionary<string, int>();
        foreach (var mat in mats)
        {
            string name = mat.shader != null ? mat.shader.name : "<missing>";
            byShader.TryGetValue(name, out int c);
            byShader[name] = c + 1;
        }
        Debug.Log($"[IndustrialURPMaterialFixer] {mats.Count} material(s) under {ImportRoot}");
        foreach (var kv in byShader) Debug.Log($"  {kv.Value,4}× {kv.Key}");
    }

    [MenuItem("PRISM-7/Map/Convert Industrial Materials To URP")]
    private static void Convert()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            EditorUtility.DisplayDialog("URP Fixer", "Universal Render Pipeline/Lit shader not found. Is URP installed?", "OK");
            return;
        }

        var mats = FindMaterials();
        int converted = 0, skipped = 0, transparentCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < mats.Count; i++)
            {
                var mat = mats[i];
                EditorUtility.DisplayProgressBar("Converting Industrial materials → URP", mat.name, (float)i / mats.Count);

                string from = mat.shader != null ? mat.shader.name : "";
                if (from == "Universal Render Pipeline/Lit")
                {
                    skipped++;
                    continue;
                }

                bool isTransparent =
                    from.Contains("Transparent") ||
                    from.Contains("Cutout") ||
                    from.Contains("Fade");

                // Snapshot Standard / Legacy properties before swapping shaders
                // (some properties disappear once the shader is changed).
                var snap = Snapshot(mat);

                Undo.RecordObject(mat, "Convert to URP/Lit");
                mat.shader = urpLit;

                ApplySnapshot(mat, snap, isTransparent);

                if (isTransparent) transparentCount++;
                converted++;
                EditorUtility.SetDirty(mat);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[IndustrialURPMaterialFixer] Converted {converted} material(s) to URP/Lit ({transparentCount} marked transparent). Skipped {skipped} already-URP.");
    }

    private static List<Material> FindMaterials()
    {
        var results = new List<Material>();
        var guids = AssetDatabase.FindAssets("t:Material", new[] { ImportRoot });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) results.Add(mat);
        }
        return results;
    }

    // ── Property snapshot / restore ─────────────────────────────────────────

    private struct PropSnapshot
    {
        public Texture mainTex; public Vector2 mainScale, mainOffset;
        public Color color;
        public float glossiness, metallic;
        public Texture metallicGlossMap;
        public Texture bumpMap; public float bumpScale;
        public Texture occlusionMap; public float occlusionStrength;
        public Texture emissionMap; public Color emissionColor; public bool emissionEnabled;
    }

    private static PropSnapshot Snapshot(Material m)
    {
        var s = new PropSnapshot();
        if (m.HasProperty("_MainTex"))
        {
            s.mainTex = m.GetTexture("_MainTex");
            s.mainScale = m.GetTextureScale("_MainTex");
            s.mainOffset = m.GetTextureOffset("_MainTex");
        }
        s.color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        s.glossiness = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
        s.metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
        if (m.HasProperty("_MetallicGlossMap")) s.metallicGlossMap = m.GetTexture("_MetallicGlossMap");
        if (m.HasProperty("_BumpMap"))
        {
            s.bumpMap = m.GetTexture("_BumpMap");
            s.bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
        }
        if (m.HasProperty("_OcclusionMap"))
        {
            s.occlusionMap = m.GetTexture("_OcclusionMap");
            s.occlusionStrength = m.HasProperty("_OcclusionStrength") ? m.GetFloat("_OcclusionStrength") : 1f;
        }
        if (m.HasProperty("_EmissionMap")) s.emissionMap = m.GetTexture("_EmissionMap");
        s.emissionColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
        s.emissionEnabled = m.IsKeywordEnabled("_EMISSION");
        return s;
    }

    private static void ApplySnapshot(Material m, PropSnapshot s, bool transparent)
    {
        if (m.HasProperty("_BaseMap"))
        {
            if (s.mainTex != null) m.SetTexture("_BaseMap", s.mainTex);
            m.SetTextureScale("_BaseMap", s.mainScale == Vector2.zero ? Vector2.one : s.mainScale);
            m.SetTextureOffset("_BaseMap", s.mainOffset);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", s.color);

        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s.glossiness);
        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", s.metallic);
        if (s.metallicGlossMap != null && m.HasProperty("_MetallicGlossMap"))
            m.SetTexture("_MetallicGlossMap", s.metallicGlossMap);

        if (s.bumpMap != null && m.HasProperty("_BumpMap"))
        {
            m.SetTexture("_BumpMap", s.bumpMap);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", s.bumpScale);
            m.EnableKeyword("_NORMALMAP");
        }

        if (s.occlusionMap != null && m.HasProperty("_OcclusionMap"))
        {
            m.SetTexture("_OcclusionMap", s.occlusionMap);
            if (m.HasProperty("_OcclusionStrength")) m.SetFloat("_OcclusionStrength", s.occlusionStrength);
        }

        if (s.emissionMap != null && m.HasProperty("_EmissionMap"))
            m.SetTexture("_EmissionMap", s.emissionMap);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", s.emissionColor);
        if (s.emissionEnabled) m.EnableKeyword("_EMISSION");

        // URP/Lit surface type: 0 = Opaque, 1 = Transparent.
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", transparent ? 1f : 0f);
            if (transparent)
            {
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                m.SetOverrideTag("RenderType", "Opaque");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.renderQueue = -1;
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
        }
    }
}
