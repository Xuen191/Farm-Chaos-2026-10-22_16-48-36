#if UNITY_EDITOR
// Farm Chaos - rounded path + decoration builder.
// Menu: Tools > Farm Chaos
// Only ever creates / deletes two root objects: "Farm_Paths_Rounded" and "Farm_Decorations".
// It never modifies existing objects. Every action supports Ctrl+Z.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmPathAndDecorBuilder
{
    const string PathsRoot = "Farm_Paths_Rounded";
    const string DecorRoot = "Farm_Decorations";
    const string Pack = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
    const string PathMaterial = "Assets/Materials/Mat_walkways.mat";

    // ---------- Path settings (tweak and re-run "Build Rounded Paths") ----------
    const float PathWidth = 3f;       // width of every trail
    const float RingPadding = 3.5f;   // gap between the HQ pad and the loop around it
    // name of the area object, and how much the trail curves (+ = bends counter-clockwise, - = clockwise)
    static readonly (string area, float bend)[] Branches =
    {
        ("chicken_area",      2.5f),
        ("dogkennelarea",    -5f),
        ("sheep_area",        3.5f),
        ("cow_area",          5f),
        ("rabbit_area",       3.5f),
        ("powerstation_area", 0f),
    };

    // ======================================================================
    [MenuItem("Tools/Farm Chaos/1. Build Rounded Paths")]
    public static void BuildPaths()
    {
        var ctx = SceneInfo.Gather();
        if (ctx == null) return;
        DestroyRoot(PathsRoot);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(PathMaterial);
        if (mat == null) { var g = AssetDatabase.FindAssets("Mat_walkways t:Material"); if (g.Length > 0) mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g[0])); }
        var root = new GameObject(PathsRoot);
        Undo.RegisterCreatedObjectUndo(root, "Build Rounded Paths");

        foreach (var p in ComputePaths(ctx))
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(root.transform, false);
            var mesh = p.closed ? Ribbon(p.points, PathWidth, true) : Ribbon(p.points, PathWidth, false);
            mesh.name = p.name + "_Mesh";
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Warn if a trail runs over something that isn't an animal area
        foreach (var p in ComputePaths(ctx))
            foreach (var ob in ctx.obstacles)
                if (!ob.isArea && p.points.Any(pt => ob.rect.Contains(new Vector2(pt.x, pt.z))))
                { Debug.LogWarning($"[Farm Chaos] {p.name} crosses '{ob.name}'. Adjust its bend in FarmPathAndDecorBuilder.cs if needed."); break; }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("[Farm Chaos] Rounded paths built under '" + PathsRoot + "'.");
    }

    [MenuItem("Tools/Farm Chaos/2. Build Decorations (new random layout)")]
    public static void BuildDecorations()
    {
        var ctx = SceneInfo.Gather();
        if (ctx == null) return;
        DestroyRoot(DecorRoot);
        int seed = System.Environment.TickCount;
        new Decorator(ctx, ComputePaths(ctx), seed).Run();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Farm Chaos] Decorations built under '" + DecorRoot + "' (seed " + seed + "). Run again for a different layout.");
    }

    [MenuItem("Tools/Farm Chaos/Top-Down View")]
    public static void TopView()
    {
        var sv = SceneView.lastActiveSceneView; if (sv == null) return;
        var ctx = SceneInfo.Gather();
        Vector3 c = ctx != null ? new Vector3(ctx.play.center.x, 0, ctx.play.center.y) : Vector3.zero;
        float size = ctx != null ? Mathf.Max(ctx.play.width, ctx.play.height) * 0.55f : 55f;
        sv.orthographic = true;
        sv.LookAt(c, Quaternion.Euler(90, 0, 0), size, true, true);
        sv.Repaint();
        if (ctx != null)
        {
            foreach (var kv in ctx.areas) Debug.Log($"[Farm Chaos] area {kv.Key}: center {kv.Value.center} size {kv.Value.size}");
            foreach (var p in ComputePaths(ctx)) Debug.Log($"[Farm Chaos] {p.name}: {p.points[0]} -> {p.points[p.points.Count - 1]}");
        }
    }

    [MenuItem("Tools/Farm Chaos/Angled Overview")]
    public static void AngledView()
    {
        var sv = SceneView.lastActiveSceneView; if (sv == null) return;
        sv.orthographic = false;
        sv.LookAt(new Vector3(0, 0, -6), Quaternion.Euler(45, 0, 0), 38f, false, true);
        sv.Repaint();
    }

    [MenuItem("Tools/Farm Chaos/Clear Generated Paths")]
    public static void ClearPaths() => DestroyRoot(PathsRoot);

    [MenuItem("Tools/Farm Chaos/Clear Generated Decorations")]
    public static void ClearDecor() => DestroyRoot(DecorRoot);

    static void DestroyRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) Undo.DestroyObjectImmediate(r);
    }

    // ======================================================================
    // Scene reading
    class Obstacle { public string name; public Rect rect; public bool isArea; }

    class SceneInfo
    {
        public Rect hq;            // XZ rect of HQ pad
        public Rect play;          // inner playable rect (inside border walls)
        public float groundY;
        public Dictionary<string, Rect> areas = new Dictionary<string, Rect>();
        public Dictionary<string, Transform> areaTransforms = new Dictionary<string, Transform>();
        public List<Obstacle> obstacles = new List<Obstacle>();

        static string Norm(string s) => s.ToLowerInvariant().Replace("_", "").Replace(" ", "");

        public static IEnumerable<Transform> All()
        {
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (r.name == PathsRoot || r.name == DecorRoot) continue;
                foreach (var t in r.GetComponentsInChildren<Transform>(true)) yield return t;
            }
        }

        static Transform Find(params string[] names)
        {
            var wanted = names.Select(Norm).ToList();
            return All().FirstOrDefault(t => wanted.Contains(Norm(t.name)));
        }

        public static bool WorldBounds(GameObject go, out Bounds b)
        {
            b = default; bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has;
        }

        static Rect XZ(Bounds b) => Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);

        public static SceneInfo Gather()
        {
            var s = new SceneInfo();
            var hq = Find("hq_area", "HQ_Area", "hqarea");
            if (hq == null || !WorldBounds(hq.gameObject, out var hb))
            { EditorUtility.DisplayDialog("Farm Chaos", "Couldn't find the HQ area object (hq_area).", "OK"); return null; }
            s.hq = XZ(hb);

            var areaNames = new[] { "hq_area", "chicken_area", "sheep_area", "cow_area", "rabbit_area", "powerstation_area", "dogkennelarea" };
            var alt = new Dictionary<string, string> { { "dogkennelarea", "Dog_Kennel_Area" } };
            foreach (var n in areaNames)
            {
                var t = Find(n, alt.ContainsKey(n) ? alt[n] : n);
                if (t != null && WorldBounds(t.gameObject, out var ab)) { s.areas[n] = XZ(ab); s.areaTransforms[n] = t; }
                else Debug.LogWarning("[Farm Chaos] Area not found: " + n);
            }

            var ground = Find("land", "Zoo_Ground", "ground");
            s.groundY = ground != null && WorldBounds(ground.gameObject, out var gb) ? gb.max.y : 0f;

            // playable rect from border walls
            Rect play = Rect.MinMaxRect(-45, -45, 45, 45);
            var bw = Find("border_west"); var be = Find("border_east"); var bn = Find("border_north"); var bs = Find("border_south");
            if (bw && be && bn && bs &&
                WorldBounds(bw.gameObject, out var w) && WorldBounds(be.gameObject, out var e) &&
                WorldBounds(bn.gameObject, out var no) && WorldBounds(bs.gameObject, out var so))
                play = Rect.MinMaxRect(w.max.x, so.max.z, e.min.x, no.min.z);
            else if (ground != null && WorldBounds(ground.gameObject, out var gb2)) play = XZ(gb2);
            s.play = play;

            // obstacles: every renderer that isn't the ground
            var areaSet = new HashSet<string>(s.areas.Keys.Select(Norm));
            foreach (var r in All().Select(t => t.GetComponent<Renderer>()).Where(r => r != null))
            {
                var rb = r.bounds;
                if (rb.size.x > 40 || rb.size.z > 40) continue; // ground / long border walls
                if (ground != null && r.transform == ground) continue;
                s.obstacles.Add(new Obstacle { name = r.name, rect = XZ(rb), isArea = areaSet.Contains(Norm(r.name)) });
            }
            return s;
        }
    }

    // ======================================================================
    // Path geometry
    class PathData { public string name; public List<Vector3> points; public bool closed; }

    static List<PathData> ComputePaths(SceneInfo s)
    {
        var list = new List<PathData>();
        Vector2 c = s.hq.center;
        float rx = s.hq.width / 2f + RingPadding, rz = s.hq.height / 2f + RingPadding;
        float yRing = s.groundY + 0.03f, yBranch = s.groundY + 0.04f;

        // Loop around HQ (ellipse)
        var ring = new List<Vector3>();
        int n = 96;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            ring.Add(new Vector3(c.x + Mathf.Cos(a) * rx, yRing, c.y + Mathf.Sin(a) * rz));
        }
        list.Add(new PathData { name = "Path_HQ_Loop", points = ring, closed = true });

        foreach (var (area, bend) in Branches)
        {
            if (!s.areas.TryGetValue(area, out var rect)) continue;
            Vector2 target = rect.center;
            Vector2 dir = (target - c).normalized;
            float t = 1f / Mathf.Sqrt(dir.x * dir.x / (rx * rx) + dir.y * dir.y / (rz * rz));
            Vector2 start = c + dir * t;

            // where the straight line from start to area centre enters the area
            Vector2 fwd = (target - start).normalized;
            Vector2 end = RayEnterRect(start, fwd, rect);
            // pads are slightly rotated: walk forward until we're really on the pad, then tuck 0.8 under it
            if (s.areaTransforms.TryGetValue(area, out var at))
            {
                for (int k = 0; k < 120 && !InsidePad(at, end); k++) end += fwd * 0.25f;
            }
            end += fwd * 0.8f;
            if ((end - start).magnitude < 0.5f) continue;

            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 ctrl = (start + end) / 2f + perp * bend * 2f;

            var pts = new List<Vector3>();
            float len = (end - start).magnitude + Mathf.Abs(bend);
            int steps = Mathf.Max(8, Mathf.CeilToInt(len / 0.4f));
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                Vector2 q = (1 - u) * (1 - u) * start + 2 * (1 - u) * u * ctrl + u * u * end;
                pts.Add(new Vector3(q.x, yBranch, q.y));
            }
            string nice = area.Replace("_area", "").Replace("area", "");
            nice = char.ToUpper(nice[0]) + nice.Substring(1);
            list.Add(new PathData { name = "Path_" + nice, points = pts, closed = false });
        }
        return list;
    }

    static bool InsidePad(Transform t, Vector2 p)
    {
        var mf = t.GetComponent<MeshFilter>();
        var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        var lp = t.InverseTransformPoint(new Vector3(p.x, t.position.y, p.y));
        return Mathf.Abs(lp.x - b.center.x) <= b.extents.x && Mathf.Abs(lp.z - b.center.z) <= b.extents.z;
    }

    static Vector2 RayEnterRect(Vector2 o, Vector2 d, Rect r)
    {
        float tmin = 0f, tmax = float.MaxValue;
        for (int ax = 0; ax < 2; ax++)
        {
            float oo = ax == 0 ? o.x : o.y, dd = ax == 0 ? d.x : d.y;
            float mn = ax == 0 ? r.xMin : r.yMin, mx = ax == 0 ? r.xMax : r.yMax;
            if (Mathf.Abs(dd) < 1e-5f) continue;
            float t1 = (mn - oo) / dd, t2 = (mx - oo) / dd;
            if (t1 > t2) { var tmp = t1; t1 = t2; t2 = tmp; }
            tmin = Mathf.Max(tmin, t1); tmax = Mathf.Min(tmax, t2);
        }
        return o + d * tmin;
    }

    // Flat ribbon mesh with rounded end caps
    static Mesh Ribbon(List<Vector3> p, float width, bool closed)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float h = width / 2f, dist = 0f;
        int count = p.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 prev = closed ? p[(i - 1 + count) % count] : p[Mathf.Max(i - 1, 0)];
            Vector3 next = closed ? p[(i + 1) % count] : p[Mathf.Min(i + 1, count - 1)];
            Vector3 tan = (next - prev); tan.y = 0; tan.Normalize();
            Vector3 left = new Vector3(-tan.z, 0, tan.x);
            if (i > 0) dist += Vector3.Distance(p[i], p[i - 1]);
            v.Add(p[i] + left * h); uv.Add(new Vector2(0, dist / width));
            v.Add(p[i] - left * h); uv.Add(new Vector2(1, dist / width));
        }
        int segs = closed ? count : count - 1;
        for (int i = 0; i < segs; i++)
        {
            int a = i * 2, b = ((i + 1) % count) * 2;
            int L0 = a, R0 = a + 1, L1 = b, R1 = b + 1;
            tri.AddRange(new[] { L0, L1, R1, L0, R1, R0 });
        }
        if (!closed)
        {
            AddCap(p[count - 1], p[count - 1] - p[count - 2], h, v, uv, tri);
            AddCap(p[0], p[0] - p[1], h, v, uv, tri);
        }
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.SetNormals(Enumerable.Repeat(Vector3.up, v.Count).ToList());
        m.RecalculateBounds();
        return m;
    }

    static void AddCap(Vector3 c, Vector3 fwd, float h, List<Vector3> v, List<Vector2> uv, List<int> tri)
    {
        fwd.y = 0; fwd.Normalize();
        Vector3 left = new Vector3(-fwd.z, 0, fwd.x);
        int ci = v.Count; v.Add(c); uv.Add(new Vector2(0.5f, 0));
        int seg = 16;
        for (int k = 0; k <= seg; k++)
        {
            float a = Mathf.PI * k / seg;
            v.Add(c + (-left * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * h); uv.Add(new Vector2(0.5f, 0));
        }
        for (int k = 0; k < seg; k++) tri.AddRange(new[] { ci, ci + 2 + k, ci + 1 + k });
    }

    // ======================================================================
    // Decorations
    class Decorator
    {
        readonly SceneInfo s; readonly List<Vector2> pathPts = new List<Vector2>(); readonly System.Random rng;
        readonly List<(Vector2 p, float r)> placed = new List<(Vector2, float)>();
        Transform root, trees, bushes, grass, props, crops;
        readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public Decorator(SceneInfo s, List<PathData> paths, int seed)
        {
            this.s = s; rng = new System.Random(seed);
            foreach (var p in paths)
            {
                // densify so distance checks are accurate
                for (int i = 0; i < p.points.Count; i++)
                {
                    var a = p.points[i]; var b = p.points[(i + 1) % p.points.Count];
                    if (!p.closed && i == p.points.Count - 1) { pathPts.Add(new Vector2(a.x, a.z)); break; }
                    for (int k = 0; k < 4; k++) { var q = Vector3.Lerp(a, b, k / 4f); pathPts.Add(new Vector2(q.x, q.z)); }
                }
            }
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        T Pick<T>(IList<T> l) => l[rng.Next(l.Count)];

        float PathDist(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var q in pathPts) { float d = (p - q).sqrMagnitude; if (d < best) best = d; }
            return Mathf.Sqrt(best);
        }

        bool InHQZone(Vector2 p, float pad)
        {
            Vector2 c = s.hq.center;
            float rx = s.hq.width / 2f + RingPadding + PathWidth / 2f + pad, rz = s.hq.height / 2f + RingPadding + PathWidth / 2f + pad;
            float dx = (p.x - c.x) / rx, dz = (p.y - c.y) / rz;
            return dx * dx + dz * dz < 1f;
        }

        bool Free(Vector2 p, float radius, float pathGap, float areaGap, float itemGap)
        {
            var pl = s.play;
            if (p.x < pl.xMin + radius + 1 || p.x > pl.xMax - radius - 1 || p.y < pl.yMin + radius + 1 || p.y > pl.yMax - radius - 1) return false;
            if (InHQZone(p, radius + 0.5f)) return false;
            foreach (var o in s.obstacles)
            {
                float g = o.isArea ? areaGap : 0.6f;
                var r = o.rect; var e = Rect.MinMaxRect(r.xMin - radius - g, r.yMin - radius - g, r.xMax + radius + g, r.yMax + radius + g);
                if (e.Contains(p)) return false;
            }
            if (PathDist(p) < PathWidth / 2f + radius + pathGap) return false;
            foreach (var (q, rr) in placed) if ((p - q).magnitude < radius + rr + itemGap) return false;
            return true;
        }

        GameObject Prefab(string n)
        {
            if (cache.TryGetValue(n, out var g)) return g;
            g = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + n + ".prefab");
            if (g == null) Debug.LogWarning("[Farm Chaos] Missing prefab: " + n);
            cache[n] = g; return g;
        }

        // Instantiates, scales to target height, sits on ground. Returns footprint radius or -1.
        GameObject Spawn(string prefab, Vector2 pos, float height, Transform parent, out float radius)
        {
            radius = -1;
            var pf = Prefab(prefab); if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            Undo.RegisterCreatedObjectUndo(go, "Decor");
            // keep the prefab's own rotation/scale (models are often imported rotated), just spin around world Y
            var baseRot = pf.transform.rotation; var baseScale = pf.transform.localScale;
            go.transform.rotation = Quaternion.Euler(0, R(0, 360), 0) * baseRot;
            go.transform.localScale = baseScale;
            go.transform.position = new Vector3(pos.x, 0, pos.y);
            if (!SceneInfo.WorldBounds(go, out var b) || b.size.y < 1e-4f) { Undo.DestroyObjectImmediate(go); return null; }
            float sc = height / b.size.y;
            go.transform.localScale = baseScale * sc;
            SceneInfo.WorldBounds(go, out b);
            go.transform.position += new Vector3(pos.x - b.center.x, s.groundY - b.min.y, pos.y - b.center.z);
            radius = Mathf.Max(b.extents.x, b.extents.z);
            return go;
        }

        static void EnsureCollider(GameObject go, bool trunkOnly)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            // local-space bounds of all meshes
            Bounds lb = default; bool has = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = go.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!has) { lb = new Bounds(lp, Vector3.zero); has = true; } else lb.Encapsulate(lp);
                }
            }
            if (!has) return;
            if (trunkOnly)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.direction = 1;
                cc.center = lb.center;
                cc.height = lb.size.y;
                cc.radius = Mathf.Min(lb.size.x, lb.size.z) * 0.12f;
            }
            else
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = lb.center; bc.size = lb.size;
            }
        }

        bool TryPlace(string prefab, Vector2 pos, float height, Transform parent, float pathGap, float areaGap, float itemGap, int collider /*0 none 1 trunk 2 box*/)
        {
            // estimate radius before spawning: spawn, test, delete if blocked
            var go = Spawn(prefab, pos, height, parent, out float r);
            if (go == null) return false;
            if (!Free(pos, r, pathGap, areaGap, itemGap)) { Undo.DestroyObjectImmediate(go); return false; }
            placed.Add((pos, r));
            if (collider == 1) EnsureCollider(go, true); else if (collider == 2) EnsureCollider(go, false);
            return true;
        }

        Vector2 RandomPoint() => new Vector2(R(s.play.xMin, s.play.xMax), R(s.play.yMin, s.play.yMax));

        public void Run()
        {
            var r = new GameObject(DecorRoot); Undo.RegisterCreatedObjectUndo(r, "Build Decorations"); root = r.transform;
            Transform Sub(string n) { var g = new GameObject(n); g.transform.SetParent(root, false); return g.transform; }
            trees = Sub("Trees"); bushes = Sub("Bushes"); crops = Sub("Crops"); props = Sub("Props"); grass = Sub("Grass");

            string[] treeTypes = { "Env_Tree_01", "Env_Tree_02", "Env_Tree_03", "Env_Tree_04", "Env_Tree_05", "Env_Tree_06", "Env_Tree_07" };
            string[] bushTypes = { "Env_Bush_01", "Env_Bush_02" };
            string[] grassTypes = { "Env_GrassPlant_01", "Env_GrassPlant_02", "Env_GrassPlant_03", "Env_GrassPlant_04", "Env_GrassPlant_05", "Env_GrassPlant_06", "Env_GrassPlant_07" };

            // 1) Tree line along the border walls (two staggered rows)
            var pl = s.play;
            for (int row = 0; row < 2; row++)
            {
                float inset = row == 0 ? 3f : 7.5f, step = row == 0 ? 5f : 7f;
                var corners = new[] { new Vector2(pl.xMin + inset, pl.yMin + inset), new Vector2(pl.xMax - inset, pl.yMin + inset),
                                      new Vector2(pl.xMax - inset, pl.yMax - inset), new Vector2(pl.xMin + inset, pl.yMax - inset) };
                for (int e = 0; e < 4; e++)
                {
                    Vector2 a = corners[e], b = corners[(e + 1) % 4];
                    int cnt = Mathf.FloorToInt((b - a).magnitude / step);
                    for (int i = 0; i <= cnt; i++)
                    {
                        if (row == 1 && rng.NextDouble() < 0.35) continue;
                        var p = Vector2.Lerp(a, b, i / (float)Mathf.Max(cnt, 1)) + new Vector2(R(-1.2f, 1.2f), R(-1.2f, 1.2f));
                        TryPlace(Pick(treeTypes), p, R(6f, 8.5f), trees, 1.5f, 2f, 0.2f, 1);
                    }
                }
            }

            // 2) Small tree groves in open grass
            for (int g = 0, tries = 0; g < 7 && tries < 400; tries++)
            {
                var c = RandomPoint();
                if (!Free(c, 4f, 2f, 3f, 1f)) continue;
                int n = rng.Next(2, 5);
                for (int i = 0; i < n; i++)
                    TryPlace(Pick(treeTypes), c + Random2(3.5f), R(5f, 7.5f), trees, 1.5f, 2.5f, 0.3f, 1);
                for (int i = 0; i < 3; i++) TryPlace(Pick(bushTypes), c + Random2(5f), R(1.1f, 1.7f), bushes, 0.8f, 1f, 0.1f, 0);
                g++;
            }

            // 3) Bushes lining the trails
            for (int i = 0, tries = 0; i < 45 && tries < 2000; tries++)
            {
                var q = Pick(pathPts);
                var p = q + Random2(1f).normalized * R(PathWidth / 2f + 1.2f, PathWidth / 2f + 3.2f);
                if (TryPlace(Pick(bushTypes), p, R(1.0f, 1.6f), bushes, 0.6f, 1f, 0.6f, 0)) i++;
            }

            // 4) Crop patches: wheat and pumpkins
            for (int patch = 0, tries = 0; patch < 5 && tries < 300; tries++)
            {
                var c = RandomPoint();
                if (!Free(c, 3.5f, 2f, 3f, 1f)) continue;
                bool wheat = patch % 2 == 0;
                float ang = R(0, 180);
                var rot = Quaternion.Euler(0, ang, 0);
                for (int x = -2; x <= 2; x++)
                    for (int z = -1; z <= 1; z++)
                    {
                        var off = rot * new Vector3(x * 1.3f, 0, z * 1.3f);
                        var p = c + new Vector2(off.x, off.z) + Random2(0.2f);
                        if (wheat) TryPlace("Env_Wheat", p, R(1.1f, 1.4f), crops, 0.8f, 1.5f, -0.6f, 0);
                        else TryPlace("food_Pumpkin", p, R(0.5f, 0.8f), crops, 0.8f, 1.5f, 0.1f, 2);
                    }
                patch++;
            }

            // 5) Little farm-prop corners next to trails (haystacks, barrels, crates, sacks...)
            string[] big = { "Prop_Haystack_01", "Prop_Haystack_02", "Prop_Haystack_03", "Prop_Haystack_04", "Prop_Haystack_05", "Prop_Haystack_06", "Prop_Haystack_07" };
            string[] small = { "Prop_Berrel_01.001", "Prop_Berrel_02_A", "Prop_Berrel_03", "Prop_Berrel_04", "Prop_WoodenBox_01", "Prop_WoodenBox_02",
                               "Prop_WoodenCrates_01", "Prop_WoodenCrates_02", "Prop_FoodSack_01", "Prop_FoodSack_02", "Prop_FoodSack_03",
                               "Prop_Bucket_01", "Prop_Bucket_02", "Prop_WateringCan_01", "Prop_Wheelbarrow", "Prop_Pallete_01", "Prop_MetalBerrel_01" };
            var smallH = new Dictionary<string, float> { { "Prop_Bucket_01", .55f }, { "Prop_Bucket_02", .55f }, { "Prop_WateringCan_01", .6f },
                                                          { "Prop_Wheelbarrow", 1f }, { "Prop_Pallete_01", .3f }, { "Prop_FoodSack_01", .9f },
                                                          { "Prop_FoodSack_02", .9f }, { "Prop_FoodSack_03", .9f } };
            for (int g = 0, tries = 0; g < 9 && tries < 1500; tries++)
            {
                var q = Pick(pathPts);
                var c = q + Random2(1f).normalized * R(PathWidth / 2f + 2.5f, PathWidth / 2f + 4.5f);
                if (!Free(c, 2f, 0.8f, 1.5f, 1f)) continue;
                if (!TryPlace(Pick(big), c, R(1.6f, 2.2f), props, 0.8f, 1.5f, 0.2f, 2)) continue;
                int n = rng.Next(2, 4);
                for (int i = 0; i < n; i++)
                {
                    var name = Pick(small);
                    TryPlace(name, c + Random2(2.5f), smallH.TryGetValue(name, out var h) ? h : R(0.9f, 1.2f), props, 0.6f, 1.2f, 0.15f, 2);
                }
                g++;
            }

            // 6) Grass tufts everywhere
            for (int i = 0, tries = 0; i < 220 && tries < 5000; tries++)
                if (TryPlace(Pick(grassTypes), RandomPoint(), R(0.45f, 0.9f), grass, 0.2f, 0.4f, 0.4f, 0)) i++;

            Selection.activeGameObject = r;
        }

        Vector2 Random2(float radius)
        {
            float a = R(0, Mathf.PI * 2), d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
        }
    }
}
#endif
