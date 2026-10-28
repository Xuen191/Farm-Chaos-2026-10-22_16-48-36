// Farm Chaos - slowly turns the cozy night into a pink/orange sunrise near the end of the round.
// Only does something when the night lighting (Farm_Night) is in the scene.
// Uses per-renderer overrides, so your material files are never changed.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class DawnLighting : MonoBehaviour
{
    [Tooltip("In-game time the sky starts to brighten, in minutes after 11 PM (330 = 4:30 AM)")]
    public float dawnStartMinutes = 330f;

    [Header("Sunrise look")]
    public Color sunColor = new Color(1f, 0.72f, 0.52f);
    public float sunIntensity = 1.1f;
    public Vector3 sunAngles = new Vector3(14f, -60f, 0f);
    public Color skyColor = new Color(0.98f, 0.72f, 0.62f);
    public Color ambientSky = new Color(0.95f, 0.75f, 0.70f);
    public Color ambientEquator = new Color(0.78f, 0.64f, 0.66f);
    public Color ambientGround = new Color(0.36f, 0.31f, 0.33f);
    [Range(0, 1)] public float lampsLeftOn = 0.15f;

    RoundClock clock;
    Light sun; Camera cam;
    Color sunC0, cam0, amb0Sky, amb0Eq, amb0Gr; float sunI0; Quaternion sunR0;
    readonly List<(Renderer r, string prop, Color night, Color day)> fades = new List<(Renderer, string, Color, Color)>();
    readonly List<(Light l, float i0)> pointLights = new List<(Light, float)>();
    ParticleSystem fireflies; float fireflyRate0;
    MaterialPropertyBlock mpb;
    float lastApplied = -1f;
    bool active;

    void Start()
    {
        clock = RoundClock.Instance;
        var night = GameObject.Find("Farm_Night");
        active = night != null;
        if (!active) return;

        mpb = new MaterialPropertyBlock();
        sun = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        cam = Camera.main;
        if (sun != null) { sunC0 = sun.color; sunI0 = sun.intensity; sunR0 = sun.transform.rotation; }
        if (cam != null) cam0 = cam.backgroundColor;
        amb0Sky = RenderSettings.ambientSkyColor; amb0Eq = RenderSettings.ambientEquatorColor; amb0Gr = RenderSettings.ambientGroundColor;

        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial; if (m == null) continue;
            string n = m.name;
            if (n.StartsWith("Mat_lamp_glow") || (n == "Mat_lamp" && m.IsKeywordEnabled("_EMISSION")))
            {
                var e = m.GetColor("_EmissionColor");
                fades.Add((r, "_EmissionColor", e, e * lampsLeftOn));
            }
            else if (n.StartsWith("Mat_light_pool"))
            {
                var c = m.GetColor("_BaseColor");
                fades.Add((r, "_BaseColor", c, new Color(c.r, c.g, c.b, 0f)));
            }
            else if (n.StartsWith("Mat_stars") || n.StartsWith("Mat_moon"))
            {
                var c = m.GetColor("_BaseColor");
                fades.Add((r, "_BaseColor", c, n.StartsWith("Mat_stars") ? skyColor : Color.Lerp(c, skyColor, 0.7f)));
            }
        }
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Point) pointLights.Add((l, l.intensity));

        var ff = night.transform.Find("Fireflies");
        if (ff != null) { fireflies = ff.GetComponent<ParticleSystem>(); fireflyRate0 = fireflies.emission.rateOverTimeMultiplier; }
    }

    void Update()
    {
        if (!active) return;
        if (clock == null) { clock = RoundClock.Instance; if (clock == null) return; }

        float d = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dawnStartMinutes, clock.TotalGameMinutes, clock.GameMinutes));
        if (Mathf.Abs(d - lastApplied) < 0.002f) return;
        lastApplied = d;

        if (sun != null)
        {
            sun.color = Color.Lerp(sunC0, sunColor, d);
            sun.intensity = Mathf.Lerp(sunI0, sunIntensity, d);
            sun.transform.rotation = Quaternion.Slerp(sunR0, Quaternion.Euler(sunAngles), d);
        }
        if (cam != null) cam.backgroundColor = Color.Lerp(cam0, skyColor, d);
        RenderSettings.ambientSkyColor = Color.Lerp(amb0Sky, ambientSky, d);
        RenderSettings.ambientEquatorColor = Color.Lerp(amb0Eq, ambientEquator, d);
        RenderSettings.ambientGroundColor = Color.Lerp(amb0Gr, ambientGround, d);

        foreach (var f in fades)
        {
            if (f.r == null) continue;
            f.r.GetPropertyBlock(mpb);
            mpb.SetColor(f.prop, Color.Lerp(f.night, f.day, d));
            f.r.SetPropertyBlock(mpb);
        }
        foreach (var p in pointLights) if (p.l != null) p.l.intensity = Mathf.Lerp(p.i0, p.i0 * 0.35f, d);
        if (fireflies != null) { var em = fireflies.emission; em.rateOverTimeMultiplier = Mathf.Lerp(fireflyRate0, 0f, d); }
    }
}
