using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class UniversalCeilingSealer : MonoBehaviour
{
    private const string CapName = "UNIVERSAL_CEILING_CAP";
    private const float Overhang = 12f;
    private const float MinCapSize = 60f;
    private const float Thickness = 0.6f;
    private const float ClearanceAboveTallest = 1.5f;
    private const float MinCapY = 12f;
    private const float MaxCapY = 30f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded) OnSceneLoaded(active, LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        string lower = scene.name.ToLowerInvariant();
        if (lower.Contains("menu") || lower.Contains("lobby") || lower.Contains("loading")) return;

        GameObject host = GameObject.Find(CapName + "_Host");
        if (host == null) host = new GameObject(CapName + "_Host");
        UniversalCeilingSealer sealer = host.GetComponent<UniversalCeilingSealer>();
        if (sealer == null) sealer = host.AddComponent<UniversalCeilingSealer>();
        sealer.ScheduleRebuild();
    }

    private Coroutine _routine;

    public void ScheduleRebuild()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RebuildAfterDelay());
    }

    private IEnumerator RebuildAfterDelay()
    {
        yield return new WaitForSeconds(1.0f);
        BuildCap();
        yield return new WaitForSeconds(3.0f);
        BuildCap();
        _routine = null;
    }

    private static int s_minimapHiddenLayer = -2;

    private static int GetMinimapHiddenLayer()
    {
        if (s_minimapHiddenLayer == -2)
            s_minimapHiddenLayer = LayerMask.NameToLayer("MinimapHidden");
        return s_minimapHiddenLayer;
    }

    private static void AssignMinimapHiddenLayer(GameObject g)
    {
        if (g == null) return;
        int layer = GetMinimapHiddenLayer();
        if (layer < 0) return;
        g.layer = layer;
        for (int i = 0; i < g.transform.childCount; i++)
            AssignMinimapHiddenLayer(g.transform.GetChild(i).gameObject);
    }

    private void BuildCap()
    {
        Bounds b;
        if (!TryComputeArenaBounds(out b))
        {
            DestroyExistingCap();
            return;
        }

        float capY = Mathf.Clamp(b.max.y + ClearanceAboveTallest, MinCapY, MaxCapY);
        float sizeX = Mathf.Max(MinCapSize, b.size.x + Overhang * 2f);
        float sizeZ = Mathf.Max(MinCapSize, b.size.z + Overhang * 2f);
        Vector3 center = new Vector3(b.center.x, capY, b.center.z);

        DestroyExistingCap();

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = CapName;
        cap.transform.SetParent(transform, true);
        cap.transform.position = center;
        cap.transform.rotation = Quaternion.identity;
        cap.transform.localScale = new Vector3(sizeX, Thickness, sizeZ);
        StripColliders(cap);
        AssignMinimapHiddenLayer(cap);

        MeshRenderer mr = cap.GetComponent<MeshRenderer>();
        Material mat = null;
        if (mr != null)
        {
            mat = LoadCapMaterial();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            mr.receiveShadows = true;
        }

        float skirtTop = capY - Thickness * 0.5f;
        float skirtBottom = Mathf.Max(b.max.y - 0.2f, capY - 3.5f);
        float skirtHeight = Mathf.Max(0.5f, skirtTop - skirtBottom);
        float skirtCenterY = skirtBottom + skirtHeight * 0.5f;
        float skirtThickness = 0.6f;

        SpawnSkirt(CapName + "_SkirtN", new Vector3(center.x, skirtCenterY, center.z + sizeZ * 0.5f - skirtThickness * 0.5f), new Vector3(sizeX, skirtHeight, skirtThickness), mat);
        SpawnSkirt(CapName + "_SkirtS", new Vector3(center.x, skirtCenterY, center.z - sizeZ * 0.5f + skirtThickness * 0.5f), new Vector3(sizeX, skirtHeight, skirtThickness), mat);
        SpawnSkirt(CapName + "_SkirtE", new Vector3(center.x + sizeX * 0.5f - skirtThickness * 0.5f, skirtCenterY, center.z), new Vector3(skirtThickness, skirtHeight, sizeZ), mat);
        SpawnSkirt(CapName + "_SkirtW", new Vector3(center.x - sizeX * 0.5f + skirtThickness * 0.5f, skirtCenterY, center.z), new Vector3(skirtThickness, skirtHeight, sizeZ), mat);

        Debug.Log($"[UniversalCeilingSealer] Cap y={capY:0.00} size={sizeX:0.0}x{sizeZ:0.0} skirtY=[{skirtBottom:0.00}..{skirtTop:0.00}] arenaMaxY={b.max.y:0.00}");
    }

    private void DestroyExistingCap()
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            GameObject g = all[i];
            if (g == null) continue;
            if (g.name == CapName || g.name.StartsWith(CapName + "_Skirt"))
                Destroy(g);
        }
    }

    private void SpawnSkirt(string n, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
        s.name = n;
        s.transform.SetParent(transform, true);
        s.transform.position = pos;
        s.transform.localScale = scale;
        StripColliders(s);
        AssignMinimapHiddenLayer(s);
        MeshRenderer mr = s.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        }
    }

    private static void StripColliders(GameObject g)
    {
        Collider[] cols = g.GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) Destroy(cols[i]);
    }

    private bool TryComputeArenaBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;

        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null || !r.enabled) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r is TrailRenderer) continue;
            if (r is LineRenderer) continue;
            if (r is SkinnedMeshRenderer) continue;

            GameObject go = r.gameObject;
            string nLower = go.name.ToLowerInvariant();
            if (nLower == CapName.ToLowerInvariant() || nLower.StartsWith(CapName.ToLowerInvariant() + "_skirt")) continue;
            if (nLower.Contains("skybox") || nLower.Contains("muzzle") || nLower.Contains("tracer") || nLower.Contains("minimap")) continue;
            if (nLower.Contains("player") || nLower.Contains("enemy") || nLower.Contains("bullet") || nLower.Contains("weapon")) continue;
            if (nLower.Contains("viewmodel") || nLower.Contains("fpscam") || nLower.Contains("hud")) continue;

            if (go.layer == LayerMask.NameToLayer("UI")) continue;
            if (go.layer == LayerMask.NameToLayer("IgnoreRaycast")) continue;

            if (r.GetComponentInParent<Camera>() != null) continue;
            if (r.GetComponentInParent<Rigidbody>() != null) continue;
            if (r.GetComponentInParent<CharacterController>() != null) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;

            Bounds rb = r.bounds;
            if (rb.size.sqrMagnitude < 0.0001f) continue;
            if (rb.size.x > 500f || rb.size.z > 500f || rb.size.y > 200f) continue;

            if (!initialized) { bounds = rb; initialized = true; }
            else bounds.Encapsulate(rb);
        }

        if (!initialized) return false;
        if (bounds.size.x < 10f || bounds.size.z < 10f) return false;
        return true;
    }

    private static Material LoadCapMaterial()
    {
        Material m = Resources.Load<Material>("Materials/CeilingCap");
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) return null;
        Material made = new Material(sh);
        made.name = "UniversalCeilingCap_Runtime";
        if (made.HasProperty("_BaseColor")) made.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.22f, 1f));
        if (made.HasProperty("_Color")) made.SetColor("_Color", new Color(0.16f, 0.18f, 0.22f, 1f));
        if (made.HasProperty("_Smoothness")) made.SetFloat("_Smoothness", 0.20f);
        if (made.HasProperty("_Metallic")) made.SetFloat("_Metallic", 0.30f);
        if (made.HasProperty("_Cull")) made.SetFloat("_Cull", 0f);
        if (made.HasProperty("_CullMode")) made.SetFloat("_CullMode", 0f);
        if (made.HasProperty("_RenderFace")) made.SetFloat("_RenderFace", 0f);
        return made;
    }
}
