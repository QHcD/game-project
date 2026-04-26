using System.Collections;
using UnityEngine;

/// <summary>
/// Procedural per-weapon "alive" animator. Different store weapons use
/// different presets so combat feels distinct without needing bespoke
/// Animator clips per weapon:
///
///   • Nunchucks → "open + spin" — chains rotate fast around the grip
///     during the swing window, with a quick wind-up + recoil.
///   • Hammer / Axe → heavy windup, slower swing arc, big follow-through.
///   • Default / Katana → light follow-through scaling.
///
/// The Player calls <see cref="PlayAttack"/> at the start of each FireAttack
/// frame; the coroutine drives <see cref="Transform.localRotation"/> for the
/// weapon root and (for Nunchucks) the chain segments around the grip.
/// </summary>
[DisallowMultipleComponent]
public class WeaponLiveAnimator : MonoBehaviour
{
    /// <summary>Cached preset selected by <see cref="Configure"/>.</summary>
    private WeaponPreset _preset = WeaponPreset.Default;

    private Transform _root;
    private Quaternion _baseRotation;
    private Coroutine  _routine;

    // Nunchuck-specific. We build a small "chain" object procedurally on
    // first attack (a couple of links + a second handle) and spin it around
    // the local up axis to simulate the chain whip.
    private Transform _chainRoot;

    private enum WeaponPreset
    {
        Default,
        Katana,
        Nunchucks,
        Heavy,   // Hammer / Axe / Morgenstern
    }

    private void Awake()
    {
        _root = transform;
        _baseRotation = _root.localRotation;
    }

    /// <summary>
    /// Picks an animation preset based on the equipped store weapon. Safe to
    /// re-call when the player swaps weapons mid-session.
    /// </summary>
    public void Configure(WeaponDefinition def)
    {
        if (def == null)
        {
            _preset = WeaponPreset.Default;
            return;
        }

        switch (def.Id)
        {
            case "nunchucks": _preset = WeaponPreset.Nunchucks; break;
            case "katana":    _preset = WeaponPreset.Katana;    break;
            case "hammer":
            case "axe":
            case "morgenstern":
                _preset = WeaponPreset.Heavy;
                break;
            default:          _preset = WeaponPreset.Default;   break;
        }

        // Rebuild the chain rig the first time we land on the Nunchuck preset.
        if (_preset == WeaponPreset.Nunchucks && _chainRoot == null)
            BuildNunchuckChainRig();
        // Hide / show the chain depending on the preset so swaps look clean.
        if (_chainRoot != null)
            _chainRoot.gameObject.SetActive(_preset == WeaponPreset.Nunchucks);
    }

