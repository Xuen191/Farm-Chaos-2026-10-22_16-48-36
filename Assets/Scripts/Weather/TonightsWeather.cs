// Farm Chaos - "Tonight's Weather": a random weather roll at the start of every round.
//
//   Clear Night   calm, nothing changes
//   Heavy Rain    rain + slippery mud (you slide when you turn or stop)
//   Thick Fog     you can only see a little way around you (lifts at dawn)
//   Thunderstorm  rain + mud + lightning flashes that light up the farm, with a little rumble
//   Windy         gusts that push everyone around
//   Earthquake    every minute or so the ground shakes and knocks you about
//
// A slot-machine card spins at the start of the round, lands on tonight's weather, then shrinks into a
// little badge in the corner. Online, the host's game picks the weather and tells everyone, and lightning
// and quakes happen at the same moments for every player (timed from the shared round clock).
//
// Testing: tick "Force Weather" on the Tonights_Weather object and pick one.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TonightsWeather : MonoBehaviour
{
    public enum Kind { Clear, Rain, Fog, Thunderstorm, Windy, Earthquake }

    public static TonightsWeather Instance { get; private set; }
    /// <summary>Tonight's weather (valid once Decided has fired).</summary>
    public static Kind Current { get; private set; }
    public static bool HasRolled { get; private set; }
    /// <summary>True while the roll card is on screen (other popups can wait for it).</summary>
    public static bool Showing { get; private set; }
    public static event Action<Kind> Decided;

    static readonly string[] Names = { "Clear Night", "Heavy Rain", "Thick Fog", "Thunderstorm", "Windy", "Earthquake" };
    static readonly string[] Icons = { "icon_clear", "icon_rain", "icon_fog", "icon_thunder", "icon_wind", "icon_quake" };
    static readonly string[] Blurbs =
    {
        "Calm skies tonight. No excuses!",
        "Slippery mud everywhere - you will slide when you turn!",
        "You can barely see past your nose. Stay close!",
        "Rain, mud and lightning - it might even knock the power out!",
        "Strong wind! Walking into it is super slow - and it blows you around.",
        "Every so often the ground shakes and knocks everyone over! Jump to dodge it.",
    };
    static readonly Color[] Tints =
    {
        new Color32(0xFF, 0xF0, 0xD9, 255), new Color32(0xE2, 0xF1, 0xFF, 255), new Color32(0xED, 0xE4, 0xFF, 255),
        new Color32(0xD9, 0xD9, 0xEE, 255), new Color32(0xE6, 0xF7, 0xE4, 255), new Color32(0xFF, 0xE4, 0xD0, 255),
    };
    public static string NameOf(Kind k) => Names[(int)k];

    [Header("Chances (higher = more likely)")]
    public int clearChance = 25;
    public int rainChance = 20;
    public int fogChance = 18;
    public int thunderChance = 12;
    public int windChance = 15;
    public int quakeChance = 10;

    [Header("Testing")]
    public bool forceWeather;
    public Kind forced = Kind.Rain;

    [Header("Strength")]
    [Range(0.1f, 1f)] public float mudGrip = 0.4f;            // rain: how much grip is left (1 = normal)
    public float fogDensity = 0.06f;
    public Color fogNight = new Color(0.20f, 0.22f, 0.32f);
    public Color fogDawn = new Color(0.86f, 0.70f, 0.68f);
    public float windStrength = 3.4f;                           // wind speed (m/s): slows you down walking into it, drifts you when still
    public float quakeStrength = 7f;
    public Vector2 quakeEvery = new Vector2(45f, 80f);          // seconds between quakes
    public float quakeLength = 4f;
    [Tooltip("How long you can't move after an earthquake knocks you over")]
    public float knockedDownFor = 1.8f;
    public Vector2 lightningEvery = new Vector2(10f, 22f);

    [Header("Optional sounds")]
    public AudioClip rollTick;
    public AudioClip rollLand;
    public AudioClip thunderSound;
    public AudioClip rainLoop;
    public AudioClip windLoop;
    public AudioClip quakeRumble;

    // ---------------------------------------------------------------- state
    static int pendingKind = -1, pendingSeed;
    int seed;
    bool running;
    readonly List<float> events = new List<float>();
    int nextEvent;
    float quakeUntil = -1f, flashT = -1f;
    Vector3 windDir = Vector3.right;
    bool fogWas; FogMode fogModeWas; float fogDensityWas; Color fogColorWas;
    ParticleSystem rain, wind;
    Light flashLight;
    Image flashOverlay;
    AudioSource loop, sfx;
    Camera cam;
    Canvas ui;
    TextMeshProUGUI toast;
    float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; HasRolled = false; Showing = false; pendingKind = -1; Decided = null; PresetKind = -1; FromIntro = false; }

    // ---------------------------------------------------------------- picked in the lobby (the story intro shows it)
    /// <summary>Tonight's weather, already rolled in the lobby so the story intro can show it. -1 = not yet.</summary>
    public static int PresetKind { get; private set; } = -1;
    static int presetSeed;
    public static bool HasPreset => PresetKind >= 0;
    /// <summary>Set by the story intro: the farm then skips the spinning card (you've seen the weather already).</summary>
    public static bool FromIntro;
    public static void Preset(int kind, int seed) { PresetKind = kind; presetSeed = seed; }

    /// <summary>Roll the weather before the farm exists (lobby). Same odds as the default Tonights_Weather settings.</summary>
    public static (int kind, int seed) RollForIntro()
    {
        int s = UnityEngine.Random.Range(1, int.MaxValue);
        int[] odds = { 25, 20, 18, 12, 15, 10 };   // clear, rain, fog, thunder, windy, quake
        int total = 0; foreach (var o in odds) total += o;
        int r = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < odds.Length; i++) { r -= odds[i]; if (r < 0) return (i, s); }
        return (0, s);
    }

    void Awake() { Instance = this; HasRolled = false; Showing = false; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SpherePlayer.Grip = 1f; SpherePlayer.Push = Vector3.zero; SpherePlayer.Wind = Vector3.zero; CameraFollow.Shake = Vector3.zero;
        if (running && Current == Kind.Fog)
        { RenderSettings.fog = fogWas; RenderSettings.fogMode = fogModeWas; RenderSettings.fogDensity = fogDensityWas; RenderSettings.fogColor = fogColorWas; }
        Showing = false;
    }

    void Start()
    {
        if (pendingKind >= 0) { var k = pendingKind; pendingKind = -1; Apply((Kind)k, pendingSeed); return; }
        if (NetSession.Online) StartCoroutine(WaitForHost());
        else { var p = Pick(); Apply((Kind)p.kind, p.seed); }
    }

    IEnumerator WaitForHost()
    {
        // the host's pick normally arrives within a second; don't wait forever if something went wrong
        for (float t = 0; t < 10f && !HasRolled; t += Time.unscaledDeltaTime) yield return null;
        if (!HasRolled) { var p = Pick(); Apply((Kind)p.kind, p.seed); }
    }

    /// <summary>Roll the dice (host / offline). Uses "Force Weather" if ticked on the farm's weather object.</summary>
    public static (int kind, int seed) Pick()
    {
        int s = UnityEngine.Random.Range(1, int.MaxValue);
        var w = Instance != null ? Instance : FindAnyObjectByType<TonightsWeather>();
        if (w != null && w.forceWeather) return ((int)w.forced, s);
        if (HasPreset) { var p = (PresetKind, presetSeed); PresetKind = -1; return p; }   // rolled in the lobby for the intro
        if (w == null) return (0, s);
        int[] odds = { w.clearChance, w.rainChance, w.fogChance, w.thunderChance, w.windChance, w.quakeChance };
        int total = 0; foreach (var o in odds) total += Mathf.Max(0, o);
        int r = UnityEngine.Random.Range(0, Mathf.Max(1, total));
        for (int i = 0; i < odds.Length; i++) { r -= Mathf.Max(0, odds[i]); if (r < 0) return (i, s); }
        return (0, s);
    }

    /// <summary>Online: the host's pick arrived.</summary>
    public static void Receive(int kind, int seed)
    {
        if (Instance != null) Instance.Apply((Kind)kind, seed);
        else { pendingKind = kind; pendingSeed = seed; }
    }

    void Apply(Kind kind, int s)
    {
        if (HasRolled) return;
        HasRolled = true; Current = kind; seed = s;
        PresetKind = -1;
        Showing = true;
        Decided?.Invoke(kind);
        StopAllCoroutines();
        if (FromIntro) { FromIntro = false; StartCoroutine(QuietStart()); }
        else StartCoroutine(RollCard());
    }

    // ================================================================ effects
    void StartEffects()
    {
        if (running) return;
        running = true;
        cam = Camera.main;
        var rng = new System.Random(seed);
        float Rand(Vector2 r) => r.x + (float)rng.NextDouble() * (r.y - r.x);
        float a = (float)rng.NextDouble() * 360f; windDir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));

        // event times on the shared round clock (same for everyone)
        float len = RoundClock.Instance != null ? RoundClock.Instance.roundLengthSeconds : 600f;
        if (Current == Kind.Thunderstorm) for (float t = Rand(new Vector2(6f, 12f)); t < len; t += Rand(lightningEvery)) events.Add(t);
        if (Current == Kind.Earthquake) for (float t = Rand(new Vector2(14f, 22f)); t < len; t += Rand(quakeEvery)) events.Add(t);
        nextEvent = 0;
        while (nextEvent < events.Count && events[nextEvent] < Now() - 1f) nextEvent++;   // joined late / long roll

        loop = gameObject.AddComponent<AudioSource>(); loop.loop = true; loop.spatialBlend = 0f; loop.volume = 0.5f;
        sfx = gameObject.AddComponent<AudioSource>(); sfx.spatialBlend = 0f;

        switch (Current)
        {
            case Kind.Rain:
            case Kind.Thunderstorm:
                SpherePlayer.Grip = mudGrip;
                rain = MakeRain(Current == Kind.Thunderstorm ? 1400 : 1000);
                if (rainLoop != null) { loop.clip = rainLoop; loop.Play(); }
                if (Current == Kind.Thunderstorm) MakeFlash();
                break;
            case Kind.Fog:
                fogWas = RenderSettings.fog; fogModeWas = RenderSettings.fogMode; fogDensityWas = RenderSettings.fogDensity; fogColorWas = RenderSettings.fogColor;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fogNight; RenderSettings.fogDensity = fogDensity;
                break;
            case Kind.Windy:
                wind = MakeWind();
                if (windLoop != null) { loop.clip = windLoop; loop.Play(); }
                break;
        }
    }

    float Now() => RoundClock.Instance != null ? RoundClock.Instance.Elapsed : Time.timeSinceLevelLoad;

    void Update()
    {
        if (!running) { UpdateToast(); return; }
        if (cam == null) cam = Camera.main;
        float now = Now();

        // timed events (lightning / quakes)
        while (nextEvent < events.Count && now >= events[nextEvent])
        {
            nextEvent++;
            if (Current == Kind.Thunderstorm) Lightning();
            if (Current == Kind.Earthquake) Quake();
        }

        switch (Current)
        {
            case Kind.Fog:
                float lift = 1f - 0.85f * DawnLighting.Amount;                 // the fog burns off at sunrise
                RenderSettings.fogDensity = fogDensity * lift;
                RenderSettings.fogColor = Color.Lerp(fogNight, fogDawn, DawnLighting.Amount);   // misty blue, pink at sunrise
                break;
            case Kind.Windy:
                float gust = 0.55f + 0.75f * Mathf.PerlinNoise(Time.time * 0.25f, seed % 1000 * 0.01f);
                SpherePlayer.Wind = windDir * windStrength * gust;
                if (wind != null)
                {
                    var main = wind.main; main.startSpeed = 9f + 9f * gust;
                    var em = wind.emission; em.rateOverTime = 40f + 80f * gust;
                }
                break;
        }

        // quake shaking
        if (quakeUntil > 0f)
        {
            float left = quakeUntil - Time.time;
            if (left <= 0f) { quakeUntil = -1f; CameraFollow.Shake = Vector3.zero; }
            else
            {
                float k = Mathf.Clamp01(left / quakeLength) * Mathf.Clamp01((quakeLength - left) * 3f);   // ease in & out
                float t = Time.time * 28f;
                CameraFollow.Shake = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f) * 0.6f * k;
            }
        }

        // lightning flash
        if (flashT >= 0f)
        {
            flashT += Time.deltaTime;
            float f = Flash(flashT);
            if (flashLight != null) flashLight.intensity = f * 2.2f;
            if (flashOverlay != null) flashOverlay.color = new Color(0.92f, 0.95f, 1f, f * 0.35f);
            if (Current == Kind.Thunderstorm) CameraFollow.Shake = UnityEngine.Random.insideUnitSphere * 0.06f * f;
            if (flashT > 1.2f) { flashT = -1f; CameraFollow.Shake = Vector3.zero; }
        }

        // rain/wind follow the camera
        if (cam != null)
        {
            Vector3 c = cam.transform.position;
            if (rain != null) rain.transform.position = new Vector3(c.x, c.y + 9f, c.z) + Flat(cam.transform.forward) * 6f;
            if (wind != null) wind.transform.position = new Vector3(c.x, c.y - 1f, c.z) + Flat(cam.transform.forward) * 6f - windDir * 14f;
        }
        UpdateToast();
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

    // double flicker: bright, dip, bright, fade
    static float Flash(float t)
    {
        if (t < 0.06f) return t / 0.06f;
        if (t < 0.14f) return Mathf.Lerp(1f, 0.25f, (t - 0.06f) / 0.08f);
        if (t < 0.2f) return Mathf.Lerp(0.25f, 0.9f, (t - 0.14f) / 0.06f);
        return Mathf.Max(0f, 0.9f * (1f - (t - 0.2f) / 0.9f));
    }

    void Lightning()
    {
        flashT = 0f;
        Debug.Log("[Farm Chaos] Weather: lightning!");
        PowerGrid.LightningStrike();          // sometimes it knocks the power out (host decides)
        if (thunderSound != null) StartCoroutine(PlayLater(thunderSound, 0.4f + UnityEngine.Random.value * 0.8f));
    }

    void Quake()
    {
        quakeUntil = Time.time + quakeLength;
        Debug.Log("[Farm Chaos] Weather: EARTHQUAKE!");
        Toast("EARTHQUAKE!", 2.5f);
        if (quakeRumble != null) sfx.PlayOneShot(quakeRumble);
        // loose things on the farm get a bump (each player's game bumps its own copy)
        foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (rb.isKinematic || rb.GetComponent<SpherePlayer>() != null) continue;
            if (rb.mass > 30f) continue;
            rb.AddForce(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(1.5f, 3f), UnityEngine.Random.Range(-1f, 1f)), ForceMode.VelocityChange);
        }
        // everyone standing on the ground falls over (every game does this for every player at the same moment,
        // so online everyone sees the same thing). In the air when it hits = dodged!
        foreach (var m in FindObjectsByType<ChibiMotor>(FindObjectsSortMode.None)) m.Knock();
        var me = FindAnyObjectByType<SpherePlayer>();
        if (me != null && me.IsGrounded) me.Stun(knockedDownFor);
    }

    IEnumerator PlayLater(AudioClip c, float delay) { yield return new WaitForSeconds(delay); if (sfx != null) sfx.PlayOneShot(c, 0.9f); }

    // ---------------------------------------------------------------- particles
    ParticleSystem MakeRain(int rate)
    {
        var go = new GameObject("Weather_Rain");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = true; m.playOnAwake = false; m.duration = 1f;
        m.startLifetime = 1.1f; m.startSpeed = 14f; m.startSize = 0.05f; m.maxParticles = 3000;
        m.startColor = new Color(0.78f, 0.86f, 1f, 0.55f);
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        var e = ps.emission; e.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30f, 30f, 0.1f);   // flat sheet above the camera
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // emit downwards
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1f;
        r.sharedMaterial = LoadMat("Mat_Rain");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var col = ps.collision; col.enabled = false;
        ps.Play();
        return ps;
    }

    ParticleSystem MakeWind()
    {
        var go = new GameObject("Weather_Wind");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.LookRotation(windDir);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = true; m.playOnAwake = false;
        m.startLifetime = 2.2f; m.startSpeed = 12f; m.startSize = 0.08f; m.maxParticles = 600;
        m.startColor = new Color(1f, 1f, 1f, 0.35f);
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        var e = ps.emission; e.rateOverTime = 80f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(26f, 6f, 0.1f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.12f; r.lengthScale = 2f;
        r.sharedMaterial = LoadMat("Mat_Wind");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    static Material LoadMat(string name)
    {
        var m = Resources.Load<Material>("Weather/" + name);
        if (m != null) return m;
        // fallback if the setup step hasn't been run
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        return new Material(sh);
    }

    void MakeFlash()
    {
        var go = new GameObject("Weather_LightningLight");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
        flashLight = go.AddComponent<Light>();
        flashLight.type = LightType.Directional; flashLight.color = new Color(0.8f, 0.86f, 1f);
        flashLight.intensity = 0f; flashLight.shadows = LightShadows.None;
        EnsureUI();
        var o = new GameObject("Flash", typeof(RectTransform), typeof(Image)); o.transform.SetParent(ui.transform, false);
        var rt = (RectTransform)o.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        flashOverlay = o.GetComponent<Image>(); flashOverlay.raycastTarget = false; flashOverlay.color = Color.clear;
        o.transform.SetAsFirstSibling();
    }

    // ================================================================ UI
    void EnsureUI()
    {
        if (ui != null) return;
        var go = new GameObject("Weather_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        ui = go.GetComponent<Canvas>(); ui.renderMode = RenderMode.ScreenSpaceOverlay; ui.sortingOrder = 190;
        var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
    }

    static Sprite cardSprite;
    static Sprite Card() => cardSprite != null ? cardSprite : (cardSprite = Resources.Load<Sprite>("Weather/card"));
    static Sprite Icon(int i) => Resources.Load<Sprite>("Weather/" + Icons[i]);

    IEnumerator RollCard()
    {
        EnsureUI();
        int final = (int)Current;
        var rootGo = new GameObject("Roll", typeof(RectTransform), typeof(CanvasGroup));
        rootGo.transform.SetParent(ui.transform, false);
        var root = (RectTransform)rootGo.transform; Stretch(root);
        var group = rootGo.GetComponent<CanvasGroup>(); group.alpha = 0f;

        var dim = Img(root, null, new Color(0.08f, 0.07f, 0.16f, 0.6f)); Stretch(dim.rectTransform); dim.raycastTarget = true;   // tap to skip
        bool skip = false;
        var btn = dim.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.onClick.AddListener(() => skip = true);

        var card = Img(root, Card(), Tints[0]); card.type = Image.Type.Sliced; card.pixelsPerUnitMultiplier = 0.8f;
        card.rectTransform.sizeDelta = new Vector2(760, 600);
        var title = Txt(card.rectTransform, "THE WEATHER TONIGHT", 50, new Color32(0x6B, 0x4A, 0x3A, 255), new Vector2(0, 238));
        var icon = Img(card.rectTransform, Icon(0), Color.white); icon.preserveAspect = true;
        icon.rectTransform.sizeDelta = new Vector2(260, 260); icon.rectTransform.anchoredPosition = new Vector2(0, 50);
        var name = Txt(card.rectTransform, Names[0], 76, new Color32(0x5E, 0x4A, 0x6E, 255), new Vector2(0, -130));
        name.outlineWidth = 0.18f; name.outlineColor = Color.white;
        var blurb = Txt(card.rectTransform, "", 34, new Color32(0x6B, 0x4A, 0x3A, 255), new Vector2(0, -222));
        blurb.textWrappingMode = TextWrappingModes.Normal; blurb.rectTransform.sizeDelta = new Vector2(660, 100);

        for (float a = 0; a < 1f; a += Time.unscaledDeltaTime * 4f) { group.alpha = a; yield return null; }
        group.alpha = 1f;

        // spin: fast at first, slowing down, ending on tonight's weather
        int steps = 14 + UnityEngine.Random.Range(0, 3);
        int start = (final - steps % Names.Length + Names.Length * 4) % Names.Length;
        for (int i = 0; i <= steps && !skip; i++)
        {
            int k = (start + i) % Names.Length;
            icon.sprite = Icon(k); name.text = Names[k]; card.color = Tints[k];
            if (rollTick != null && sfx != null) sfx.PlayOneShot(rollTick, 0.6f);
            float wait = Mathf.Lerp(0.05f, 0.34f, Mathf.Pow(i / (float)steps, 2.2f));
            for (float t = 0; t < wait && !skip; t += Time.unscaledDeltaTime)
            {
                icon.rectTransform.anchoredPosition = new Vector2(0, 50 + 18f * (1f - t / wait));   // little drop-in
                yield return null;
            }
        }
        icon.sprite = Icon(final); name.text = Names[final]; card.color = Tints[final];
        icon.rectTransform.anchoredPosition = new Vector2(0, 50);
        if (rollLand != null && sfx != null) sfx.PlayOneShot(rollLand);
        StartEffects();

        // land: punch + description
        blurb.text = Blurbs[final];
        for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float s = 1f + 0.12f * Mathf.Sin(t / 0.35f * Mathf.PI);
            card.rectTransform.localScale = Vector3.one * s;
            blurb.alpha = t / 0.35f;
            yield return null;
        }
        card.rectTransform.localScale = Vector3.one; blurb.alpha = 1f;
        skip = false;
        for (float t = 0; t < 2.4f && !skip; t += Time.unscaledDeltaTime) yield return null;

        // fade away (no badge - the weather speaks for itself)
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 3f)
        {
            group.alpha = 1f - t;
            card.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, t);
            yield return null;
        }
        Destroy(rootGo);
        Showing = false;
        if (Current == Kind.Clear) Toast("A calm night... for now.", 2.5f);
    }

    // after the story intro: no spinning card (the intro showed the weather), just start it after the fly-over
    IEnumerator QuietStart()
    {
        while (FarmFlyover.Active) yield return null;
        StartEffects();
        Showing = false;
        Toast("Tonight... " + Names[(int)Current], 2.5f);
    }

    public void Toast(string msg, float seconds)
    {
        EnsureUI();
        if (toast == null)
        {
            toast = Txt(ui.transform, "", 64, new Color32(0xFF, 0xF0, 0xD9, 255), new Vector2(0, 170));
            toast.outlineWidth = 0.22f; toast.outlineColor = new Color32(0x5E, 0x4A, 0x6E, 255);
        }
        toast.text = msg; toastUntil = Time.time + seconds;
    }

    void UpdateToast()
    {
        if (toast == null) return;
        float left = toastUntil - Time.time;
        toast.alpha = Mathf.Clamp01(left * 2f);
        if (left > 0f && Current == Kind.Earthquake) toast.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(Time.time * 60f) * 6f, 170);
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static Image Img(Transform parent, Sprite s, Color c)
    {
        var g = new GameObject("Image", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var i = g.GetComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false;
        return i;
    }

    static TextMeshProUGUI Txt(Transform parent, string text, float size, Color c, Vector2 pos)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(720, size * 1.4f); t.rectTransform.anchoredPosition = pos;
        return t;
    }
}
