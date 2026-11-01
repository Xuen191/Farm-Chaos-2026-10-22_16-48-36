// Farm Chaos - decorating the lobby. Six sparkly spots around the plaza:
//   roll onto an empty one -> pick a decoration you own (bought in the FARM SHOP) to place there
//   roll onto a decorated one -> swap it for another, or put it away
// Saved on this device. Online, everyone sees the HOST's decorations (like visiting their farm);
// friends can't change them (they decorate when they host).
// Made by LobbyUI at the start of the lobby - nothing to set up.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyDeco : MonoBehaviour
{
    public static readonly Vector3[] Spots =
    {
        // close to the path, clear of the bushes: pairs either side of it, from the way in up to the farmhouse
        new Vector3(-4.4f, 0f, -10.0f), new Vector3(4.4f, 0f, -10.0f),   // by the way in
        new Vector3(-4.8f, 0f, 5.4f), new Vector3(4.8f, 0f, 5.4f),       // between the circle and the house
        new Vector3(-4.6f, 0f, 10.4f), new Vector3(4.6f, 0f, 10.4f),     // by the farmhouse door
    };
    const float Enter = 1.5f, Rearm = 2.8f;

    static string Key => "FarmChaos.Deco" + ChibiLook.TestWindowSuffix;
    public static LobbyDeco Instance { get; private set; }

    string[] placed = new string[Spots.Length];           // deco id per spot ("" = empty)
    readonly Transform[] built = new Transform[Spots.Length];
    readonly Transform[] sparkle = new Transform[Spots.Length];
    readonly bool[] armed = Enumerable.Repeat(true, Spots.Length).ToArray();
    RectTransform picker, list;
    TextMeshProUGUI pickerTitle;
    int pickerSpot = -1;
    SpherePlayer me;
    string shownLayout = null;

    public static LobbyDeco Make(Transform canvas)
    {
        var d = new GameObject("Lobby_Deco").AddComponent<LobbyDeco>();
        Instance = d;
        d.placed = Parse(PlayerPrefs.GetString(Key, ""));
        d.BuildPicker(canvas);
        for (int i = 0; i < Spots.Length; i++) d.sparkle[i] = d.MakeSparkle(Spots[i]);
        return d;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ------------------------------------------------------------ save + share
    /// <summary>"flowers,,pond,..." - one entry per spot.</summary>
    public static string MyLayout => PlayerPrefs.GetString(Key, "");
    static string[] Parse(string s)
    {
        var a = (s ?? "").Split(',');
        var r = new string[Spots.Length];
        for (int i = 0; i < r.Length; i++) r[i] = i < a.Length ? a[i] : "";
        return r;
    }

    void SaveMine() { PlayerPrefs.SetString(Key, string.Join(",", placed)); PlayerPrefs.Save(); }

    /// <summary>Online friend: the host's decorations arrived.</summary>
    public static string HostLayout;

    static bool IsHostOrSolo => !NetSession.Online || Unity.Netcode.NetworkManager.Singleton.IsServer;

    // ------------------------------------------------------------ every frame
    void Update()
    {
        // whose decorations to show: mine (solo / I'm the host), or the host's (I'm a friend)
        string layout = IsHostOrSolo ? string.Join(",", placed) : (HostLayout ?? "");
        if (layout != shownLayout) { shownLayout = layout; Show(Parse(layout)); }

        for (int i = 0; i < Spots.Length; i++)
        {
            bool empty = string.IsNullOrEmpty(Parse(layout)[i]);
            bool show = empty && IsHostOrSolo;
            if (sparkle[i].gameObject.activeSelf != show) sparkle[i].gameObject.SetActive(show);
            if (show)
            {
                sparkle[i].localRotation = Quaternion.Euler(0f, Time.time * 90f + i * 40f, 45f);
                sparkle[i].localPosition = Spots[i] + Vector3.up * (0.9f + Mathf.Sin(Time.time * 2f + i) * 0.12f);
            }
        }

        if (me == null) me = FindAnyObjectByType<SpherePlayer>();
        if (me == null) return;
        if (pickerSpot >= 0)
        {
            if (LobbyKit.EscPressed()) { ClosePicker(); return; }
            if (LobbyKit.FlatDistance(me.transform.position, Spots[pickerSpot]) > Rearm) ClosePicker();
            return;
        }
        if (!IsHostOrSolo || LobbyKit.BusyPanel) return;
        for (int i = 0; i < Spots.Length; i++)
        {
            float d = LobbyKit.FlatDistance(me.transform.position, Spots[i]);
            if (d > Rearm) armed[i] = true;
            if (armed[i] && d < Enter) { armed[i] = false; OpenPicker(i); break; }
        }
    }

    void Show(string[] ids)
    {
        for (int i = 0; i < Spots.Length; i++)
        {
            string want = ids[i] ?? "";
            if (built[i] != null && built[i].name == "Deco_" + want) continue;
            if (built[i] != null) Destroy(built[i].gameObject);
            built[i] = string.IsNullOrEmpty(want) ? null : Build(want, Spots[i], i);
        }
    }

    Transform MakeSparkle(Vector3 at)
    {
        var s = LobbyKit.Prim(PrimitiveType.Cube, transform, "Sparkle", at + Vector3.up, Vector3.one * 0.3f, new Color32(0xFF, 0xE8, 0x8A, 255), false, true);
        LobbyKit.Prim(PrimitiveType.Cylinder, transform, "Spot_Mark", at + Vector3.up * 0.03f, new Vector3(2.6f, 0.01f, 2.6f), new Color32(0xFF, 0xF0, 0xD9, 200));
        return s;
    }

    // ------------------------------------------------------------ picker
    void BuildPicker(Transform canvas)
    {
        picker = LobbyKit.Box(canvas, LobbyKit.Cream, new Vector2(1240, 300), new Vector2(0.5f, 0f), new Vector2(0, 210), true);
        picker.name = "Deco_Picker";
        pickerTitle = LobbyKit.Txt(picker, "", 40, LobbyKit.Berry, new Vector2(0, 112), new Vector2(1100, 54));
        list = new GameObject("Choices", typeof(RectTransform)).GetComponent<RectTransform>();
        list.SetParent(picker, false); list.sizeDelta = new Vector2(1200, 200); list.anchoredPosition = new Vector2(0, -20);
        picker.gameObject.SetActive(false);
    }

    void OpenPicker(int spot)
    {
        pickerSpot = spot;
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
        string current = placed[spot];
        // decorations I own that aren't already out somewhere else
        var free = ShopCatalog.Decos.Where(d => Bank.Owns(d.id) && (d.id == current || !placed.Contains(d.id))).ToList();
        var buttons = new List<(string label, Color c, UnityEngine.Events.UnityAction act)>();
        foreach (var d in free)
        {
            if (d.id == current) continue;
            var id = d.id;
            buttons.Add((d.name, d.colour, () => Place(spot, id)));
        }
        if (!string.IsNullOrEmpty(current)) buttons.Add(("PUT AWAY", LobbyKit.Grey, () => Place(spot, "")));
        buttons.Add(("NOT NOW", LobbyKit.Pink, ClosePicker));

        if (string.IsNullOrEmpty(current))
            pickerTitle.text = free.Count == 0 ? "An empty spot... buy decorations at the FARM SHOP" : "Decorate this spot!";
        else pickerTitle.text = "Swap the " + ShopCatalog.Find(current)?.name + "...";

        int n = Mathf.Min(buttons.Count, 6);
        float w = 1180f / n;
        for (int i = 0; i < n; i++)
        {
            var (label, c, act) = buttons[i];
            LobbyKit.Btn(list, label, c, new Vector2(w - 16f, 110), new Vector2((i - (n - 1) / 2f) * w, 0), 30, act);
        }
        picker.gameObject.SetActive(true);
        picker.SetAsLastSibling();
    }

    void ClosePicker() { pickerSpot = -1; if (picker != null) picker.gameObject.SetActive(false); }

    void Place(int spot, string id)
    {
        placed[spot] = id;
        SaveMine();
        ClosePicker();
    }

    // ------------------------------------------------------------ the decorations (cute shapes)
    static readonly Color Wood = new Color32(0xA8, 0x7B, 0x5C, 255), LightWood = new Color32(0xD9, 0xB9, 0x9B, 255), Leaf = new Color32(0x7F, 0xB0, 0x6E, 255),
        Water = new Color32(0x86, 0xC3, 0xFA, 255), Stone = new Color32(0xD8, 0xD8, 0xE4, 255), Straw = new Color32(0xD6, 0xB7, 0x5A, 255),
        Red = new Color32(0xE0, 0x4C, 0x5C, 255), White = Color.white, Yellow = new Color32(0xFF, 0xD2, 0x7A, 255), Pink = new Color32(0xF4, 0xB8, 0xC1, 255);

    Transform Build(string id, Vector3 at, int spot)
    {
        var root = new GameObject("Deco_" + id).transform;
        root.SetParent(transform, false);
        root.localPosition = at;
        root.localRotation = Quaternion.Euler(0f, spot % 2 == 0 ? 20f : -20f, 0f);
        root.localScale = Vector3.one * 1.6f;                     // nice and big
        System.Func<PrimitiveType, string, Vector3, Vector3, Color, Transform> P = (t, n, p, s, c) => LobbyKit.Prim(t, root, n, p, s, c, false, false);
        var anim = root.gameObject.AddComponent<DecoBob>();
        switch (id)
        {
            case "flowers":
                P(PrimitiveType.Cylinder, "Bed", new Vector3(0, 0.12f, 0), new Vector3(1.8f, 0.12f, 1.1f), Wood);
                P(PrimitiveType.Cylinder, "Soil", new Vector3(0, 0.25f, 0), new Vector3(1.6f, 0.02f, 0.95f), new Color32(0x6B, 0x4A, 0x3A, 255));
                Color[] petals = { Pink, Yellow, new Color32(0xC3, 0xA6, 0xF2, 255), Red, White };
                for (int i = 0; i < 9; i++)
                {
                    var p = new Vector3(-0.6f + (i % 3) * 0.6f, 0, -0.3f + (i / 3) * 0.3f);
                    P(PrimitiveType.Cylinder, "Stem", p + new Vector3(0, 0.42f, 0), new Vector3(0.04f, 0.18f, 0.04f), Leaf);
                    var f = P(PrimitiveType.Sphere, "Flower", p + new Vector3(0, 0.64f, 0), new Vector3(0.24f, 0.12f, 0.24f), petals[i % petals.Length]);
                    P(PrimitiveType.Sphere, "Middle", p + new Vector3(0, 0.69f, 0), Vector3.one * 0.08f, Yellow);
                    anim.Add(f, 0.03f, 2f + i);
                }
                break;
            case "picnic":
                for (int x = 0; x < 4; x++) for (int z = 0; z < 4; z++)
                    P(PrimitiveType.Cube, "Check", new Vector3(-0.6f + x * 0.4f, 0.03f, -0.6f + z * 0.4f), new Vector3(0.4f, 0.02f, 0.4f), (x + z) % 2 == 0 ? Red : White);
                P(PrimitiveType.Cube, "Basket", new Vector3(0.3f, 0.2f, 0.1f), new Vector3(0.5f, 0.3f, 0.35f), Straw);
                P(PrimitiveType.Cylinder, "Handle", new Vector3(0.3f, 0.42f, 0.1f), new Vector3(0.4f, 0.02f, 0.06f), Wood).localRotation = Quaternion.Euler(90, 0, 0);
                P(PrimitiveType.Sphere, "Apple", new Vector3(-0.35f, 0.1f, -0.25f), Vector3.one * 0.16f, Red);
                P(PrimitiveType.Cylinder, "Cup", new Vector3(-0.2f, 0.1f, 0.35f), new Vector3(0.12f, 0.08f, 0.12f), new Color32(0x8E, 0xC5, 0xF0, 255));
                break;
            case "balloons":
                P(PrimitiveType.Cube, "Weight", new Vector3(0, 0.12f, 0), new Vector3(0.3f, 0.24f, 0.3f), Pink);
                Color[] bc = { new Color32(0x8E, 0xC5, 0xF0, 255), Pink, Yellow, new Color32(0x9B, 0xDE, 0xA8, 255), new Color32(0xC3, 0xA6, 0xF2, 255) };
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72f * Mathf.Deg2Rad;
                    var top = new Vector3(Mathf.Cos(a) * 0.35f, 2.3f + (i % 2) * 0.35f, Mathf.Sin(a) * 0.35f);
                    var str = P(PrimitiveType.Cylinder, "String", (top + new Vector3(0, 0.24f, 0)) * 0.5f, new Vector3(0.015f, top.magnitude * 0.5f, 0.015f), White);
                    str.localRotation = Quaternion.FromToRotation(Vector3.up, top.normalized);
                    var b = P(PrimitiveType.Sphere, "Balloon", top, new Vector3(0.5f, 0.6f, 0.5f), bc[i]);
                    anim.Add(b, 0.08f, 1.3f + i * 0.4f);
                }
                break;
            case "lanterns":
                P(PrimitiveType.Cylinder, "Post_L", new Vector3(-1.0f, 1.0f, 0), new Vector3(0.08f, 1.0f, 0.08f), Wood);
                P(PrimitiveType.Cylinder, "Post_R", new Vector3(1.0f, 1.0f, 0), new Vector3(0.08f, 1.0f, 0.08f), Wood);
                P(PrimitiveType.Cylinder, "Rope", new Vector3(0, 1.95f, 0), new Vector3(0.02f, 1.0f, 0.02f), Wood).localRotation = Quaternion.Euler(0, 0, 90);
                Color[] lc = { Pink, Yellow, new Color32(0x9B, 0xDE, 0xA8, 255) };
                for (int i = 0; i < 3; i++)
                {
                    var l = LobbyKit.Prim(PrimitiveType.Sphere, root, "Lantern", new Vector3(-0.55f + i * 0.55f, 1.7f, 0), new Vector3(0.32f, 0.38f, 0.32f), lc[i], false, true);
                    anim.Add(l, 0.04f, 1.5f + i);
                }
                var light = new GameObject("Glow").AddComponent<Light>(); light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(0, 1.6f, 0); light.type = LightType.Point; light.color = new Color(1f, 0.85f, 0.6f); light.range = 4f; light.intensity = 1.5f;
                break;
            case "mushroom":
                P(PrimitiveType.Cylinder, "Stalk", new Vector3(0, 0.6f, 0), new Vector3(0.5f, 0.6f, 0.5f), new Color32(0xFF, 0xF0, 0xD9, 255));
                var cap = P(PrimitiveType.Sphere, "Cap", new Vector3(0, 1.3f, 0), new Vector3(1.6f, 0.9f, 1.6f), Red);
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 51f * Mathf.Deg2Rad; float h = i == 0 ? 0f : 0.55f;
                    P(PrimitiveType.Sphere, "Spot", new Vector3(Mathf.Cos(a) * h, i == 0 ? 1.76f : 1.58f, Mathf.Sin(a) * h), new Vector3(0.22f, 0.08f, 0.22f), White);
                }
                break;
            case "scarecrow":
                P(PrimitiveType.Cylinder, "Pole", new Vector3(0, 0.9f, 0), new Vector3(0.1f, 0.9f, 0.1f), Wood);
                P(PrimitiveType.Cylinder, "Arms", new Vector3(0, 1.3f, 0), new Vector3(0.08f, 0.7f, 0.08f), Wood).localRotation = Quaternion.Euler(0, 0, 90);
                P(PrimitiveType.Capsule, "Shirt", new Vector3(0, 1.25f, 0), new Vector3(0.6f, 0.45f, 0.4f), new Color32(0x8E, 0xC5, 0xF0, 255));
                P(PrimitiveType.Sphere, "Head", new Vector3(0, 1.85f, 0), Vector3.one * 0.5f, Straw);
                P(PrimitiveType.Cylinder, "Hat_Brim", new Vector3(0, 2.08f, 0), new Vector3(0.75f, 0.02f, 0.75f), Wood);
                P(PrimitiveType.Cylinder, "Hat_Top", new Vector3(0, 2.22f, 0), new Vector3(0.38f, 0.14f, 0.38f), Wood);
                P(PrimitiveType.Sphere, "Eye_L", new Vector3(-0.1f, 1.9f, -0.22f), Vector3.one * 0.07f, new Color32(0x2E, 0x2A, 0x3A, 255));
                P(PrimitiveType.Sphere, "Eye_R", new Vector3(0.1f, 1.9f, -0.22f), Vector3.one * 0.07f, new Color32(0x2E, 0x2A, 0x3A, 255));
                P(PrimitiveType.Sphere, "Straw_L", new Vector3(-0.72f, 1.3f, 0), new Vector3(0.18f, 0.12f, 0.12f), Straw);
                P(PrimitiveType.Sphere, "Straw_R", new Vector3(0.72f, 1.3f, 0), new Vector3(0.18f, 0.12f, 0.12f), Straw);
                break;
            case "swing":
                P(PrimitiveType.Cylinder, "Leg_L", new Vector3(-0.9f, 1.1f, 0), new Vector3(0.12f, 1.1f, 0.12f), Wood);
                P(PrimitiveType.Cylinder, "Leg_R", new Vector3(0.9f, 1.1f, 0), new Vector3(0.12f, 1.1f, 0.12f), Wood);
                P(PrimitiveType.Cylinder, "Top", new Vector3(0, 2.2f, 0), new Vector3(0.12f, 1.0f, 0.12f), Wood).localRotation = Quaternion.Euler(0, 0, 90);
                var seat = new GameObject("Seat_Pivot").transform; seat.SetParent(root, false); seat.localPosition = new Vector3(0, 2.2f, 0);
                LobbyKit.Prim(PrimitiveType.Cylinder, seat, "Rope_L", new Vector3(-0.3f, -0.75f, 0), new Vector3(0.02f, 0.75f, 0.02f), LightWood);
                LobbyKit.Prim(PrimitiveType.Cylinder, seat, "Rope_R", new Vector3(0.3f, -0.75f, 0), new Vector3(0.02f, 0.75f, 0.02f), LightWood);
                LobbyKit.Prim(PrimitiveType.Cube, seat, "Seat", new Vector3(0, -1.5f, 0), new Vector3(0.8f, 0.08f, 0.35f), Pink);
                anim.Swing(seat);
                break;
            case "arch":
                for (int i = 0; i <= 10; i++)
                {
                    float a = Mathf.PI * i / 10f;
                    var p = new Vector3(Mathf.Cos(a) * 1.1f, 0.1f + Mathf.Sin(a) * 2.0f, 0);
                    P(PrimitiveType.Sphere, "Vine", p, Vector3.one * 0.28f, Leaf);
                    if (i % 2 == 1)
                    {
                        var bulb = LobbyKit.Prim(PrimitiveType.Sphere, root, "Fairy_Light", p + new Vector3(0, 0, -0.14f), Vector3.one * 0.12f, i % 4 == 1 ? Yellow : Pink, false, true);
                        anim.Twinkle(bulb, i);
                    }
                }
                break;
            case "pond":
                P(PrimitiveType.Cylinder, "Rim", new Vector3(0, 0.06f, 0), new Vector3(2.2f, 0.06f, 1.6f), Stone);
                P(PrimitiveType.Cylinder, "Water", new Vector3(0, 0.1f, 0), new Vector3(1.9f, 0.02f, 1.35f), Water);
                for (int i = 0; i < 2; i++)
                {
                    var duck = new GameObject("Duck").transform; duck.SetParent(root, false);
                    LobbyKit.Prim(PrimitiveType.Sphere, duck, "Body", new Vector3(0, 0.18f, 0), new Vector3(0.3f, 0.2f, 0.38f), Yellow);
                    LobbyKit.Prim(PrimitiveType.Sphere, duck, "Head", new Vector3(0, 0.35f, 0.14f), Vector3.one * 0.18f, Yellow);
                    LobbyKit.Prim(PrimitiveType.Cube, duck, "Beak", new Vector3(0, 0.33f, 0.25f), new Vector3(0.08f, 0.04f, 0.08f), new Color32(0xF5, 0xA6, 0x23, 255));
                    anim.Paddle(duck, i * Mathf.PI, 0.45f);
                }
                P(PrimitiveType.Cylinder, "Lily", new Vector3(0.55f, 0.12f, -0.3f), new Vector3(0.35f, 0.01f, 0.35f), Leaf);
                break;
            case "fountain":
                P(PrimitiveType.Cylinder, "Basin", new Vector3(0, 0.25f, 0), new Vector3(1.8f, 0.25f, 1.8f), Stone);
                P(PrimitiveType.Cylinder, "Water", new Vector3(0, 0.48f, 0), new Vector3(1.6f, 0.02f, 1.6f), Water);
                P(PrimitiveType.Cylinder, "Column", new Vector3(0, 0.9f, 0), new Vector3(0.3f, 0.5f, 0.3f), Stone);
                P(PrimitiveType.Cylinder, "Bowl", new Vector3(0, 1.45f, 0), new Vector3(0.9f, 0.08f, 0.9f), Stone);
                for (int i = 0; i < 6; i++)
                {
                    var drop = LobbyKit.Prim(PrimitiveType.Sphere, root, "Drop", new Vector3(0, 1.6f, 0), Vector3.one * 0.1f, Water, false, true);
                    anim.Spray(drop, i / 6f);
                }
                break;
        }
        return root;
    }
}

