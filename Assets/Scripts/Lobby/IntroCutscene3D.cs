// Farm Chaos - the 3D story intro, played after START in the lobby (everyone plays it together online).
// Starring YOUR bean (and everyone else's at a sleepover when you play online), in tonight's real weather.
//   1. the farmhouse at night (rain / fog / storm / wind / earthquake = tonight's weather)
//   2. sleepover: every player's bean asleep on their mat, fan humming
//   3. the fan dies, the lights die, everyone wakes up SOOO HOT
//   4. why? the power box pops (lightning, wind, quake...) - with 3+ players a shadowy someone sneaks away (the spy!)
//   5. animal escape montage: chickens burst out, a cow strolls out, bunnies hop out in a line
//   6. faces squished at the window, panic... everybody dives out and lands in a pile
//   7. fighting-game player cards, one per bean
//   8. FARM CHAOS!  -> on the farm the camera flies over every pen and lands behind you (FarmFlyover)
// Tap = next shot, Skip = straight to the farm. Shot lengths are in the Inspector (Timing).
// Acting moves (Sleep, WakeUp, HotFan, Panic, Dive, Pose) come from Art/bean_buddy.py (Blender).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntroCutscene3D : MonoBehaviour
{
    [Header("Set by Tools > Farm Chaos > 15. Add 3D Story Intro")]
    public Material litBase;
    public Material unlitBase;
    public GameObject farmhouse;
    public GameObject tree;
    public GameObject bush;
    public Sprite rounded;
    [Header("The cast (also set by step 15)")]
    public GameObject beanPrefab;
    [Tooltip("The bean's animations (Assets/Models/Chibi/Chibi.controller) - with the intro acting moves")]
    public RuntimeAnimatorController beanAnimations;
    public GameObject chickenPrefab, sheepPrefab, cowPrefab, bunnyPrefab;

    [Header("Timing (seconds) - tapping skips to the next shot")]
    public float houseShot = 2.8f;
    public float sleepShot = 3.4f;
    public float hotShot = 4.0f;
    public float causeShot = 3.0f;
    public float escapeShot = 3.9f;
    public float windowShot = 3.8f;
    public float cardEach = 0.95f;
    public float titleShot = 3.0f;

    [Header("Testing (editor)")]
    [Tooltip("In Play mode in the Lobby: F9 = play the intro solo, F10 = play it as a pretend 4-player party (sleepover + spy)")]
    public bool testKeys = true;
    static int fakePlayers;

    /// <summary>True right after the intro played: the farm then does its fly-over.</summary>
    public static bool JustPlayed;

    static readonly Vector3 Origin = new Vector3(0f, -500f, 0f);

    // palette
    static readonly Color Cream = new Color32(0xFF, 0xF0, 0xD9, 255), Pink = new Color32(0xF4, 0xB8, 0xC1, 255),
        Mint = new Color32(0xC8, 0xE6, 0xD0, 255), Blue = new Color32(0xC9, 0xDD, 0xF5, 255), Wood = new Color32(0xD9, 0xB9, 0x9B, 255),
        DarkWood = new Color32(0xA8, 0x7B, 0x5C, 255), Straw = new Color32(0xD6, 0xB7, 0x5A, 255), Grass = new Color32(0x7F, 0xB0, 0x6E, 255),
        Ink = new Color32(0x2E, 0x2A, 0x3A, 255), Night = new Color32(0x1B, 0x1D, 0x3E, 255), Brown = new Color32(0x6B, 0x4A, 0x3A, 255),
        Metal = new Color32(0x8A, 0x92, 0xA6, 255), Spark = new Color32(0xFF, 0xE0, 0x6A, 255), Sweat = new Color32(0x9F, 0xD8, 0xF5, 255),
        Red = new Color32(0xE0, 0x4C, 0x5C, 255);
    static readonly Color[] CardColors = { new Color32(0x8E, 0xC5, 0xF0, 255), new Color32(0xF4, 0x9A, 0xB0, 255), new Color32(0x8C, 0xCF, 0xA4, 255), new Color32(0xFF, 0xC8, 0x6A, 255), new Color32(0xB9, 0x9C, 0xF0, 255) };
    static readonly string[] Weathers = { "Clear", "Rain", "Fog", "Thunderstorm", "Windy", "Earthquake" };
    static readonly string[] HouseSubs = { "A calm, starry night...", "Pouring with rain...", "Thick fog rolling in...", "A big storm is brewing...", "The wind is howling...", "Hmm... the ground is shaking..." };

    Transform root;
    Camera cam;
    RectTransform stage, banner;
    Image fader, flash;
    TextMeshProUGUI capTitle, capSub, bannerName, bannerSub;
    GameObject capBox;
    bool skip, next;
    readonly Dictionary<Color, Material> lit = new Dictionary<Color, Material>(), unlit = new Dictionary<Color, Material>();
    readonly List<System.Action<float, float>> always = new List<System.Action<float, float>>();   // (time, dt)
    float clock, shakeAmp;
    int weather;            // TonightsWeather.Kind as int
    bool spyTease;

    // set pieces we animate
    Transform fanPivot, title3D, bang, powerBox, boxLid;
    Light lampLight, roomLight, moonLight, boxLight;
    Renderer bulb;
    float fanSpeed;
    ParticleSystem weatherFx;
    readonly List<Transform> swaying = new List<Transform>();

    // ================================================================ the cast
    struct Who { public string name, json; public bool you; }

    class Bean
    {
        public Who who;
        public Transform t, head;
        public Animator anim;
        public ChibiLook look;
        public readonly List<Renderer> eyes = new List<Renderer>();
        public readonly List<GameObject> lids = new List<GameObject>();
        public readonly List<Transform> sweat = new List<Transform>();
        public TextMeshPro zzz;
        public Vector3 home;
    }
    readonly List<Bean> beans = new List<Bean>();

    // ================================================================ public
#if UNITY_EDITOR
    // find the bean + the project's own animals automatically (no setup step needed in the editor)
    void OnValidate() { FillMissing(); }
    void FillMissing()
    {
        GameObject P(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (beanPrefab == null) beanPrefab = P("Assets/Prefabs/Chibi_Player.prefab");
        if (beanAnimations == null) beanAnimations = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Models/Chibi/Chibi.controller");
        if (chickenPrefab == null) chickenPrefab = P("Assets/Prefabs/Animals/Chicken.prefab");
        if (sheepPrefab == null) sheepPrefab = P("Assets/Prefabs/Animals/Sheep.prefab");
        if (cowPrefab == null) cowPrefab = P("Assets/Prefabs/Animals/Cow.prefab");
        if (bunnyPrefab == null) bunnyPrefab = P("Assets/Prefabs/Animals/Bunny.prefab");
    }
#endif

    public IEnumerator Play()
    {
#if UNITY_EDITOR
        FillMissing();
#endif
        if (beanPrefab == null) Debug.LogWarning("[Farm Chaos] Intro: no bean prefab - run Tools > Farm Chaos > 15. Add 3D Story Intro.");
        skip = false;
        JustPlayed = true;
        if (!TonightsWeather.HasPreset) { var p = TonightsWeather.RollForIntro(); TonightsWeather.Preset(p.kind, p.seed); }
        TonightsWeather.FromIntro = true;
        weather = Mathf.Clamp(TonightsWeather.PresetKind, 0, Weathers.Length - 1);

        var cast = Cast();
        spyTease = cast.Count >= NetPlayer.MinPlayersForSpy;

        PrepareScene();
        BuildSet(cast);
        BuildUI();

        yield return Shot(houseShot, ShotHouse);
        if (!skip) yield return Shot(sleepShot, ShotSleep);
        if (!skip) yield return Shot(hotShot, ShotHot);
        if (!skip) yield return Shot(causeShot, ShotCause);
        if (!skip) yield return Shot(escapeShot, ShotEscape);
        if (!skip) yield return Shot(windowShot, ShotWindow);
        for (int i = 0; i < beans.Count && !skip; i++) { int k = i; yield return Shot(beans.Count == 1 ? cardEach * 1.6f : cardEach, () => ShotCard(k)); }
        if (!skip) yield return Shot(titleShot, ShotTitle);
        // leave the screen dark while the farm loads
        fader.color = Night;
        if (capBox != null) capBox.SetActive(false);
        if (banner != null) banner.gameObject.SetActive(false);
        if (weatherFx != null) weatherFx.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    void Update()
    {
        bool f9 = false, f10 = false;
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null) { f9 |= kb.f9Key.wasPressedThisFrame; f10 |= kb.f10Key.wasPressedThisFrame; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        f9 |= Input.GetKeyDown(KeyCode.F9); f10 |= Input.GetKeyDown(KeyCode.F10);
#endif
        if (!testKeys || LobbyUI.Instance == null || !(f9 || f10)) return;
        fakePlayers = f10 ? 4 : 0;
        LobbyUI.Instance.TestStart();
    }
#endif

    List<Who> Cast()
    {
        var list = new List<Who>();
#if UNITY_EDITOR
        if (fakePlayers > 0)
        {
            for (int i = 0; i < fakePlayers; i++)
            {
                var d = ChibiLook.LoadSaved();
                if (i > 0) { d.outfit = (d.outfit + i * 2) % 8; d.hat = (d.hat + i) % 7; d.hairStyle = (d.hairStyle + i) % 5; d.hairColor = (d.hairColor + i * 3) % 8; d.hatColor = (d.hatColor + i) % 8; }
                list.Add(new Who { name = i == 0 ? "YOU" : "PLAYER " + (i + 1), json = JsonUtility.ToJson(d), you = i == 0 });
            }
            return list;
        }
#endif
        if (NetSession.Online)
        {
            foreach (var np in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None).OrderBy(n => n.OwnerClientId))
            {
                string json = np.IsOwner ? JsonUtility.ToJson(ChibiLook.LoadSaved()) : np.LookJson;
                if (string.IsNullOrEmpty(json)) json = JsonUtility.ToJson(new ChibiLook.Data { outfit = np.Number - 1 });
                string typed = string.IsNullOrEmpty(np.DisplayName) ? "" : np.DisplayName.ToUpperInvariant();
                if (np.IsOwner) { var mine = ChibiLook.LoadSaved(); typed = string.IsNullOrEmpty(mine.name) ? "" : mine.name.ToUpperInvariant(); }
                list.Add(new Who { name = typed != "" ? typed : (np.IsOwner ? "YOU" : "PLAYER " + np.Number), json = json, you = np.IsOwner });
            }
        }
        if (list.Count == 0)
        {
            var mine = ChibiLook.LoadSaved();
            list.Add(new Who { name = string.IsNullOrEmpty(mine.name) ? "YOU" : mine.name.ToUpperInvariant(), json = JsonUtility.ToJson(mine), you = true });
        }
        return list.Take(5).ToList();
    }

    // ================================================================ scene prep
    void PrepareScene()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
        var myCanvas = GetComponentInParent<Canvas>();
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c != myCanvas && c.isRootCanvas) c.enabled = false;
        for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(false);

        RenderSettings.fog = weather == 2;
        if (RenderSettings.fog)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.032f;
            RenderSettings.fogColor = new Color(0.33f, 0.35f, 0.48f);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.4f, 0.55f);
        DynamicGI.UpdateEnvironment();

        var cg = new GameObject("Intro_Camera");
        cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = weather == 2 ? RenderSettings.fogColor : Night;
        cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 260f; cam.depth = 100;
        // a soft light riding on the camera so the beans' faces always read, even on a dark night
        var cl = new GameObject("Intro_CamLight").AddComponent<Light>();
        cl.transform.SetParent(cam.transform, false); cl.transform.localPosition = new Vector3(0.4f, 0.3f, 0f);
        cl.type = LightType.Point; cl.color = new Color(1f, 0.95f, 0.9f); cl.range = 12f; cl.intensity = 2.2f; cl.shadows = LightShadows.None;
    }

    // ================================================================ shots
    System.Action<float> ShotHouse()
    {
        Outdoors(true);
        Caption("11 PM on the farm...", HouseSubs[weather]);
        Vector3 at = new Vector3(-60f, 0f, 0f);
        Vector3 look = houseLook;
        Vector3 a = look + new Vector3(-3f, 1.5f, -24f), b = look + new Vector3(0f, 0.5f, -15f);
        return t =>
        {
            float k = Ease(t / houseShot);
            Quake(t);
            Cam(Vector3.Lerp(a, b, k), look + Vector3.up * 0.5f);
        };
    }

    System.Action<float> ShotSleep()
    {
        Outdoors(false);
        Caption(beans.Count > 1 ? "Sleepover at the farmhouse!" : "Bedtime at the farmhouse!", "Zzz... sweet dreams.");
        fanSpeed = 540f;
        for (int i = 0; i < beans.Count; i++)
        {
            var b = beans[i];
            b.t.localPosition = b.home; b.t.localRotation = Quaternion.identity;
            Anim(b, i % 2 == 0 ? "Sleep" : "SleepSprawl", 0f, (i * 0.37f) % 1f);
            Face(b, ChibiLook.Face.Sleepy); Eyes(b, false);
            b.zzz.gameObject.SetActive(true);
        }
        // looking down from above the beans' feet, so the sleepy faces read the right way up
        float w = Mathf.Max(1.2f, beans.Count * 0.6f);
        Vector3 a = new Vector3(-w * 0.15f, 2.75f, 0.9f + w * 0.12f), c = new Vector3(w * 0.15f, 2.6f, 0.7f + w * 0.12f);
        return t =>
        {
            float k = Ease(t / sleepShot);
            Cam(Vector3.Lerp(a, c, k), new Vector3(0f, 0f, -0.95f));
            for (int i = 0; i < beans.Count; i++) Zzz(beans[i], t + i * 0.4f);
        };
    }

    System.Action<float> ShotHot()
    {
        Outdoors(false);
        Caption("...hmm... why is it so HOT!", "Wait... the fan stopped!");
        bool dark = false;
        var woke = new bool[beans.Count]; var fanning = new bool[beans.Count];
        foreach (var b in beans) b.zzz.gameObject.SetActive(false);
        float w = Mathf.Max(1.4f, beans.Count * 0.55f);
        return t =>
        {
            Quake(t);
            Cam(new Vector3(0f, 1.3f, 2.3f + w * 0.25f), new Vector3(0f, 0.65f, -0.4f));
            fanSpeed = Mathf.Lerp(540f, 0f, Mathf.Clamp01(t / 1.2f));
            for (int i = 0; i < beans.Count; i++)
            {
                var b = beans[i];
                float wakeAt = 0.55f + i * 0.13f;
                if (!woke[i] && t > wakeAt) { woke[i] = true; Anim(b, "WakeUp", 0.08f); Eyes(b, true); Face(b, ChibiLook.Face.Surprised); }
                if (!fanning[i] && t > wakeAt + 1.15f) { fanning[i] = true; Anim(b, "HotFan", 0.2f, i * 0.31f % 1f); Face(b, ChibiLook.Face.Sad); }
                Drops(b, fanning[i] ? t - wakeAt - 1.15f : -1f);
            }
            // lamp flickers, then the power dies
            if (t > 1.6f && t < 2.2f) { bool on = Mathf.PerlinNoise(t * 16f, 0.5f) > 0.45f; Lamp(on); }
            if (!dark && t >= 2.2f)
            {
                dark = true; Lamp(false);
                RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.3f); DynamicGI.UpdateEnvironment();
                Caption("The POWER is OUT!", "No fan... no lights... sooo HOT!");
            }
        };
    }

    System.Action<float> ShotCause()
    {
        Outdoors(true);
        foreach (var b in beans) b.t.gameObject.SetActive(false);
        Vector3 box = powerBox.localPosition;
        Vector3 camPos = box + new Vector3(-2.2f, 1.5f, -2.6f), look = box + new Vector3(0f, 1.3f, 0f);
        int cause = spyTease ? 9 : weather;            // 9 = the spy did it
        string[] titles = { "POP! FIZZ!", "FZZT... POP!", "POP! FIZZ!", "KA-BOOM!", "BONK!", "CRASH!" };
        string[] subs = { "The old power box gave up...", "Rain got into the power box...", "The old power box gave up...", "Lightning hit the power box!", "The wind threw a branch at the power box!", "The quake knocked the power box over!" };
        Caption("Meanwhile, at the power box...", "");
        bool popped = false;
        var sparks = new List<Transform>(); var smoke = new List<Transform>();
        for (int i = 0; i < 8; i++) { var s = Prim(PrimitiveType.Sphere, root, box, Vector3.one * 0.08f, Spark, true); s.gameObject.SetActive(false); sparks.Add(s); }
        for (int i = 0; i < 6; i++) { var s = Prim(PrimitiveType.Sphere, root, box, Vector3.one * 0.3f, new Color(0.45f, 0.45f, 0.5f)); s.gameObject.SetActive(false); smoke.Add(s); }
        Transform bolt = null, branch = null;
        if (cause == 3) bolt = Bolt(box + Vector3.up * 1.9f);
        if (cause == 4) { branch = Prim(PrimitiveType.Cylinder, root, box, new Vector3(0.12f, 0.9f, 0.12f), DarkWood); branch.gameObject.SetActive(false); }
        Bean shadow = null;
        if (cause == 9) shadow = ShadowBean(box + new Vector3(0.8f, 0f, -0.2f));
        const float popAt = 1.1f;
        return t =>
        {
            Quake(cause == 5 ? t : -1f);
            Vector3 shake = t > popAt && t < popAt + 0.35f ? Random.insideUnitSphere * 0.04f : Vector3.zero;
            Cam(Vector3.Lerp(camPos, camPos + new Vector3(0.4f, -0.1f, 0.6f), Ease(t / causeShot)) + shake, look);
            // before the pop: the box hums, little sparks
            float pre = Mathf.Clamp01(t / popAt);
            if (boxLight != null) boxLight.intensity = t < popAt ? 1.5f + Mathf.PerlinNoise(t * 20f, 0f) * 2f : Mathf.Max(0f, 6f - (t - popAt) * 12f);
            if (cause == 3 && bolt != null) bolt.gameObject.SetActive(t > popAt - 0.15f && t < popAt + 0.25f && Mathf.Sin(t * 60f) > -0.3f);
            if (cause == 3 && t > popAt - 0.15f && t < popAt + 0.3f) Flash(1f - (t - popAt + 0.15f) / 0.45f);
            if (cause == 4 && branch != null)
            {
                branch.gameObject.SetActive(t < popAt + 0.6f);
                float k = Mathf.Clamp01(t / popAt);
                branch.localPosition = t < popAt ? Vector3.Lerp(box + new Vector3(-6f, 3f, 1f), box + new Vector3(0f, 1.3f, -0.2f), k) : box + new Vector3(0.3f + (t - popAt), 1.3f - (t - popAt) * 2f, -0.3f);
                branch.localRotation = Quaternion.Euler(t * 700f, 0f, 70f);
            }
            if (cause == 5) powerBox.localRotation = Quaternion.Euler(0f, 0f, t < popAt ? Mathf.Sin(t * 40f) * 3f * pre : -Mathf.Min(80f, (t - popAt) * 320f));
            if (cause == 9 && shadow != null)
            {
                // the shadow fiddles with the box, then sneaks off... and looks back
                if (shadow.anim != null) shadow.anim.SetFloat("Speed", t < popAt || t > causeShot - 0.8f ? 0f : 2.2f);
                if (t < popAt) shadow.t.localRotation = Quaternion.Euler(0f, -90f, 0f);
                else
                {
                    shadow.t.localRotation = Quaternion.Euler(0f, t > causeShot - 0.8f ? 200f : 90f, 0f);
                    shadow.t.localPosition = box + new Vector3(0.8f + Mathf.Min(t - popAt, causeShot - 0.8f - popAt) * 1.6f, 0f, -0.2f);
                }
            }
            for (int i = 0; i < sparks.Count; i++)
            {
                float a = (t * 2.3f + i / (float)sparks.Count) % 1f;
                bool on = t < popAt ? Mathf.PerlinNoise(t * 9f, i) > 0.62f : t < popAt + 0.6f;
                sparks[i].gameObject.SetActive(on);
                float spread = t < popAt ? 0.4f : 1.6f;
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * new Vector3(1f, 0f, 0f);
                sparks[i].localPosition = box + new Vector3(0f, 1.3f, -0.25f) + dir * a * spread + Vector3.up * (a * 0.8f - a * a * 1.6f);
            }
            if (!popped && t >= popAt)
            {
                popped = true;
                Caption(cause == 9 ? "POP! ...wait... who was THAT!" : titles[weather], cause == 9 ? "Someone at this sleepover is up to no good..." : subs[weather]);
                if (cause != 3) Flash(0.6f);
            }
            for (int i = 0; i < smoke.Count; i++)
            {
                float a = t - popAt - i * 0.12f;
                smoke[i].gameObject.SetActive(a > 0f);
                if (a > 0f) { smoke[i].localPosition = box + new Vector3(Mathf.Sin(i * 2f) * 0.2f, 1.5f + a * 0.9f, -0.2f); smoke[i].localScale = Vector3.one * (0.2f + a * 0.5f); }
            }
            if (t > popAt + 0.3f && cause != 3) Flash(Mathf.Max(0f, 0.6f - (t - popAt) * 2f));
        };
    }

    System.Action<float> ShotEscape()
    {
        Outdoors(true);
        Caption("The gates spring open...", "");
        foreach (var r in runners) r.go.gameObject.SetActive(false);
        int part = -1;
        return t =>
        {
            int p = t < escapeShot / 3f ? 0 : t < escapeShot * 2f / 3f ? 1 : 2;
            float local = t - p * escapeShot / 3f;
            if (p != part)
            {
                part = p;
                foreach (var r in runners) r.go.gameObject.SetActive(r.group == p);
                Caption(p == 0 ? "The animals are LOOSE!" : p == 1 ? "...very casually" : "...and away they hop!", p == 0 ? "Chickens everywhere!" : p == 1 ? "Moo." : "");
            }
            var pen = pens[p];
            pen.gate.localRotation = Quaternion.Euler(0f, -Mathf.Lerp(0f, 115f, Mathf.Clamp01(local / 0.25f)), 0f);
            Vector3[] camAt = { new Vector3(0.6f, 0.75f, -4.6f), new Vector3(-3.4f, 0.55f, -2.4f), new Vector3(3.2f, 1.1f, -3.8f) };
            Vector3[] lookAt = { new Vector3(0f, 0.55f, 0f), new Vector3(0f, 0.9f, 0.4f), new Vector3(-0.5f, 0.35f, 0f) };
            Cam(pen.at + camAt[p] + new Vector3(0f, 0f, local * 0.25f), pen.at + lookAt[p]);
            foreach (var r in runners) if (r.group == p) r.Step(local);
        };
    }

    System.Action<float> ShotWindow()
    {
        Outdoors(true);
        Caption("Everyone runs to the window...", "");
        int n = beans.Count;
        var from = new Vector3[n]; var seat = new Vector3[n]; var land = new Vector3[n];
        var dived = new bool[n]; var landed = new bool[n];
        float span = Mathf.Min(2.3f, (n - 1) * 0.58f);
        for (int i = 0; i < n; i++)
        {
            var b = beans[i];
            b.t.gameObject.SetActive(true);
            from[i] = b.t.localPosition;
            seat[i] = new Vector3(n == 1 ? 0f : Mathf.Lerp(-span / 2f, span / 2f, i / (float)(n - 1)), 0.67f, 2.72f);
            land[i] = new Vector3(Mathf.Sin(i * 2.4f) * 0.35f, 0.33f * i, 5.4f + Mathf.Cos(i * 1.7f) * 0.2f);
            b.t.localRotation = Quaternion.identity;
            Anim(b, "Panic", 0.15f, i * 0.27f % 1f); Face(b, ChibiLook.Face.Surprised); Drops(b, -1f);
        }
        bool told = false;
        const float diveAt = 1.75f, diveGap = 0.17f, flight = 0.62f;
        return t =>
        {
            if (t < diveAt) Cam(new Vector3(0f, 1.75f, 5.3f), new Vector3(0f, 1.35f, 2.8f));
            else Cam(new Vector3(4.4f, 1.5f, 3.6f + (t - diveAt) * 0.3f), new Vector3(0f, 0.9f, 4.4f));
            if (!told && t >= diveAt - 0.35f) { told = true; Caption("Everybody... OUT!", "Round them up before sunrise!"); }
            for (int i = 0; i < n; i++)
            {
                var b = beans[i];
                float d0 = diveAt + i * diveGap;
                if (t < 0.6f) b.t.localPosition = Arc(from[i], seat[i], t / 0.6f, 0.5f);
                else if (t < d0) b.t.localPosition = seat[i] + Vector3.up * Mathf.Abs(Mathf.Sin(t * 9f + i)) * 0.05f;
                else if (t < d0 + flight)
                {
                    if (!dived[i]) { dived[i] = true; Anim(b, "Dive", 0.05f); Face(b, ChibiLook.Face.Determined); }
                    float k = (t - d0) / flight;
                    Vector3 p = Vector3.Lerp(seat[i], land[i], k); p.y = Mathf.Lerp(seat[i].y, land[i].y, k) + Mathf.Sin(k * Mathf.PI) * 1.4f;
                    b.t.localPosition = p;
                }
                else
                {
                    if (!landed[i]) { landed[i] = true; Anim(b, "Fall", 0.03f, 0.33f); Face(b, ChibiLook.Face.Happy); b.t.localRotation = Quaternion.Euler(0f, Mathf.Sin(i * 3f) * 25f, 0f); }
                    float s = Mathf.Clamp01((t - d0 - flight - 0.9f) / 0.4f);
                    b.t.localPosition = Vector3.Lerp(land[i], new Vector3(land[i].x * 2.2f + (i - (n - 1) / 2f) * 0.5f, 0f, land[i].z), s);
                }
            }
        };
    }

    System.Action<float> ShotCard(int i)
    {
        Outdoors(false);
        if (capBox != null) capBox.SetActive(false);
        for (int j = 0; j < beans.Count; j++) beans[j].t.gameObject.SetActive(j == i);
        var b = beans[i];
        b.t.localPosition = booth; b.t.localRotation = Quaternion.identity;
        string[] moves = { "Pose", "Cheer", "Wave", "Pose", "Cheer" };
        ChibiLook.Face[] faces = { ChibiLook.Face.Determined, ChibiLook.Face.Love, ChibiLook.Face.Wink, ChibiLook.Face.Happy, ChibiLook.Face.Angry };
        Anim(b, moves[i % moves.Length], 0f); Face(b, faces[i % faces.Length]); Drops(b, -1f);
        boothBack.GetComponent<Renderer>().sharedMaterial = U(CardColors[i % CardColors.Length]);
        foreach (var s in boothStripes) s.GetComponent<Renderer>().sharedMaterial = U(Color.Lerp(CardColors[i % CardColors.Length], Color.white, 0.35f));
        banner.gameObject.SetActive(true);
        banner.GetComponent<Image>().color = Color.Lerp(CardColors[i % CardColors.Length], Color.black, 0.15f);
        bannerName.text = b.who.name;
        bannerSub.text = beans.Count == 1 ? "READY TO ROUND UP SOME ANIMALS" : (i == 0 ? "THE HOST" : "JOINED THE PARTY");
        if (beans.Count > 1 && !NetSession.Online) bannerSub.text = "";
        float dur = beans.Count == 1 ? cardEach * 1.6f : cardEach;
        return t =>
        {
            float k = Ease(t / 0.25f);
            cam.fieldOfView = Mathf.Lerp(44f, 36f, Ease(t / dur));
            Cam(booth + new Vector3(0.35f, 0.95f, 2.6f), booth + new Vector3(0f, 0.68f, 0f));
            banner.anchoredPosition = new Vector2(Mathf.Lerp(-1800f, -260f, k) + t * 30f, 120f);
            foreach (var s in boothStripes) s.localPosition += new Vector3(Time.unscaledDeltaTime * 2f, 0f, 0f);
            foreach (var s in boothStripes) if (s.localPosition.x > booth.x + 5f) s.localPosition -= new Vector3(10f, 0f, 0f);
            if (t > dur - 0.1f) banner.gameObject.SetActive(false);
        };
    }

    System.Action<float> ShotTitle()
    {
        Outdoors(true);
        cam.fieldOfView = 55f;
        if (banner != null) banner.gameObject.SetActive(false);
        Caption("", "Catch the animals and fix the power before sunrise!");
        int n = beans.Count;
        for (int i = 0; i < n; i++)
        {
            var b = beans[i];
            b.t.gameObject.SetActive(true);
            b.t.localPosition = titleSpot + new Vector3(n == 1 ? 0f : Mathf.Lerp(-n * 0.45f, n * 0.45f, i / (float)(n - 1)), 0f, 0f);
            b.t.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Anim(b, "Cheer", 0f, i * 0.2f % 1f); Face(b, ChibiLook.Face.Happy);
        }
        foreach (var r in runners) { r.go.gameObject.SetActive(true); r.Wander(true); }
        title3D.gameObject.SetActive(true);
        Vector3 c0 = titleSpot + new Vector3(0f, 0.9f, -2.6f), c1 = titleSpot + new Vector3(0f, 1.5f, -5.4f);
        Vector3 titlePos = titleSpot + new Vector3(0f, 3.4f, 4f);
        return t =>
        {
            Cam(Vector3.Lerp(c0, c1, Ease(t / 1.6f)), Vector3.Lerp(titleSpot + Vector3.up * 0.7f, titleSpot + new Vector3(0f, 1.6f, 2f), Ease(t / 1.6f)));
            foreach (var r in runners) r.Step(t + 5f);
            float s = Mathf.Clamp01((t - 0.7f) / 0.45f);
            float bounce = 1f + Mathf.Sin(s * Mathf.PI) * 0.3f + (s >= 1f ? Mathf.Sin(t * 3f) * 0.03f : 0f);
            title3D.localScale = Vector3.one * Mathf.Max(0.001f, s * bounce);
            title3D.localPosition = titlePos + Vector3.up * Mathf.Sin(t * 2f) * 0.1f;
            title3D.rotation = Quaternion.LookRotation(title3D.position - cam.transform.position);
            if (s > 0f && s < 0.3f) shakeAmp = 0.05f; else shakeAmp = 0f;
        };
    }

    IEnumerator Shot(float dur, System.Func<System.Action<float>> begin)
    {
        var update = begin();
        next = false;
        float t = 0f;
        while (t < dur && !next && !skip)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            t += dt; clock += dt;
            if (fanPivot != null) fanPivot.Rotate(0f, fanSpeed * dt, 0f, Space.Self);
            foreach (var a in always) a(clock, dt);
            update(t);
            float fadeIn = 1f - Mathf.Clamp01(t / 0.25f), fadeOut = Mathf.Clamp01((t - (dur - 0.2f)) / 0.2f);
            fader.color = new Color(Night.r, Night.g, Night.b, Mathf.Max(fadeIn, fadeOut));
            yield return null;
        }
        if (next && !skip)
        {
            float f = 0f;
            while (f < 0.15f) { f += Time.unscaledDeltaTime; fader.color = new Color(Night.r, Night.g, Night.b, f / 0.15f); yield return null; }
        }
        shakeAmp = 0f;
    }

    // ================================================================ weather
    void BuildWeather()
    {
        // tonight's weather in every outdoor shot (and through the window)
        if (weather == 1 || weather == 3) weatherFx = Particles(new Color(0.75f, 0.82f, 1f), weather == 3 ? 1500 : 1100, new Vector3(-1.5f, -16f, 0.5f), 0.04f, true);
        if (weather == 4)
        {
            weatherFx = Particles(new Color32(0x9B, 0xC8, 0x6E, 255), 160, new Vector3(9f, -0.8f, 1.5f), 0.12f, false);
            foreach (var s in swaying)
            {
                var baseRot = s.localRotation; float ph = s.GetSiblingIndex() * 0.7f;
                always.Add((t, dt) => s.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.6f + ph) * 6f + 5f));
            }
        }
        if (weather == 3)   // lightning every few seconds
            always.Add((t, dt) =>
            {
                float ph = t % 4.3f;
                if (ph > 1.2f && ph < 1.5f) Flash(Mathf.Sin((ph - 1.2f) / 0.3f * Mathf.PI) * (Mathf.Sin(t * 70f) > 0f ? 0.8f : 0.4f));
            });
        if (weatherFx != null) always.Add((t, dt) =>
        {
            if (cam != null) weatherFx.transform.position = cam.transform.position + cam.transform.forward * 6f + Vector3.up * 7f;
        });
    }

    ParticleSystem Particles(Color c, int rate, Vector3 velocity, float size, bool streaks)
    {
        var go = new GameObject("Intro_Weather");
        go.transform.SetParent(root, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.startLifetime = 1.6f; main.startSpeed = 0f; main.startSize = size; main.maxParticles = 3000;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = true; main.startColor = c;
        if (!streaks) { main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); }
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(22f, 1f, 18f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(velocity.x * 0.8f, velocity.x * 1.2f);
        vel.y = new ParticleSystem.MinMaxCurve(velocity.y * 0.9f, velocity.y * 1.1f);
        vel.z = new ParticleSystem.MinMaxCurve(velocity.z * 0.8f, velocity.z * 1.2f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = 0.035f; r.lengthScale = 1f;
        r.sharedMaterial = U(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    void Outdoors(bool on)
    {
        if (weatherFx != null) weatherFx.gameObject.SetActive(on);
    }

    void Quake(float t)
    {
        if (weather != 5 || t < 0f) { shakeAmp = 0f; return; }
        float ph = t % 2.4f;
        shakeAmp = ph < 0.7f ? 0.06f * Mathf.Sin(ph / 0.7f * Mathf.PI) : 0f;
    }

    void Flash(float a)
    {
        if (flash == null) return;
        flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a) * 0.75f);
        if (moonLight != null) moonLight.intensity = 0.55f + Mathf.Clamp01(a) * 2.5f;
    }

    // ================================================================ the set
    Vector3 houseLook, booth, titleSpot;
    Transform boothBack;
    readonly List<Transform> boothStripes = new List<Transform>();

    void BuildSet(List<Who> cast)
    {
        root = new GameObject("Intro3D_Set").transform;
        root.position = Origin;

        // ground + sky bits
        Prim(PrimitiveType.Cube, root, new Vector3(-20f, -0.06f, 10f), new Vector3(160f, 0.1f, 120f), Grass);
        moonLight = new GameObject("Moonlight").AddComponent<Light>();
        moonLight.transform.SetParent(root, false);
        moonLight.type = LightType.Directional; moonLight.color = new Color(0.62f, 0.68f, 1f); moonLight.intensity = 0.55f; moonLight.shadows = LightShadows.None;
        moonLight.transform.rotation = Quaternion.Euler(40f, 160f, 0f);
        if (weather == 0 || weather == 4 || weather == 5)
        {
            Prim(PrimitiveType.Sphere, root, new Vector3(-30f, 30f, 85f), Vector3.one * 7f, new Color(1f, 0.93f, 0.68f), true);   // moon
            var rng = new System.Random(4);
            for (int i = 0; i < 70; i++)
            {
                float yaw = (float)rng.NextDouble() * 220f - 110f, el = 12f + (float)rng.NextDouble() * 50f;
                Vector3 dir = Quaternion.Euler(-el, yaw, 0f) * Vector3.forward;
                var s = Prim(PrimitiveType.Sphere, root, new Vector3(-20f, 0f, 10f) + dir * 110f, Vector3.one * (0.35f + (float)rng.NextDouble() * 0.45f), new Color(1f, 0.97f, 0.88f), true);
                float ph = (float)rng.NextDouble() * 6f; Vector3 baseScale = s.localScale;
                always.Add((t, dt) => s.localScale = baseScale * (0.7f + 0.3f * Mathf.Sin(t * 2.2f + ph)));
            }
        }
        else
        {
            // clouds instead of stars
            var rng = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                float yaw = (float)rng.NextDouble() * 200f - 100f, el = 15f + (float)rng.NextDouble() * 30f;
                Vector3 dir = Quaternion.Euler(-el, yaw, 0f) * Vector3.forward;
                Prim(PrimitiveType.Sphere, root, new Vector3(-20f, 0f, 10f) + dir * 100f, new Vector3(22f, 7f, 10f) * (0.6f + (float)rng.NextDouble() * 0.6f), new Color(0.24f, 0.25f, 0.38f), true);
            }
        }

        BuildHouseExterior(new Vector3(-60f, 0f, 0f));
        BuildBedroom(cast);
        BuildPowerBox(new Vector3(9f, 0f, 9f));
        BuildPens();
        BuildBooth(new Vector3(40f, 0f, -40f));
        titleSpot = new Vector3(1f, 0f, 9.5f);
        BuildWeather();

        var tt = Text3D("FARM CHAOS!", 13f, Color.white, new Color32(0xE0, 0x6C, 0x8C, 255));
        tt.rectTransform.sizeDelta = new Vector2(60f, 10f);
        title3D = tt.transform; title3D.gameObject.SetActive(false);
    }

    void BuildHouseExterior(Vector3 at)
    {
        if (farmhouse != null)
        {
            var h = Instantiate(farmhouse, root);
            h.transform.localPosition = at; h.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            StripPhysics(h);
            var b = Bounds(h);
            float scale = 9f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            h.transform.localScale *= scale;
            b = Bounds(h);
            h.transform.position += new Vector3(W(at).x - b.center.x, W(at).y - b.min.y, W(at).z - b.center.z);
            b = Bounds(h);
            houseLook = root.InverseTransformPoint(b.center);
        }
        else
        {
            Prim(PrimitiveType.Cube, root, at + new Vector3(0, 2f, 0), new Vector3(7f, 4f, 5f), Cream);
            var roof = Prim(PrimitiveType.Cube, root, at + new Vector3(0, 4.6f, 0), new Vector3(5.2f, 5.2f, 5.4f), Pink);
            roof.localRotation = Quaternion.Euler(0, 0, 45f); roof.localScale = new Vector3(3.8f, 3.8f, 5.4f);
            houseLook = at + new Vector3(0, 2.5f, 0);
        }
        var porch = new GameObject("Porch_Light").AddComponent<Light>();
        porch.transform.SetParent(root, false); porch.transform.localPosition = houseLook + new Vector3(0f, 0f, -6f);
        porch.type = LightType.Point; porch.color = new Color(1f, 0.78f, 0.5f); porch.range = 16f; porch.intensity = 4f; porch.shadows = LightShadows.None;
        float[] tx = { -14f, -9f, 8f, 13f, -17f, 16f };
        float[] tz = { 4f, 9f, 6f, 1f, -6f, -5f };
        for (int i = 0; i < tx.Length; i++) PlaceProp(i % 2 == 0 ? tree : bush, at + new Vector3(tx[i], 0f, tz[i]), i % 2 == 0 ? 5f : 1.6f, i * 50f);
        for (int i = -5; i <= 5; i++) FencePost(at + new Vector3(i * 1.6f, 0f, -8f));
        Prim(PrimitiveType.Cube, root, at + new Vector3(0, 0.55f, -8f), new Vector3(17f, 0.12f, 0.08f), Cream);
    }

    void BuildBedroom(List<Who> cast)
    {
        // floor, ceiling, walls; the back wall (+z) has a wide window (x -1.4..1.4, y 1.0..2.2)
        Prim(PrimitiveType.Cube, root, new Vector3(0, -0.05f, 0), new Vector3(6.2f, 0.1f, 6.2f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(0, 3.05f, 0), new Vector3(6.2f, 0.1f, 6.2f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(-3.05f, 1.5f, 0), new Vector3(0.1f, 3.1f, 6.2f), Pink);
        Prim(PrimitiveType.Cube, root, new Vector3(3.05f, 1.5f, 0), new Vector3(0.1f, 3.1f, 6.2f), Pink);
        Prim(PrimitiveType.Cube, root, new Vector3(0, 1.5f, -3.05f), new Vector3(6.2f, 3.1f, 0.1f), Pink);
        Prim(PrimitiveType.Cube, root, new Vector3(-2.25f, 1.5f, 3.05f), new Vector3(1.7f, 3.1f, 0.1f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(2.25f, 1.5f, 3.05f), new Vector3(1.7f, 3.1f, 0.1f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.5f, 3.05f), new Vector3(2.8f, 1.0f, 0.1f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 2.65f, 3.05f), new Vector3(2.8f, 0.9f, 0.1f), Cream);
        // window frame, curtains, window seat
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.98f, 2.95f), new Vector3(3.0f, 0.08f, 0.25f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 2.22f, 2.98f), new Vector3(3.0f, 0.08f, 0.14f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(-1.44f, 1.6f, 2.98f), new Vector3(0.08f, 1.32f, 0.14f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(1.44f, 1.6f, 2.98f), new Vector3(0.08f, 1.32f, 0.14f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(-1.72f, 1.55f, 2.92f), new Vector3(0.4f, 1.7f, 0.06f), Mint);
        Prim(PrimitiveType.Cube, root, new Vector3(1.72f, 1.55f, 2.92f), new Vector3(0.4f, 1.7f, 0.06f), Mint);
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 2.45f, 2.9f), new Vector3(4.0f, 0.1f, 0.1f), DarkWood);
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.3f, 2.62f), new Vector3(3.0f, 0.6f, 0.72f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.63f, 2.62f), new Vector3(2.95f, 0.08f, 0.68f), Blue);
        // rug, side table + lamp in the corner, a ceiling light, the ceiling fan
        Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0.9f), new Vector3(3.2f, 0.01f, 1.6f), Mint);
        Prim(PrimitiveType.Cube, root, new Vector3(-2.55f, 0.28f, -2.45f), new Vector3(0.55f, 0.56f, 0.5f), Wood);
        Prim(PrimitiveType.Cylinder, root, new Vector3(-2.55f, 0.66f, -2.45f), new Vector3(0.1f, 0.1f, 0.1f), Cream);
        bulb = Prim(PrimitiveType.Sphere, root, new Vector3(-2.55f, 0.82f, -2.45f), Vector3.one * 0.16f, new Color(1f, 0.9f, 0.6f), true).GetComponent<Renderer>();
        Prim(PrimitiveType.Cylinder, root, new Vector3(-2.55f, 0.95f, -2.45f), new Vector3(0.34f, 0.12f, 0.34f), Pink);
        lampLight = PointLight("Lamp_Light", new Vector3(-2.4f, 1.1f, -2.2f), new Color(1f, 0.78f, 0.52f), 6f, 3f);
        roomLight = PointLight("Room_Light", new Vector3(0f, 2.5f, 0.3f), new Color(1f, 0.86f, 0.7f), 8f, 1.6f);
        Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 2.9f, -0.4f), new Vector3(0.05f, 0.12f, 0.05f), Cream);
        fanPivot = new GameObject("Fan").transform; fanPivot.SetParent(root, false); fanPivot.localPosition = new Vector3(0f, 2.76f, -0.4f);
        Prim(PrimitiveType.Cylinder, fanPivot, Vector3.zero, new Vector3(0.24f, 0.05f, 0.24f), Cream);
        for (int i = 0; i < 4; i++)
        {
            var arm = new GameObject("Blade").transform; arm.SetParent(fanPivot, false); arm.localRotation = Quaternion.Euler(0, i * 90f, 0);
            var bl = Prim(PrimitiveType.Cube, arm, new Vector3(0.5f, 0f, 0f), new Vector3(0.8f, 0.025f, 0.2f), i % 2 == 0 ? Pink : Mint);
            bl.localRotation = Quaternion.Euler(8f, 0, 0);
        }

        // a sleeping mat + pillow + bean for every player, side by side
        int n = cast.Count;
        float span = Mathf.Min(4.4f, (n - 1) * 1.1f);
        for (int i = 0; i < n; i++)
        {
            float x = n == 1 ? 0f : Mathf.Lerp(-span / 2f, span / 2f, i / (float)(n - 1));
            Prim(PrimitiveType.Cube, root, new Vector3(x, 0.03f, -0.45f), new Vector3(0.95f, 0.06f, 1.9f), CardColors[i % CardColors.Length]);
            Prim(PrimitiveType.Cube, root, new Vector3(x, 0.1f, -1.05f), new Vector3(0.7f, 0.14f, 0.38f), Cream);
            var b = MakeBean(cast[i], new Vector3(x, 0.06f, -0.15f));
            if (b != null) beans.Add(b);
        }
    }

    void Lamp(bool on)
    {
        lampLight.enabled = on; roomLight.enabled = on;
        bulb.sharedMaterial = U(on ? new Color(1f, 0.9f, 0.6f) : new Color(0.3f, 0.3f, 0.35f));
    }

    void BuildPowerBox(Vector3 at)
    {
        Prim(PrimitiveType.Cylinder, root, at + new Vector3(0.45f, 1.6f, 0.15f), new Vector3(0.14f, 1.6f, 0.14f), DarkWood);    // pole
        Prim(PrimitiveType.Cube, root, at + new Vector3(0.45f, 3.0f, 0.15f), new Vector3(1.2f, 0.08f, 0.08f), DarkWood);
        var wire = Prim(PrimitiveType.Cylinder, root, at + new Vector3(-2.5f, 3.0f, 0.15f), new Vector3(0.025f, 3f, 0.025f), Ink);
        wire.localRotation = Quaternion.Euler(0f, 0f, 90f);
        powerBox = new GameObject("PowerBox").transform; powerBox.SetParent(root, false); powerBox.localPosition = at;
        Prim(PrimitiveType.Cube, powerBox, new Vector3(0f, 1.3f, 0f), new Vector3(0.75f, 0.95f, 0.4f), Metal);
        boxLid = Prim(PrimitiveType.Cube, powerBox, new Vector3(0f, 1.3f, -0.21f), new Vector3(0.68f, 0.86f, 0.03f), new Color32(0xA9, 0xB1, 0xC4, 255));
        Prim(PrimitiveType.Cube, powerBox, new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.9f, 0.12f), Metal);
        // yellow warning sign with a lightning bolt
        var sign = Prim(PrimitiveType.Cube, powerBox, new Vector3(0f, 1.45f, -0.235f), new Vector3(0.3f, 0.3f, 0.01f), new Color32(0xFF, 0xD2, 0x4A, 255), true);
        sign.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var z = Text3D("!", 3f, Ink, new Color32(0xFF, 0xD2, 0x4A, 255)); z.transform.SetParent(powerBox, false);
        z.transform.localPosition = new Vector3(0f, 1.45f, -0.25f); z.transform.localRotation = Quaternion.identity;
        boxLight = PointLight("Box_Light", at + new Vector3(0f, 1.4f, -0.8f), new Color(1f, 0.85f, 0.4f), 4f, 2f);
        PlaceProp(bush, at + new Vector3(-1.2f, 0f, 0.6f), 1.2f, 30f);
        PlaceProp(tree, at + new Vector3(2.5f, 0f, 2.5f), 5f, 70f);
        var yl = PointLight("Yard_Light", at + new Vector3(-1f, 3f, -2.5f), new Color(1f, 0.85f, 0.65f), 9f, 2.2f);
    }

    Light PointLight(string name, Vector3 at, Color c, float range, float intensity)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(root, false); l.transform.localPosition = at;
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        return l;
    }

    // ---------------------------------------------------------------- pens + escaping animals
    class Pen { public Vector3 at; public Transform gate; }
    readonly List<Pen> pens = new List<Pen>();

    class Runner
    {
        public Transform go; public int group;
        public Vector3 a, b; public float delay, speed, hop, hopRate, flap;
        public bool wander; public Vector3 centre; public float radius, phase;
        public void Wander(bool on) { wander = on; }
        public void Step(float t)
        {
            if (wander)
            {
                float ang = t * speed * 0.35f / Mathf.Max(0.5f, radius) + phase;
                Vector3 p = centre + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius * 0.6f);
                Vector3 v = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang) * 0.6f);
                p.y = Mathf.Abs(Mathf.Sin(t * hopRate + phase)) * hop;
                go.localPosition = p; go.localRotation = Quaternion.LookRotation(v) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * hopRate) * 6f);
                return;
            }
            float k = Mathf.Clamp01((t - delay) * speed / Mathf.Max(0.01f, Vector3.Distance(a, b)));
            Vector3 pos = Vector3.Lerp(a, b, k);
            bool moving = k > 0f && k < 1f;
            pos.y = moving ? Mathf.Abs(Mathf.Sin((t - delay) * hopRate)) * hop : 0f;
            go.localPosition = pos;
            Vector3 d = b - a; d.y = 0f;
            float wob = moving ? Mathf.Sin((t - delay) * hopRate) * flap : 0f;
            if (d.sqrMagnitude > 0.001f) go.localRotation = Quaternion.LookRotation(d.normalized) * Quaternion.Euler(0f, 0f, wob);
        }
    }
    readonly List<Runner> runners = new List<Runner>();

    void BuildPens()
    {
        // three little pens in a row behind the house, each with a gate that springs open
        Vector3[] at = { new Vector3(0f, 0f, 16f), new Vector3(-12f, 0f, 16f), new Vector3(12f, 0f, 16f) };
        foreach (var p in at)
        {
            var pen = new Pen { at = p };
            for (int i = -4; i <= 4; i++) if (i != 0) FencePost(p + new Vector3(i * 1.0f, 0f, 0f));
            for (int i = 1; i <= 5; i++) { FencePost(p + new Vector3(-4f, 0f, i * 1.0f)); FencePost(p + new Vector3(4f, 0f, i * 1.0f)); }
            Prim(PrimitiveType.Cube, root, p + new Vector3(-2.5f, 0.55f, 0f), new Vector3(3.0f, 0.1f, 0.08f), Cream);
            Prim(PrimitiveType.Cube, root, p + new Vector3(2.5f, 0.55f, 0f), new Vector3(3.0f, 0.1f, 0.08f), Cream);
            Prim(PrimitiveType.Cylinder, root, p + new Vector3(-2.6f, 0.4f, 3.4f), new Vector3(1.2f, 0.4f, 1.2f), Straw);
            pen.gate = new GameObject("Gate").transform; pen.gate.SetParent(root, false); pen.gate.localPosition = p + new Vector3(-0.95f, 0f, 0f);
            Prim(PrimitiveType.Cube, pen.gate, new Vector3(0.95f, 0.55f, 0f), new Vector3(1.9f, 0.1f, 0.08f), Straw);
            Prim(PrimitiveType.Cube, pen.gate, new Vector3(0.95f, 0.3f, 0f), new Vector3(1.9f, 0.1f, 0.08f), Straw);
            Prim(PrimitiveType.Cube, pen.gate, new Vector3(0.95f, 0.42f, 0f), new Vector3(0.08f, 0.4f, 0.08f), Straw);
            PointLight("Pen_Light", p + new Vector3(0f, 3f, -3f), new Color(1f, 0.86f, 0.66f), 10f, 2.6f);
            PlaceProp(tree, p + new Vector3(5.5f, 0f, 4f), 5.5f, p.x * 7f);
            pens.Add(pen);
        }
        // 0: chickens burst out, flapping, straight at the camera
        for (int i = 0; i < 6; i++)
        {
            var c = AnimalOf(chickenPrefab, 0.55f, Chicken);
            Vector3 a = at[0] + new Vector3((i - 2.5f) * 0.4f, 0f, 1.2f + (i % 2) * 0.6f);
            Vector3 b = at[0] + new Vector3((i - 2.5f) * 1.1f, 0f, -4.5f - (i % 3) * 0.6f);
            runners.Add(new Runner { go = c, group = 0, a = a, b = b, delay = 0.15f + (i % 3) * 0.12f, speed = 3.2f, hop = 0.25f, hopRate = 16f, flap = 14f });
        }
        // 1: one cow strolls out like nothing happened
        var cow = AnimalOf(cowPrefab, 1.5f, Cow);
        runners.Add(new Runner { go = cow, group = 1, a = at[1] + new Vector3(0f, 0f, 2.2f), b = at[1] + new Vector3(0.3f, 0f, -3f), delay = 0.25f, speed = 1.0f, hop = 0.03f, hopRate = 6f, flap = 3f });
        // 2: bunnies hopping out in a neat line, a sheep bolting after them
        for (int i = 0; i < 5; i++)
        {
            var r = AnimalOf(bunnyPrefab, 0.5f, Rabbit);
            runners.Add(new Runner { go = r, group = 2, a = at[2] + new Vector3(0f, 0f, 1.0f + i * 0.7f), b = at[2] + new Vector3(-4.5f, 0f, -3.2f), delay = i * 0.16f, speed = 2.6f, hop = 0.3f, hopRate = 11f, flap = 4f });
        }
        var sheep = AnimalOf(sheepPrefab, 0.95f, Sheep);
        runners.Add(new Runner { go = sheep, group = 2, a = at[2] + new Vector3(1.2f, 0f, 3.0f), b = at[2] + new Vector3(-2.5f, 0f, -4f), delay = 0.5f, speed = 3.4f, hop = 0.1f, hopRate = 12f, flap = 5f });

        // for the title shot: everyone runs around the yard behind the beans
        var rng = new System.Random(11);
        foreach (var r in runners)
        {
            r.centre = new Vector3(1f + (float)(rng.NextDouble() * 10 - 5), 0f, 13.5f + (float)(rng.NextDouble() * 2.5));
            r.radius = 0.8f + (float)rng.NextDouble() * 1.6f; r.phase = (float)rng.NextDouble() * 6f;
            r.Step(0f); r.go.gameObject.SetActive(false);
        }
    }

    Transform AnimalOf(GameObject prefab, float height, System.Func<Transform> fallback)
    {
        if (prefab == null) return fallback();
        var holder = new GameObject("Holder"); holder.SetActive(false); holder.transform.SetParent(root, false);
        var g = Instantiate(prefab, holder.transform);
        // just the looks: no brains, no physics, no networking
        for (int pass = 0; pass < 3; pass++)
            foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        var a = new GameObject(prefab.name).transform; a.SetParent(root, false);
        g.transform.SetParent(a, false); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
        Destroy(holder);
        var b = Bounds(g);
        if (b.size.y > 0.01f) g.transform.localScale *= height / b.size.y;
        b = Bounds(g);
        g.transform.position += new Vector3(a.position.x - b.center.x, a.position.y - b.min.y, a.position.z - b.center.z);
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        return a;
    }

    void BuildBooth(Vector3 at)
    {
        booth = at;
        boothBack = Prim(PrimitiveType.Cube, root, at + new Vector3(0f, 2f, -1.2f), new Vector3(12f, 6f, 0.1f), CardColors[0], true);
        for (int i = 0; i < 10; i++)
        {
            var s = Prim(PrimitiveType.Cube, root, at + new Vector3(-5f + i * 1.1f, 2f, -1.13f), new Vector3(0.35f, 9f, 0.02f), Color.white, true);
            s.localRotation = Quaternion.Euler(0f, 0f, 30f);
            boothStripes.Add(s);
        }
        Prim(PrimitiveType.Cylinder, root, at + new Vector3(0f, -0.02f, 0f), new Vector3(1.6f, 0.04f, 1.6f), Cream);
        var key = PointLight("Booth_Key", at + new Vector3(1.2f, 2.2f, 2.2f), Color.white, 8f, 3.2f);
        PointLight("Booth_Fill", at + new Vector3(-1.6f, 1.2f, 1.6f), new Color(1f, 0.9f, 0.95f), 6f, 1.6f);
    }

    // ---------------------------------------------------------------- beans
    Bean MakeBean(Who who, Vector3 at)
    {
        if (beanPrefab == null) return null;
        var holder = new GameObject("Holder"); holder.SetActive(false); holder.transform.SetParent(root, false);
        var g = Instantiate(beanPrefab, holder.transform);
        g.name = "Bean_" + who.name;
        foreach (var m in g.GetComponentsInChildren<ChibiMotor>(true)) DestroyImmediate(m);
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        var look = g.GetComponentInChildren<ChibiLook>(true);
        if (look != null)
        {
            look.loadSaved = false;
            try { JsonUtility.FromJsonOverwrite(who.json, look.data); } catch { }
        }
        g.transform.SetParent(root, false);            // wakes up here (ChibiLook dresses it in that player's look)
        Destroy(holder);
        g.transform.localPosition = at; g.transform.localRotation = Quaternion.identity;

        var b = new Bean { who = who, t = g.transform, look = look, home = at };
        b.anim = g.GetComponentInChildren<Animator>(true);
        if (b.anim != null && beanAnimations != null) b.anim.runtimeAnimatorController = beanAnimations;
        if (b.anim != null) { b.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; b.anim.SetBool("Grounded", true); }
        foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") { b.head = t; break; }
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Eye_")) b.eyes.Add(r);
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        MakeLids(b);
        for (int i = 0; i < 2; i++) { var d = Prim(PrimitiveType.Sphere, root, at, new Vector3(0.06f, 0.09f, 0.06f), Sweat, true); d.gameObject.SetActive(false); b.sweat.Add(d); }
        b.zzz = Text3D("z Z", 2.4f, new Color(0.88f, 0.9f, 1f), Pink); b.zzz.gameObject.SetActive(false);
        return b;
    }

    // closed eyes for sleeping: two little "u" lines on each eye (the big round eyes hide underneath)
    void MakeLids(Bean b)
    {
        Renderer headR = b.head != null ? b.head.GetComponent<Renderer>() : null;
        Renderer rimL = b.eyes.FirstOrDefault(r => r.name == "Eye_L_Rim"), rimR = b.eyes.FirstOrDefault(r => r.name == "Eye_R_Rim");
        if (headR == null || rimL == null || rimR == null) return;
        Vector3 hc = headR.bounds.center, cl = rimL.bounds.center, cr = rimR.bounds.center;
        Vector3 side = (cr - cl).normalized;
        float r = Vector3.Distance(cl, cr) * 0.4f;
        foreach (var (rim, c) in new[] { (rimL, cl), (rimR, cr) })
        {
            Vector3 n = (c - hc).normalized;
            Vector3 up = Vector3.Cross(side, n); if (up.y < 0f) up = -up;
            var lid = new GameObject("Lid").transform;
            lid.SetParent(rim.transform, true);
            lid.SetPositionAndRotation(c + n * r * 0.15f, Quaternion.LookRotation(n, up));
            for (int s = -1; s <= 1; s += 2)
            {
                var seg = Prim(PrimitiveType.Capsule, lid, Vector3.zero, Vector3.one, Ink, true);
                seg.position = lid.position + lid.rotation * new Vector3(s * r * 0.42f, -r * 0.12f, 0f);
                seg.rotation = lid.rotation * Quaternion.Euler(0f, 0f, 90f + s * 22f);
                seg.localScale = Vector3.one; seg.SetParent(lid, true);
                Vector3 want = new Vector3(r * 0.2f, r * 0.5f, r * 0.08f);
                Vector3 ls = lid.lossyScale;
                seg.localScale = new Vector3(want.x / Mathf.Max(ls.x, 1e-4f), want.y / Mathf.Max(ls.y, 1e-4f), want.z / Mathf.Max(ls.z, 1e-4f));
            }
            lid.gameObject.SetActive(false);
            b.lids.Add(lid.gameObject);
        }
    }

    Bean ShadowBean(Vector3 at)
    {
        var b = MakeBean(new Who { name = "???", json = JsonUtility.ToJson(new ChibiLook.Data { hat = 2, hairStyle = 0 }) }, at);
        if (b == null) return null;
        b.t.gameObject.SetActive(true);
        if (b.look != null) Destroy(b.look);
        var dark = new Color(0.07f, 0.06f, 0.11f);
        var mpb = new MaterialPropertyBlock();
        foreach (var r in b.t.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.StartsWith("Eye_") || r.name.StartsWith("Brow_") || r.name.StartsWith("Mouth_") || r.name.StartsWith("Blush")) { r.enabled = false; continue; }
            for (int i = 0; i < r.sharedMaterials.Length; i++) { r.GetPropertyBlock(mpb, i); mpb.SetColor("_BaseColor", dark); mpb.SetColor("_EmissionColor", Color.black); r.SetPropertyBlock(mpb, i); }
        }
        // two glowing eyes
        foreach (var e in b.eyes.Where(r => r.name.EndsWith("_Rim")))
        {
            var g = Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.07f, new Color(1f, 0.95f, 0.5f), true);
            g.SetParent(e.transform, true); g.position = e.bounds.center; g.localScale = Vector3.one;
            var ls = g.lossyScale; g.localScale = new Vector3(0.07f / ls.x, 0.045f / ls.y, 0.07f / ls.z);
        }
        var q = Text3D("!!", 3.5f, new Color(1f, 0.95f, 0.5f), Ink); q.transform.SetParent(b.t, false); q.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        always.Add((t, dt) => { if (q != null && cam != null) q.transform.rotation = Quaternion.LookRotation(q.transform.position - cam.transform.position); });
        return b;
    }

    void Anim(Bean b, string state, float fade, float offset = 0f)
    {
        if (b?.anim == null) return;
        if (fade <= 0f) b.anim.Play(state, 0, offset); else b.anim.CrossFade(state, fade, 0, offset);
    }


    void Face(Bean b, ChibiLook.Face f) { if (b?.look != null) b.look.SetExpression(f, 0f); }

    void Eyes(Bean b, bool open)
    {
        foreach (var r in b.eyes) r.enabled = open;
        foreach (var l in b.lids) l.SetActive(!open);
    }

    void Zzz(Bean b, float t)
    {
        if (b.head == null) return;
        float a = (t * 0.5f) % 1f;
        b.zzz.transform.position = b.head.position + new Vector3(0.15f + a * 0.3f, 0.35f + a * 0.6f, 0f);
        b.zzz.alpha = Mathf.Sin(a * Mathf.PI);
        b.zzz.fontSize = 1.8f + a * 1.4f;
        b.zzz.transform.rotation = Quaternion.LookRotation(b.zzz.transform.position - cam.transform.position);
    }

    // sweat drops flying off the head (t < 0 = hide)
    void Drops(Bean b, float t)
    {
        for (int i = 0; i < b.sweat.Count; i++)
        {
            var d = b.sweat[i];
            if (t < 0f || b.head == null) { d.gameObject.SetActive(false); continue; }
            d.gameObject.SetActive(true);
            float a = (t * 1.4f + i * 0.5f) % 1f, s = i == 0 ? -1f : 1f;
            d.position = b.head.position + b.t.rotation * new Vector3(s * (0.36f + a * 0.25f), 0.2f + a * 0.2f - a * a * 0.7f, 0.05f);
        }
    }

    Transform Bolt(Vector3 top)
    {
        var b = new GameObject("Lightning").transform; b.SetParent(root, false);
        Vector3 p = top + new Vector3(-1.5f, 14f, 1f);
        var rng = new System.Random(3);
        for (int i = 0; i < 7; i++)
        {
            Vector3 q = i == 6 ? top : Vector3.Lerp(p, top, 1f / (7 - i)) + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 1.4f;
            var seg = Prim(PrimitiveType.Cube, b, (p + q) / 2f, new Vector3(0.12f, Vector3.Distance(p, q), 0.12f), new Color(0.95f, 0.97f, 1f), true);
            seg.localRotation = Quaternion.FromToRotation(Vector3.up, (q - p).normalized);
            p = q;
        }
        b.gameObject.SetActive(false);
        return b;
    }

    // ================================================================ cute fallback animals (forward = +z)
    Transform Animal(string name) { var a = new GameObject(name).transform; a.SetParent(root, false); return a; }

    Transform Chicken()
    {
        var a = Animal("Chicken");
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.3f, 0), new Vector3(0.45f, 0.4f, 0.5f), Color.white);
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.6f, 0.18f), Vector3.one * 0.28f, Color.white);
        Prim(PrimitiveType.Cube, a, new Vector3(0, 0.58f, 0.35f), new Vector3(0.09f, 0.06f, 0.12f), new Color32(0xF5, 0xA6, 0x23, 255));
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.77f, 0.17f), new Vector3(0.07f, 0.12f, 0.14f), Red);
        Prim(PrimitiveType.Sphere, a, new Vector3(-0.09f, 0.64f, 0.29f), Vector3.one * 0.05f, Ink, true);
        Prim(PrimitiveType.Sphere, a, new Vector3(0.09f, 0.64f, 0.29f), Vector3.one * 0.05f, Ink, true);
        return a;
    }

    Transform Sheep()
    {
        var a = Animal("Sheep");
        Vector3[] puffs = { new Vector3(0, 0.6f, 0), new Vector3(0.2f, 0.62f, 0.2f), new Vector3(-0.2f, 0.62f, 0.2f), new Vector3(0.2f, 0.6f, -0.2f), new Vector3(-0.2f, 0.6f, -0.2f), new Vector3(0, 0.78f, 0) };
        foreach (var p in puffs) Prim(PrimitiveType.Sphere, a, p, Vector3.one * 0.48f, Color.white);
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.62f, 0.48f), new Vector3(0.3f, 0.32f, 0.3f), Ink);
        foreach (var p in new[] { new Vector3(0.15f, 0.15f, 0.18f), new Vector3(-0.15f, 0.15f, 0.18f), new Vector3(0.15f, 0.15f, -0.18f), new Vector3(-0.15f, 0.15f, -0.18f) })
            Prim(PrimitiveType.Cylinder, a, p, new Vector3(0.1f, 0.16f, 0.1f), Ink);
        return a;
    }

    Transform Cow()
    {
        var a = Animal("Cow");
        var body = Prim(PrimitiveType.Capsule, a, new Vector3(0, 0.85f, 0), new Vector3(0.85f, 0.75f, 0.85f), Color.white);
        body.localRotation = Quaternion.Euler(90f, 0, 0);
        Prim(PrimitiveType.Sphere, a, new Vector3(0.38f, 0.95f, 0.15f), new Vector3(0.12f, 0.4f, 0.45f), Ink);
        Prim(PrimitiveType.Cube, a, new Vector3(0, 1.15f, 0.85f), new Vector3(0.5f, 0.45f, 0.45f), Color.white);
        Prim(PrimitiveType.Cube, a, new Vector3(0, 1.03f, 1.09f), new Vector3(0.46f, 0.22f, 0.1f), Pink);
        foreach (var p in new[] { new Vector3(0.25f, 0.25f, 0.45f), new Vector3(-0.25f, 0.25f, 0.45f), new Vector3(0.25f, 0.25f, -0.45f), new Vector3(-0.25f, 0.25f, -0.45f) })
            Prim(PrimitiveType.Cylinder, a, p, new Vector3(0.16f, 0.25f, 0.16f), Color.white);
        return a;
    }

    Transform Rabbit()
    {
        var a = Animal("Bunny");
        Color fur = new Color32(0xFF, 0xF4, 0xF6, 255);
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.22f, 0), new Vector3(0.4f, 0.38f, 0.45f), fur);
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.45f, 0.18f), Vector3.one * 0.3f, fur);
        Prim(PrimitiveType.Capsule, a, new Vector3(-0.07f, 0.7f, 0.14f), new Vector3(0.08f, 0.17f, 0.05f), fur);
        Prim(PrimitiveType.Capsule, a, new Vector3(0.07f, 0.7f, 0.14f), new Vector3(0.08f, 0.17f, 0.05f), fur);
        return a;
    }

    // ================================================================ captions / UI
    void BuildUI()
    {
        if (stage != null) Destroy(stage.gameObject);
        var go = new GameObject("Intro3D_UI", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        stage = go.GetComponent<RectTransform>(); Stretch(stage); stage.SetAsLastSibling();
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);   // catches taps
        go.GetComponent<Button>().transition = Selectable.Transition.None;
        go.GetComponent<Button>().onClick.AddListener(() => next = true);

        // lightning flash (under everything else)
        var fl = Box(stage, null, new Color(1f, 1f, 1f, 0f), Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero); Stretch(fl);
        flash = fl.GetComponent<Image>();

        // cinematic bars
        var top = Box(stage, null, Color.black, new Vector2(0, 70), new Vector2(0.5f, 1f), new Vector2(0, -35)); top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.sizeDelta = new Vector2(0, 70);
        var bot = Box(stage, null, Color.black, new Vector2(0, 70), new Vector2(0.5f, 0f), new Vector2(0, 35)); bot.anchorMin = new Vector2(0, 0); bot.anchorMax = new Vector2(1, 0); bot.sizeDelta = new Vector2(0, 70);

        // caption pill
        var pill = Box(stage, rounded, new Color(0.1f, 0.09f, 0.2f, 0.62f), new Vector2(1500, 180), new Vector2(0.5f, 0f), new Vector2(0, 190));
        capBox = pill.gameObject;
        capTitle = Txt(pill, "", 64, Color.white, new Vector2(0, 32), new Vector2(1460, 90));
        capTitle.outlineWidth = 0.18f; capTitle.outlineColor = new Color32(0xE0, 0x6C, 0x8C, 255);
        capSub = Txt(pill, "", 40, new Color(1f, 0.95f, 0.88f), new Vector2(0, -42), new Vector2(1460, 60));
        capSub.fontStyle = FontStyles.Normal; capSub.enableAutoSizing = true; capSub.fontSizeMin = 22; capSub.fontSizeMax = 40;

        // player-card banner (fighting-game style, slides in from the left)
        banner = Box(stage, null, CardColors[0], new Vector2(1500, 230), new Vector2(0.5f, 0.5f), new Vector2(-1800, 120));
        banner.localRotation = Quaternion.Euler(0f, 0f, 6f);
        bannerName = Txt(banner, "YOU", 130, Color.white, new Vector2(80, 25), new Vector2(1300, 150));
        bannerName.outlineWidth = 0.22f; bannerName.outlineColor = Ink; bannerName.alignment = TextAlignmentOptions.Left;
        bannerSub = Txt(banner, "", 40, new Color(1f, 0.97f, 0.9f), new Vector2(80, -78), new Vector2(1300, 50));
        bannerSub.alignment = TextAlignmentOptions.Left;
        banner.gameObject.SetActive(false);

        // fader (under the skip button)
        var f = Box(stage, null, Night, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero); Stretch(f);
        fader = f.GetComponent<Image>();

        // skip
        var sb = Box(stage, rounded, new Color(1f, 1f, 1f, 0.88f), new Vector2(190, 70), new Vector2(1f, 1f), new Vector2(-130, -115));
        sb.GetComponent<Image>().raycastTarget = true;
        sb.gameObject.AddComponent<Button>().onClick.AddListener(() => skip = true);
        Txt(sb, "Skip  >", 34, Brown, Vector2.zero, new Vector2(180, 64));
    }

    void Caption(string title, string sub)
    {
        if (capTitle == null) return;
        capBox.SetActive(true);
        capTitle.text = title; capSub.text = sub;
        capTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
        capSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        capSub.rectTransform.anchoredPosition = string.IsNullOrEmpty(title) ? Vector2.zero : new Vector2(0, -42);
        capTitle.rectTransform.anchoredPosition = string.IsNullOrEmpty(sub) ? Vector2.zero : new Vector2(0, 32);
    }

    RectTransform Box(RectTransform parent, Sprite sprite, Color color, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        var g = new GameObject("Box", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var r = g.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = anchor; r.sizeDelta = size; r.anchoredPosition = pos;
        var img = g.GetComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false;
        if (sprite != null) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 60f / 40f; }
        return r;
    }

    TextMeshProUGUI Txt(RectTransform parent, string text, float size, Color color, Vector2 pos, Vector2 box)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = color; t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = box; r.anchoredPosition = pos;
        return t;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    // ================================================================ helpers
    Vector3 W(Vector3 local) => root.TransformPoint(local);

    void Cam(Vector3 pos, Vector3 look)
    {
        Vector3 shake = shakeAmp > 0f ? Random.insideUnitSphere * shakeAmp : Vector3.zero;
        cam.transform.position = W(pos) + shake;
        cam.transform.rotation = Quaternion.LookRotation(W(look) - W(pos));
    }

    static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static Vector3 Arc(Vector3 a, Vector3 b, float k, float height)
    {
        k = Mathf.Clamp01(k);
        return Vector3.Lerp(a, b, Ease(k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * height;
    }

    Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, bool glow = false)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = glow ? U(color) : L(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return g.transform;
    }

    Material L(Color c)
    {
        if (lit.TryGetValue(c, out var m)) return m;
        m = litBase != null ? new Material(litBase) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        lit[c] = m; return m;
    }

    Material U(Color c)
    {
        if (unlit.TryGetValue(c, out var m)) return m;
        m = unlitBase != null ? new Material(unlitBase) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.color = c;
        unlit[c] = m; return m;
    }

    TextMeshPro Text3D(string text, float size, Color color, Color outline)
    {
        var g = new GameObject("Text3D_" + text); g.transform.SetParent(root, false);
        var t = g.AddComponent<TextMeshPro>();
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(6f, 2f);
        t.outlineWidth = 0.22f; t.outlineColor = outline;
        return t;
    }

    void FencePost(Vector3 at)
    {
        Prim(PrimitiveType.Cube, root, at + new Vector3(0, 0.4f, 0), new Vector3(0.14f, 0.8f, 0.14f), Wood);
    }

    void PlaceProp(GameObject prefab, Vector3 at, float height, float yaw)
    {
        Transform placed;
        if (prefab == null) placed = Prim(PrimitiveType.Sphere, root, at + Vector3.up * height * 0.4f, Vector3.one * height * 0.6f, new Color32(0x5E, 0x9E, 0x5A, 255));
        else
        {
            var g = Instantiate(prefab, root);
            g.transform.localPosition = at; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            StripPhysics(g);
            var b = Bounds(g);
            if (b.size.y > 0.01f) g.transform.localScale *= height / b.size.y;
            b = Bounds(g);
            g.transform.position += new Vector3(0, W(at).y - b.min.y, 0);
            placed = g.transform;
        }
        if (height > 2f) swaying.Add(placed);
    }

    static void StripPhysics(GameObject g)
    {
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
    }

    static Bounds Bounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }
}
