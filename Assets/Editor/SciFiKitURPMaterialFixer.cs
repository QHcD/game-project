#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SciFiKitURPMaterialFixer
{
    private const string ImportRoot = "Assets/SciFi Warehouse Kit";
    private const string SentinelMaterial = "Assets/SciFi Warehouse Kit/Art/Materials/Floor Tile Mat.mat";

    static SciFiKitURPMaterialFixer()
    {
        EditorApplication.delayCall += AutoConvertIfNeeded;
    }

    private static void AutoConvertIfNeeded()
    {
        if (Application.isPlaying) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        Material sentinel = AssetDatabase.LoadAssetAtPath<Material>(SentinelMaterial);
        if (sentinel == null) return;
        if (sentinel.shader != null && sentinel.shader.name == "Universal Render Pipeline/Lit") return;

        Convert();
    }

    [MenuItem("Tools/PRISM-7/Convert SciFi Kit Materials To URP")]
    public static void Convert()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[SciFiKitURP] URP/Lit shader not found — is URP installed?");
            return;
        }

        List<Material> mats = FindMaterials();
        int converted = 0, skipped = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (Material mat in mats)
            {
                if (mat == null) continue;
                string from = mat.shader != null ? mat.shader.name : "";
                if (from == "Universal Render Pipeline/Lit") { skipped++; continue; }

                bool transparent = from.Contains("Transparent") || from.Contains("Cutout") || from.Contains("Fade");
                PropSnapshot snap = Snapshot(mat);
                Undo.RecordObject(mat, "Convert SciFi Kit material to URP/Lit");
                mat.shader = urpLit;
                ApplySnapshot(mat, snap, transparent);
                EditorUtility.SetDirty(mat);
                converted++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[SciFiKitURP] Converted {converted} material(s) to URP/Lit. Skipped {skipped} already-URP.");
    }

    private static List<Material> FindMaterials()
    {
        List<Material> results = new List<Material>();
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { ImportRoot });
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) results.Add(m);
        }
        return results;
    }

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
        PropSnapshot s = new PropSnapshot();
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
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", s.metallic);
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
#endif
