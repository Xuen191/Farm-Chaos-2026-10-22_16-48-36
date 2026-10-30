// Farm Chaos - 3D story intro played after START in the lobby (replaces the 2D slides).
// Builds a tiny 3D set far below the lobby out of cute shapes + the Farm Ranch pack:
//   1. farmhouse at night  2. you (blue ball) asleep, fan + lamp  3. fan stops, lamp dies, HOT!
//   4. hop to the window: animals running loose  5. fly out the window -> big 3D "FARM CHAOS!"
// Tap = next shot, Skip = straight to the farm.
using System.Collections;
using System.Collections.Generic;
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

    static readonly Vector3 Origin = new Vector3(0f, -500f, 0f);

    // palette
    static readonly Color Cream = new Color32(0xFF, 0xF0, 0xD9, 255), Pink = new Color32(0xF4, 0xB8, 0xC1, 255),
        Mint = new Color32(0xC8, 0xE6, 0xD0, 255), Blue = new Color32(0xC9, 0xDD, 0xF5, 255), Wood = new Color32(0xD9, 0xB9, 0x9B, 255),
        DarkWood = new Color32(0xA8, 0x7B, 0x5C, 255), Straw = new Color32(0xD6, 0xB7, 0x5A, 255), Grass = new Color32(0x7F, 0xB0, 0x6E, 255),
        Ink = new Color32(0x2E, 0x2A, 0x3A, 255), Ball = new Color32(0x86, 0xC3, 0xFA, 255), Night = new Color32(0x1B, 0x1D, 0x3E, 255),
        Brown = new Color32(0x6B, 0x4A, 0x3A, 255);

    Transform root;
    Camera cam;
    RectTransform stage;
    Image fader;
    TextMeshProUGUI capTitle, capSub;
    GameObject capBox;
    bool skip, next;
    readonly Dictionary<Color, Material> lit = new Dictionary<Color, Material>(), unlit = new Dictionary<Color, Material>();
    readonly List<System.Action<float, float>> always = new List<System.Action<float, float>>();   // (time, dt)
    float clock;

    // set pieces we animate
    Transform kid, kidBody, kidFace, eyesClosed, eyesOpen, fanPivot, title3D, bang;
    Light lampLight, moonLight;
    Renderer bulb;
    readonly List<TextMeshPro> zzz = new List<TextMeshPro>();
    Vector3 houseLook;
    float fanSpeed;
    readonly List<Transform> sweat = new List<Transform>();

    // ================================================================ public
    public IEnumerator Play()
    {
        skip = false;
        PrepareScene();
        BuildSet();
        BuildUI();
        yield return Shot(4.0f, ShotHouse);
        if (!skip) yield return Shot(4.0f, ShotBed);
        if (!skip) yield return Shot(4.4f, ShotHot);
        if (!skip) yield return Shot(4.6f, ShotWindow);
        if (!skip) yield return Shot(4.8f, ShotTitle);
        // leave the screen dark while the farm loads
        fader.color = Night;
        if (capBox != null) capBox.SetActive(false);
    }

    // ================================================================ scene prep
    void PrepareScene()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
        var myCanvas = GetComponentInParent<Canvas>();
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c != myCanvas && c.isRootCanvas) c.enabled = false;
        for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(false);

        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.4f, 0.55f);
        DynamicGI.UpdateEnvironment();

        var cg = new GameObject("Intro_Camera");
        cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Night;
        cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 260f; cam.depth = 100;
    }

    // ================================================================ shots
    System.Action<float> ShotHouse()
    {
        Caption("11:00 PM on the farm...", "Everyone's tucked in for the night.");
        Vector3 a = houseLook + new Vector3(-3f, 1.5f, -24f), b = houseLook + new Vector3(0f, 0.5f, -15f);
        return t => { float k = Ease(t / 4f); Cam(Vector3.Lerp(a, b, k), houseLook + Vector3.up * 0.5f); };
    }

    System.Action<float> ShotBed()
    {
        Caption("Time for bed!", "Zzz... sweet dreams.");
        fanSpeed = 540f;
        foreach (var z in zzz) z.gameObject.SetActive(true);
        return t =>
        {
            float k = Ease(t / 4f);
            Cam(Vector3.Lerp(new Vector3(1.9f, 2.1f, -2.3f), new Vector3(0.9f, 1.75f, -1.4f), k), new Vector3(-1.5f, 1.0f, 1.0f));
            kidBody.localScale = 0.6f * new Vector3(1f, 0.95f + 0.04f * Mathf.Sin(t * 2.4f), 1f);   // breathing
            FaceToward(cam.transform.position);
            for (int i = 0; i < zzz.Count; i++)
            {
                float a = (t * 0.45f + i * 0.33f) % 1f;
                zzz[i].transform.position = W(new Vector3(-1.5f + a * 0.5f + Mathf.Sin(a * 6f) * 0.08f, 1.45f + a * 0.9f, 1.35f));
                zzz[i].alpha = Mathf.Sin(a * Mathf.PI);
                zzz[i].transform.rotation = Quaternion.LookRotation(zzz[i].transform.position - cam.transform.position);
            }
        };
    }

    System.Action<float> ShotHot()
    {
        Caption("...it's sooo HOT!", "Wait... the fan stopped?!");
        foreach (var z in zzz) z.gameObject.SetActive(false);
        bool woke = false, dark = false;
        var drops = sweat;
        for (int i = 0; i < 6; i++)
        {
            var d = Prim(PrimitiveType.Sphere, kid, new Vector3(0, 0, 0), new Vector3(0.07f, 0.1f, 0.07f), new Color32(0x9F, 0xD8, 0xF5, 255), true);
            d.gameObject.SetActive(false); drops.Add(d);
        }
        return t =>
        {
            Vector3 shake = t > 2.0f && t < 2.6f ? Random.insideUnitSphere * 0.03f : Vector3.zero;
            Cam(new Vector3(-0.2f, 1.65f, -0.1f) + shake, new Vector3(-1.7f, 1.15f, 1.35f));
            fanSpeed = Mathf.Lerp(540f, 0f, Mathf.Clamp01(t / 1.3f));
            if (!woke && t > 0.7f) { woke = true; eyesClosed.gameObject.SetActive(false); eyesOpen.gameObject.SetActive(true); }
            float sit = Mathf.Clamp01((t - 0.7f) / 0.25f);
            kid.localPosition = new Vector3(-1.7f, 1.05f + Mathf.Sin(sit * Mathf.PI) * 0.35f + sit * 0.05f, 1.45f);
            kidBody.localScale = 0.6f * new Vector3(1f + 0.04f * Mathf.Sin(t * 20f) * sit, 1f, 1f);
            FaceToward(cam.transform.position);
            for (int i = 0; i < drops.Count; i++)
            {
                if (t < 0.8f) continue;
                float a = ((t - 0.8f) * 1.3f + i / (float)drops.Count) % 1f;
                float side = (i % 2 == 0 ? 1f : -1f) * (0.22f + 0.04f * i);
                drops[i].gameObject.SetActive(true);
                drops[i].position = kid.position + kidFace.rotation * new Vector3(side + side * a * 0.6f, 0.28f + a * 0.15f - a * a * 0.9f, 0.05f);
            }
            // lamp flickers, then the power dies
            if (t > 1.5f && t < 2.1f) { bool on = Mathf.PerlinNoise(t * 16f, 0.5f) > 0.45f; lampLight.enabled = on; bulb.sharedMaterial = U(on ? new Color(1f, 0.9f, 0.6f) : new Color(0.3f, 0.3f, 0.35f)); }
            if (!dark && t >= 2.1f)
            {
                dark = true; lampLight.enabled = false; bulb.sharedMaterial = U(new Color(0.3f, 0.3f, 0.35f));
                RenderSettings.ambientLight = new Color(0.14f, 0.15f, 0.26f); DynamicGI.UpdateEnvironment();
                Caption("The POWER is OUT!", "Everything goes dark...");
            }
        };
    }

    System.Action<float> ShotWindow()
    {
        Caption("You peek outside...", "Huh? What's all that noise?");
        bool loose = false;
        foreach (var d in sweat) d.gameObject.SetActive(false);
        Vector3 bed = new Vector3(-1.7f, 1.10f, 1.45f), floor = new Vector3(-0.3f, 0.32f, 1.6f), below = new Vector3(1.0f, 0.32f, 1.9f), seat = new Vector3(1.0f, 0.92f, 2.5f);
        return t =>
        {
            // hop off the bed, roll over, hop up onto the window seat
            Vector3 p;
            if (t < 0.45f) p = Arc(bed, floor, t / 0.45f, 0.5f);
            else if (t < 1.15f) p = Vector3.Lerp(floor, below, Ease((t - 0.45f) / 0.7f));
            else if (t < 1.55f) p = Arc(below, seat, (t - 1.15f) / 0.4f, 0.45f);
            else p = seat;
            kid.localPosition = p;
            kidBody.localScale = 0.6f * (t > 1.55f && t < 1.8f ? new Vector3(1.12f, 0.86f, 1.12f) : Vector3.one);
            FaceToward(kid.position + Vector3.forward);

            float k = Ease(Mathf.Clamp01(t / 1.6f));
            Vector3 camA = new Vector3(-0.6f, 1.9f, -1.6f), camB = new Vector3(1.0f, 1.78f, 1.25f);
            Vector3 lookA = new Vector3(0.6f, 0.9f, 2.0f), lookB = new Vector3(1.0f, 1.2f, 12f);
            Cam(Vector3.Lerp(camA, camB, k), Vector3.Lerp(lookA, lookB, k));

            bang.gameObject.SetActive(t > 2.0f);
            if (t > 2.0f)
            {
                bang.position = kid.position + new Vector3(0.5f + Mathf.Sin(t * 30f) * 0.02f, 0.55f, 0.35f);
                bang.rotation = Quaternion.LookRotation(bang.position - cam.transform.position);
            }
            if (!loose && t > 2.0f) { loose = true; Caption("The animals are LOOSE!", "Chickens, sheep, cows, bunnies... everywhere!"); }
        };
    }

    System.Action<float> ShotTitle()
    {
        Caption("", "Round up the animals & fix the power before sunrise!");
        bang.gameObject.SetActive(false);
        title3D.gameObject.SetActive(true);
        Vector3 a = new Vector3(1.0f, 1.78f, 1.25f), b = new Vector3(1.0f, 1.6f, 3.2f), c = new Vector3(1.0f, 2.2f, 5.0f), d = new Vector3(1.2f, 6.5f, 8.5f);
        Vector3 titlePos = new Vector3(1.0f, 8.5f, 22f);
        return t =>
        {
            Vector3 p;
            if (t < 0.6f) p = Vector3.Lerp(a, b, Ease(t / 0.6f));
            else if (t < 1.0f) p = Vector3.Lerp(b, c, (t - 0.6f) / 0.4f);
            else p = Vector3.Lerp(c, d, Ease(Mathf.Clamp01((t - 1.0f) / 1.8f)));
            float lk = Ease(Mathf.Clamp01((t - 0.6f) / 2.0f));
            Cam(p, Vector3.Lerp(new Vector3(1f, 1.2f, 12f), titlePos, lk));

            float s = Mathf.Clamp01((t - 1.2f) / 0.5f);
            float bounce = 1f + Mathf.Sin(s * Mathf.PI) * 0.25f + (s >= 1f ? Mathf.Sin(t * 3f) * 0.03f : 0f);
            title3D.localScale = Vector3.one * Mathf.Max(0.001f, s * bounce);
            title3D.localPosition = titlePos + Vector3.up * Mathf.Sin(t * 2f) * 0.15f;
            title3D.rotation = Quaternion.LookRotation(title3D.position - cam.transform.position);
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
            float fadeIn = 1f - Mathf.Clamp01(t / 0.35f), fadeOut = Mathf.Clamp01((t - (dur - 0.3f)) / 0.3f);
            fader.color = new Color(Night.r, Night.g, Night.b, Mathf.Max(fadeIn, fadeOut));
            yield return null;
        }
        if (next && !skip)
        {
            float f = 0f;
            while (f < 0.2f) { f += Time.unscaledDeltaTime; fader.color = new Color(Night.r, Night.g, Night.b, f / 0.2f); yield return null; }
        }
    }

    // ================================================================ the set
    void BuildSet()
    {
        root = new GameObject("Intro3D_Set").transform;
        root.position = Origin;

        // ground + sky bits
        Prim(PrimitiveType.Cube, root, new Vector3(-20f, -0.06f, 10f), new Vector3(160f, 0.1f, 120f), Grass);
        moonLight = new GameObject("Moonlight").AddComponent<Light>();
        moonLight.transform.SetParent(root, false);
        moonLight.type = LightType.Directional; moonLight.color = new Color(0.62f, 0.68f, 1f); moonLight.intensity = 0.55f; moonLight.shadows = LightShadows.None;
        moonLight.transform.rotation = Quaternion.Euler(40f, 160f, 0f);
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

        BuildHouseExterior(new Vector3(-60f, 0f, 0f));
        BuildBedroom();
        BuildYard();
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
        // warm glowing windows + porch light so it reads as "someone lives here"
        var porch = new GameObject("Porch_Light").AddComponent<Light>();
        porch.transform.SetParent(root, false); porch.transform.localPosition = houseLook + new Vector3(0f, 0f, -6f);
        porch.type = LightType.Point; porch.color = new Color(1f, 0.78f, 0.5f); porch.range = 16f; porch.intensity = 4f; porch.shadows = LightShadows.None;
        float[] tx = { -14f, -9f, 8f, 13f, -17f, 16f };
        float[] tz = { 4f, 9f, 6f, 1f, -6f, -5f };
        for (int i = 0; i < tx.Length; i++) PlaceProp(i % 2 == 0 ? tree : bush, at + new Vector3(tx[i], 0f, tz[i]), i % 2 == 0 ? 5f : 1.6f, i * 50f);
        // little fence along the front
        for (int i = -5; i <= 5; i++) FencePost(at + new Vector3(i * 1.6f, 0f, -8f));
        Prim(PrimitiveType.Cube, root, at + new Vector3(0, 0.55f, -8f), new Vector3(17f, 0.12f, 0.08f), Cream);
    }

    void BuildBedroom()
    {
        // floor, ceiling, walls (back wall has a window hole at x 0.2..1.8, y 1.0..2.2)
        Prim(PrimitiveType.Cube, root, new Vector3(0, -0.05f, 0), new Vector3(6.2f, 0.1f, 6.2f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(0, 3.05f, 0), new Vector3(6.2f, 0.1f, 6.2f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(-3.05f, 1.5f, 0), new Vector3(0.1f, 3.1f, 6.2f), Pink);
        Prim(PrimitiveType.Cube, root, new Vector3(3.05f, 1.5f, 0), new Vector3(0.1f, 3.1f, 6.2f), Pink);
        Prim(PrimitiveType.Cube, root, new Vector3(0, 1.5f, -3.05f), new Vector3(6.2f, 3.1f, 0.1f), Pink);
        Prim(PrimitiveType.Cube, root, new Vector3(-1.4f, 1.5f, 3.05f), new Vector3(3.2f, 3.1f, 0.1f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(2.4f, 1.5f, 3.05f), new Vector3(1.2f, 3.1f, 0.1f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(1.0f, 0.5f, 3.05f), new Vector3(1.6f, 1.0f, 0.1f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(1.0f, 2.6f, 3.05f), new Vector3(1.6f, 0.8f, 0.1f), Cream);
        // window frame + curtains + seat
        Prim(PrimitiveType.Cube, root, new Vector3(1.0f, 0.98f, 2.95f), new Vector3(1.8f, 0.08f, 0.25f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(1.0f, 2.22f, 2.98f), new Vector3(1.8f, 0.08f, 0.14f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(0.16f, 1.6f, 2.98f), new Vector3(0.08f, 1.32f, 0.14f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(1.84f, 1.6f, 2.98f), new Vector3(0.08f, 1.32f, 0.14f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(-0.05f, 1.55f, 2.92f), new Vector3(0.35f, 1.6f, 0.06f), Mint);
        Prim(PrimitiveType.Cube, root, new Vector3(2.05f, 1.55f, 2.92f), new Vector3(0.35f, 1.6f, 0.06f), Mint);
        Prim(PrimitiveType.Cube, root, new Vector3(1.0f, 2.42f, 2.9f), new Vector3(2.5f, 0.1f, 0.1f), DarkWood);
        Prim(PrimitiveType.Cube, root, new Vector3(1.0f, 0.3f, 2.6f), new Vector3(1.5f, 0.6f, 0.75f), Wood);
        Prim(PrimitiveType.Cube, root, new Vector3(1.0f, 0.63f, 2.6f), new Vector3(1.45f, 0.08f, 0.7f), Blue);   // cushion

        // rug
        Prim(PrimitiveType.Cylinder, root, new Vector3(0.3f, 0.01f, 0.4f), new Vector3(2.4f, 0.01f, 1.8f), Mint);
        // bed against the left wall
        Prim(PrimitiveType.Cube, root, new Vector3(-1.7f, 0.25f, 0.8f), new Vector3(1.5f, 0.5f, 2.4f), DarkWood);
        Prim(PrimitiveType.Cube, root, new Vector3(-1.7f, 0.62f, 0.8f), new Vector3(1.4f, 0.25f, 2.3f), Color.white);
        Prim(PrimitiveType.Cube, root, new Vector3(-1.7f, 0.75f, 2.0f), new Vector3(1.6f, 1.5f, 0.12f), DarkWood);   // headboard
        Prim(PrimitiveType.Cube, root, new Vector3(-1.7f, 0.82f, 1.6f), new Vector3(1.0f, 0.18f, 0.5f), Cream);   // pillow
        var blanket = Prim(PrimitiveType.Cube, root, new Vector3(-1.7f, 0.79f, 0.45f), new Vector3(1.48f, 0.12f, 1.5f), Pink);
        for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, blanket, new Vector3(0, 0.55f, -0.3f + i * 0.3f), new Vector3(1.01f, 0.2f, 0.06f), Cream);   // stripes

        // you: the blue ball
        kid = new GameObject("You").transform; kid.SetParent(root, false); kid.localPosition = new Vector3(-1.7f, 1.05f, 1.45f);
        kidBody = Prim(PrimitiveType.Sphere, kid, Vector3.zero, Vector3.one * 0.6f, Ball);
        kidFace = new GameObject("Face").transform; kidFace.SetParent(kid, false);
        eyesClosed = new GameObject("EyesClosed").transform; eyesClosed.SetParent(kidFace, false);
        Prim(PrimitiveType.Cube, eyesClosed, new Vector3(-0.1f, 0.07f, 0.285f), new Vector3(0.12f, 0.03f, 0.06f), Ink, true);
        Prim(PrimitiveType.Cube, eyesClosed, new Vector3(0.1f, 0.07f, 0.285f), new Vector3(0.12f, 0.03f, 0.06f), Ink, true);
        eyesOpen = new GameObject("EyesOpen").transform; eyesOpen.SetParent(kidFace, false);
        foreach (float x in new[] { -0.1f, 0.1f })
        {
            Prim(PrimitiveType.Sphere, eyesOpen, new Vector3(x, 0.07f, 0.275f), new Vector3(0.1f, 0.13f, 0.08f), Ink, true);
            Prim(PrimitiveType.Sphere, eyesOpen, new Vector3(x + 0.02f, 0.1f, 0.31f), Vector3.one * 0.035f, Color.white, true);
        }
        eyesOpen.gameObject.SetActive(false);
        Prim(PrimitiveType.Sphere, kidFace, new Vector3(-0.17f, -0.02f, 0.245f), new Vector3(0.1f, 0.06f, 0.05f), Pink, true);
        Prim(PrimitiveType.Sphere, kidFace, new Vector3(0.17f, -0.02f, 0.245f), new Vector3(0.1f, 0.06f, 0.05f), Pink, true);

        // bedside table + lamp
        Prim(PrimitiveType.Cube, root, new Vector3(-0.55f, 0.28f, 2.45f), new Vector3(0.55f, 0.56f, 0.5f), Wood);
        Prim(PrimitiveType.Cylinder, root, new Vector3(-0.55f, 0.66f, 2.45f), new Vector3(0.1f, 0.1f, 0.1f), Cream);
        bulb = Prim(PrimitiveType.Sphere, root, new Vector3(-0.55f, 0.82f, 2.45f), Vector3.one * 0.16f, new Color(1f, 0.9f, 0.6f), true).GetComponent<Renderer>();
        Prim(PrimitiveType.Cylinder, root, new Vector3(-0.55f, 0.95f, 2.45f), new Vector3(0.34f, 0.12f, 0.34f), Pink);
        lampLight = new GameObject("Lamp_Light").AddComponent<Light>();
        lampLight.transform.SetParent(root, false); lampLight.transform.localPosition = new Vector3(-0.55f, 1.1f, 2.2f);
        lampLight.type = LightType.Point; lampLight.color = new Color(1f, 0.78f, 0.52f); lampLight.range = 5.5f; lampLight.intensity = 3f; lampLight.shadows = LightShadows.None;

        // ceiling fan
        Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 2.9f, 0.3f), new Vector3(0.05f, 0.12f, 0.05f), Cream);
        fanPivot = new GameObject("Fan").transform; fanPivot.SetParent(root, false); fanPivot.localPosition = new Vector3(0f, 2.76f, 0.3f);
        Prim(PrimitiveType.Cylinder, fanPivot, Vector3.zero, new Vector3(0.24f, 0.05f, 0.24f), Cream);
        for (int i = 0; i < 4; i++)
        {
            var arm = new GameObject("Blade").transform; arm.SetParent(fanPivot, false); arm.localRotation = Quaternion.Euler(0, i * 90f, 0);
            var bl = Prim(PrimitiveType.Cube, arm, new Vector3(0.5f, 0f, 0f), new Vector3(0.8f, 0.025f, 0.2f), i % 2 == 0 ? Pink : Mint);
            bl.localRotation = Quaternion.Euler(8f, 0, 0);
        }

        // Zzz + "!!"
        for (int i = 0; i < 3; i++) zzz.Add(Text3D("Z", 4.5f - i * 0.8f, new Color(0.88f, 0.9f, 1f), Pink));
        foreach (var z in zzz) z.gameObject.SetActive(false);
        var b = Text3D("!!", 2.6f, new Color32(0xE0, 0x4C, 0x5C, 255), Color.white); bang = b.transform; bang.gameObject.SetActive(false);
        var tt = Text3D("FARM CHAOS!", 15f, Color.white, new Color32(0xE0, 0x6C, 0x8C, 255));
        tt.rectTransform.sizeDelta = new Vector2(60f, 10f);
        title3D = tt.transform; title3D.gameObject.SetActive(false);
    }

    void BuildYard()
    {
        // pen fence across the yard with a sprung-open gate
        for (int i = -6; i <= 9; i++) { if (i == 1 || i == 2) continue; FencePost(new Vector3(i * 1.2f, 0f, 17f)); }
        Prim(PrimitiveType.Cube, root, new Vector3(-3.6f, 0.55f, 17f), new Vector3(7.4f, 0.1f, 0.08f), Cream);
        Prim(PrimitiveType.Cube, root, new Vector3(7.2f, 0.55f, 17f), new Vector3(5.2f, 0.1f, 0.08f), Cream);
        var gate = new GameObject("Gate").transform; gate.SetParent(root, false); gate.localPosition = new Vector3(0.6f, 0f, 17f);
        gate.localRotation = Quaternion.Euler(0, -115f, 0);
        Prim(PrimitiveType.Cube, gate, new Vector3(1.2f, 0.55f, 0f), new Vector3(2.4f, 0.1f, 0.08f), Straw);
        Prim(PrimitiveType.Cube, gate, new Vector3(1.2f, 0.3f, 0f), new Vector3(2.4f, 0.1f, 0.08f), Straw);
        // haystacks / trees in the yard
        Prim(PrimitiveType.Cylinder, root, new Vector3(-4.5f, 0.45f, 19.5f), new Vector3(1.4f, 0.45f, 1.4f), Straw);
        Prim(PrimitiveType.Cylinder, root, new Vector3(6.5f, 0.4f, 20.5f), new Vector3(1.2f, 0.4f, 1.2f), Straw);
        PlaceProp(tree, new Vector3(-9f, 0f, 22f), 6f, 10f);
        PlaceProp(tree, new Vector3(11f, 0f, 24f), 6.5f, 80f);
        PlaceProp(bush, new Vector3(-6f, 0f, 9f), 1.6f, 0f);
        PlaceProp(bush, new Vector3(8f, 0f, 10f), 1.6f, 40f);
        // a soft yard light so the animals are readable at night
        var yl = new GameObject("Yard_Light").AddComponent<Light>();
        yl.transform.SetParent(root, false); yl.transform.localPosition = new Vector3(1f, 3.5f, 8.5f);
        yl.type = LightType.Point; yl.color = new Color(1f, 0.85f, 0.65f); yl.range = 18f; yl.intensity = 3.2f; yl.shadows = LightShadows.None;

        // the escaped animals running around
        Wander(Chicken(), new Vector3(0.0f, 0f, 7.5f), 1.6f, 1.7f, 0.0f, 0.35f, 9f);
        Wander(Chicken(), new Vector3(2.2f, 0f, 8.0f), 1.2f, -2.1f, 1.5f, 0.3f, 10f);
        Wander(Chicken(), new Vector3(0.8f, 0f, 9.5f), 2.0f, 1.4f, 3.0f, 0.35f, 8f);
        Wander(Chicken(), new Vector3(3.0f, 0f, 10.5f), 1.0f, 2.4f, 4.2f, 0.3f, 11f);
        Wander(Sheep(), new Vector3(-1.2f, 0f, 10.5f), 1.8f, 0.9f, 0.7f, 0.15f, 7f);
        Wander(Sheep(), new Vector3(3.2f, 0f, 12.0f), 1.6f, -1.0f, 2.5f, 0.15f, 7f);
        Wander(Cow(), new Vector3(1f, 0f, 13f), 2.6f, 0.6f, 1.2f, 0.08f, 5f);
        Wander(Rabbit(), new Vector3(0.5f, 0f, 6.8f), 1.2f, -2.0f, 0.3f, 0.45f, 6f);
        Wander(Rabbit(), new Vector3(2.8f, 0f, 9.0f), 1.4f, 1.8f, 2.0f, 0.45f, 6f);
        Wander(Rabbit(), new Vector3(-1.5f, 0f, 8.5f), 1.0f, 2.6f, 4.0f, 0.45f, 6f);
    }

    // ================================================================ cute animals (forward = +z)
    Transform Animal(string name) { var a = new GameObject(name).transform; a.SetParent(root, false); return a; }

    Transform Chicken()
    {
        var a = Animal("Chicken");
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.3f, 0), new Vector3(0.45f, 0.4f, 0.5f), Color.white);
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.6f, 0.18f), Vector3.one * 0.28f, Color.white);
        Prim(PrimitiveType.Cube, a, new Vector3(0, 0.58f, 0.35f), new Vector3(0.09f, 0.06f, 0.12f), new Color32(0xF5, 0xA6, 0x23, 255));
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.77f, 0.17f), new Vector3(0.07f, 0.12f, 0.14f), new Color32(0xE0, 0x4C, 0x5C, 255));
        Prim(PrimitiveType.Sphere, a, new Vector3(-0.09f, 0.64f, 0.29f), Vector3.one * 0.05f, Ink, true);
        Prim(PrimitiveType.Sphere, a, new Vector3(0.09f, 0.64f, 0.29f), Vector3.one * 0.05f, Ink, true);
        var wl = Prim(PrimitiveType.Sphere, a, new Vector3(-0.22f, 0.33f, 0), new Vector3(0.08f, 0.24f, 0.3f), Cream);
        var wr = Prim(PrimitiveType.Sphere, a, new Vector3(0.22f, 0.33f, 0), new Vector3(0.08f, 0.24f, 0.3f), Cream);
        float ph = Random.value * 6f;
        always.Add((t, dt) => { float f = Mathf.Sin(t * 30f + ph) * 40f; wl.localRotation = Quaternion.Euler(0, 0, -f); wr.localRotation = Quaternion.Euler(0, 0, f); });
        return a;
    }

    Transform Sheep()
    {
        var a = Animal("Sheep");
        Vector3[] puffs = { new Vector3(0, 0.6f, 0), new Vector3(0.2f, 0.62f, 0.2f), new Vector3(-0.2f, 0.62f, 0.2f), new Vector3(0.2f, 0.6f, -0.2f), new Vector3(-0.2f, 0.6f, -0.2f), new Vector3(0, 0.78f, 0) };
        foreach (var p in puffs) Prim(PrimitiveType.Sphere, a, p, Vector3.one * 0.48f, Color.white);
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.62f, 0.48f), new Vector3(0.3f, 0.32f, 0.3f), Ink);
        Prim(PrimitiveType.Sphere, a, new Vector3(-0.08f, 0.67f, 0.61f), Vector3.one * 0.06f, Color.white, true);
        Prim(PrimitiveType.Sphere, a, new Vector3(0.08f, 0.67f, 0.61f), Vector3.one * 0.06f, Color.white, true);
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
        Prim(PrimitiveType.Sphere, a, new Vector3(-0.38f, 0.85f, -0.3f), new Vector3(0.12f, 0.35f, 0.4f), Ink);
        Prim(PrimitiveType.Sphere, a, new Vector3(0.1f, 1.27f, -0.35f), new Vector3(0.35f, 0.08f, 0.3f), Ink);
        Prim(PrimitiveType.Cube, a, new Vector3(0, 1.15f, 0.85f), new Vector3(0.5f, 0.45f, 0.45f), Color.white);
        Prim(PrimitiveType.Cube, a, new Vector3(0, 1.03f, 1.09f), new Vector3(0.46f, 0.22f, 0.1f), Pink);
        Prim(PrimitiveType.Sphere, a, new Vector3(-0.12f, 1.24f, 1.08f), Vector3.one * 0.07f, Ink, true);
        Prim(PrimitiveType.Sphere, a, new Vector3(0.12f, 1.24f, 1.08f), Vector3.one * 0.07f, Ink, true);
        Prim(PrimitiveType.Cylinder, a, new Vector3(-0.18f, 1.45f, 0.8f), new Vector3(0.06f, 0.08f, 0.06f), Cream);
        Prim(PrimitiveType.Cylinder, a, new Vector3(0.18f, 1.45f, 0.8f), new Vector3(0.06f, 0.08f, 0.06f), Cream);
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
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.44f, 0.33f), Vector3.one * 0.05f, Pink, true);
        Prim(PrimitiveType.Sphere, a, new Vector3(-0.08f, 0.5f, 0.3f), Vector3.one * 0.045f, Ink, true);
        Prim(PrimitiveType.Sphere, a, new Vector3(0.08f, 0.5f, 0.3f), Vector3.one * 0.045f, Ink, true);
        Prim(PrimitiveType.Sphere, a, new Vector3(0, 0.25f, -0.24f), Vector3.one * 0.12f, Color.white);
        return a;
    }

    // run around an ellipse, hopping, always facing the way it's going
    void Wander(Transform a, Vector3 centre, float radius, float speed, float phase, float hop, float hopRate)
    {
        always.Add((t, dt) =>
        {
            float ang = t * speed / Mathf.Max(0.5f, radius) + phase;
            Vector3 p = centre + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius * 0.6f);
            Vector3 vel = new Vector3(-Mathf.Sin(ang) * radius, 0f, Mathf.Cos(ang) * radius * 0.6f) * Mathf.Sign(speed);
            p.y = Mathf.Abs(Mathf.Sin(t * hopRate + phase)) * hop;
            a.localPosition = p;
            if (vel.sqrMagnitude > 0.0001f) a.localRotation = Quaternion.LookRotation(vel.normalized) * Quaternion.Euler(0, 0, Mathf.Sin(t * hopRate) * 6f);
        });
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
        capTitle.text = title; capSub.text = sub;
        capTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
        capSub.rectTransform.anchoredPosition = string.IsNullOrEmpty(title) ? Vector2.zero : new Vector2(0, -42);
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
        cam.transform.position = W(pos);
        cam.transform.rotation = Quaternion.LookRotation(W(look) - W(pos));
    }

    void FaceToward(Vector3 worldTarget)
    {
        Vector3 d = worldTarget - kid.position; d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) kidFace.rotation = Quaternion.LookRotation(d.normalized);
    }

    static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static Vector3 Arc(Vector3 a, Vector3 b, float k, float height)
    {
        k = Mathf.Clamp01(k);
        return Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * height;
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
        if (prefab == null) { Prim(PrimitiveType.Sphere, root, at + Vector3.up * height * 0.4f, Vector3.one * height * 0.6f, new Color32(0x5E, 0x9E, 0x5A, 255)); return; }
        var g = Instantiate(prefab, root);
        g.transform.localPosition = at; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        StripPhysics(g);
        var b = Bounds(g);
        if (b.size.y > 0.01f) g.transform.localScale *= height / b.size.y;
        b = Bounds(g);
        g.transform.position += new Vector3(0, W(at).y - b.min.y, 0);
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
