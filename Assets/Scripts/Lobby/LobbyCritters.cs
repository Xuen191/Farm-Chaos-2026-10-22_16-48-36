// Farm Chaos - a couple of chickens and a bunny pottering about the lobby, plus a little PRACTICE PEN.
// Roll up to an animal and it starts following you; lead it into the pen for a cheer - just for fun, no coins.
// After a little rest it hops back out to play again. (Each player has their own lobby animals.)
// Made by LobbyUI at the start of the lobby - nothing to set up.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LobbyCritters : MonoBehaviour
{
    public static readonly Vector3 Pen = new Vector3(12.2f, 0f, 9.2f);
    const float PenRadius = 2.0f;
    static readonly Vector3 Meadow = new Vector3(10.5f, 0f, 4.5f);

    class Critter
    {
        public Transform t; public float height, speed, hop;
        public Vector3 target; public float retarget;
        public bool following, penned; public float penUntil;
    }
    readonly List<Critter> critters = new List<Critter>();
    SpherePlayer me;
    TextMeshPro cheer; float cheerT = -1f;

    public static LobbyCritters Make(GameObject chicken, GameObject bunny)
    {
        var c = new GameObject("Lobby_Critters").AddComponent<LobbyCritters>();
        c.BuildPen();
        c.Spawn(chicken, 0.8f, 2.6f, 0.18f, new Vector3(9f, 0f, 3f));
        c.Spawn(chicken, 0.8f, 2.6f, 0.18f, new Vector3(11.5f, 0f, 5.5f));
        c.Spawn(bunny, 0.7f, 3.0f, 0.3f, new Vector3(13f, 0f, 3.5f));
        return c;
    }

    void BuildPen()
    {
        var pen = new GameObject("Practice_Pen").transform; pen.SetParent(transform, false); pen.position = Pen;
        Color wood = new Color32(0xD9, 0xB9, 0x9B, 255), rail = new Color32(0xFF, 0xF0, 0xD9, 255);
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * 360f;
            if (a > 160f && a < 215f) continue;                                   // the way in (facing the plaza)
            var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, PenRadius + 0.3f);
            LobbyKit.Prim(PrimitiveType.Cube, pen, "Post", p + Vector3.up * 0.35f, new Vector3(0.14f, 0.7f, 0.14f), wood, true);
            var r = LobbyKit.Prim(PrimitiveType.Cube, pen, "Rail", Quaternion.Euler(0f, a + 360f / 28f, 0f) * new Vector3(0f, 0.55f, PenRadius + 0.3f),
                new Vector3(1.0f, 0.08f, 0.06f), rail, false);
            r.localRotation = Quaternion.Euler(0f, a + 360f / 28f, 0f);
        }
        LobbyKit.Prim(PrimitiveType.Cylinder, pen, "Hay", new Vector3(0f, 0.04f, 0f), new Vector3(PenRadius * 2f, 0.02f, PenRadius * 2f), new Color32(0xE8, 0xD3, 0x8A, 255));
        var sign = new GameObject("Pen_Sign").AddComponent<TextMeshPro>();
        sign.transform.SetParent(pen, false); sign.transform.localPosition = new Vector3(0f, 1.6f, -PenRadius - 0.4f);
        sign.text = "PRACTICE PEN"; sign.fontSize = 3f; sign.alignment = TextAlignmentOptions.Center; sign.fontStyle = FontStyles.Bold;
        sign.color = Color.white; sign.outlineWidth = 0.22f; sign.outlineColor = LobbyKit.Berry; sign.rectTransform.sizeDelta = new Vector2(4f, 1f);
        cheer = new GameObject("Pen_Cheer").AddComponent<TextMeshPro>();
        cheer.transform.SetParent(pen, false); cheer.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        cheer.fontSize = 5f; cheer.alignment = TextAlignmentOptions.Center; cheer.fontStyle = FontStyles.Bold; cheer.color = Color.white;
        cheer.outlineWidth = 0.25f; cheer.outlineColor = new Color32(0x3F, 0xAF, 0x5A, 255); cheer.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
        cheer.gameObject.SetActive(false);
    }

    void Spawn(GameObject prefab, float height, float speed, float hop, Vector3 at)
    {
        Transform t;
        if (prefab != null)
        {
            var holder = new GameObject("Holder"); holder.SetActive(false);
            var g = Instantiate(prefab, holder.transform);
            for (int pass = 0; pass < 3; pass++)                                   // just the looks: no brains, physics or networking
                foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
            t = new GameObject(prefab.name).transform; t.SetParent(transform, false);
            g.transform.SetParent(t, false); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
            Destroy(holder);
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y > 0.01f) g.transform.localScale *= height / b.size.y;
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.position += new Vector3(t.position.x - b.center.x, t.position.y - b.min.y, t.position.z - b.center.z);
            }
        }
        else t = LobbyKit.Prim(PrimitiveType.Sphere, transform, "Critter", Vector3.zero, Vector3.one * height, Color.white);
        t.position = at;
        critters.Add(new Critter { t = t, height = height, speed = speed, hop = hop, target = at });
    }

    void Update()
    {
        if (me == null) me = FindAnyObjectByType<SpherePlayer>();
        float dt = Time.deltaTime;
        int followers = 0;
        foreach (var c in critters)
        {
            Vector3 pos = c.t.position; pos.y = 0f;
            if (c.penned)
            {
                if (Time.time > c.penUntil) { c.penned = false; c.target = Meadow + Random.insideUnitSphere.Flat() * 3f; }
                else { Wander(c, Pen, PenRadius * 0.6f); }
            }
            else if (c.following && me != null)
            {
                Vector3 mp = me.transform.position; mp.y = 0f;
                float keep = 1.3f + followers * 0.8f; followers++;
                c.target = mp + (pos - mp).normalized * keep;
                if (LobbyKit.FlatDistance(pos, Pen) < PenRadius * 0.85f) Penned(c);   // brought home!
            }
            else
            {
                Wander(c, Meadow, 4f);
                if (me != null && LobbyKit.FlatDistance(pos, me.transform.position) < 1.4f) { c.following = true; Bubble(c.t, "!"); }
            }
            // move + little hops, facing where we're going
            Vector3 to = c.target - pos; to.y = 0f;
            float spd = c.following ? c.speed * 1.4f : c.speed * 0.5f;
            bool moving = to.magnitude > 0.15f;
            if (moving) pos += to.normalized * Mathf.Min(to.magnitude, spd * dt);
            float h = moving ? Mathf.Abs(Mathf.Sin(Time.time * 10f + c.GetHashCode())) * c.hop : 0f;
            c.t.position = pos + Vector3.up * h;
            if (moving) c.t.rotation = Quaternion.Slerp(c.t.rotation, Quaternion.LookRotation(to.normalized), 1f - Mathf.Exp(-8f * dt));
        }

        if (cheerT >= 0f)
        {
            cheerT += dt;
            cheer.transform.localScale = Vector3.one * (Mathf.Min(1f, cheerT * 6f) * (1f + 0.1f * Mathf.Sin(cheerT * 12f)));
            var cam = Camera.main; if (cam != null) cheer.transform.rotation = Quaternion.LookRotation(cheer.transform.position - cam.transform.position);
            if (cheerT > 2f) { cheerT = -1f; cheer.gameObject.SetActive(false); }
        }
    }

    void Wander(Critter c, Vector3 centre, float radius)
    {
        if (Time.time > c.retarget || LobbyKit.FlatDistance(c.t.position, c.target) < 0.2f)
        {
            c.retarget = Time.time + Random.Range(1.5f, 4f);
            c.target = centre + Random.insideUnitSphere.Flat() * radius;
        }
    }

    void Penned(Critter c)
    {
        c.following = false; c.penned = true; c.penUntil = Time.time + 8f;
        string[] yay = { "GOT IT!", "NICE ONE!", "SAFE AND SOUND!", "FARMER OF THE YEAR!" };
        cheer.text = yay[Random.Range(0, yay.Length)];
        cheer.gameObject.SetActive(true); cheerT = 0f;
        foreach (var m in FindObjectsByType<ChibiMotor>(FindObjectsSortMode.None))
            if (m.transform.parent != null && m.transform.parent.GetComponent<SpherePlayer>() != null) { m.Emote(true); break; }
    }

    void Bubble(Transform t, string s)
    {
        var b = new GameObject("Bubble").AddComponent<TextMeshPro>();
        b.transform.SetParent(t, false); b.transform.localPosition = Vector3.up * 1.1f;
        b.text = s; b.fontSize = 6f; b.alignment = TextAlignmentOptions.Center; b.color = new Color32(0xE0, 0x6C, 0x8C, 255); b.fontStyle = FontStyles.Bold;
        b.rectTransform.sizeDelta = new Vector2(1f, 1f);
        var cam = Camera.main; if (cam != null) b.transform.rotation = Quaternion.LookRotation(b.transform.position - cam.transform.position);
        Destroy(b.gameObject, 1.2f);
    }
}

static class FlatExt { public static Vector3 Flat(this Vector3 v) { v.y = 0f; return v; } }
