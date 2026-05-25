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
    private const float ClearanceAboveTallest = 0.4f;
    private const float MaxSensibleHeight = 28f;
    private const float MinSensibleHeight = 4f;

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
        if (host == null)
        {
            host = new GameObject(CapName + "_Host");
        }
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
        yield return null;
        yield return new WaitForSeconds(0.5f);
        BuildCap();
        yield return new WaitForSeconds(2.0f);
        BuildCap();
        _routine = null;
    }

    private void BuildCap()
    {
        Bounds b;
        if (!TryComputeArenaBounds(out b)) return;

        float capY = Mathf.Clamp(b.max.y + ClearanceAboveTallest, MinSensibleHeight, MaxSensibleHeight);
        float sizeX = Mathf.Max(MinCapSize, b.size.x + Overhang * 2f);
        float sizeZ = Mathf.Max(MinCapSize, b.size.z + Overhang * 2f);
        Vector3 center = new Vector3(b.center.x, capY, b.center.z);

        GameObject existing = GameObject.Find(CapName);
        if (existing != null) Destroy(existing);

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = CapName;
        cap.transform.SetParent(transform, true);
        cap.transform.position = center;
        cap.transform.rotation = Quaternion.identity;
        cap.transform.localScale = new Vector3(sizeX, Thickness, sizeZ);

        Collider col = cap.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        MeshRenderer mr = cap.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Material mat = LoadCapMaterial();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            mr.receiveShadows = true;
        }

        BuildSkirt(center, sizeX, sizeZ, capY, mr != null ? mr.sharedMaterial : null);

        Debug.Log($"[UniversalCeilingSealer] Cap built at y={capY:0.00} size={sizeX:0.0}x{sizeZ:0.0} center=({center.x:0.0},{center.z:0.0}) arenaBounds={b}");
    }

    private void BuildSkirt(Vector3 center, float sizeX, float sizeZ, float capY, Material mat)
    {
        float skirtHeight = 2.5f;
        float skirtThickness = 0.6f;
        float skirtCenterY = capY - skirtHeight * 0.5f - Thickness * 0.5f + 0.1f;

        SpawnSkirtPanel(CapName + "_SkirtN", new Vector3(center.x, skirtCenterY, center.z + sizeZ * 0.5f - skirtThickness * 0.5f), new Vector3(sizeX, skirtHeight, skirtThickness), mat);
        SpawnSkirtPanel(CapName + "_SkirtS", new Vector3(center.x, skirtCenterY, center.z - sizeZ * 0.5f + skirtThickness * 0.5f), new Vector3(sizeX, skirtHeight, skirtThickness), mat);
        SpawnSkirtPanel(CapName + "_SkirtE", new Vector3(center.x + sizeX * 0.5f - skirtThickness * 0.5f, skirtCenterY, center.z), new Vector3(skirtThickness, skirtHeight, sizeZ), mat);
        SpawnSkirtPanel(CapName + "_SkirtW", new Vector3(center.x - sizeX * 0.5f + skirtThickness * 0.5f, skirtCenterY, center.z), new Vector3(skirtThickness, skirtHeight, sizeZ), mat);
    }

    private void SpawnSkirtPanel(string n, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject existing = GameObject.Find(n);
        if (existing != null) Destroy(existing);
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
        s.name = n;
        s.transform.SetParent(transform, true);
        s.transform.position = pos;
        s.transform.localScale = scale;
        MeshRenderer mr = s.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        Collider c = s.GetComponent<Collider>();
        if (c != null) c.enabled = true;
    }

    private bool TryComputeArenaBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null) continue;
            if (!r.enabled) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r is TrailRenderer) continue;
            if (r is LineRenderer) continue;
            string n = r.gameObject.name;
            if (n == CapName || n.StartsWith(CapName + "_Skirt")) continue;
            string lower = n.ToLowerInvariant();
            if (lower.Contains("skybox") || lower.Contains("muzzle") || lower.Contains("tracer") || lower.Contains("minimap")) continue;
            if (r.GetComponentInParent<Camera>() != null) continue;
            Bounds rb = r.bounds;
            if (rb.size.sqrMagnitude < 0.0001f) continue;
            if (rb.size.x > 500f || rb.size.z > 500f) continue;
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
        if (made.HasProperty("_BaseColor")) made.SetColor("_BaseColor", new Color(0.18f, 0.20f, 0.24f, 1f));
        if (made.HasProperty("_Color")) made.SetColor("_Color", new Color(0.18f, 0.20f, 0.24f, 1f));
        if (made.HasProperty("_Smoothness")) made.SetFloat("_Smoothness", 0.25f);
        if (made.HasProperty("_Metallic")) made.SetFloat("_Metallic", 0.35f);
        if (made.HasProperty("_Cull")) made.SetFloat("_Cull", 0f);
        if (made.HasProperty("_CullMode")) made.SetFloat("_CullMode", 0f);
        if (made.HasProperty("_RenderFace")) made.SetFloat("_RenderFace", 0f);
        return made;
    }
}