    /// <summary>
    /// Plays the "opening" + swing animation for a single attack. The
    /// player's <c>attackDelay</c> is the time before the hitbox enables;
    /// we time the wind-up to land on that frame.
    /// </summary>
    public void PlayAttack(float attackSpeedMul, float attackDelay)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(AnimateAttack(attackSpeedMul, attackDelay));
    }

    private IEnumerator AnimateAttack(float attackSpeedMul, float attackDelay)
    {
        if (Mathf.Approximately(attackSpeedMul, 0f)) attackSpeedMul = 1f;

        float windupTime  = Mathf.Max(0.05f, attackDelay);
        float swingTime   = 0.18f / Mathf.Max(0.4f, attackSpeedMul);
        float settleTime  = 0.16f / Mathf.Max(0.4f, attackSpeedMul);

        switch (_preset)
        {
            case WeaponPreset.Nunchucks:
                yield return AnimateNunchucks(windupTime, swingTime, settleTime);
                break;
            case WeaponPreset.Heavy:
                yield return AnimateHeavy(windupTime, swingTime, settleTime);
                break;
            case WeaponPreset.Katana:
                yield return AnimateKatana(windupTime, swingTime, settleTime);
                break;
            default:
                yield return AnimateDefault(windupTime, swingTime, settleTime);
                break;
        }

        _root.localRotation = _baseRotation;
        if (_chainRoot != null && _preset == WeaponPreset.Nunchucks)
            _chainRoot.localRotation = Quaternion.identity;

        _routine = null;
    }

    // ─── Animation curves ──────────────────────────────────────────────────

    private IEnumerator AnimateNunchucks(float windupTime, float swingTime, float settleTime)
    {
        // Wind-up: the second handle "opens" away from the grip and starts
        // spinning around the local Z axis (chain whip).
        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windupTime);
            // Subtle pitch back as the player rears up for the strike.
            _root.localRotation = _baseRotation * Quaternion.Euler(-22f * k, 0f, 0f);
            if (_chainRoot != null)
                _chainRoot.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            yield return null;
        }

        // Swing: fast spin + sweep forward. The chain spins multiple times
        // around the grip during the active hitbox window.
        t = 0f;
        while (t < swingTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / swingTime);
            _root.localRotation = _baseRotation * Quaternion.Euler(Mathf.Lerp(-22f, 28f, k), 0f, 0f);
            if (_chainRoot != null)
                _chainRoot.localRotation = Quaternion.Euler(0f, 0f, 90f + k * 720f);
            yield return null;
        }

        // Settle: ease back to neutral.
        t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleTime);
            _root.localRotation = Quaternion.Slerp(_root.localRotation, _baseRotation, k);
            if (_chainRoot != null)
                _chainRoot.localRotation = Quaternion.Slerp(_chainRoot.localRotation, Quaternion.identity, k);
            yield return null;
        }
    }

    private IEnumerator AnimateHeavy(float windupTime, float swingTime, float settleTime)
    {
        // Big windup pitch + a slower forward arc.
        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / windupTime));
            _root.localRotation = _baseRotation * Quaternion.Euler(-55f * k, 0f, -8f * k);
            yield return null;
        }
        t = 0f;
        while (t < swingTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / swingTime);
            _root.localRotation = _baseRotation * Quaternion.Euler(Mathf.Lerp(-55f, 35f, k), 0f, Mathf.Lerp(-8f, 6f, k));
            yield return null;
        }
        t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleTime);
            _root.localRotation = Quaternion.Slerp(_root.localRotation, _baseRotation, k);
            yield return null;
        }
    }

    private IEnumerator AnimateKatana(float windupTime, float swingTime, float settleTime)
    {
        // Quick draw-and-cut: short windup + horizontal sweep.
        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windupTime);
            _root.localRotation = _baseRotation * Quaternion.Euler(0f, -28f * k, -18f * k);
            yield return null;
        }
        t = 0f;
        while (t < swingTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / swingTime);
            _root.localRotation = _baseRotation * Quaternion.Euler(0f, Mathf.Lerp(-28f, 32f, k), Mathf.Lerp(-18f, 12f, k));
            yield return null;
        }
        t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleTime);
            _root.localRotation = Quaternion.Slerp(_root.localRotation, _baseRotation, k);
            yield return null;
        }
    }

    private IEnumerator AnimateDefault(float windupTime, float swingTime, float settleTime)
    {
        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windupTime);
            _root.localRotation = _baseRotation * Quaternion.Euler(-12f * k, 0f, 0f);
            yield return null;
        }
        t = 0f;
        while (t < swingTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / swingTime);
            _root.localRotation = _baseRotation * Quaternion.Euler(Mathf.Lerp(-12f, 18f, k), 0f, 0f);
            yield return null;
        }
        t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleTime);
            _root.localRotation = Quaternion.Slerp(_root.localRotation, _baseRotation, k);
            yield return null;
        }
    }

    // ─── Procedural chain rig ───────────────────────────────────────────────

    private void BuildNunchuckChainRig()
    {
        // Don't recreate if it's already there.
        Transform existing = _root.Find("ChainRig");
        if (existing != null) { _chainRoot = existing; return; }

        GameObject root = new GameObject("ChainRig");
        root.transform.SetParent(_root, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        _chainRoot = root.transform;

        // Try to find the bottom of the equipped weapon's bounds so the
        // chain visually attaches to the grip end.
        Bounds bounds = new Bounds(_root.position, Vector3.zero);
        Renderer[] renderers = _root.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!any) { bounds = renderers[i].bounds; any = true; }
            else      { bounds.Encapsulate(renderers[i].bounds); }
        }

        // 4 chain links + a second handle. All scaled to a fraction of the
        // primary weapon's bounds so they look right regardless of which
        // procedural mesh GameManager spawned for level 5 (Nunchucks).
        float bladeLen = any ? bounds.size.y : 0.6f;
        float linkSize = Mathf.Max(0.02f, bladeLen * 0.06f);
        for (int i = 0; i < 4; i++)
        {
            GameObject link = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            link.name = "ChainLink_" + i;
            Destroy(link.GetComponent<Collider>());
            link.transform.SetParent(_chainRoot, false);
            link.transform.localScale    = Vector3.one * linkSize;
            link.transform.localPosition = new Vector3(0f, -bladeLen * 0.55f - linkSize * (i + 0.5f) * 1.6f, 0f);
            TintLink(link, new Color(0.20f, 0.20f, 0.24f, 1f));
        }

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "SecondHandle";
        Destroy(handle.GetComponent<Collider>());
        handle.transform.SetParent(_chainRoot, false);
        handle.transform.localScale    = new Vector3(linkSize * 1.6f, bladeLen * 0.45f, linkSize * 1.6f);
        handle.transform.localPosition = new Vector3(0f, -bladeLen * 0.55f - linkSize * 8f - bladeLen * 0.225f, 0f);
        TintLink(handle, new Color(0.18f, 0.10f, 0.06f, 1f));
    }

    private static void TintLink(GameObject obj, Color color)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;
        Shader s = Shader.Find("Standard") ?? r.sharedMaterial.shader;
        Material m = new Material(s) { color = color };
        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic",   0.55f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.45f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.45f);
        r.material = m;
    }
}
