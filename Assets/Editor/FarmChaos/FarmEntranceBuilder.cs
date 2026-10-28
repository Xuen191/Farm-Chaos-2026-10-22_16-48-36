#if UNITY_EDITOR
// Farm Chaos - main entrance archway in the south wall (between the rabbit and cow pens),
// a spawn plaza with 4 spawn points, and a trail from the plaza to the HQ loop.
// Menu: Tools > Farm Chaos > 7. Build Main Entrance
// The original south wall is switched off (not deleted) and replaced by two pieces with a gap.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmEntranceBuilder
{
    const string Root = "Main_Entrance";
    const float GapWidth = 9f;
    const float PathWidth = 3f;

    [MenuItem("Tools/Farm Chaos/7. Build Main Entrance")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        var all = All();

        // clean previous run
        foreach (var t in all.Where(t => t != null && (t.name == Root || t.name == "Path_Entrance" || t.name == "border_south_W" || t.name == "border_south_E")).ToList())
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        all = All();

        var wall = all.FirstOrDefault(t => t.name == "border_south");
        var rabbit = all.FirstOrDefault(t => t.name == "rabbit_area");
        var cow = all.FirstOrDefault(t => t.name == "cow_area");
        var ps = all.FirstOrDefault(t => t.name == "powerstation_area");
        var loop = all.FirstOrDefault(t => t.name == "Path_HQ_Loop");
        if (wall == null || rabbit == null || cow == null) { EditorUtility.DisplayDialog("Farm Chaos", "Couldn't find border_south / rabbit_area / cow_area.", "OK"); return; }

        var land = all.FirstOrDefault(t => t.name == "land");
        float groundY = land != null ? land.GetComponent<Renderer>().bounds.max.y : 0f;
        var wb = wall.GetComponent<Renderer>().bounds;
        float gx = (rabbit.GetComponent<Renderer>().bounds.center.x + cow.GetComponent<Renderer>().bounds.center.x) / 2f;
        float wallZ = wb.center.z, innerZ = wb.max.z;

        // ---- split the south wall
        Undo.RecordObject(wall.gameObject, "Main Entrance");
        wall.gameObject.SetActive(false);
        Segment(wall, "border_south_W", wb.min.x, gx - GapWidth / 2f);
        Segment(wall, "border_south_E", gx + GapWidth / 2f, wb.max.x);

        var root = new GameObject(Root); Undo.RegisterCreatedObjectUndo(root, "Main Entrance");
        root.transform.position = new Vector3(gx, groundY, wallZ);
        var M = Materials();

        BuildArch(root.transform, GapWidth, wb.size.z, M);

        // ---- spawn plaza
        Vector3 plaza = new Vector3(gx, groundY, innerZ + 7.5f);
        var plazaGo = Prim(PrimitiveType.Cylinder, "Spawn_Plaza", root.transform, Vector3.zero, new Vector3(12f, 0.03f, 12f), M["path"]);
        plazaGo.transform.position = plaza + Vector3.up * 0.035f;
        Prim(PrimitiveType.Cylinder, "Plaza_Ring", root.transform, Vector3.zero, new Vector3(12.6f, 0.025f, 12.6f), M["mint"])
            .transform.position = plaza + Vector3.up * 0.02f;
        // welcome mat between gate and plaza
        var mat = Prim(PrimitiveType.Cube, "Welcome_Path", root.transform, Vector3.zero, new Vector3(PathWidth + 1f, 0.06f, 3f), M["path"]);
        mat.transform.position = new Vector3(gx, groundY + 0.03f, innerZ + 1.5f);

        var spawns = new GameObject("Spawn_Points").transform; spawns.SetParent(root.transform, true);
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            var sp = new GameObject("Spawn_" + (i + 1)).transform; sp.SetParent(spawns, true);
            sp.position = plaza + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3f + Vector3.up * 1f;
            sp.rotation = Quaternion.identity; // facing north, into the farm
            var marker = Prim(PrimitiveType.Cylinder, "Marker", sp, Vector3.zero, new Vector3(1.4f, 0.02f, 1.4f), M["mint"]);
            marker.transform.position = new Vector3(sp.position.x, groundY + 0.07f, sp.position.z);
            var dot = Prim(PrimitiveType.Cylinder, "Marker_Dot", sp, Vector3.zero, new Vector3(0.6f, 0.025f, 0.6f), M["pink"]);
            dot.transform.position = new Vector3(sp.position.x, groundY + 0.075f, sp.position.z);
        }
        // planters with bushes either side of the plaza
        foreach (var sx in new[] { -1, 1 })
        {
            var box = Prim(PrimitiveType.Cube, "Planter", root.transform, Vector3.zero, new Vector3(1.4f, 0.6f, 1.4f), M["wood"], true);
            box.transform.position = new Vector3(gx + sx * 7.8f, groundY + 0.3f, innerZ + 1.6f);
            Pack("Env_Bush_01", root.transform, new Vector3(gx + sx * 7.8f, groundY + 0.6f, innerZ + 1.6f), 1.1f);
        }

        // ---- trail from the plaza to the HQ loop
        Vector3 start = plaza + new Vector3(0, 0, 6f);
        Vector3 end = start + new Vector3(0, 0, 25f);
        var loopPts = LoopCentre(loop);
        Bounds psB = ps != null ? ps.GetComponent<Renderer>().bounds : new Bounds(new Vector3(9999, 0, 9999), Vector3.zero);
        if (loopPts.Count > 0)
        {
            // nearest loop point that isn't blocked by the power station pad
            float m = PathWidth / 2f + 1.6f;
            end = loopPts.Where(p => p.x < psB.min.x - m || p.x > psB.max.x + m)
                         .OrderBy(p => Vector3.Distance(p, start)).First();
        }
        Vector3 ctrl = (start + end) / 2f;
        List<Vector3> pts = null;
        for (int tries = 0; tries < 12; tries++)
        {
            pts = Bezier(start, ctrl, end, groundY + 0.04f);
            bool clash = pts.Any(p => DistXZ(psB, p) < PathWidth / 2f + 0.8f);
            if (!clash) break;
            ctrl.x += end.x < psB.center.x ? -2f : 2f;   // bend away from the power station
        }
        var pathsRoot = all.FirstOrDefault(t => t.name == "Farm_Paths_Rounded");
        var pathGo = new GameObject("Path_Entrance");
        Undo.RegisterCreatedObjectUndo(pathGo, "Main Entrance");
        if (pathsRoot != null) pathGo.transform.SetParent(pathsRoot, false);
        pathGo.AddComponent<MeshFilter>().sharedMesh = Ribbon(pts, PathWidth);
        var mr = pathGo.AddComponent<MeshRenderer>(); mr.sharedMaterial = M["path"];
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ---- clear decorations that are now in the way
        int removed = ClearDecor(pts, plaza, new Vector3(gx, groundY, wallZ));

        // ---- move the player to spawn 1
        var ball = All().FirstOrDefault(t => t.name == "Player_Sphere");
        if (ball != null)
        {
            Undo.RecordObject(ball, "Main Entrance");
            ball.position = spawns.GetChild(0).position;
            var cam = Camera.main;
            if (cam != null) { Undo.RecordObject(cam.transform, "Main Entrance"); cam.transform.position = ball.position + new Vector3(0, 11, -9); cam.transform.LookAt(ball.position); }
        }

        // ---- lamps for the new path if night mode is on
        bool night = All().Any(t => t.name == "Farm_Night");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (night) FarmNightBuilder.Night();
        Debug.Log($"[Farm Chaos] Main entrance built at x={gx:0.0}; {removed} decorations moved out of the way; player now spawns at the plaza (scene saved).");
    }

    // =====================================================================
    static List<Transform> All() =>
        SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();

    static float DistXZ(Bounds b, Vector3 p)
    {
        float dx = Mathf.Max(b.min.x - p.x, 0, p.x - b.max.x), dz = Mathf.Max(b.min.z - p.z, 0, p.z - b.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static void Segment(Transform wall, string name, float x0, float x1)
    {
        if (x1 - x0 < 0.1f) return;
        var copy = Object.Instantiate(wall.gameObject, wall.parent);
        copy.name = name; copy.SetActive(true);
        Undo.RegisterCreatedObjectUndo(copy, "Main Entrance");
        var s = wall.localScale;
        float sx = wall.lossyScale.x != 0 ? s.x / wall.lossyScale.x : 1f;
        copy.transform.localScale = new Vector3((x1 - x0) * sx, s.y, s.z);
        var p = wall.position; copy.transform.position = new Vector3((x0 + x1) / 2f, p.y, p.z);
    }

    static Dictionary<string, Material> Materials()
    {
        Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);
        var path = Load("Assets/Materials/Mat_walkways.mat");
        if (path == null) { var g = AssetDatabase.FindAssets("Mat_walkways t:Material"); if (g.Length > 0) path = Load(AssetDatabase.GUIDToAssetPath(g[0])); }
        var glow = Load("Assets/Lighting/Mat_lamp_glow.mat") ?? Load("Assets/Materials/Buildings/Mat_lamp.mat");
        return new Dictionary<string, Material>
        {
            ["path"] = path,
            ["cream"] = Load("Assets/Materials/Buildings/Mat_hq_wall.mat"),
            ["pink"] = Load("Assets/Materials/Buildings/Mat_hq_roof.mat"),
            ["mint"] = Load("Assets/Materials/Buildings/Mat_building_trim.mat"),
            ["wood"] = Load("Assets/Materials/Buildings/Mat_building_wood.mat"),
            ["wood_dark"] = Load("Assets/Materials/Buildings/Mat_building_wood_dark.mat"),
            ["door"] = Load("Assets/Materials/Buildings/Mat_hq_door.mat"),
            ["glow"] = glow,
        };
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 lp, Vector3 size, Material m, bool collider = false)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localScale = size;
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    // archway centred on the root (root sits on the wall line, local +Z points into the farm)
    static void BuildArch(Transform root, float gap, float wallDepth, Dictionary<string, Material> M)
    {
        float pw = 1.7f, ph = 5.2f;
        foreach (var sx in new[] { -1, 1 })
        {
            float x = sx * (gap / 2f + pw / 2f - 0.2f);
            Prim(PrimitiveType.Cube, "Pillar", root, new Vector3(x, ph / 2f, 0), new Vector3(pw, ph, wallDepth + 0.6f), M["cream"], true);
            Prim(PrimitiveType.Cube, "Pillar_Base", root, new Vector3(x, 0.3f, 0), new Vector3(pw + 0.3f, 0.6f, wallDepth + 0.9f), M["mint"]);
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Cube, "Pillar_Stripe", root, new Vector3(x, 1.4f + k * 1.2f, 0), new Vector3(pw + 0.04f, 0.25f, wallDepth + 0.64f), M["pink"]);
            Prim(PrimitiveType.Cube, "Pillar_Cap", root, new Vector3(x, ph + 0.15f, 0), new Vector3(pw + 0.35f, 0.3f, wallDepth + 0.95f), M["mint"]);
            // lantern on top
            Prim(PrimitiveType.Cube, "Lantern", root, new Vector3(x, ph + 0.62f, 0), new Vector3(0.55f, 0.65f, 0.55f), M["glow"])
                .transform.localRotation = Quaternion.Euler(0, 45, 0);
            Prim(PrimitiveType.Cylinder, "Lantern_Roof", root, new Vector3(x, ph + 1.0f, 0), new Vector3(0.85f, 0.08f, 0.85f), M["pink"]);
            Prim(PrimitiveType.Sphere, "Lantern_Topper", root, new Vector3(x, ph + 1.16f, 0), Vector3.one * 0.22f, M["mint"]);
        }

        // rounded arch: pink segments along a half-circle
        float r = gap / 2f + pw / 2f - 0.2f, cy = ph - 0.2f;
        int segs = 14;
        for (int i = 0; i < segs; i++)
        {
            float a0 = Mathf.PI * i / segs, a1 = Mathf.PI * (i + 1) / segs, am = (a0 + a1) / 2f;
            Vector3 p = new Vector3(Mathf.Cos(am) * r, cy + Mathf.Sin(am) * r * 0.55f, 0);
            Vector3 q0 = new Vector3(Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r * 0.55f, 0), q1 = new Vector3(Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r * 0.55f, 0);
            var seg = Prim(PrimitiveType.Cube, "Arch", root, p, new Vector3(Vector3.Distance(q0, q1) + 0.12f, 0.55f, 0.9f), M["pink"]);
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.right, q1 - q0);
        }
        // sign
        float signY = cy + r * 0.55f + 0.05f;
        Prim(PrimitiveType.Cube, "Sign_Board", root, new Vector3(0, signY, 0.1f), new Vector3(5.6f, 1.2f, 0.25f), M["mint"]);
        Prim(PrimitiveType.Cube, "Sign_Trim", root, new Vector3(0, signY, 0.08f), new Vector3(5.9f, 1.45f, 0.2f), M["wood"]);
        foreach (var dir in new[] { 1f, -1f })   // readable from inside the farm and from outside
        {
            var g = new GameObject(dir > 0 ? "Sign_Text_Inside" : "Sign_Text_Outside");
            g.transform.SetParent(root, false);
            g.transform.localPosition = new Vector3(0, signY, 0.1f + dir * 0.14f);
            g.transform.localRotation = Quaternion.Euler(0, dir > 0 ? 180f : 0f, 0);
            var tmp = g.AddComponent<TextMeshPro>();
            tmp.text = "FARM CHAOS"; tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.1f; tmp.fontSizeMax = 30f;
            tmp.color = new Color32(0x6B, 0x4A, 0x3A, 255);
            tmp.rectTransform.sizeDelta = new Vector2(5.2f, 0.95f);
        }

        // closed double gate in the arch (keeps players inside the farm)
        foreach (var sx in new[] { -1, 1 })
        {
            float w = gap / 2f - 0.15f, x = sx * (w / 2f + 0.05f);
            var leaf = new GameObject(sx < 0 ? "Gate_L" : "Gate_R").transform; leaf.SetParent(root, false);
            leaf.localPosition = new Vector3(x, 0, 0);
            Prim(PrimitiveType.Cube, "Frame_Bottom", leaf, new Vector3(0, 0.35f, 0), new Vector3(w, 0.2f, 0.16f), M["wood_dark"]);
            Prim(PrimitiveType.Cube, "Frame_Top", leaf, new Vector3(0, 3.0f, 0), new Vector3(w, 0.2f, 0.16f), M["wood_dark"]);
            for (int k = 0; k < 6; k++)
                Prim(PrimitiveType.Cube, "Picket", leaf, new Vector3(-w / 2f + 0.3f + k * (w - 0.6f) / 5f, 1.65f, 0), new Vector3(0.18f, 2.9f, 0.1f), M["door"]);
            var brace = Prim(PrimitiveType.Cube, "Brace", leaf, new Vector3(0, 1.67f, 0.07f), new Vector3(Mathf.Sqrt(w * w + 2.6f * 2.6f) - 0.2f, 0.18f, 0.06f), M["wood_dark"]);
            brace.transform.localRotation = Quaternion.Euler(0, 0, sx * Mathf.Atan2(2.6f, w) * Mathf.Rad2Deg);
            var col = leaf.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.7f, 0); col.size = new Vector3(w, 3.4f, 0.3f);
        }
    }

    static void Pack(string prefab, Transform parent, Vector3 pos, float height)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/" + prefab + ".prefab");
        if (pf == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos;
        var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y < 1e-4f) return;
        go.transform.localScale = pf.transform.localScale * (height / b.size.y);
        rs = go.GetComponentsInChildren<Renderer>(); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
    }

    // centre line of the HQ loop from its ribbon mesh (vertex pairs: left, right)
    static List<Vector3> LoopCentre(Transform loop)
    {
        var res = new List<Vector3>();
        if (loop == null) return res;
        var mf = loop.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return res;
        var v = mf.sharedMesh.vertices;
        for (int i = 0; i + 1 < v.Length; i += 2) res.Add(loop.TransformPoint((v[i] + v[i + 1]) / 2f));
        return res;
    }

    static List<Vector3> Bezier(Vector3 a, Vector3 c, Vector3 b, float y)
    {
        var pts = new List<Vector3>();
        int steps = Mathf.Max(10, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.4f));
        for (int i = 0; i <= steps; i++)
        {
            float u = i / (float)steps;
            var q = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * c + u * u * b;
            pts.Add(new Vector3(q.x, y, q.z));
        }
        return pts;
    }

    // same layout as the other trails (pairs of verts, then two 17-segment round caps)
    static Mesh Ribbon(List<Vector3> p, float width)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float h = width / 2f, dist = 0f; int n = p.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = p[Mathf.Max(i - 1, 0)], next = p[Mathf.Min(i + 1, n - 1)];
            Vector3 tan = next - prev; tan.y = 0; tan.Normalize();
            Vector3 left = new Vector3(-tan.z, 0, tan.x);
            if (i > 0) dist += Vector3.Distance(p[i], p[i - 1]);
            v.Add(p[i] + left * h); uv.Add(new Vector2(0, dist / width));
            v.Add(p[i] - left * h); uv.Add(new Vector2(1, dist / width));
        }
        for (int i = 0; i < n - 1; i++)
        {
            int L0 = i * 2, R0 = L0 + 1, L1 = L0 + 2, R1 = L0 + 3;
            tri.AddRange(new[] { L0, L1, R1, L0, R1, R0 });
        }
        Cap(p[n - 1], p[n - 1] - p[n - 2], h, v, uv, tri);
        Cap(p[0], p[0] - p[1], h, v, uv, tri);
        var m = new Mesh { name = "Path_Entrance_Mesh" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.SetNormals(Enumerable.Repeat(Vector3.up, v.Count).ToList());
        m.RecalculateBounds();
        return m;
    }

    static void Cap(Vector3 c, Vector3 fwd, float h, List<Vector3> v, List<Vector2> uv, List<int> tri)
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

    static int ClearDecor(List<Vector3> path, Vector3 plaza, Vector3 gate)
    {
        var decor = All().FirstOrDefault(t => t.name == "Farm_Decorations");
        if (decor == null) return 0;
        int n = 0;
        foreach (Transform group in decor)
            foreach (var item in group.Cast<Transform>().ToList())
            {
                Vector3 p = item.position;
                var rs = item.GetComponentsInChildren<Renderer>();
                float r = 0.5f;
                if (rs.Length > 0) { var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); r = Mathf.Max(b.extents.x, b.extents.z); p = b.center; }
                bool hit = Flat(p - plaza).magnitude < 7.5f + r
                           || (Mathf.Abs(p.x - gate.x) < GapWidth / 2f + 3.5f + r && p.z < gate.z + 6f)
                           || path.Any(q => Flat(p - q).magnitude < PathWidth / 2f + 0.8f + r);
                if (hit) { Undo.DestroyObjectImmediate(item.gameObject); n++; }
            }
        return n;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
#endif
