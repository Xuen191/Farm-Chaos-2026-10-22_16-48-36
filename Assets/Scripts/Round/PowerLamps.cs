// Farm Chaos - electric lights react to the power:
//   power cut  -> lamp posts, HQ/power-station windows, porch lamps and indoor lights flicker
//                 and fade to a faint emergency glow
//   power back -> they flicker back on one by one, rippling out from the power station
// Non-electric lights (moon, stars, fireflies, entrance lanterns, the player's lantern, the fuse glow) are untouched.
// Creates itself automatically when the night lighting (Farm_Night) is in the scene.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PowerLamps : MonoBehaviour
{
    [Range(0, 1)] public float emergencyGlow = 0.12f;   // how much light is left during the outage
    public float flickerTime = 0.9f;
    public float rippleSpeed = 0.045f;                   // seconds of delay per unit of distance when power returns
    [Tooltip("How bright the whole farm is during the power cut (moonlight + sky glow). 1 = no change")]
    [Range(0.1f, 1f)] public float outageBrightness = 0.3f;
    [Tooltip("Extra camera darkening during the power cut (exposure stops)")]
    public float outageExposureDrop = 0.9f;
    public float darkenSpeed = 0.6f;                     // how fast the farm darkens / brightens

    /// <summary>1 = normal, lower = darker farm during the power cut. DawnLighting applies it.</summary>
    public static float SceneDim { get; private set; } = 1f;
    float dimTarget = 1f, dimStartAt;
    ColorAdjustments colorAdj; float baseExposure;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { SceneDim = 1f; }

    class Entry { public Renderer r; public string prop; public Color full; }
    class Unit
    {
        public List<Entry> entries = new List<Entry>();
        public List<(Light l, float i)> lights = new List<(Light, float)>();
        public Vector3 pos;
        public float level = 1f;          // 0 = emergency glow, 1 = full
        public float target = 1f;
        public float startAt, flickerUntil;
        public float applied = -1f;
        public float seed = Random.Range(0f, 100f);
    }

    readonly List<Unit> units = new List<Unit>();
    MaterialPropertyBlock mpb;
    float lastDawn = -1f;

    // ---------- auto-create in any scene that has the night lighting
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Spawn();
    }
    static void OnLoaded(Scene s, LoadSceneMode m) => Spawn();
    static void Spawn()
    {
        if (FindAnyObjectByType<PowerLamps>() != null) return;
        if (GameObject.Find("Farm_Night") == null) return;
        new GameObject("Power_Lamps").AddComponent<PowerLamps>();
    }

    /// <summary>True for lights that run on electricity (used by DawnLighting to leave them to us).</summary>
    public static bool IsElectric(Transform t)
    {
        var mr = t.GetComponent<Renderer>();
        if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.name.StartsWith("Mat_window_glass")) return true;
        for (var p = t; p != null; p = p.parent)
        {
            if (p.name == "Lamp_Posts" || p.name == "Building_Lights") return true;
            if (p.name == "Lamp" && p.parent != null && p.parent.name == "Zoo_HQ_Building") return true;
        }
        return false;
    }

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        var night = GameObject.Find("Farm_Night");

        // lamp posts: one unit each
        var posts = night != null ? night.transform.Find("Lamp_Posts") : null;
        if (posts != null) foreach (Transform lamp in posts) AddUnit(lamp.GetComponentsInChildren<Renderer>(true), lamp.GetComponentsInChildren<Light>(true), lamp.position);

        // building lights (door glows, porch glows, indoor point lights): one unit each
        var bl = night != null ? night.transform.Find("Building_Lights") : null;
        if (bl != null) foreach (Transform t in bl) AddUnit(t.GetComponentsInChildren<Renderer>(true), t.GetComponentsInChildren<Light>(true), t.position);

        // HQ porch lamps + windows, grouped per building
        foreach (var b in new[] { "Zoo_HQ_Building", "PowerStation_Building" })
        {
            var go = GameObject.Find(b); if (go == null) continue;
            var rends = go.GetComponentsInChildren<Renderer>(true).Where(r => IsElectric(r.transform)).ToArray();
            AddUnit(rends, new Light[0], go.transform.position);
        }

        // the night post-processing volume: we nudge its exposure during the outage (runtime copy only)
        var vol = FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v.name == "Night_PostFX");
        if (vol != null && vol.profile != null && vol.profile.TryGet(out colorAdj)) baseExposure = colorAdj.postExposure.value;

        ElectricGate.PowerChanged += OnPower;
        SceneDim = 1f;
        if (!ElectricGate.PowerOn) { foreach (var u in units) { u.level = u.target = 0f; } SceneDim = dimTarget = outageBrightness; }
    }

    void OnDestroy() { ElectricGate.PowerChanged -= OnPower; }

    void AddUnit(Renderer[] rends, Light[] lights, Vector3 pos)
    {
        var u = new Unit { pos = pos };
        foreach (var r in rends)
        {
            var m = r.sharedMaterial; if (m == null) continue;
            if (m.name.StartsWith("Mat_light_pool")) u.entries.Add(new Entry { r = r, prop = "_BaseColor", full = m.GetColor("_BaseColor") });
            else if (m.IsKeywordEnabled("_EMISSION")) u.entries.Add(new Entry { r = r, prop = "_EmissionColor", full = m.GetColor("_EmissionColor") });
        }
        foreach (var l in lights) if (l != null) u.lights.Add((l, l.intensity));
        if (u.entries.Count > 0 || u.lights.Count > 0) units.Add(u);
    }

    void OnPower(bool on)
    {
        Vector3 origin = GameObject.Find("PowerStation_Building") is GameObject ps ? ps.transform.position : Vector3.zero;
        foreach (var u in units)
        {
            float delay = on ? Vector3.Distance(origin, u.pos) * rippleSpeed + Random.Range(0f, 0.15f)
                             : Random.Range(0f, 0.5f);
            u.startAt = Time.time + delay;
            u.flickerUntil = u.startAt + flickerTime * Random.Range(0.6f, 1.2f);
            u.target = on ? 1f : 0f;
        }
        dimTarget = on ? 1f : outageBrightness;
        dimStartAt = Time.time + (on ? 0.3f : 0.6f);   // darken right after the flicker
    }

    void Update()
    {
        if (Time.time >= dimStartAt) SceneDim = Mathf.MoveTowards(SceneDim, dimTarget, darkenSpeed * Time.deltaTime);
        float dawn = DawnLighting.Amount;
        if (colorAdj != null)
        {
            float outage = Mathf.InverseLerp(1f, outageBrightness, SceneDim) * (1f - dawn);   // 0 = normal, 1 = full outage
            colorAdj.postExposure.Override(baseExposure - outage * outageExposureDrop);
        }
        bool dawnChanged = Mathf.Abs(dawn - lastDawn) > 0.002f; lastDawn = dawn;
        float dawnMul = Mathf.Lerp(1f, 0.15f, dawn);

        foreach (var u in units)
        {
            float shown;
            if (Time.time < u.startAt) shown = u.level;
            else if (Time.time < u.flickerUntil)
                shown = Mathf.PerlinNoise(Time.time * 14f + u.seed, u.seed) > 0.45f ? 1f : 0f;   // flicker!
            else { u.level = u.target; shown = u.level; }

            if (!dawnChanged && Mathf.Abs(shown - u.applied) < 0.001f) continue;
            u.applied = shown;

            float k = Mathf.Lerp(emergencyGlow, 1f, shown) * dawnMul;
            foreach (var e in u.entries)
            {
                if (e.r == null) continue;
                e.r.GetPropertyBlock(mpb);
                if (e.prop == "_BaseColor") mpb.SetColor(e.prop, new Color(e.full.r, e.full.g, e.full.b, e.full.a * k));
                else mpb.SetColor(e.prop, e.full * k);
                e.r.SetPropertyBlock(mpb);
            }
            foreach (var (l, i) in u.lights) if (l != null) l.intensity = i * Mathf.Lerp(emergencyGlow, 1f, shown) * Mathf.Lerp(1f, 0.35f, dawn);
        }
    }
}