/// <summary>Tiny animations for the decorations (bobbing balloons, a swing, paddling ducks, twinkling lights, a fountain).</summary>
public class DecoBob : MonoBehaviour
{
    readonly List<System.Action<float>> anims = new List<System.Action<float>>();
    public void Add(Transform t, float amount, float speed) { var b = t.localPosition; anims.Add(time => t.localPosition = b + Vector3.up * Mathf.Sin(time * speed) * amount); }
    public void Swing(Transform pivot) => anims.Add(time => pivot.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.6f) * 18f, 0, 0));
    public void Twinkle(Transform t, int i) { var s = t.localScale; anims.Add(time => t.localScale = s * (0.75f + 0.35f * Mathf.Abs(Mathf.Sin(time * 2.5f + i)))); }
    public void Paddle(Transform duck, float phase, float r) => anims.Add(time =>
    {
        float a = time * 0.6f + phase;
        duck.localPosition = new Vector3(Mathf.Cos(a) * r, 0.02f * Mathf.Sin(time * 4f + phase), Mathf.Sin(a) * r * 0.6f);
        duck.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a) * 0.6f));
    });
    public void Spray(Transform d, float offset) => anims.Add(time =>
    {
        float k = (time * 0.8f + offset) % 1f; float ang = offset * Mathf.PI * 2f;
        d.localPosition = new Vector3(Mathf.Cos(ang) * k * 0.6f, 1.6f + k * 0.8f - k * k * 1.4f, Mathf.Sin(ang) * k * 0.6f);
    });
    void Update() { float t = Time.time; foreach (var a in anims) a(t); }
}
