// Farm Chaos - right after the story intro: the camera swoops over the whole farm, naming every pen
// (doubles as a map tour for new players), then glides down and lands behind YOUR bean - no loading cut.
// Only plays after the intro (not when you press Play straight in Farm_Main). Tap / click to skip.
// Nothing to set up: it switches itself on when the farm loads.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]   // after CameraFollow, so we can blend from its view
public class FarmFlyover : MonoBehaviour
{
    public static bool Active { get; private set; }

    [Tooltip("Seconds spent on each stop")]
    public float perStop = 0.85f;
    public float landTime = 1.4f;
    public float height = 13f;

    struct Stop { public string label; public Vector3 at; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

    static void OnScene(Scene s, LoadSceneMode m)
    {
        if (!IntroCutscene3D.JustPlayed || FindAnyObjectByType<RoundClock>() == null) return;
        IntroCutscene3D.JustPlayed = false;
        Active = true;                                       // claim it now so the weather card waits for us
        new GameObject("Farm_Flyover").AddComponent<FarmFlyover>();
    }

    Camera cam;
    SpherePlayer[] players = new SpherePlayer[0];
    readonly List<Stop> stops = new List<Stop>();
    readonly List<TextMeshPro> labels = new List<TextMeshPro>();
    float t = -0.2f, total;
    bool skip;

    void Start()
    {
        cam = Camera.main;
        if (cam == null) { Finish(); return; }
        Add("CHICKENS", new Vector3(-28f, 0f, 10f), "Chicken_Pen", "Chicken_Coops");
        Add("BUNNIES", new Vector3(-25f, 0f, -28f), "Rabbit_Pen");
        Add("POWER STATION", new Vector3(0f, 0f, -14f), "PowerStation_Building");
        Add("COWS", new Vector3(26f, 0f, -26f), "Cow_Pen");
        Add("SHEEP", new Vector3(28f, 0f, 18f), "Sheep_Pen");
        Add("HQ  -  SHOP", new Vector3(0f, 0f, 0f), "Zoo_HQ_Building");
        total = stops.Count * perStop + landTime;
        players = FindObjectsByType<SpherePlayer>(FindObjectsSortMode.None);
    }

    void Add(string label, Vector3 fallback, params string[] names)
    {
        Vector3 at = fallback;
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go == null) continue;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { at = go.transform.position; break; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            at = b.center; at.y = b.min.y; break;
        }
        stops.Add(new Stop { label = label, at = at });
        var tm = new GameObject("Flyover_Label").AddComponent<TextMeshPro>();
        tm.text = label; tm.fontSize = 14f; tm.fontStyle = FontStyles.Bold; tm.alignment = TextAlignmentOptions.Center;
        tm.textWrappingMode = TextWrappingModes.NoWrap; tm.rectTransform.sizeDelta = new Vector2(30f, 5f);
        tm.color = Color.white; tm.outlineWidth = 0.25f; tm.outlineColor = new Color32(0xE0, 0x6C, 0x8C, 255);
        tm.transform.position = at + Vector3.up * 4.5f; tm.transform.localScale = Vector3.zero;
        labels.Add(tm);
    }

    // where the camera is at time t along the tour (circling outside each stop, looking at it)
    void Tour(float time, out Vector3 pos, out Vector3 look)
    {
        float f = Mathf.Clamp(time / perStop, 0f, stops.Count - 1f);
        int i = Mathf.Min(Mathf.FloorToInt(f), stops.Count - 2); float k = Smooth(f - i);
        if (stops.Count == 1) { i = 0; k = 0f; }
        Vector3 a = stops[i].at, b = stops[Mathf.Min(i + 1, stops.Count - 1)].at;
        look = Vector3.Lerp(a, b, k) + Vector3.up * 1f;
        Vector3 outA = Out(a), outB = Out(b);
        pos = look + Vector3.Slerp(outA, outB, k) * 15f + Vector3.up * height;
    }

    static Vector3 Out(Vector3 p) { p.y = 0f; return p.sqrMagnitude < 1f ? Vector3.back : p.normalized; }
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void LateUpdate()
    {
        if (cam == null) return;
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) skip = true;
        t += Time.unscaledDeltaTime;
        foreach (var p in players) if (p != null) p.Stun(0.15f);   // hold still while we look around
        float tour = stops.Count * perStop;
        if (skip && t < tour) t = tour;                        // skip -> straight into the landing

        // CameraFollow has already placed the camera behind the player this frame: that's where we land
        Vector3 followPos = cam.transform.position; Quaternion followRot = cam.transform.rotation;

        Tour(Mathf.Min(Mathf.Max(t, 0f), tour), out var pos, out var look);
        Quaternion rot = Quaternion.LookRotation(look - pos);
        float land = Smooth((t - tour) / landTime);
        if (t > tour)
        {
            Vector3 mid = Vector3.Lerp(pos, followPos, land) + Vector3.up * Mathf.Sin(land * Mathf.PI) * 3f;
            pos = mid; rot = Quaternion.Slerp(rot, followRot, land);
        }
        cam.transform.SetPositionAndRotation(pos, rot);

        // labels pop up as we pass each stop
        for (int i = 0; i < labels.Count; i++)
        {
            var l = labels[i]; if (l == null) continue;
            float local = t - i * perStop;
            float s = local < -0.15f ? 0f : local < 0.15f ? Smooth((local + 0.15f) / 0.3f) * 1.15f : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((local - 0.15f) / 0.2f));
            if (t > tour) s *= 1f - land;
            l.transform.localScale = Vector3.one * s;
            l.transform.rotation = Quaternion.LookRotation(l.transform.position - cam.transform.position);
        }

        if (t >= total) Finish();
    }

    void Finish()
    {
        foreach (var l in labels) if (l != null) Destroy(l.gameObject);
        Active = false;
        Destroy(gameObject);
    }

    void OnDestroy() { if (Active && this != null) Active = false; }
}
